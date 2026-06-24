using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.UI.WebControls;

namespace Falk_Plugins.SalesLifecycle
{
    public class SalesLifecycleProcessPlugin : PluginBase
    {
        public SalesLifecycleProcessPlugin() : base(typeof(SalesLifecycleProcessPlugin)) { }

        #region Private Variables
        private IOrganizationService service { get; set; }
        private IPluginExecutionContext context { get; set; }
        private ITracingService tracingService { get; set; }
        private Entity targetEntity { get; set; }
        #endregion

        protected override void ExecuteCrmPlugin(LocalPluginContext localcontext)
        {
            #region Init
            if (localcontext == null)
                throw new ArgumentNullException(nameof(localcontext));

            InitProperties(localcontext);
            #endregion

            try
            {
                if (context.InputParameters.Contains(CONST_TARGETENTITY) && context.InputParameters[CONST_TARGETENTITY] is Entity)
                {
                    targetEntity = (Entity)context.InputParameters[CONST_TARGETENTITY];
                    if (targetEntity.LogicalName == "tbs_saleslifecycleprocess")
                    {
                        if (context.MessageName == CONST_UPDATE && context.Stage == PostOperation)
                        {
                            if (context.Depth > 1) return;

                            // Execute only when Active Stage changes
                            
                            if (!targetEntity.Contains("activestageid"))
                                return;

                            Entity bpfRecord = service.Retrieve(
                                "tbs_saleslifecycleprocess",
                                targetEntity.Id,
                                new ColumnSet("activestageid", "bpf_opportunityid"));

                            EntityReference activeStageRef =
                                bpfRecord.GetAttributeValue<EntityReference>("activestageid");

                            if (activeStageRef == null)
                                return;

                            Entity processStage = service.Retrieve(
                                "processstage",
                                activeStageRef.Id,
                                new ColumnSet("stagename"));

                            string stageName = processStage.GetAttributeValue<string>("stagename");
                            tracingService.Trace("Current Stage: " + stageName);

                            if (stageName == "Budget Quote")
                            {
                                EntityReference opportunityRef =
                                    bpfRecord.GetAttributeValue<EntityReference>("bpf_opportunityid");

                                if (opportunityRef != null)
                                {
                                    Entity task = new Entity("task");
                                    task["subject"] = "Budget Quote Follow Up"; 
                                    task["regardingobjectid"] = opportunityRef;

                                    service.Create(task);
                                }
                            }
                        }
                    }
                }
            }catch(Exception ex)
            {
                tracingService.Trace("SalesLifecycleProcessPlugin Exception: {0}", ex.ToString());
                throw new InvalidPluginExecutionException($"Error in SalesLifecycleProcessPlugin: {ex.Message}");
            }
        }

        private void InitProperties(LocalPluginContext localcontext)
        {
            throw new NotImplementedException();
        }
    }
}