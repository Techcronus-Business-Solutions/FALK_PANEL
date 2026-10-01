using Falk_Plugins.Pricing_Master;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Newtonsoft.Json.Linq;
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

        private Entity PreImage { get; set; }
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
                    if(targetEntity.LogicalName == "salesorder")
                    {
                        EntityReference customer = targetEntity.GetAttributeValue<EntityReference>("customerid");
                        if (customer != null && customer.LogicalName == "account")
                        {
                            Entity account = service.Retrieve("account", customer.Id, new ColumnSet("customertypecode"));
                            if (account != null)
                            {
                                OptionSetValue relationshipType = account.GetAttributeValue<OptionSetValue>("customertypecode");
                                if (relationshipType != null && relationshipType.Value == 3)
                                {
                                    
                                }
                                else
                                {
                                    throw new InvalidPluginExecutionException("Potential customer relationship type is not customer please select valid customer.");
                                }
                            }
                            else
                            {
                                throw new InvalidPluginExecutionException("Potential customer is not slected.");
                            }
                        }
                    }
                    if (targetEntity.LogicalName == "salesorderdetail")
                    {
                        if (context.MessageName == CONST_CREATE && context.Stage == PreOperation)
                        {
                            GenerateProductForOrderProduct();
                        }
                        if (context.MessageName == CONST_UPDATE && context.Stage == PreOperation)
                        {
                            tracingService.Trace("Update - PreOperation");
                            if (context.PreEntityImages.Contains("PreImage"))
                            {
                                tracingService.Trace("Pre-Image exist");
                                PreImage = context.PreEntityImages["PreImage"];
                            }
                            GenerateProductForOrderProduct();
                        }
                        if (context.MessageName == CONST_CREATE && context.Stage == PostOperation)
                        {
                            Entity orderProduct = service.Retrieve("salesorderdetail", targetEntity.Id, new ColumnSet("tbs_quoteproduct"));

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
                JObject defaultUnitSetup = JObject.Parse(GetEnvironmentVariable(service, "tbs_DefaultUnitSetup"));

                Guid uomScheduleId = Guid.Parse(
                    defaultUnitSetup["uomschedule"].ToString()
                );

                Guid uomId = Guid.Parse(
                    defaultUnitSetup["uom"].ToString()
                );

                Entity product = new Entity("product");

                product["name"] = productId;
                product["productnumber"] = productId;
                product["defaultuomscheduleid"] = new EntityReference("uomschedule", uomScheduleId);
                product["defaultuomid"] = new EntityReference("uom", uomId);
                product["quantitydecimal"] = 0;

                productGuid = service.Create(product);

                tracingService.Trace("New Product created: " + productGuid);
            }

            targetEntity["tbs_bcitemid"] = productId;

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

            string panelFamily = GetLookupCode("productid");

            string thickness = GetLookupCode("tbs_panelthickness");

            //get from product
            string core = GetCore("productid");
            tracingService.Trace(core);

            string exteriorFinish = GetLookupCode("tbs_exteriorfinish");
            tracingService.Trace(exteriorFinish);

            string exteriorColor = GetLookupCode("tbs_exteriorcolor");
            tracingService.Trace(exteriorColor);

            string exteriorGauge = GetLookupCode("tbs_exteriorgauge");
            tracingService.Trace(exteriorGauge);

            string exteriorProfile = GetLookupCode("tbs_exteriorprofile");
            tracingService.Trace(exteriorProfile);

            //Yes/No


            int exteriorEmbossint;
            OptionSetValue exteriorEmbossOption = null;

            if (targetEntity.Contains("tbs_exterioremboss"))
            {
                exteriorEmbossOption = targetEntity.GetAttributeValue<OptionSetValue>("tbs_exterioremboss");
            }
            else if (PreImage != null && PreImage.Contains("tbs_exterioremboss"))
            {
                exteriorEmbossOption = PreImage.GetAttributeValue<OptionSetValue>("tbs_exterioremboss");
            }

            if (exteriorEmbossOption == null)
            {
                throw new InvalidPluginExecutionException("Required field 'tbs_exterioremboss' is empty.");
            }

            exteriorEmbossint = exteriorEmbossOption.Value;

            string exteriorEmboss;

            if (exteriorEmbossint == 1)
            {
                exteriorEmboss = "N";
            }
            else
            {
                exteriorEmboss = "Y";
            }

            tracingService.Trace("Exterior Emboss: " + exteriorEmboss);

            string interiorFinish = GetLookupCode("tbs_interiorfinish");
            tracingService.Trace(interiorFinish);

            string interiorColor = GetLookupCode("tbs_interiorcolor");
            tracingService.Trace(interiorColor);

            string interiorGauge = GetLookupCode("tbs_interiorgauge");
            tracingService.Trace(interiorGauge);

            string interiorProfile = GetLookupCode("tbs_interiorprofile");
            tracingService.Trace(interiorProfile);

            //Yes/No
            int interiorEmbossint;
            OptionSetValue interiorEmbossOption = null;

            if (targetEntity.Contains("tbs_interioremboss"))
            {
                interiorEmbossOption = targetEntity.GetAttributeValue<OptionSetValue>("tbs_interioremboss");
            }
            else if (PreImage != null && PreImage.Contains("tbs_interioremboss"))
            {
                interiorEmbossOption = PreImage.GetAttributeValue<OptionSetValue>("tbs_interioremboss");
            }

            if (interiorEmbossOption == null)
            {
                throw new InvalidPluginExecutionException("Required field 'tbs_interioremboss' is empty.");
            }

            interiorEmbossint = interiorEmbossOption.Value;

            string interiorEmboss;

            if (interiorEmbossint == 1)
            {
                interiorEmboss = "N";
            }
            else
            {
                interiorEmboss = "Y";
            }

            tracingService.Trace("Interior Emboss: " + interiorEmboss);

            string productId = scheme + panelFamily + thickness + core + exteriorFinish + exteriorColor + exteriorGauge + exteriorProfile + exteriorEmboss + interiorFinish + interiorColor + interiorGauge + interiorProfile + interiorEmboss;
            tracingService.Trace(productId);

            tracingService.Trace("Final Product ID: " + productId);

            if (productId.Length != 20)
            {
                throw new InvalidPluginExecutionException("Generated Product ID '" + productId + "' must contain exactly 20 characters. " + "Current length: " + productId.Length);
            }

            return productId;
        }

        private string GetLookupCode(string lookupField)
        {
            EntityReference lookup;
            if (PreImage != null)
            {
                tracingService.Trace("PreImage not null");
                lookup = targetEntity.Contains(lookupField) ? targetEntity.GetAttributeValue<EntityReference>(lookupField) : PreImage.GetAttributeValue<EntityReference>(lookupField);
                tracingService.Trace(lookup.Id.ToString());
            }
            else
            {
                tracingService.Trace("PreImage null");
                lookup = targetEntity.Contains(lookupField) ? targetEntity.GetAttributeValue<EntityReference>(lookupField) : null;
                tracingService.Trace(lookup.Id.ToString());
            }
            if (lookup == null)
            {
                throw new InvalidPluginExecutionException("Required field '" + lookupField + "' is empty.");
            }

            Entity lookupRecord = service.Retrieve(lookup.LogicalName, lookup.Id, new ColumnSet("tbs_code"));

            string code = lookupRecord.Contains("tbs_code") ? lookupRecord.GetAttributeValue<string>("tbs_code") : null;

            if (string.IsNullOrWhiteSpace(code))
            {
                throw new InvalidPluginExecutionException("Code is not configured for " + lookupField + ".");
            }
            return code.Trim().ToUpperInvariant();
        }
        private string GetCore(string lookupField)
        {
            EntityReference lookup;
            if (PreImage != null)
            {
                tracingService.Trace("PreImage not null");
                lookup = targetEntity.Contains(lookupField) ? targetEntity.GetAttributeValue<EntityReference>(lookupField) : PreImage.GetAttributeValue<EntityReference>(lookupField);
                tracingService.Trace(lookup.Id.ToString());
            }
            else
            {
                tracingService.Trace("PreImage null");
                lookup = targetEntity.Contains(lookupField) ? targetEntity.GetAttributeValue<EntityReference>(lookupField) : null;
                tracingService.Trace(lookup.Id.ToString());
            }

            if (lookup == null)
            {
                throw new InvalidPluginExecutionException("Required field '" + lookupField + "' is empty.");
            }

            QueryExpression query = new QueryExpression("product");

            LinkEntity core = query.AddLink("tbs_core", "tbs_core", "tbs_coreid");
            core.EntityAlias = "core";
            core.Columns.AddColumn("tbs_code");

            query.ColumnSet = new ColumnSet(true);
            query.Criteria.AddCondition("productid", ConditionOperator.Equal, lookup.Id);
            EntityCollection lookupRecord = service.RetrieveMultiple(query);

            if (lookupRecord.Entities.FirstOrDefault() == null)
            {
                throw new InvalidPluginExecutionException("Required field '" + core + "' is empty.");
            }
            string code = lookupRecord.Entities.FirstOrDefault().GetAttributeValue<AliasedValue>("core.tbs_code").Value.ToString();

            if (string.IsNullOrWhiteSpace(code))
            {
                throw new InvalidPluginExecutionException("Code is not configured for " + lookupField + ".");
            }
            return code.Trim().ToUpperInvariant();
        }

        private Entity GetProductByProductNumber(string productId)
        {
            QueryExpression query = new QueryExpression("product");

            query.ColumnSet = new ColumnSet("productid", "productnumber");

            query.Criteria.AddCondition("productnumber", ConditionOperator.Equal, productId);

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
                    if (attribute.Key == "tbs_quotepanelaccessoryid" || attribute.Key == "tbs_quoteproduct")
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