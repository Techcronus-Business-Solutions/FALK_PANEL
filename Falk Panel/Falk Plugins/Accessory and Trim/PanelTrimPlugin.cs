using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Falk_Plugins.Accessory_and_Trim
{
    public class PanelTrimPlugin : PluginBase
    {
        public PanelTrimPlugin() : base(typeof(PanelTrimPlugin)) { }

        #region Private Variables
        private IOrganizationService service { get; set; }
        private IPluginExecutionContext context { get; set; }
        private ITracingService tracingService { get; set; }
        private Entity targetEntity { get; set; }
        #endregion

        protected override void ExecuteCrmPlugin(LocalPluginContext localcontext)
        {
            if (localcontext == null)
                throw new ArgumentNullException(nameof(localcontext));

            InitProperties(localcontext);

            try
            {
                if (context.InputParameters.Contains(CONST_TARGETENTITY) && context.InputParameters[CONST_TARGETENTITY] is Entity)
                {
                    targetEntity = (Entity)context.InputParameters[CONST_TARGETENTITY];
                    if (targetEntity.LogicalName == "tbs_opppaneltrim")
                    {
                        // ---------------- CREATE (PreOperation) ----------------
                        if (context.Stage == PreOperation && context.MessageName == CONST_CREATE)
                        {
                            EntityReference trimRef = targetEntity.Contains("tbs_trim") ? targetEntity.GetAttributeValue<EntityReference>("tbs_trim") : null;
                            EntityReference oppProductRef = targetEntity.Contains("tbs_opportunityproduct") ? targetEntity.GetAttributeValue<EntityReference>("tbs_opportunityproduct") : null;

                            if (trimRef != null && oppProductRef != null)
                            {
                                Entity trim = service.Retrieve("tbs_trim", trimRef.Id, new ColumnSet("tbs_unit", "tbs_price", "tbs_usdembossedprice", "tbs_description", "tbs_finish", "tbs_canadacustomerprice"));

                                Entity opportunityProduct = service.Retrieve("opportunityproduct", oppProductRef.Id, new ColumnSet("opportunityid", "tbs_priceleveltier", "tbs_exteriorcolor", "tbs_interiorcolor", "tbs_exteriorgauge", "tbs_interiorgauge", "tbs_interiorfinish", "tbs_exteriorfinish"));

                                #region Add Unit & Base Price in Panel Trim based on Trim
                                Money unitPrice = trim.Contains("tbs_price") ? trim.GetAttributeValue<Money>("tbs_price") : new Money(0);

                                targetEntity["tbs_unit"] = trim.Contains("tbs_unit") ? trim.GetAttributeValue<EntityReference>("tbs_unit") : null;
                                targetEntity["tbs_unitprice"] = unitPrice;
                                #endregion

                                #region Calculate Total Price
                                CalculatePanelTrimPrice(targetEntity, trim, opportunityProduct);
                                #endregion
                            }

                        }
                    }
                }
            }
            catch (Exception ex)
            {
                tracingService.Trace("TrimPlugin Exception: {0}", ex.ToString());
                throw new InvalidPluginExecutionException($"Error in TrimPlugin: {ex.Message}");
            }
        }

        private void CalculatePanelTrimPrice(Entity panelTrim, Entity trim, Entity opportunityProduct)
        {
            tracingService.Trace("CalculatePanelTrimPrice Started");

            EntityReference trimRef = panelTrim.Contains("tbs_trim") ? panelTrim.GetAttributeValue<EntityReference>("tbs_trim") : null;
            EntityReference oppProductRef = panelTrim.Contains("tbs_opportunityproduct") ? panelTrim.GetAttributeValue<EntityReference>("tbs_opportunityproduct") : null;

            if (trimRef == null || oppProductRef == null)
            {
                tracingService.Trace("Trim or Opportunity Product is null.");
                return;
            }

            int quantity = panelTrim.Contains("tbs_quantity") ? panelTrim.GetAttributeValue<int>("tbs_quantity") : 1;

            Money basePriceMoney = trim.GetAttributeValue<Money>("tbs_price") ?? new Money(0);
            Money embossPriceMoney = trim.GetAttributeValue<Money>("tbs_usdembossedprice") ?? new Money(0);

            decimal basePrice = basePriceMoney.Value;
            decimal embossPrice = embossPriceMoney.Value;

            string trimDescription = trim.GetAttributeValue<string>("tbs_description") ?? string.Empty;

            OptionSetValue finishOption = trim.Contains("tbs_finish") ? trim.GetAttributeValue<OptionSetValue>("tbs_finish") : new OptionSetValue(0);

            int finishValue = finishOption != null ? finishOption.Value : 0;

            decimal multiplier = 1m;

            #region Determine Category (SS / FM / Tier2)
            string categoryName = "Tier2";

            // Determine finish from Opportunity Product
            EntityReference finishRef = null;

            switch (finishValue)
            {
                case 2: // Interior Match
                    finishRef = opportunityProduct.GetAttributeValue<EntityReference>("tbs_interiorfinish");
                    break;

                case 3: // Exterior Match
                    finishRef = opportunityProduct.GetAttributeValue<EntityReference>("tbs_exteriorfinish");
                    break;
            }

            string finishName = string.Empty;

            tracingService.Trace("Finish ID: " + finishRef.Id);

            if (finishRef != null)
            {
                Entity finish = service.Retrieve(
                    "tbs_finish",
                    finishRef.Id,
                    new ColumnSet("tbs_name"));

                finishName = finish.GetAttributeValue<string>("tbs_name") ?? "";

                tracingService.Trace("Finish Name: " + finishName);
            }

            tracingService.Trace("Finish Name: " + finishName);

            // Static Comparison for Setting Tier In Opp Panel Trim based on Finish
            if (finishValue == 2 || finishValue == 3)
            {
                if (finishName.Equals("304 Stainless", StringComparison.OrdinalIgnoreCase))
                {
                    categoryName = "SS";
                }
                else if (trimDescription.IndexOf("FM", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    categoryName = "FM";
                }
                else
                {
                    categoryName = "Tier2";
                }
            }
            #endregion

            #region Retrieve Tier record using Category
            QueryExpression tierQuery = new QueryExpression("tbs_tier");
            tierQuery.ColumnSet = new ColumnSet("tbs_multiplier");
            tierQuery.Criteria.AddCondition("tbs_name", ConditionOperator.Equal, categoryName);

            Entity tier = service.RetrieveMultiple(tierQuery).Entities.FirstOrDefault();

            if (tier == null)
            {
                throw new InvalidPluginExecutionException($"Tier '{categoryName}' not found.");
            }

            // Set Category lookup
            panelTrim["tbs_category"] = tier.ToEntityReference();

            // Read Multiplier
            int multiplierPercent = tier.GetAttributeValue<int>("tbs_multiplier");

            multiplier = multiplierPercent / 100m;

            tracingService.Trace("Category={0}, Finish={1}, Multiplier={2}", categoryName, finishName, multiplier);
            #endregion

            #region Packaging Rule
            if (trimDescription.IndexOf("Packaging", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                decimal packagingTotal = basePrice * quantity;

                panelTrim["tbs_totalprice"] = new Money(packagingTotal);

                tracingService.Trace("Packaging Rule Applied");
                return;
            }
            #endregion

            #region Determine Color/Gauge based on Finish
            EntityReference colorRef = null;
            EntityReference gaugeRef = null;

            switch (finishValue)
            {
                case 2: // Interior Match
                    colorRef = opportunityProduct.Contains("tbs_interiorcolor") ? opportunityProduct.GetAttributeValue<EntityReference>("tbs_interiorcolor") : null;
                    gaugeRef = opportunityProduct.Contains("tbs_interiorgauge") ? opportunityProduct.GetAttributeValue<EntityReference>("tbs_interiorgauge") : null;
                    break;

                case 3: // Exterior Match
                    colorRef = opportunityProduct.Contains("tbs_exteriorcolor") ? opportunityProduct.GetAttributeValue<EntityReference>("tbs_exteriorcolor") : null;
                    gaugeRef = opportunityProduct.Contains("tbs_exteriorgauge") ? opportunityProduct.GetAttributeValue<EntityReference>("tbs_exteriorgauge") : null;
                    break;

                case 1: // Galvanized
                default:

                    decimal galvanizedUnitPrice = basePrice * multiplier;
                    decimal galvanizedTotalPrice = galvanizedUnitPrice * quantity;

                    panelTrim["tbs_totalprice"] = new Money(galvanizedTotalPrice);

                    tracingService.Trace("Galvanized Rule Applied");
                    return;
            }

            bool colorEmbossable = false;

            if (colorRef != null)
            {
                Entity color = service.Retrieve("tbs_color", colorRef.Id, new ColumnSet("tbs_isembossable"));

                colorEmbossable = color.Contains("tbs_isembossable") ? color.GetAttributeValue<bool>("tbs_isembossable") : false;
            }

            bool gaugeOK = false;

            if (gaugeRef != null)
            {
                Entity gauge = service.Retrieve("tbs_gauge", gaugeRef.Id, new ColumnSet("tbs_name"));

                string gaugeName = gauge.GetAttributeValue<string>("tbs_name") ?? string.Empty;

                gaugeOK = !gaugeName.Equals("22ga", StringComparison.OrdinalIgnoreCase);
            }

            bool trimNot22 = trimDescription.IndexOf("22ga", StringComparison.OrdinalIgnoreCase) < 0;

            bool embossEnabled = false;

            EntityReference opportunityRef = opportunityProduct.GetAttributeValue<EntityReference>("opportunityid");

            if (opportunityRef != null)
            {
                Entity opportunity = service.Retrieve("opportunity", opportunityRef.Id, new ColumnSet("tbs_embossedtrims"));

                embossEnabled = opportunity.Contains("tbs_embossedtrims") ? opportunity.GetAttributeValue<bool>("tbs_embossedtrims") : false;
            }

            decimal calculatedUnitPrice;

            if (embossEnabled && colorEmbossable && gaugeOK && trimNot22)
            {
                calculatedUnitPrice = embossPrice * multiplier;

                tracingService.Trace("Emboss Pricing Applied. EmbossPrice={0}, Multiplier={1}", embossPrice, multiplier);
                //throw new InvalidPluginExecutionException("Emboss Pricing Applied. EmbossPrice={0}, Multiplier={1}" + embossPrice + ", " + multiplier + " Calculated Unit Price: " + calculatedUnitPrice);
            }
            else
            {
                calculatedUnitPrice = basePrice * multiplier;

                tracingService.Trace("Base Pricing Applied. BasePrice={0}, Multiplier={1}", basePrice, multiplier);
                //throw new InvalidPluginExecutionException("Base Pricing Applied. BasePrice={0}, Multiplier={1}" + basePrice + ", " + multiplier + " Calculated Unit Price: " + calculatedUnitPrice);
            }

            decimal totalPrice = calculatedUnitPrice * quantity;

            panelTrim["tbs_totalprice"] = new Money(totalPrice);

            tracingService.Trace("Total Price Updated. Quantity={0}, TotalPrice={1}", quantity, totalPrice);
            //throw new InvalidPluginExecutionException("Total Price Updated. Quantity, TotalPrice" + quantity + ", " + totalPrice);
            #endregion
        }

        private void InitProperties(LocalPluginContext localcontext)
        {
            //// Obtain the execution context service from the LocalContext.
            context = localcontext.PluginExecutionContext;
            if (context == null)
            {
                throw new InvalidPluginExecutionException("Failed to retrieve Plugin Execution Context !");
            }

            //Get the Organization Service from the LocalContext
            service = localcontext.OrganizationService;
            if (service == null)
            {
                throw new InvalidPluginExecutionException("Failed to retrieve Organization Service !");
            }

            //Get the Tracing Service from the LocalContext
            tracingService = localcontext.TracingService;
            if (tracingService == null)
            {
                throw new InvalidPluginExecutionException("Failed to retrieve Tracing Service !");
            }
        }
    }
}
