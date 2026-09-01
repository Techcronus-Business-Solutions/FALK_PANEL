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
    public class QuotePlugin : PluginBase
    {
        public QuotePlugin() : base(typeof(QuotePlugin)) { }
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
                        if (context.MessageName == CONST_CREATE && context.Stage == PostOperation)
                        {
                            Entity quoteProduct = service.Retrieve("quotedetail",targetEntity.Id,new ColumnSet("tbs_opportunityproduct"));

                            EntityReference oppProductRef = quoteProduct.GetAttributeValue<EntityReference>("tbs_opportunityproduct");

                            if (oppProductRef == null)
                            {
                                tracingService.Trace("Opportunity Product mapping not found.");
                                return;
                            }
                            CreatequoteLineItems(targetEntity.ToEntityReference(), oppProductRef);
                            CreateQuotePanelAccessories(targetEntity.ToEntityReference(), oppProductRef);
                            CreateQuotePanelTrims(targetEntity.ToEntityReference(), oppProductRef);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                throw new InvalidPluginExecutionException(e.Message, e);
            }
        }

        private void CreatequoteLineItems(EntityReference quoteProductRef, EntityReference oppProductRef)
        {
            QueryExpression query = new QueryExpression("tbs_lineitem");
            query.ColumnSet = new ColumnSet(true);
            query.Criteria.AddCondition("tbs_opportunityproduct", ConditionOperator.Equal, oppProductRef.Id);

            EntityCollection lineitems = service.RetrieveMultiple(query);

            tracingService.Trace($"line items Found : {lineitems.Entities.Count}");

            for (int i = 0; i < lineitems.Entities.Count; i++)
            {
                Entity oppLineItems = lineitems.Entities[i];

                Entity quoteLineItems = new Entity("tbs_quotelineitem");

                foreach (var attribute in oppLineItems.Attributes)
                {
                    if (attribute.Key == "tbs_lineitemid" || attribute.Key == "tbs_opportunityproduct")
                        continue;

                    quoteLineItems[attribute.Key] = attribute.Value;
                }

                quoteLineItems["tbs_quoteproduct"] = quoteProductRef;

                service.Create(quoteLineItems);
            }
        }

        private void CreateQuotePanelAccessories(EntityReference quoteProductRef, EntityReference oppProductRef)
        {
            QueryExpression query = new QueryExpression("tbs_opppanelaccessory");
            query.ColumnSet = new ColumnSet(true);
            query.Criteria.AddCondition("tbs_opportunityproduct",ConditionOperator.Equal,oppProductRef.Id);

            EntityCollection accessories = service.RetrieveMultiple(query);

            tracingService.Trace($"Accessories Found : {accessories.Entities.Count}");

            foreach (Entity oppAccessory in accessories.Entities)
            {
                Entity quoteAccessory = new Entity("tbs_quotepanelaccessory");

                foreach (var attribute in oppAccessory.Attributes)
                {
                    if (attribute.Key == "tbs_opppanelaccessoryid" ||attribute.Key == "tbs_opportunityproduct")
                        continue;

                    quoteAccessory[attribute.Key] = attribute.Value;
                }

                quoteAccessory["tbs_quoteproduct"] = quoteProductRef;

                service.Create(quoteAccessory);
            }
        }

        private void CreateQuotePanelTrims(EntityReference quoteProductRef, EntityReference oppProductRef)
        {
            QueryExpression query = new QueryExpression("tbs_opppaneltrim");
            query.ColumnSet = new ColumnSet(true);
            query.Criteria.AddCondition("tbs_opportunityproduct", ConditionOperator.Equal, oppProductRef.Id);

            EntityCollection trims = service.RetrieveMultiple(query);

            tracingService.Trace($"Trims Found : {trims.Entities.Count}");

            foreach (Entity oppTrim in trims.Entities)
            {
                Entity quoteTrim = new Entity("tbs_quotepaneltrim");

                foreach (var attribute in oppTrim.Attributes)
                {
                    if (attribute.Key == "tbs_opppaneltrimid" || attribute.Key == "tbs_opportunityproduct")
                        continue;

                    quoteTrim[attribute.Key] = attribute.Value;
                }

                quoteTrim["tbs_quoteproduct"] = quoteProductRef;

                service.Create(quoteTrim);
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
    }
}