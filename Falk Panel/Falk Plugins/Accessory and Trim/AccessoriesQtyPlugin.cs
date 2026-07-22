using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Activities.Statements;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Caching;
using System.Web.UI.WebControls;
using System.Windows.Forms;

namespace Falk_Plugins.Accessory_and_Trim
{
    public class AccessoriesQtyPlugin : PluginBase
    {
        public AccessoriesQtyPlugin() : base(typeof(AccessoriesQtyPlugin)) { }

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
                    if (targetEntity.LogicalName == "opportunityproduct")
                    {
                        if (context.MessageName == CONST_UPDATE && context.Stage == PostOperation)
                        {
                            try
                            {
                                Guid oppProductId = targetEntity.Id;
                                tracingService.Trace("Opportunity ProductId = " + oppProductId.ToString());
                                OrganizationRequest customApiRequest = new OrganizationRequest("tbs_CalculateAccessoryQty");

                                customApiRequest["tbs_oppProduct"] = oppProductId;

                                OrganizationResponse customApiResponse = service.Execute(customApiRequest);
                            }
                            catch (Exception ex)
                            {
                                throw new InvalidPluginExecutionException(ex.Message);
                            }
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

