using Falk_Plugins.Pricing_Master;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Falk_Plugins
{
    public class OrderPlugin : PluginBase
    {
        public OrderPlugin() : base(typeof(OrderPlugin)) { }
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
                    if (targetEntity.LogicalName == "salesorderdetail")
                    {
                        if(context.MessageName == CONST_CREATE && context.Stage == PreOperation)
                        {
                            GenerateProductForOrderProduct();
                        }
                        if (context.MessageName == CONST_CREATE && context.Stage == PostOperation)
                        {
                            Entity orderProduct = service.Retrieve("salesorderdetail", targetEntity.Id,new ColumnSet("tbs_quoteproduct"));

                            EntityReference quoteProductRef = orderProduct.GetAttributeValue<EntityReference>("tbs_quoteproduct");

                            if (quoteProductRef == null)
                            {
                                tracingService.Trace("Opportunity Product mapping not found.");
                                return;
                            }
                            CreateOrderLineItems(targetEntity.ToEntityReference(), quoteProductRef);
                            CreateOrderPanelAccessories(targetEntity.ToEntityReference(), quoteProductRef);
                            CreateOrderPanelTrims(targetEntity.ToEntityReference(), quoteProductRef);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                throw new InvalidPluginExecutionException(e.Message, e);
            }
        }
        private void GenerateProductForOrderProduct()
        {
            tracingService.Trace("GenerateProductForOrderProduct started.");

            string productId = GenerateProductId();

            if (string.IsNullOrWhiteSpace(productId))
            {
                throw new InvalidPluginExecutionException("Product ID could not be generated.");
            }

            tracingService.Trace("Generated Product ID: " + productId);

            Entity existingProduct = GetProductByProductNumber(productId);

            Guid productGuid;

            if (existingProduct != null)
            {
                productGuid = existingProduct.Id;

                tracingService.Trace("Existing Product found: " + productGuid);
            }
            else
            {
                Entity product = new Entity("product");

                product["name"] = productId;
                product["productnumber"] = productId;

                productGuid = service.Create(product);

                tracingService.Trace("New Product created: " + productGuid);
            }

            targetEntity["productid"] = new EntityReference("product", productGuid);

            tracingService.Trace("Product lookup set on Order Product.");
        }


        private string GenerateProductId()
        {
            /*
             * FALK Scheme A
             *
             * Position:
             *
             * 1      Scheme
             * 2      Panel Family
             * 3      Thickness
             * 4      Core
             * 5      Exterior Finish
             * 6-9    Exterior Color
             * 10     Exterior Gauge
             * 11     Exterior Profile
             * 12     Exterior Emboss
             * 13     Interior Finish
             * 14-17  Interior Color
             * 18     Interior Gauge
             * 19     Interior Profile
             * 20     Interior Emboss
             */

            string scheme = "A";

            string panelFamily = GetLookupCode("tbs_panelfamily");

            string thickness = GetLookupCode("tbs_panelthickness");

            string core = GetLookupCode("tbs_core");

            string exteriorFinish = GetLookupCode("tbs_exteriorfinish");

            string exteriorColor = GetLookupCode("tbs_exteriorcolor");

            string exteriorGauge = GetLookupCode("tbs_exteriorgauge");

            string exteriorProfile = GetLookupCode("tbs_exteriorprofile");

            string exteriorEmboss = GetLookupCode("tbs_exterioremboss");

            string interiorFinish = GetLookupCode("tbs_interiorfinish");

            string interiorColor = GetLookupCode("tbs_interiorcolor");

            string interiorGauge = GetLookupCode("tbs_interiorgauge");

            string interiorProfile = GetLookupCode("tbs_interiorprofile");

            string interiorEmboss = GetLookupCode("tbs_interioremboss");

            string productId = scheme + panelFamily + thickness + core + exteriorFinish + exteriorColor + exteriorGauge + exteriorProfile + exteriorEmboss + interiorFinish + interiorColor + interiorGauge + interiorProfile + interiorEmboss;


            tracingService.Trace("Final Product ID: " + productId);

            if (productId.Length != 20)
            {
                throw new InvalidPluginExecutionException("Generated Product ID '" + productId + "' must contain exactly 20 characters. " + "Current length: " + productId.Length);
            }

            return productId;
        }

        private string GetLookupCode(string lookupField)
        {
            EntityReference lookup = targetEntity.GetAttributeValue<EntityReference>(lookupField);

            if (lookup == null)
            {
                throw new InvalidPluginExecutionException("Required field '" + lookupField + "' is empty.");
            }

            Entity lookupRecord = service.Retrieve(lookup.LogicalName, lookup.Id, new ColumnSet("tbs_code"));

            string code = lookupRecord.GetAttributeValue<string>("tbs_code");

            if (string.IsNullOrWhiteSpace(code))
            {
                throw new InvalidPluginExecutionException("Code is not configured for " + lookupField + ".");
            }
            return code.Trim().ToUpperInvariant();
        }

        private Entity GetProductByProductNumber(string productId)
        {
            QueryExpression query = new QueryExpression("product");

            query.ColumnSet = new ColumnSet("productid","productnumber");

            query.Criteria.AddCondition("productnumber",ConditionOperator.Equal,productId);

            query.TopCount = 2;

            EntityCollection products = service.RetrieveMultiple(query);

            if (products.Entities.Count > 1)
            {
                throw new InvalidPluginExecutionException("Multiple Products found with Product ID: " + productId);
            }

            return products.Entities.FirstOrDefault();
        }

        private void CreateOrderLineItems(EntityReference orderProductRef, EntityReference quoteProductRef)
        {
            QueryExpression query = new QueryExpression("tbs_quotelineitem");
            query.ColumnSet = new ColumnSet(true);
            query.Criteria.AddCondition("tbs_quoteproduct", ConditionOperator.Equal, quoteProductRef.Id);

            EntityCollection quotelineitems = service.RetrieveMultiple(query);

            tracingService.Trace($"line items Found : {quotelineitems.Entities.Count}");

            foreach (Entity quoteLineItem in quotelineitems.Entities)
            {
                Entity orderLineItems = new Entity("tbs_orderlineitem");

                foreach (var attribute in quoteLineItem.Attributes)
                {
                    if (attribute.Key == "tbs_quotelineitemid" || attribute.Key == "tbs_quoteproduct")
                        continue;

                    orderLineItems[attribute.Key] = attribute.Value;
                }

                orderLineItems["tbs_orderproduct"] = orderProductRef;

                service.Create(orderLineItems);
            }
        }

        private void CreateOrderPanelAccessories(EntityReference orderProductRef, EntityReference quoteProductRef)
        {
            QueryExpression query = new QueryExpression("tbs_quotepanelaccessory");
            query.ColumnSet = new ColumnSet(true);
            query.Criteria.AddCondition("tbs_quoteproduct", ConditionOperator.Equal, quoteProductRef.Id);

            EntityCollection quoteAccessories = service.RetrieveMultiple(query);

            tracingService.Trace($"Accessories Found : {quoteAccessories.Entities.Count}");

            foreach (Entity quoteAccessory in quoteAccessories.Entities)
            {
                Entity orderAccessory = new Entity("tbs_orderpanelaccessory");

                foreach (var attribute in quoteAccessory.Attributes)
                {
                    if (attribute.Key == "tbs_quotepanelaccessoryid" ||attribute.Key == "tbs_quoteproduct")
                        continue;

                    orderAccessory[attribute.Key] = attribute.Value;
                }

                orderAccessory["tbs_orderproduct"] = orderProductRef;

                service.Create(orderAccessory);
            }
        }

        private void CreateOrderPanelTrims(EntityReference orderProductRef, EntityReference quoteProductRef)
        {
            QueryExpression query = new QueryExpression("tbs_quotepaneltrim");
            query.ColumnSet = new ColumnSet(true);
            query.Criteria.AddCondition("tbs_quoteproduct", ConditionOperator.Equal, quoteProductRef.Id);

            EntityCollection quotetrims = service.RetrieveMultiple(query);

            tracingService.Trace($"Trims Found : {quotetrims.Entities.Count}");

            foreach (Entity quoteTrim in quotetrims.Entities)
            {
                Entity orderTrim = new Entity("tbs_orderpaneltrim");

                foreach (var attribute in quoteTrim.Attributes)
                {
                    if (attribute.Key == "tbs_quotepaneltrimid" || attribute.Key == "tbs_quoteproduct")
                        continue;

                    orderTrim[attribute.Key] = attribute.Value;
                }

                orderTrim["tbs_orderproduct"] = orderProductRef;

                service.Create(orderTrim);
            }
            tracingService.Trace("trims Created");
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