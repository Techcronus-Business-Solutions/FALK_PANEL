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
    public class CloneOpportunityProduct : CustomAPIBase
    {
        public CloneOpportunityProduct() : base(typeof(CloneOpportunityProduct)) { }

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
                tracingService.Trace("execution started");

                EntityReference oppProductRef = GetInputRef("Target");
                tracingService.Trace("Opportunity ProductId = " + oppProductRef.Id.ToString());

                // Optional: when supplied, the cloned line is created directly on this opportunity
                Guid? newOpportunityId = GetOptionalGuid("NewOpportunityId");
                tracingService.Trace("NewOpportunityId = " + (newOpportunityId.HasValue ? newOpportunityId.Value.ToString() : "not supplied"));

                Guid ClonedOppProduct = CloneOpportunityProductEnt(oppProductRef.Id, newOpportunityId);
                CreateOpportunityLineItems(oppProductRef.Id, ClonedOppProduct);
                CreateOpportunityPanelAccessories(oppProductRef.Id, ClonedOppProduct);
                CreateOpportunityPanelTrims(oppProductRef.Id, ClonedOppProduct);

                CalculateRollupFieldRequest calcularRollupAcces = new CalculateRollupFieldRequest
                {
                    Target = new EntityReference("opportunityproduct", ClonedOppProduct),
                    FieldName = "tbs_accessoriespricetotal"
                };
                service.Execute(calcularRollupAcces);

                CalculateRollupFieldRequest calcularRollupTrim = new CalculateRollupFieldRequest
                {
                    Target = new EntityReference("opportunityproduct", ClonedOppProduct),
                    FieldName = "tbs_trimspricetotal"
                };
                service.Execute(calcularRollupTrim);

                Entity OppProduct = new Entity("opportunityproduct", ClonedOppProduct);
                OppProduct["skippricecalculation"] = new OptionSetValue(0);
                service.Update(OppProduct);

                context.OutputParameters["ClonedOpportunityProductId"] = ClonedOppProduct;
            }
            catch (Exception ex)
            {
                tracingService.Trace("CloneOpportunityProduct Custom API Exception: {0}", ex.ToString());
                throw new InvalidPluginExecutionException($"Error in CloneOpportunityProduct Custom API: {ex.Message}");
            }
        }

        private Guid CloneOpportunityProductEnt(Guid oppProductId, Guid? newOpportunityId)
        {
            Entity oldOppProduct = service.Retrieve("opportunityproduct", oppProductId, new ColumnSet(true));
            Entity newOppProduct = new Entity("opportunityproduct");

            foreach (var attribute in oldOppProduct.Attributes)
            {
                if (attribute.Key == "opportunityproductid")
                    continue;

                newOppProduct[attribute.Key] = attribute.Value;
            }

            // Create the line on the NEW (open) opportunity, not on the original (possibly Won/Lost) one
            if (newOpportunityId.HasValue)
            {
                newOppProduct["opportunityid"] = new EntityReference("opportunity", newOpportunityId.Value);
            }

            newOppProduct["skippricecalculation"] = new OptionSetValue(1);

            Guid newOppProductId = service.Create(newOppProduct);
            tracingService.Trace($"New Opportunity Product Created with ID: {newOppProductId}");
            return newOppProductId;
        }

        private void CreateOpportunityLineItems(Guid oldOppProductId, Guid newOppProductId)
        {
            QueryExpression query = new QueryExpression("tbs_lineitem");
            query.ColumnSet = new ColumnSet("tbs_numberofpanels", "tbs_ft", "tbs_in", "tbs_cbmode", "tbs_cblengthinch", "tbs_linearftinch", "tbs_totalsqft", "tbs_widthpanel");
            query.Criteria.AddCondition("tbs_opportunityproduct", ConditionOperator.Equal, oldOppProductId);

            EntityCollection oldLineitems = service.RetrieveMultiple(query);

            tracingService.Trace($"line items Found : {oldLineitems.Entities.Count}");

            foreach (Entity oppLineItems in oldLineitems.Entities)
            {
                Entity newOppLineItem = new Entity("tbs_lineitem");

                foreach (var attribute in oppLineItems.Attributes)
                {
                    if (attribute.Key == "tbs_lineitemid" || attribute.Key == "tbs_opportunityproduct")
                        continue;

                    newOppLineItem[attribute.Key] = attribute.Value;
                }

                newOppLineItem["tbs_opportunityproduct"] = new EntityReference("opportunityproduct", newOppProductId);

                Guid createdId = service.Create(newOppLineItem);
                tracingService.Trace("Line item created: " + createdId);
            }
        }

        private void CreateOpportunityPanelAccessories(Guid oldOppProductId, Guid newOppProductId)
        {
            QueryExpression query = new QueryExpression("tbs_opppanelaccessory");
            query.ColumnSet = new ColumnSet(true);
            query.Criteria.AddCondition("tbs_opportunityproduct", ConditionOperator.Equal, oldOppProductId);

            EntityCollection oldAccessories = service.RetrieveMultiple(query);

            tracingService.Trace($"Accessories Found : {oldAccessories.Entities.Count}");

            foreach (Entity oldOppAccessory in oldAccessories.Entities)
            {
                Entity newOppAccessory = new Entity("tbs_opppanelaccessory");

                foreach (var attribute in oldOppAccessory.Attributes)
                {
                    if (attribute.Key == "tbs_opppanelaccessoryid" || attribute.Key == "tbs_opportunityproduct")
                        continue;

                    newOppAccessory[attribute.Key] = attribute.Value;
                }

                newOppAccessory["tbs_opportunityproduct"] = new EntityReference("opportunityproduct", newOppProductId);

                service.Create(newOppAccessory);
            }
        }

        private void CreateOpportunityPanelTrims(Guid oldOppProductId, Guid newOppProductId)
        {
            QueryExpression query = new QueryExpression("tbs_opppaneltrim");
            query.ColumnSet = new ColumnSet(true);
            query.Criteria.AddCondition("tbs_opportunityproduct", ConditionOperator.Equal, oldOppProductId);

            EntityCollection oldTrims = service.RetrieveMultiple(query);

            tracingService.Trace($"Trims Found : {oldTrims.Entities.Count}");

            foreach (Entity oldOppTrim in oldTrims.Entities)
            {
                Entity newOppTrim = new Entity("tbs_opppaneltrim");

                foreach (var attribute in oldOppTrim.Attributes)
                {
                    if (attribute.Key == "tbs_opppaneltrimid" || attribute.Key == "tbs_opportunityproduct")
                        continue;

                    newOppTrim[attribute.Key] = attribute.Value;
                }

                newOppTrim["tbs_opportunityproduct"] = new EntityReference("opportunityproduct", newOppProductId);

                service.Create(newOppTrim);
            }
        }

        private Guid? GetOptionalGuid(string parameterName)
        {
            if (!context.InputParameters.Contains(parameterName) || context.InputParameters[parameterName] == null)
                return null;

            object value = context.InputParameters[parameterName];

            if (value is Guid guid)
                return guid == Guid.Empty ? (Guid?)null : guid;

            if (value is EntityReference entityRef)
                return entityRef.Id;

            throw new InvalidPluginExecutionException($"Input parameter '{parameterName}' is invalid.");
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
            context = localcontext.PluginExecutionContext;
            if (context == null)
            {
                throw new InvalidPluginExecutionException("Failed to retrieve Plugin Execution Context !");
            }

            service = localcontext.OrganizationService;
            if (service == null)
            {
                throw new InvalidPluginExecutionException("Failed to retrieve Organization Service !");
            }

            tracingService = localcontext.TracingService;
            if (tracingService == null)
            {
                throw new InvalidPluginExecutionException("Failed to retrieve Tracing Service !");
            }
        }
    }
}