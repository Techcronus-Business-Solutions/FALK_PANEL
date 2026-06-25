using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;

namespace Falk_Plugins.SalesLifecycle
{
    public class SalesLifecycleProcessPlugin : PluginBase
    {
        private const string BudgetQuoteTaskSubject = "Prepare RFQ and Quote for ";
        private const string BudgetQuoteStageName = "Budget Quote";

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
                if (!context.InputParameters.Contains(CONST_TARGETENTITY) ||
                    !(context.InputParameters[CONST_TARGETENTITY] is Entity))
                {
                    return;
                }

                targetEntity = (Entity)context.InputParameters[CONST_TARGETENTITY];
                tracingService.Trace("Entity Name: " + targetEntity.LogicalName);

                if (targetEntity.LogicalName != "tbs_saleslifecycleprocess")
                    return;

                if (!targetEntity.Contains("activestageid"))
                    return;


                if (context.MessageName == CONST_UPDATE && context.Stage == PostOperation)
                {
                    HandlePostOperation();
                }
            }
            catch (InvalidPluginExecutionException)
            {
                throw;
            }
            catch (Exception ex)
            {
                tracingService.Trace("SalesLifecycleProcessPlugin Exception: {0}", ex.ToString());
                throw new InvalidPluginExecutionException($"Error in SalesLifecycleProcessPlugin: {ex.Message}");
            }
        }

        private void HandlePostOperation()
        {
            tracingService.Trace($"PostOperation Depth: {context.Depth}, MessageName: {context.MessageName}");

            Entity bpfRecord = service.Retrieve(
                "tbs_saleslifecycleprocess",
                targetEntity.Id,
                new ColumnSet("activestageid", "bpf_opportunityid"));

            tracingService.Trace("BPF retrieved");
            EntityReference activeStageRef = bpfRecord.GetAttributeValue<EntityReference>("activestageid");

            if (activeStageRef == null)
                return;

            string stageName = GetStageName(activeStageRef.Id);
            tracingService.Trace($"Stage name = {stageName}");

            if (stageName == BudgetQuoteStageName)
            {
                EntityReference opportunityRef = bpfRecord.GetAttributeValue<EntityReference>("bpf_opportunityid");
                tracingService.Trace($"Opportunity ref = {opportunityRef}");

                if (opportunityRef == null)
                    return;

                // Retrieve Opportunity Name
                string opportunityName = opportunityRef.Name;

                if (string.IsNullOrWhiteSpace(opportunityName))
                {
                    Entity opportunity = service.Retrieve(
                        "opportunity",
                        opportunityRef.Id,
                        new ColumnSet("name"));

                    opportunityName = opportunity.GetAttributeValue<string>("name");
                    tracingService.Trace($"Opportunity name = {opportunityName}");
                }

                Entity task = new Entity("task");
                task["subject"] = $"{BudgetQuoteTaskSubject}{opportunityName}";
                task["regardingobjectid"] = opportunityRef;

                service.Create(task);

                tracingService.Trace($"Budget Quote task created for Opportunity: {opportunityName}");
            }

            tracingService.Trace($"Current Stage: {stageName}");
        }

        private string GetStageName(Guid stageId)
        {
            Entity stage = service.Retrieve("processstage", stageId, new ColumnSet("stagename"));
            return stage?.GetAttributeValue<string>("stagename");
        }

        private void InitProperties(LocalPluginContext localcontext)
        {
            service = localcontext.OrganizationService;
            context = localcontext.PluginExecutionContext;
            tracingService = localcontext.TracingService;
        }
    }
}