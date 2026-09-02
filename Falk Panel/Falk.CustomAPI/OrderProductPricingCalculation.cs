using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Falk.CustomAPI
{
    public class OrderProductPricingCalculation : CustomAPIBase
    {
        public OrderProductPricingCalculation() : base(typeof(OrderProductPricingCalculation)) { }

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
                var product = GetInputRef("Product");
                var panelThickness = GetInputRef("PanelThickness");
                var tier = GetInputRef("PriceLevelTier");
                var exteriorFinish = GetInputRef("ExteriorFinish");
                var interiorFinish = GetInputRef("InteriorFinish");
                var exteriorGauge = GetInputRef("ExteriorGauge");
                var interiorGauge = GetInputRef("InteriorGauge");
                int? exteriorColorCategory = GetInputChoice("OrderExteriorColorCategory", false);
                var exteriorColor = GetInputRef("ExteriorColor", false);
                int? interiorColorCategory = GetInputChoice("OrderInteriorColorCategory", false);
                var interiorColor = GetInputRef("InteriorColor", false);
                int? interiorEmboss = GetInputChoice("InteriorEmboss");
                int? exteriorEmboss = GetInputChoice("ExteriorEmboss");

                var orderProduct = GetInputRef("Target");

                string interiorCategoryText = GetColorCategoryText(interiorColorCategory);
                string exteriorCategoryText = GetColorCategoryText(exteriorColorCategory);

                //throw new InvalidPluginExecutionException(exteriorFinish.Name + " " + exteriorColor.Name + " " + exteriorGauge.Name + " Color Category: " + interiorColor.Name + " tier: " + tier.Name + " exterior EMboss: " + exteriorEmboss + " Interior Emboss: " + interiorEmboss);

                #region Calculate Interior/Exterior Finish Price
                var interiorConditions = new Dictionary<string, object>
                {
                    { "tbs_paneltype", product.Id },
                    { "tbs_panelthickness", panelThickness.Id },
                    { "tbs_interiorfinish", interiorFinish.Id },
                    { "tbs_interiorgauge", interiorGauge.Id }
                };

                decimal interiorPrice = 0;
                bool interiorFound = false;

                if (interiorColor != null)
                {
                    interiorConditions.Add("tbs_interiorcolorcategory", interiorColor.Id);

                    interiorPrice = GetPrice(
                        "tbs_pricingmasterinterior",
                        "tbs_interiorprice",
                        interiorConditions,
                        out interiorFound);
                }

                if (!interiorFound && interiorColorCategory != null)
                {
                    interiorPrice = GetPriceByCategory(
                        "tbs_pricingmasterinterior",
                        "tbs_interiorprice",
                        interiorConditions,
                        "tbs_interiorcolorcategory",
                        interiorColorCategory.Value,
                        out interiorFound);
                }

                var exteriorConditions = new Dictionary<string, object>
                {
                    { "tbs_paneltype", product.Id },
                    { "tbs_panelthickness", panelThickness.Id },
                    { "tbs_exteriorfinish", exteriorFinish.Id },
                    { "tbs_exteriorgauge", exteriorGauge.Id }
                };

                decimal exteriorPrice = 0;
                bool exteriorFound = false;

                if (exteriorColor != null)
                {
                    exteriorConditions.Add("tbs_exteriorcolorcategory", exteriorColor.Id);

                    exteriorPrice = GetPrice(
                        "tbs_pricingmasterexterior",
                        "tbs_exteriorprice",
                        exteriorConditions,
                        out exteriorFound);
                }

                if (!exteriorFound && !string.IsNullOrEmpty(exteriorCategoryText))
                {
                    exteriorPrice = GetPriceByCategory(
                        "tbs_pricingmasterexterior",
                        "tbs_exteriorprice",
                        exteriorConditions,
                        "tbs_exteriorcolorcategory",
                        exteriorColorCategory.Value,
                        out exteriorFound);
                }

                context.OutputParameters["InteriorPrice"] = new Money(interiorPrice);
                context.OutputParameters["ExteriorPrice"] = new Money(exteriorPrice);
                #endregion

                #region Calculate Interior/Exterior Emboss Price
                decimal interiorEmbossPrice = 0;
                decimal exteriorEmbossPrice = 0;

                if (interiorEmboss == 1) // No
                {
                    interiorEmbossPrice = GetEmbossPrice(panelThickness.Id);
                }

                if (exteriorEmboss == 1) // No
                {
                    exteriorEmbossPrice = GetEmbossPrice(panelThickness.Id);
                }

                context.OutputParameters["InteriorEmbossPrice"] = new Money(interiorEmbossPrice);
                context.OutputParameters["ExteriorEmbossPrice"] = new Money(exteriorEmbossPrice);
                #endregion

                #region Calculate Base Price
                if (panelThickness == null || tier == null)
                {
                    //target["tbs_baseprice"] = null;
                    return;
                }

                Entity tierRecord = service.Retrieve("tbs_tier", tier.Id, new ColumnSet("tbs_multiplier"));

                decimal multiplier = Convert.ToDecimal(tierRecord.GetAttributeValue<int>("tbs_multiplier"));

                Entity thickness = service.Retrieve("tbs_thickness", panelThickness.Id, new ColumnSet("tbs_baseprice"));

                Money basePriceMoney = thickness.GetAttributeValue<Money>("tbs_baseprice");

                if (basePriceMoney == null)
                {
                    //target["tbs_baseprice"] = null;
                    return;
                }

                decimal calculatedPrice = (basePriceMoney.Value * multiplier) / 100m;
                #endregion

                #region Save Record In Order Product
                Entity salesorderdetail = new Entity("salesorderdetail", orderProduct.Id);

                salesorderdetail["tbs_interiorprice"] = new Money(roundValues(interiorPrice));
                salesorderdetail["tbs_exteriorprice"] = new Money(roundValues(exteriorPrice));
                salesorderdetail["tbs_ribbingmodelsweatherprice"] = new Money(0);
                salesorderdetail["tbs_ribbingmodeusinteriorprice"] = new Money(0);
                salesorderdetail["tbs_embossinglsweatherprice"] = new Money(roundValues(exteriorEmbossPrice));
                salesorderdetail["tbs_embossingusinteriorprice"] = new Money(roundValues(interiorEmbossPrice));
                salesorderdetail["tbs_baseprice"] = new Money(roundValues(calculatedPrice));

                decimal totalPropertyPrice = roundValues(interiorPrice) + roundValues(exteriorPrice) + roundValues(interiorEmbossPrice) + roundValues(exteriorEmbossPrice);
                salesorderdetail["tbs_totalpropertiesprice"] = new Money(roundValues(totalPropertyPrice));

                decimal usPrice = roundValues(calculatedPrice) + roundValues(totalPropertyPrice);
                tracingService.Trace(usPrice.ToString());
                salesorderdetail["tbs_usprice"] = new Money(roundValues(usPrice));

                Entity order = service.Retrieve("salesorderdetail", orderProduct.Id, new ColumnSet("quantity", "tbs_usdpriceadjustment"));
                decimal sqft = order.GetAttributeValue<decimal>("quantity");
                tracingService.Trace(sqft.ToString());

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
                tracingService.Trace(upcharge.ToString());

                salesorderdetail["tbs_smallorderupcharge"] = new Money(roundValues(upcharge));

                decimal usdAdjustment = order.GetAttributeValue<Money>("tbs_usdpriceadjustment")?.Value ?? 0;
                decimal pricePerUnit = roundValues(usPrice) + roundValues(usdAdjustment) + roundValues(upcharge);

                decimal lineTotal = sqft * roundValues(pricePerUnit);

                tracingService.Trace(roundValues(pricePerUnit).ToString());

                tracingService.Trace(roundValues(lineTotal).ToString());

                Entity existingLine = service.Retrieve(
                    "salesorderdetail",
                    orderProduct.Id,
                    new ColumnSet("ispriceoverridden"));

                bool currentOverride = existingLine.GetAttributeValue<bool?>("ispriceoverridden") ?? false;

                if (!currentOverride)
                {
                    salesorderdetail["ispriceoverridden"] = true;
                }

                decimal currentPrice = existingLine.GetAttributeValue<Money>("priceperunit")?.Value ?? 0;

                if (currentPrice != roundValues(pricePerUnit))
                {
                    salesorderdetail["priceperunit"] = new Money(roundValues(pricePerUnit));
                }

                    //salesorderdetail["ispriceoverridden"] = true;
                    //salesorderdetail["priceperunit"] = new Money(roundValues(pricePerUnit));
                    salesorderdetail["baseamount"] = new Money(roundValues(lineTotal));
                //foreach (var attr in salesorderdetail.Attributes)
                //{
                //    tracingService.Trace("Updating Field = " + attr.Key);
                //}
                service.Update(salesorderdetail);
                #endregion
            }
            catch (Exception ex)
            {
                tracingService.Trace("OrderProductPricingCalculation Custom API Exception: {0}", ex.ToString());
                throw new InvalidPluginExecutionException($"Error in OrderProductPricingCalculation Custom API: {ex.Message}");
            }
        }

        private decimal roundValues(decimal value)
        {
            return Math.Round(value, 2, MidpointRounding.AwayFromZero);
        }

        private EntityReference GetInputRef(string parameterName, bool required = true)
        {
            if (!context.InputParameters.Contains(parameterName))
            {
                if (required)
                    throw new InvalidPluginExecutionException($"Input parameter '{parameterName}' is missing.");

                return null;
            }

            if (context.InputParameters[parameterName] == null)
            {
                if (required)
                    throw new InvalidPluginExecutionException($"Input parameter '{parameterName}' is null.");

                return null;
            }

            if (!(context.InputParameters[parameterName] is EntityReference entityReference))
                throw new InvalidPluginExecutionException($"Input parameter '{parameterName}' is invalid.");

            return entityReference;
        }

        private int? GetInputChoice(string parameterName, bool required = true)
        {
            if (!context.InputParameters.Contains(parameterName))
            {
                if (required)
                    throw new InvalidPluginExecutionException($"Input parameter '{parameterName}' is missing.");

                return null;
            }

            if (context.InputParameters[parameterName] == null)
                return null;

            if (!(context.InputParameters[parameterName] is int value))
                throw new InvalidPluginExecutionException($"Input parameter '{parameterName}' is invalid.");

            return value;
        }

        private decimal GetPrice(string entityName, string priceField, Dictionary<string, object> conditions, out bool found)
        {
            QueryExpression query = new QueryExpression(entityName)
            {
                ColumnSet = new ColumnSet(priceField),
                TopCount = 1
            };

            foreach (var item in conditions)
            {
                query.Criteria.AddCondition(item.Key, ConditionOperator.Equal, item.Value);
            }

            Entity record = service.RetrieveMultiple(query).Entities.FirstOrDefault();

            if (record == null)
            {
                found = false;
                return 0;
            }

            found = true;
            return record.GetAttributeValue<Money>(priceField)?.Value ?? 0;
        }

        private decimal GetEmbossPrice(Guid panelThicknessId)
        {
            Entity thickness = service.Retrieve(
                "tbs_thickness",
                panelThicknessId,
                new ColumnSet("tbs_embossingnoprice"));

            return thickness.GetAttributeValue<Money>("tbs_embossingnoprice")?.Value ?? 0;
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
        private string GetColorCategoryText(int? category)
        {
            if (!category.HasValue)
                return null;

            switch (category.Value)
            {
                case 0:
                    return "CAT 1";

                case 1:
                    return "CAT 2";

                case 2:
                    return "CAT 3";

                case 3:
                    return "CAT 4";

                case 4:
                    return "CAT 5";

                case 5:
                    return "CAT 6";

                case 6:
                    return "Stainless Steel";

                default:
                    return null;
            }
        }
        private decimal GetPriceByCategory(string entityName, string priceField, Dictionary<string, object> conditions, string colorLookupField, int categoryValue, out bool found)
        {
            QueryExpression query = new QueryExpression(entityName)
            {
                ColumnSet = new ColumnSet(priceField, colorLookupField)
            };

            foreach (var item in conditions)
            {
                query.Criteria.AddCondition(item.Key, ConditionOperator.Equal, item.Value);
            }

            EntityCollection records = service.RetrieveMultiple(query);

            foreach (Entity record in records.Entities)
            {
                EntityReference colorRef = record.GetAttributeValue<EntityReference>(colorLookupField);

                if (colorRef == null)
                    continue;

                Entity color = service.Retrieve(colorRef.LogicalName, colorRef.Id, new ColumnSet("tbs_colorcategory"));

                int colorCategory = color.GetAttributeValue<OptionSetValue>("tbs_colorcategory")?.Value ?? -1;

                if (colorCategory == categoryValue)
                {
                    found = true;
                    return record.GetAttributeValue<Money>(priceField)?.Value ?? 0;
                }
            }
            found = false;
            return 0;
        }
    }
}
