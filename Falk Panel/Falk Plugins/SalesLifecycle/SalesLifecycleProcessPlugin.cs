using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
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
        private Entity preImage { get; set; }
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
                    tracingService.Trace("Entity Name: " + targetEntity.LogicalName);


                    foreach (var attr in targetEntity.Attributes)
                    {
                        tracingService.Trace("Target Attribute: " + attr.Key);
                    }
                    if (targetEntity.LogicalName == "tbs_saleslifecycleprocess")
                    {
                        // Restrict stage movement if task is not completed
                        if (context.MessageName == CONST_UPDATE && context.Stage == PreValidation)
                        {
                            if (!targetEntity.Contains("activestageid"))
                                return;

                            if (!context.PreEntityImages.Contains("PreImage"))
                            {
                                tracingService.Trace("PreImage not found.");
                                return;
                            }

                            preImage = context.PreEntityImages["PreImage"];

                            EntityReference currentStageRef =
                                preImage.GetAttributeValue<EntityReference>("activestageid");

                            EntityReference newStageRef =
                                targetEntity.GetAttributeValue<EntityReference>("activestageid");

                            if (currentStageRef == null || newStageRef == null)
                                return;

                            if (currentStageRef.Id == newStageRef.Id)
                                return;

                            Entity currentStage = service.Retrieve(
    "processstage",
    currentStageRef.Id,
    new ColumnSet("stagename"));

                            string currentStageName =
                                currentStage.GetAttributeValue<string>("stagename");

                            tracingService.Trace($"Current Stage: {currentStageName}");

                            if (!string.Equals(currentStageName, "Budget Quote", StringComparison.OrdinalIgnoreCase))
                            {
                                return;
                            }

                            tracingService.Trace(
    $"PreImage contains bpf_opportunityid: {preImage.Contains("bpf_opportunityid")}");
                            EntityReference opportunityRef =
    preImage.GetAttributeValue<EntityReference>("bpf_opportunityid");

                            if (opportunityRef == null)
                                return;
                            tracingService.Trace($"Opportunity Id: {opportunityRef.Id}");

                            QueryExpression taskQuery = new QueryExpression("task");
                            taskQuery.ColumnSet = new ColumnSet("activityid");

                            taskQuery.Criteria.AddCondition(
                                "regardingobjectid",
                                ConditionOperator.Equal,
                                opportunityRef.Id);                          

                            taskQuery.Criteria.AddCondition(
                                "statecode",
                                ConditionOperator.Equal,
                                0);

                            EntityCollection tasks = service.RetrieveMultiple(taskQuery);

                            tracingService.Trace($"Open Task Count: {tasks.Entities.Count}");

                            if (tasks.Entities.Count > 0)
                            {
                                throw new InvalidPluginExecutionException(
                                    "Please complete the Budget Quote task before moving to the next stage.");
                            }
                        }

                        // Create task when Budget Quote stage is reached
                        if (context.MessageName == CONST_UPDATE && context.Stage == PostOperation)
                        {
                            if (context.Depth > 1)
                                return;

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

                            string stageName =
                                processStage.GetAttributeValue<string>("stagename");

                            tracingService.Trace($"Current Stage: {stageName}");

                            if (stageName == "Budget Quote")
                            {
                                EntityReference opportunityRef =
                                    bpfRecord.GetAttributeValue<EntityReference>("bpf_opportunityid");

                                if (opportunityRef == null)
                                    return;

                                QueryExpression taskQuery = new QueryExpression("task");
                                taskQuery.ColumnSet = new ColumnSet("activityid");

                                taskQuery.Criteria.AddCondition(
                                    "regardingobjectid",
                                    ConditionOperator.Equal,
                                    opportunityRef.Id);

                                taskQuery.Criteria.AddCondition(
                                    "subject",
                                    ConditionOperator.Equal,
                                    "Budget Quote Follow Up");

                                EntityCollection existingTasks =
                                    service.RetrieveMultiple(taskQuery);

                                if (existingTasks.Entities.Count == 0)
                                {
                                    Entity task = new Entity("task");
                                    task["subject"] = "Budget Quote Follow Up";
                                    task["regardingobjectid"] = opportunityRef;

                                    service.Create(task);

                                    tracingService.Trace("Budget Quote task created.");
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                tracingService.Trace("SalesLifecycleProcessPlugin Exception: {0}", ex.ToString());
                throw new InvalidPluginExecutionException($"Error in SalesLifecycleProcessPlugin: {ex.Message}");
            }
        }

        private void InitProperties(LocalPluginContext localcontext)
        {
            service = localcontext.OrganizationService;
            context = localcontext.PluginExecutionContext;
            tracingService = localcontext.TracingService;
        }
    }
}