using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Falk_Plugins
{
    public class OrderLineItemPlugin : PluginBase
    {
        public OrderLineItemPlugin() : base(typeof(OrderLineItemPlugin)) { }

        private IOrganizationService service { get; set; }

        private IPluginExecutionContext context { get; set; }

        private ITracingService tracingService { get; set; }

        private Entity targetEntity { get; set; }

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
                    if (targetEntity.LogicalName == "tbs_orderlineitem")
                    {
                        if (context.MessageName == CONST_CREATE && context.Stage == PostOperation)
                        {
                            EntityReference orderProductRef = targetEntity.GetAttributeValue<EntityReference>("tbs_orderproduct");
                            if (orderProductRef == null)
                            {
                                return;
                            }
                            Guid orderProd = orderProductRef.Id;

                            Entity OrderProduct = service.Retrieve("salesorderdetail", orderProd, new ColumnSet("skippricecalculation"));
                            if (GetOptionSetAttributeValue(OrderProduct, "skippricecalculation") == 1)
                            {
                                return;
                            }

                            #region Calculate Order Product Rollups
                            try
                            {
                                decimal width = targetEntity.Contains("tbs_widthpanel") ? targetEntity.GetAttributeValue<decimal>("tbs_widthpanel") : 0;

                                string fetchXml = $@"
                                            <fetch aggregate='true'>
                                              <entity name='tbs_orderlineitem'>
                                                <attribute name='tbs_totalsqft' alias='sqft' aggregate='sum' />
                                                <filter>
                                                  <condition attribute='tbs_orderproduct' operator='eq' value='{orderProd}' />
                                                </filter>
                                              </entity>
                                            </fetch>";

                                Entity SQFT = service.RetrieveMultiple(new FetchExpression(fetchXml)).Entities.FirstOrDefault();
                                decimal totalSQFT = 0;

                                if (SQFT != null && SQFT.Contains("sqft"))
                                {
                                    AliasedValue sqftAlias = SQFT.GetAttributeValue<AliasedValue>("sqft");
                                    if (sqftAlias != null && sqftAlias.Value != null)
                                    {
                                        totalSQFT = Convert.ToDecimal(sqftAlias.Value);
                                    }
                                }
                                tracingService.Trace($"totalSQFT: {totalSQFT}");

                                decimal linearFt = width > 0 ? (totalSQFT * 12) / width : 0;

                                Entity OrderProductToUpdate = new Entity("salesorderdetail", orderProd);
                                OrderProductToUpdate["quantity"] = totalSQFT;
                                OrderProductToUpdate["tbs_linearfeet"] = linearFt;
                                service.Update(OrderProductToUpdate);
                            }
                            catch (Exception e)
                            {
                                tracingService.Trace($"Error Occurred in Calculating Total Sqft :{e.Message}");
                                throw new InvalidPluginExecutionException(e.Message);
                            }

                            // Force rollup total amount of panels on the order product
                            try
                            {
                                CalculateRollupFieldRequest calcularRollup = new CalculateRollupFieldRequest
                                {
                                    Target = new EntityReference("salesorderdetail", orderProd),
                                    FieldName = "tbs_totalamountofpanels"
                                };
                                CalculateRollupFieldResponse calcularRollupResult = (CalculateRollupFieldResponse)service.Execute(calcularRollup);
                            }
                            catch (Exception e)
                            {
                                tracingService.Trace($"Error Occurred in Calculating Total Number of Panel :{e.Message}");
                                throw new InvalidPluginExecutionException(e.Message);
                            }

                            if (orderProd != Guid.Empty)
                            {
                                TriggerOrderProductPricing(orderProd);
                            }
                            #endregion
                        }

                        else if (context.MessageName == CONST_UPDATE && context.Stage == PostOperation)
                        {
                            Entity PreImage = context.PreEntityImages["PreImage"];
                            EntityReference orderProductRef = targetEntity.GetAttributeValue<EntityReference>("tbs_orderproduct")
                                                        ?? PreImage.GetAttributeValue<EntityReference>("tbs_orderproduct");
                            Guid orderProd = orderProductRef != null ? orderProductRef.Id : Guid.Empty;
                            bool shouldRecalculatePricing = false;

                            if (orderProd == Guid.Empty)
                            {
                                return;
                            }

                            #region Calculate Order Product Rollups
                            // Calculate total SQFT & Linear Ft of all line items on the order product
                            if (targetEntity.Contains("tbs_totalsqft"))
                            {
                                try
                                {
                                    decimal width = targetEntity.Contains("tbs_widthpanel")
                                        ? GetDecimalAttributeValue(targetEntity, "tbs_widthpanel")
                                        : GetDecimalAttributeValue(PreImage, "tbs_widthpanel");

                                    string fetchXml = $@"
                                            <fetch aggregate='true'>
                                              <entity name='tbs_orderlineitem'>
                                                <attribute name='tbs_totalsqft' alias='sqft' aggregate='sum' />
                                                <filter>
                                                  <condition attribute='tbs_orderproduct' operator='eq' value='{orderProd}' />
                                                </filter>
                                              </entity>
                                            </fetch>";

                                    Entity SQFT = service.RetrieveMultiple(new FetchExpression(fetchXml)).Entities.FirstOrDefault();
                                    decimal totalSQFT = 0;

                                    if (SQFT != null && SQFT.Contains("sqft"))
                                    {
                                        AliasedValue sqftAlias = SQFT.GetAttributeValue<AliasedValue>("sqft");
                                        if (sqftAlias != null && sqftAlias.Value != null)
                                        {
                                            totalSQFT = Convert.ToDecimal(sqftAlias.Value);
                                        }
                                    }
                                    tracingService.Trace($"totalSQFT: {totalSQFT}");

                                    decimal linearFt = width > 0 ? (totalSQFT * 12) / width : 0;

                                    Entity OrderProductToUpdate = new Entity("salesorderdetail", orderProd);
                                    OrderProductToUpdate["quantity"] = totalSQFT;
                                    OrderProductToUpdate["tbs_linearfeet"] = linearFt;
                                    service.Update(OrderProductToUpdate);
                                    shouldRecalculatePricing = true;
                                }
                                catch (Exception e)
                                {
                                    tracingService.Trace($"Error Occurred in Calculating Total Sqft :{e.Message}");
                                    throw new InvalidPluginExecutionException(e.Message);
                                }
                            }

                            // Force rollup total amount of panels on the order product if number of panel is changed
                            if (targetEntity.Contains("tbs_numberofpanels"))
                            {
                                try
                                {
                                    CalculateRollupFieldRequest calcularRollup = new CalculateRollupFieldRequest
                                    {
                                        Target = new EntityReference("salesorderdetail", orderProd),
                                        FieldName = "tbs_totalamountofpanels"
                                    };
                                    CalculateRollupFieldResponse calcularRollupResult = (CalculateRollupFieldResponse)service.Execute(calcularRollup);
                                }
                                catch (Exception e)
                                {
                                    tracingService.Trace($"Error Occurred in Calculating Total Number of Panel :{e.Message}");
                                    throw new InvalidPluginExecutionException(e.Message);
                                }
                            }

                            if (shouldRecalculatePricing && orderProd != Guid.Empty)
                            {
                                TriggerOrderProductPricing(orderProd);
                            }
                            #endregion
                        }
                    }
                }

                else if (context.InputParameters.Contains("Target") && (context.InputParameters["Target"] is EntityReference))
                {
                    EntityReference targetEntityRef = (EntityReference)context.InputParameters["Target"];
                    if (targetEntityRef.LogicalName == "tbs_orderlineitem")
                    {
                        if (context.MessageName == CONST_DELETE && context.Stage == PostOperation)
                        {
                            Entity PreImage = context.PreEntityImages["PreImage"];
                            EntityReference orderProductRef = PreImage.GetAttributeValue<EntityReference>("tbs_orderproduct");
                            Guid orderProd = orderProductRef != null ? orderProductRef.Id : Guid.Empty;

                            if (orderProd == Guid.Empty)
                            {
                                return;
                            }

                            #region Calculate Order Product Rollups
                            // Calculate total SQFT & Linear Ft of all line items on the order product
                            try
                            {
                                decimal width = GetDecimalAttributeValue(PreImage, "tbs_widthpanel");

                                string fetchXml = $@"
                                            <fetch aggregate='true'>
                                              <entity name='tbs_orderlineitem'>
                                                <attribute name='tbs_totalsqft' alias='sqft' aggregate='sum' />
                                                <filter>
                                                  <condition attribute='tbs_orderproduct' operator='eq' value='{orderProd}' />
                                                </filter>
                                              </entity>
                                            </fetch>";

                                Entity SQFT = service.RetrieveMultiple(new FetchExpression(fetchXml)).Entities.FirstOrDefault();
                                decimal totalSQFT = 0;

                                if (SQFT != null && SQFT.Contains("sqft"))
                                {
                                    AliasedValue sqftAlias = SQFT.GetAttributeValue<AliasedValue>("sqft");
                                    if (sqftAlias != null && sqftAlias.Value != null)
                                    {
                                        totalSQFT = Convert.ToDecimal(sqftAlias.Value);
                                    }
                                }
                                tracingService.Trace($"totalSQFT: {totalSQFT}");

                                decimal linearFt = width > 0 ? (totalSQFT * 12) / width : 0;

                                Entity OrderProductToUpdate = new Entity("salesorderdetail", orderProd);
                                OrderProductToUpdate["quantity"] = totalSQFT;
                                OrderProductToUpdate["tbs_linearfeet"] = linearFt;
                                service.Update(OrderProductToUpdate);
                            }
                            catch (Exception e)
                            {
                                tracingService.Trace($"Error Occurred in Calculating Total Sqft :{e.Message}");
                                throw new InvalidPluginExecutionException(e.Message);
                            }

                            // Force rollup total amount of panels on the order product
                            try
                            {
                                CalculateRollupFieldRequest calcularRollup = new CalculateRollupFieldRequest
                                {
                                    Target = new EntityReference("salesorderdetail", orderProd),
                                    FieldName = "tbs_totalamountofpanels"
                                };
                                CalculateRollupFieldResponse calcularRollupResult = (CalculateRollupFieldResponse)service.Execute(calcularRollup);
                            }
                            catch (Exception e)
                            {
                                tracingService.Trace($"Error Occurred in Calculating Total Number of Panel :{e.Message}");
                                throw new InvalidPluginExecutionException(e.Message);
                            }

                            if (orderProd != Guid.Empty)
                            {
                                TriggerOrderProductPricing(orderProd);
                            }
                            #endregion
                        }
                    }
                }
            }
            catch (Exception e)
            {
                throw new InvalidPluginExecutionException("exception : " + e.Message);
            }
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

        private void TriggerOrderProductPricing(Guid orderProductId)
        {
            Entity orderProd = service.Retrieve(
                "salesorderdetail",
                orderProductId,
                new ColumnSet(
                    "productid",
                    "tbs_panelthickness",
                    "tbs_priceleveltier",
                    "tbs_exteriorfinish",
                    "tbs_interiorfinish",
                    "tbs_exteriorgauge",
                    "tbs_interiorgauge",
                    "tbs_orderexteriorcolorcategory",
                    "tbs_exteriorcolor",
                    "tbs_orderinteriorcolorcategory",
                    "tbs_interiorcolor",
                    "tbs_interioremboss",
                    "tbs_exterioremboss"
                ));

            var req = new OrganizationRequest("tbs_FalkCustomAPIOrderProductPricingCalculation")
            {
                ["Target"] = new EntityReference("salesorderdetail", orderProductId),
                ["Product"] = orderProd.GetAttributeValue<EntityReference>("productid"),
                ["PanelThickness"] = orderProd.GetAttributeValue<EntityReference>("tbs_panelthickness"),
                ["PriceLevelTier"] = orderProd.GetAttributeValue<EntityReference>("tbs_priceleveltier"),
                ["ExteriorFinish"] = orderProd.GetAttributeValue<EntityReference>("tbs_exteriorfinish"),
                ["InteriorFinish"] = orderProd.GetAttributeValue<EntityReference>("tbs_interiorfinish"),
                ["ExteriorGauge"] = orderProd.GetAttributeValue<EntityReference>("tbs_exteriorgauge"),
                ["InteriorGauge"] = orderProd.GetAttributeValue<EntityReference>("tbs_interiorgauge"),
                ["OrderExteriorColorCategory"] = orderProd.GetAttributeValue<OptionSetValue>("tbs_orderexteriorcolorcategory")?.Value,
                ["ExteriorColor"] = orderProd.GetAttributeValue<EntityReference>("tbs_exteriorcolor"),
                ["OrderInteriorColorCategory"] = orderProd.GetAttributeValue<OptionSetValue>("tbs_orderinteriorcolorcategory")?.Value,
                ["InteriorColor"] = orderProd.GetAttributeValue<EntityReference>("tbs_interiorcolor"),
                ["InteriorEmboss"] = orderProd.GetAttributeValue<OptionSetValue>("tbs_interioremboss")?.Value,
                ["ExteriorEmboss"] = orderProd.GetAttributeValue<OptionSetValue>("tbs_exterioremboss")?.Value
            };

            service.Execute(req);
        }
    }
}