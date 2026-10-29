using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Activities.Expressions;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.UI.WebControls;

namespace Falk_Plugins.Pricing_Master
{
    public class QuoteProductPricingPlugin : PluginBase
    {
        public QuoteProductPricingPlugin() : base(typeof(QuoteProductPricingPlugin)) { }
        #region Private Variables
        private IOrganizationService service { get; set; }
        private IPluginExecutionContext context { get; set; }
        private ITracingService tracingService { get; set; }
        private IOrganizationServiceFactory factory { get; set; }
        private Entity targetEntity { get; set; }
        #endregion
        protected override void ExecuteCrmPlugin(LocalPluginContext localcontext)
        {

            if (localcontext == null)
            {
                throw new ArgumentNullException(nameof(localcontext));
            }
            InitProperties(localcontext);

            try
            {
                if (context.InputParameters.Contains(CONST_TARGETENTITY) && context.InputParameters[CONST_TARGETENTITY] is Entity)
                {
                    targetEntity = (Entity)context.InputParameters[CONST_TARGETENTITY];
                    if (targetEntity.LogicalName == "quotedetail")
                    {
                        if (context.MessageName == CONST_CREATE && context.Stage == PreOperation)
                        {
                            if(GetOptionSetAttributeValue(targetEntity, "skippricecalculation") == 1)
                            {
                                tracingService.Trace("Skipping price calculation for Quote Product.");
                                return;
                            }
                            CalculatePricing(targetEntity, new Entity("quotedetail"));
                        }

                        if (context.MessageName == CONST_UPDATE && context.Stage == PreOperation)
                        {
                            Entity preImage = context.PreEntityImages["PreImage"];

                            CalculatePricing(targetEntity, preImage);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                tracingService.Trace($"CreatePanelAccessoryAndTrim Error : {ex}");
                throw new InvalidPluginExecutionException("Error occurred while creating Panel Accessory and Panel Trim records.", ex);
            }
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

        private decimal roundValues(decimal value)
        {
            return Math.Round(value, 2, MidpointRounding.AwayFromZero);
        }

        private string GetTerritoryRegion(Guid quoteId)
        {
            try
            {
                //Fetch Quote Territory
                Entity Quote = service.Retrieve("quote", quoteId, new ColumnSet("tbs_territory"));


                EntityReference territoryRef = Quote.GetAttributeValue<EntityReference>("tbs_territory")
                                            ?? Quote.GetAttributeValue<EntityReference>("tbs_territory");
                EntityReference currencyRef = Quote.GetAttributeValue<EntityReference>("transactioncurrencyid")
                                            ?? Quote.GetAttributeValue<EntityReference>("transactioncurrencyid");

                if (territoryRef == null || currencyRef == null)
                    return string.Empty;

                // Validate Currency Code is CAD
                Entity fetchCurrency = service.Retrieve(currencyRef.LogicalName, currencyRef.Id, new ColumnSet("isocurrencycode"));
                string currencyCode = fetchCurrency.GetAttributeValue<string>("isocurrencycode");

                if (string.IsNullOrEmpty(currencyCode) || !currencyCode.Equals("CAD", StringComparison.OrdinalIgnoreCase))
                {
                    return string.Empty; // If not CAD, return
                }

                //Fetch Territory record and its parent
                Entity territory = service.Retrieve(territoryRef.LogicalName, territoryRef.Id, new ColumnSet("name", "parentterritoryid"));
                string territoryName = territory.GetAttributeValue<string>("name");

                if (!string.IsNullOrEmpty(territoryName) && (territoryName.Equals("Canada East", StringComparison.OrdinalIgnoreCase) || territoryName.Equals("Canada West", StringComparison.OrdinalIgnoreCase)))
                {
                    return territoryName;
                }

                // If not Canada East/West directly, check Parent Territory
                EntityReference parentRef = territory.GetAttributeValue<EntityReference>("parentterritoryid");
                if (parentRef != null)
                {
                    Entity parentTerritory = service.Retrieve(parentRef.LogicalName, parentRef.Id, new ColumnSet("name"));
                    string parentName = parentTerritory.GetAttributeValue<string>("name");

                    if (!string.IsNullOrEmpty(parentName) && (parentName.Equals("Canada East", StringComparison.OrdinalIgnoreCase) || parentName.Equals("Canada West", StringComparison.OrdinalIgnoreCase)))
                    {
                        return parentName;
                    }
                }
            }
            catch (Exception ex)
            {
                tracingService.Trace($"Error resolving territory region: {ex.Message}");
            }

            return string.Empty;
        }
        private void CalculatePricing(Entity target, Entity preImage)
        {
            decimal basePrice = target.GetAttributeValue<Money>("tbs_baseprice")?.Value
                ?? preImage.GetAttributeValue<Money>("tbs_baseprice")?.Value
                ?? 0;

            decimal totalPropertyPrice = target.GetAttributeValue<Money>("tbs_totalpropertiesprice")?.Value
                ?? preImage.GetAttributeValue<Money>("tbs_totalpropertiesprice")?.Value
                ?? 0;

            decimal usPrice = roundValues(basePrice) + roundValues(totalPropertyPrice);

            // Territory & Product Markup Calculation
            try
            {
                EntityReference QuoteRef = target.GetAttributeValue<EntityReference>("quoteid")
                                              ?? preImage.GetAttributeValue<EntityReference>("quoteid");

                EntityReference productRef = target.GetAttributeValue<EntityReference>("productid")
                                          ?? preImage.GetAttributeValue<EntityReference>("productid");

                if (QuoteRef != null)
                {
                    string region = GetTerritoryRegion(QuoteRef.Id);
                    tracingService.Trace($"Resolved Region: {region}");

                    if (!string.IsNullOrEmpty(region) && productRef != null)
                    {
                        // Fetch Panel Type
                        Entity product = service.Retrieve(productRef.LogicalName, productRef.Id, new ColumnSet("name"));
                        string productName = product.GetAttributeValue<string>("name") ?? string.Empty;
                        tracingService.Trace($"Product Name: {productName}");

                        decimal markupMultiplier = 1.0m;

                        if (region.Equals("Canada East", StringComparison.OrdinalIgnoreCase))
                        {
                            if (productName.IndexOf("SSR", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                productName.IndexOf("RRP", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                markupMultiplier = 1.05m;
                            }
                            else if (productName.IndexOf("CSW", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                markupMultiplier = 1.03m;
                            }
                        }
                        else if (region.Equals("Canada West", StringComparison.OrdinalIgnoreCase))
                        {
                            if (productName.IndexOf("SSR", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                productName.IndexOf("RRP", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                markupMultiplier = 1.05m;
                            }
                            else if (productName.IndexOf("CSW", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                productName.IndexOf("HFW", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                markupMultiplier = 1.03m;
                            }
                        }

                        usPrice *= markupMultiplier;
                        tracingService.Trace($"US Price after Markup ({markupMultiplier}): {usPrice}");
                    }
                }
            }
            catch (Exception ex)
            {
                tracingService.Trace($"Error applying markup: {ex.Message}");
            }
            target["tbs_usprice"] = new Money(roundValues(usPrice));

            decimal sqft = target.Contains("quantity")
                ? target.GetAttributeValue<decimal>("quantity")
                : preImage.GetAttributeValue<decimal>("quantity");

            decimal usdAdjustment = target.GetAttributeValue<Money>("tbs_usdpriceadjustment")?.Value
                ?? preImage.GetAttributeValue<Money>("tbs_usdpriceadjustment")?.Value
                ?? 0;

            decimal upcharge = 0;

            if (sqft > 0)
            {
                if (sqft < 1000)
                {
                    upcharge = (usPrice * 0.10m) + (1200m / sqft);
                }
                else if (sqft < 3500)
                {
                    upcharge = (usPrice * 0.10m) + (750m / sqft);
                }
            }

            target["tbs_smallorderupcharge"] = new Money(roundValues(upcharge));

            decimal pricePerUnit = roundValues(usPrice) + roundValues(usdAdjustment) + roundValues(upcharge);

            target["ispriceoverridden"] = true;
            target["priceperunit"] = new Money(roundValues(pricePerUnit));
            target["extendedamount"] = new Money(roundValues(pricePerUnit) * roundValues(sqft));
        }
    }
}