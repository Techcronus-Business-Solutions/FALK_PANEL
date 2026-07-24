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
                            Entity quoteProductEntity = (Entity)context.InputParameters["Target"];

                            Entity quote = service.Retrieve("quote",quoteProductEntity.Id,new ColumnSet("opportunityid"));

                            QueryExpression query = new QueryExpression("opportunityproduct");
                            query.ColumnSet = new ColumnSet(true);
                            query.Criteria.AddCondition("opportunityid", ConditionOperator.Equal, quote.GetAttributeValue<EntityReference>("opportunityid"));
                            EntityCollection oppProducts = service.RetrieveMultiple(query);

                            EntityReference oppProduct = quoteProductEntity.GetAttributeValue<EntityReference>("opportunityproductid");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                throw new InvalidPluginExecutionException(e.Message, e);
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