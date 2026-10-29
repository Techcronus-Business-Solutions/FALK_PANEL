using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Crm.Sdk.Messages;


namespace Falk.CustomAPI
{
    public class CloneOpportunity: CustomAPIBase
    {
        public CloneOpportunity() : base(typeof(CloneOpportunity)) { }

        #region Private Variables
        private IOrganizationService service { get; set; }
        private IPluginExecutionContext context { get; set; }
        private ITracingService tracingService { get; set; }
        #endregion

        protected override void ExecuteCrmPlugin(LocalPluginContext localcontext)
        {
            if (localcontext == null)
                throw new ArgumentNullException(nameof(localcontext));

            InitProperties(localcontext);

            try
            {
                tracingService.Trace("Clone Opportunity Started");

                EntityReference oppRef = GetInputRef("Target");

                tracingService.Trace("Opportunity Id = " + oppRef.Id);

                Guid newOpportunityId = CloneOpportunityRecord(oppRef.Id);

                tracingService.Trace("New Opportunity Id = " + newOpportunityId);

                context.OutputParameters["NewOpportunityId"] = newOpportunityId;
            }
            catch (Exception ex)
            {
                tracingService.Trace(ex.ToString());

                throw new InvalidPluginExecutionException("Clone Opportunity Error : " + ex.Message);
            }
        }
        private Guid CloneOpportunityRecord(Guid opportunityId)
        {
            Entity oldOpp = service.Retrieve("opportunity", opportunityId, new ColumnSet(true));

            Entity newOpp = new Entity("opportunity");

            foreach (var attribute in oldOpp.Attributes)
            {
                if (attribute.Key == "opportunityid")
                    continue;

                newOpp[attribute.Key] = attribute.Value;
            }

            if (oldOpp.Contains("name"))
            {
                newOpp["name"] = oldOpp.GetAttributeValue<string>("name") + " - Copy";
            }

            Guid newOpportunityId = service.Create(newOpp);

            return newOpportunityId;
        }
        private EntityReference GetInputRef(string parameterName, bool required = true)
        {
            if (!context.InputParameters.Contains(parameterName))
            {
                if (required)
                    throw new InvalidPluginExecutionException($"Input parameter '{parameterName}' is missing.");

                return null;
            }

            if (context.InputParameters[parameterName] == null)
            {
                if (required)
                    throw new InvalidPluginExecutionException($"Input parameter '{parameterName}' is null.");

                return null;
            }

            if (!(context.InputParameters[parameterName] is EntityReference entityReference))
                throw new InvalidPluginExecutionException($"Input parameter '{parameterName}' is invalid.");

            return entityReference;
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
