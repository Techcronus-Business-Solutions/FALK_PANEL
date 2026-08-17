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