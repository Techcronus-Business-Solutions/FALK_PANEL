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
                            EntityReference oppProductRef = targetEntity.Contains("tbs_opportunityproduct") ? targetEntity.GetAttributeValue<EntityReference>("tbs_opportunityproduct") : null;

                            if (oppProductRef == null)
                                return;

                            Entity opportunityProduct = service.Retrieve("opportunityproduct", oppProductRef.Id, new ColumnSet("opportunityid", "tbs_priceleveltier", "tbs_exteriorcolor", "tbs_interiorcolor", "tbs_exteriorgauge", "tbs_interiorgauge", "tbs_interiorfinish", "tbs_exteriorfinish"));

                            bool isCustomTrim = targetEntity.GetAttributeValue<bool>("tbs_iscustomtrim");

                            if (isCustomTrim)
                            {
                                CalculateCustomPanelTrimPrice(targetEntity, opportunityProduct);
                            }
                            else
                            {
                                tracingService.Trace("Standard Trim Pricing Calculation Started...");
                                EntityReference trimRef = targetEntity.Contains("tbs_trim") ? targetEntity.GetAttributeValue<EntityReference>("tbs_trim") : null;

                                if (trimRef == null)
                                    throw new InvalidPluginExecutionException("Trim is required.");

                                tracingService.Trace("Trim Exists...");

                                Entity trim = service.Retrieve("tbs_trim", trimRef.Id, new ColumnSet("tbs_name", "tbs_description", "tbs_itemcategory", "tbs_trimpricing"));

                                EntityReference trimPricingRef = trim.Contains("tbs_trimpricing") ? trim.GetAttributeValue<EntityReference>("tbs_trimpricing") : null;

                                if (trimPricingRef == null)
                                {
                                    targetEntity["tbs_unitprice"] = new Money(0);
                                    tracingService.Trace("Trim Pricing Not Found!");
                                    return;
                                }

                                tracingService.Trace("Standard Trim Pricing Exists...");

                                Entity trimPricing = service.Retrieve("tbs_trimpricing", trimPricingRef.Id, new ColumnSet("tbs_unit", "tbs_finish", "tbs_description", "tbs_cost", "tbs_embossingmarkup", "tbs_canadacustomermargin", "tbs_canadacustomerprice", "tbs_cadembossedprice", "tbs_margin", "tbs_price", "tbs_usdembossedprice"));

                                CalculatePanelTrimPrice(targetEntity, trimPricing, opportunityProduct);
                            }
                        }
                    }
                    else if (targetEntity.LogicalName == "tbs_quotepaneltrim")
                    {
                        // ---------------- CREATE (PreOperation) ----------------
                        if (context.Stage == PreOperation && context.MessageName == CONST_CREATE)
                        {
                            EntityReference quoteProductRef = targetEntity.Contains("tbs_quoteproduct") ? targetEntity.GetAttributeValue<EntityReference>("tbs_quoteproduct") : null;

                            if (quoteProductRef == null)
                                return;

                            Entity quoteProduct = service.Retrieve("quotedetail", quoteProductRef.Id, new ColumnSet("quotedetailid", "tbs_priceleveltier", "tbs_exteriorcolor", "tbs_interiorcolor", "tbs_exteriorgauge", "tbs_interiorgauge", "tbs_interiorfinish", "tbs_exteriorfinish"));

                            bool isCustomTrim = targetEntity.GetAttributeValue<bool>("tbs_iscustomtrim");

                            if (isCustomTrim)
                            {
                                CalculateCustomPanelTrimPrice(targetEntity, quoteProduct);
                            }
                            else
                            {
                                tracingService.Trace("Standard Trim Pricing Calculation Started...");
                                EntityReference trimRef = targetEntity.Contains("tbs_trim") ? targetEntity.GetAttributeValue<EntityReference>("tbs_trim") : null;

                                if (trimRef == null)
                                    throw new InvalidPluginExecutionException("Trim is required.");

                                tracingService.Trace("Trim Exists...");

                                Entity trim = service.Retrieve("tbs_trim", trimRef.Id, new ColumnSet("tbs_name", "tbs_description", "tbs_itemcategory", "tbs_trimpricing"));

                                EntityReference trimPricingRef = trim.Contains("tbs_trimpricing") ? trim.GetAttributeValue<EntityReference>("tbs_trimpricing") : null;

                                if (trimPricingRef == null)
                                {
                                    targetEntity["tbs_unitprice"] = new Money(0);
                                    tracingService.Trace("Trim Pricing Not Found!");
                                    return;
                                }

                                tracingService.Trace("Standard Trim Pricing Exists...");

                                Entity trimPricing = service.Retrieve("tbs_trimpricing", trimPricingRef.Id, new ColumnSet("tbs_unit", "tbs_finish", "tbs_description", "tbs_cost", "tbs_embossingmarkup", "tbs_canadacustomermargin", "tbs_canadacustomerprice", "tbs_cadembossedprice", "tbs_margin", "tbs_price", "tbs_usdembossedprice"));

                                CalculatePanelTrimPrice(targetEntity, trimPricing, quoteProduct);
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

        private void CalculatePanelTrimPrice(Entity panelTrim, Entity trimPricing, Entity parentProduct)
        {
            tracingService.Trace("CalculatePanelTrimPrice Started");

            panelTrim["tbs_unitprice"] = new Money(0);

            EntityReference trimRef = panelTrim.Contains("tbs_trim") ? panelTrim.GetAttributeValue<EntityReference>("tbs_trim") : null;

            EntityReference parentRef = null;

            if (panelTrim.LogicalName == "tbs_opppaneltrim")
                parentRef = panelTrim.Contains("tbs_opportunityproduct") ? panelTrim.GetAttributeValue<EntityReference>("tbs_opportunityproduct") : null;
            else
                parentRef = panelTrim.Contains("tbs_quoteproduct") ? panelTrim.GetAttributeValue<EntityReference>("tbs_quoteproduct") : null;

            if (trimRef == null || parentRef == null)
            {
                tracingService.Trace("Trim or Parent Opp/Quote Product is null.");
                return;
            }

            int quantity = panelTrim.Contains("tbs_quantity") ? panelTrim.GetAttributeValue<int>("tbs_quantity") : 1;

            Money basePriceMoney = trimPricing.GetAttributeValue<Money>("tbs_price") ?? new Money(0);
            Money embossPriceMoney = trimPricing.GetAttributeValue<Money>("tbs_usdembossedprice") ?? new Money(0);

            decimal basePrice = basePriceMoney.Value;
            decimal embossPrice = embossPriceMoney.Value;

            string trimDescription = trimPricing.GetAttributeValue<string>("tbs_description") ?? string.Empty;

            OptionSetValue finishOption = trimPricing.Contains("tbs_finish") ? trimPricing.GetAttributeValue<OptionSetValue>("tbs_finish") : null;

            int finishValue = finishOption != null ? finishOption.Value : 0;

            decimal multiplier = 1m;

            #region Determine Category (SS / FM / Tier2)
            string categoryName = "Tier2";

            // Determine finish from Opportunity Product
            EntityReference finishRef = null;

            switch (finishValue)
            {
                case (int)TrimFinish.InteriorMatch:
                    finishRef = parentProduct.Contains("tbs_interiorfinish") ? parentProduct.GetAttributeValue<EntityReference>("tbs_interiorfinish") : null;
                    break;

                case (int)TrimFinish.ExteriorMatch:
                    finishRef = parentProduct.Contains("tbs_exteriorfinish") ? parentProduct.GetAttributeValue<EntityReference>("tbs_exteriorfinish") : null;
                    break;
            }

            string finishName = string.Empty;

            if (finishRef != null)
            {
                Entity finish = service.Retrieve("tbs_finish", finishRef.Id, new ColumnSet("tbs_name"));

                finishName = finish.GetAttributeValue<string>("tbs_name") ?? "";
                tracingService.Trace("Finish Name: " + finishName);
            }

            // Static Comparison for Setting Tier In Opp Panel Trim based on Finish
            if (finishValue == 0 || finishValue == 1)
            {
                categoryName = "Tier1";
            }
            else if (finishValue == 2 || finishValue == 3)
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
            tierQuery.ColumnSet = new ColumnSet("tbs_multiplier", "tbs_name");
            tierQuery.Criteria.AddCondition("tbs_name", ConditionOperator.Equal, categoryName);
            tierQuery.Criteria.AddCondition("tbs_type", ConditionOperator.Equal, 1);

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

                panelTrim["tbs_unitprice"] = new Money(basePrice);
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
                case (int)TrimFinish.InteriorMatch:
                    colorRef = parentProduct.Contains("tbs_interiorcolor") ? parentProduct.GetAttributeValue<EntityReference>("tbs_interiorcolor") : null;
                    gaugeRef = parentProduct.Contains("tbs_interiorgauge") ? parentProduct.GetAttributeValue<EntityReference>("tbs_interiorgauge") : null;
                    break;

                case (int)TrimFinish.ExteriorMatch:
                    colorRef = parentProduct.Contains("tbs_exteriorcolor") ? parentProduct.GetAttributeValue<EntityReference>("tbs_exteriorcolor") : null;
                    gaugeRef = parentProduct.Contains("tbs_exteriorgauge") ? parentProduct.GetAttributeValue<EntityReference>("tbs_exteriorgauge") : null;
                    break;

                case (int)TrimFinish.Galvanized:
                default:
                    decimal galvanizedTotal = basePrice * quantity;
                    panelTrim["tbs_unitprice"] = new Money(basePrice);
                    panelTrim["tbs_totalprice"] = new Money(galvanizedTotal);
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

            if(parentProduct.LogicalName == "opportunity")
            {
                EntityReference opportunityRef = parentProduct.GetAttributeValue<EntityReference>("opportunityid");

                if (opportunityRef != null)
                {
                    Entity opportunity = service.Retrieve("opportunity", opportunityRef.Id, new ColumnSet("tbs_embossedtrims"));
                    embossEnabled = opportunity.Contains("tbs_embossedtrims") ? opportunity.GetAttributeValue<bool>("tbs_embossedtrims") : false;
                }
            }
            else
            {
                EntityReference quoteRef = parentProduct.GetAttributeValue<EntityReference>("quoteid");

                if (quoteRef != null)
                {
                    Entity quote = service.Retrieve("quote", quoteRef.Id, new ColumnSet("tbs_embossedtrims"));
                    embossEnabled = quote.Contains("tbs_embossedtrims") ? quote.GetAttributeValue<bool>("tbs_embossedtrims") : false;
                }
            }            

            decimal calculatedUnitPrice;

            if (embossEnabled && colorEmbossable && gaugeOK && trimNot22)
            {
                calculatedUnitPrice = embossPrice * multiplier;
                tracingService.Trace("Emboss Pricing Applied. EmbossPrice={0}, Multiplier={1}", embossPrice, multiplier);
            }
            else
            {
                calculatedUnitPrice = basePrice * multiplier;
                tracingService.Trace("Base Pricing Applied. BasePrice={0}, Multiplier={1}", basePrice, multiplier);
            }

            decimal totalPrice = calculatedUnitPrice * quantity;

            panelTrim["tbs_unitprice"] = new Money(calculatedUnitPrice);
            //panelTrim["tbs_totalprice"] = new Money(totalPrice);

            tracingService.Trace("Total Price Updated. Quantity={0}, TotalPrice={1}", quantity, totalPrice);
            #endregion
        }

        private void CalculateCustomPanelTrimPrice(Entity panelTrim, Entity parentProduct)
        {
            tracingService.Trace("Calculate Custom Panel Trim Price Started");

            #region Read Panel Trim Fields
            bool isCustomTrim = panelTrim.GetAttributeValue<bool>("tbs_iscustomtrim");

            EntityReference coatingRef = panelTrim.Contains("tbs_coating") ? panelTrim.GetAttributeValue<EntityReference>("tbs_coating") : null;

            decimal width = panelTrim.Contains("tbs_width") ? panelTrim.GetAttributeValue<decimal>("tbs_width") : 0;
            decimal bends = panelTrim.Contains("tbs_bends") ? panelTrim.GetAttributeValue<decimal>("tbs_bends") : 0;
            decimal hems = panelTrim.Contains("tbs_hems") ? panelTrim.GetAttributeValue<decimal>("tbs_hems") : 0;

            int quantity = panelTrim.Contains("tbs_quantity") ? panelTrim.GetAttributeValue<int>("tbs_quantity") : 1;

            tracingService.Trace("Read Panel Trim Fields Completed...");
            #endregion

            #region Fetch Coating
            Entity coating = service.Retrieve("tbs_coating", coatingRef.Id, new ColumnSet("tbs_name", "tbs_materialcost"));
            decimal materialCostCWT = coating.Contains("tbs_materialcost") ? coating.GetAttributeValue<Money>("tbs_materialcost").Value : 0;
            string coatingName = coating.Contains("tbs_name") ? coating.GetAttributeValue<string>("tbs_name") : string.Empty;

            tracingService.Trace("Fetch Coating Completed...");
            #endregion

            #region Fetch Custom Trim Settings
            QueryExpression qe = new QueryExpression("tbs_customtrimsetting");
            qe.ColumnSet = new ColumnSet("tbs_sheetwidth", "tbs_sf", "tbs_cuttime", "tbs_setuptime", "tbs_hourlyrate", "tbs_filmcost", "tbs_filmlength", "tbs_sharpeningcost", "tbs_sharpeningfrequency", "tbs_productiontosales", "tbs_salestocustomer", "tbs_customtrimadder", "tbs_unit", "tbs_canadacustomermargin", "tbs_embossingmarkup");
            qe.Criteria.AddCondition("statecode", ConditionOperator.Equal, 0);

            Entity settings = service.RetrieveMultiple(qe).Entities.FirstOrDefault();

            if (settings == null)
            {
                throw new InvalidPluginExecutionException("Custom Trim Settings not found.");
            }

            decimal sheetWidth = settings.Contains("tbs_sheetwidth") ? settings.GetAttributeValue<decimal>("tbs_sheetwidth") : 0;
            decimal inventorySF = settings.Contains("tbs_sf") ? settings.GetAttributeValue<decimal>("tbs_sf") : 0;
            decimal cutTime = settings.Contains("tbs_cuttime") ? settings.GetAttributeValue<decimal>("tbs_cuttime") : 0;
            decimal setupTime = settings.Contains("tbs_setuptime") ? settings.GetAttributeValue<decimal>("tbs_setuptime") : 0;
            decimal hourlyRate = settings.Contains("tbs_hourlyrate") ? settings.GetAttributeValue<Money>("tbs_hourlyrate").Value : 0;
            decimal filmCost = settings.Contains("tbs_filmcost") ? settings.GetAttributeValue<Money>("tbs_filmcost").Value : 0;
            decimal filmLength = settings.Contains("tbs_filmlength") ? settings.GetAttributeValue<decimal>("tbs_filmlength") : 0;
            decimal sharpeningCost = settings.Contains("tbs_sharpeningcost") ? settings.GetAttributeValue<Money>("tbs_sharpeningcost").Value : 0;
            decimal sharpeningFrequency = settings.Contains("tbs_sharpeningfrequency") ? settings.GetAttributeValue<decimal>("tbs_sharpeningfrequency") : 0;
            decimal productionToSales = settings.Contains("tbs_productiontosales") ? settings.GetAttributeValue<decimal>("tbs_productiontosales") : 0;
            decimal salesToCustomer = settings.Contains("tbs_salestocustomer") ? settings.GetAttributeValue<decimal>("tbs_salestocustomer") : 0;
            decimal customTrimAdder = settings.Contains("tbs_customtrimadder") ? settings.GetAttributeValue<decimal>("tbs_customtrimadder") : 0;
            tracingService.Trace("Fetch Custom Trim Settings Completed...");
            #endregion

            #region Excel Variables
            decimal pcsPerSheet = Math.Floor(sheetWidth / width);

            if (pcsPerSheet <= 0)
                throw new InvalidPluginExecutionException("Invalid Width. Width cannot exceed Sheet Width.");

            decimal sheetLength = 10m;
            decimal qtyMultiplier = 10m;
            #endregion

            #region Emboss Material Cost
            decimal embAdd = 0m;
            bool embossEnabled = false;

            if (parentProduct.LogicalName == "opportunity")
            {
                EntityReference opportunityRef = parentProduct.GetAttributeValue<EntityReference>("opportunityid");

                if (opportunityRef != null)
                {
                    Entity opportunity = service.Retrieve("opportunity", opportunityRef.Id, new ColumnSet("tbs_embossedtrims"));
                    embossEnabled = opportunity.Contains("tbs_embossedtrims") ? opportunity.GetAttributeValue<bool>("tbs_embossedtrims") : false;
                }
            }
            else
            {
                EntityReference quoteRef = parentProduct.GetAttributeValue<EntityReference>("quoteid");

                if (quoteRef != null)
                {
                    Entity quote = service.Retrieve("quote", quoteRef.Id, new ColumnSet("tbs_embossedtrims"));
                    embossEnabled = quote.Contains("tbs_embossedtrims") ? quote.GetAttributeValue<bool>("tbs_embossedtrims") : false;
                }
            }

            // If emboss enabled, retrieve Emb - Add / CWT material cost
            if (embossEnabled)
            {
                QueryExpression embQuery = new QueryExpression("tbs_coating");
                embQuery.ColumnSet = new ColumnSet("tbs_materialcost");
                embQuery.Criteria.AddCondition("tbs_name", ConditionOperator.Equal, "Emb - Add / CWT");

                Entity embRecord = service.RetrieveMultiple(embQuery).Entities.FirstOrDefault();

                if (embRecord != null)
                {
                    embAdd = embRecord.GetAttributeValue<Money>("tbs_materialcost").Value;
                }
            }
            #endregion

            #region Steel Cost / Material
            decimal steelCostSF = CeilingToIncrement(
                Math.Round(
                    Math.Round(
                        Math.Round((materialCostCWT + embAdd) * 1.10m, 2) / 100m,
                    4) * inventorySF,
                2),
            0.01m);

            decimal steelCostLF = Math.Round(steelCostSF * (sheetWidth / 12m), 2);
            decimal material = Math.Round(steelCostLF / pcsPerSheet, 2);
            #endregion

            #region Flat Stock Cut
            decimal flatstockCut = CeilingToIncrement(
                Math.Round(
                    ((Math.Round(cutTime * hourlyRate, 2) / sheetLength)
                    * 0.10m)
                    * 1.10m,
                2),
            0.01m);
            #endregion

            #region Protective Film
            decimal filmCostLF = Math.Round(filmCost / filmLength, 2);

            decimal protFilm = CeilingToIncrement(
                Math.Round(
                    (filmCostLF / pcsPerSheet) * 1.20m,
                2),
            0.025m);
            #endregion

            #region Setup Allocation
            decimal setupFee = Math.Round(setupTime * hourlyRate, 2);

            decimal setupAlloc = CeilingToIncrement(
                Math.Round(
                    (
                        (setupFee /
                        Math.Round((sheetLength * qtyMultiplier) * 1.25m, 2))
                        * 0.225m
                    ) * 1.10m,
                2),
            0.05m);
            #endregion

            #region Slitter Wear
            decimal wearSlitter =
                Math.Round(
                    Math.Round(sharpeningCost / sharpeningFrequency, 2)
                    * 1.05m,
                2);
            #endregion

            #region Bending
            decimal bending = CeilingToIncrement(
                Math.Round(
                    (
                        Math.Round(
                            1.15m * ((bends * 1m) + (hems * 2m)),
                        2)
                        / sheetLength
                    ) * 1.10m,
                2),
            0.01m);
            #endregion

            #region Falk Transfer
            decimal falkTransfer =
                Math.Round(
                    (
                        material +
                        flatstockCut +
                        protFilm +
                        setupAlloc +
                        wearSlitter +
                        bending
                    ) * (1 + productionToSales),
                2);
            #endregion

            #region Falk Customer Price
            decimal falkPrice =
                Math.Round(
                    (falkTransfer * (1 + salesToCustomer))
                    * (1 + customTrimAdder),
                2);
            #endregion

            #region Update Panel Trim
            string Tier1ID = GetEnvironmentVariableValue("tbs_TrimTier1ID");

            panelTrim["tbs_unit"] = settings.GetAttributeValue<EntityReference>("tbs_unit");
            panelTrim["tbs_unitprice"] = new Money(falkPrice);
            //panelTrim["tbs_totalprice"] = new Money(falkPrice * quantity);
            panelTrim["tbs_category"] = new EntityReference("tbs_tier", new Guid(Tier1ID));

            tracingService.Trace("Trim Tier ID: ", Tier1ID);
            tracingService.Trace("pcsPerSheet = {0}", pcsPerSheet);
            tracingService.Trace("steelCostSF = {0}", steelCostSF);
            tracingService.Trace("steelCostLF = {0}", steelCostLF);
            tracingService.Trace("material = {0}", material);
            tracingService.Trace("flatstockCut = {0}", flatstockCut);
            tracingService.Trace("filmCostLF = {0}", filmCostLF);
            tracingService.Trace("protFilm = {0}", protFilm);
            tracingService.Trace("setupFee = {0}", setupFee);
            tracingService.Trace("setupAlloc = {0}", setupAlloc);
            tracingService.Trace("wearSlitter = {0}", wearSlitter);
            tracingService.Trace("bending = {0}", bending);
            tracingService.Trace("falkTransfer = {0}", falkTransfer);
            tracingService.Trace("falkPrice = {0}", falkPrice);
            tracingService.Trace("Custom Trim Price Calculated. Unit Price={0}, Qty={1}, Total={2}", falkPrice, quantity, falkPrice * quantity);
            #endregion
        }

        private decimal CeilingToIncrement(decimal value, decimal increment)
        {
            return Math.Ceiling(value / increment) * increment;
        }

        private string GetEnvironmentVariableValue(string schemaName)
        {
            QueryExpression qe = new QueryExpression("environmentvariabledefinition")
            {
                ColumnSet = new ColumnSet("environmentvariabledefinitionid"),
                Criteria =
                {
                    Conditions =
                    {
                        new ConditionExpression("schemaname", ConditionOperator.Equal, schemaName)
                    }
                }
            };

            var def = service.RetrieveMultiple(qe).Entities.FirstOrDefault();
            if (def == null)
                return null;

            QueryExpression valQuery = new QueryExpression("environmentvariablevalue")
            {
                ColumnSet = new ColumnSet("value"),
                Criteria =
                {
                    Conditions =
                    {
                        new ConditionExpression("environmentvariabledefinitionid", ConditionOperator.Equal, def.Id)
                    }
                }
            };

            var val = service.RetrieveMultiple(valQuery).Entities.FirstOrDefault();
            return val?.GetAttributeValue<string>("value");
        }

        private void InitProperties(LocalPluginContext localcontext)
        {
            // Obtain the execution context service from the LocalContext.
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
