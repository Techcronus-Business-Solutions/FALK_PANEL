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
    public class CloneQuote : CustomAPIBase
    {
        public CloneQuote() : base(typeof(CloneQuote)) { }

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
                tracingService.Trace("Clone Quote Started");

                EntityReference quoteRef = GetInputRef("Target");

                tracingService.Trace("Quote Id = " + quoteRef.Id);

                Guid newQuoteId = CloneQuoteRecord(quoteRef.Id);

                CloneQuoteProducts(quoteRef.Id, newQuoteId);

                tracingService.Trace("New Quote Id = " + newQuoteId);

                context.OutputParameters["ClonedQuoteId"] = newQuoteId;
            }
            catch (Exception ex)
            {
                tracingService.Trace(ex.ToString());

                throw new InvalidPluginExecutionException("Clone Quote Error : " + ex.Message);
            }
        }
        private Guid CloneQuoteRecord(Guid quoteId)
        {
            Entity oldQuote = service.Retrieve("quote", quoteId, new ColumnSet(true));

            Entity newQuote = new Entity("quote");

            foreach (var attribute in oldQuote.Attributes)
            {
                if (attribute.Key == "quoteid")
                    continue;
                switch (attribute.Key.ToLower())
                {
                    case "quoteid":
                    case "quotenumber":
                    case "revisionnumber":
                    case "tbs_quoteno":
                    case "createdon":
                    case "createdby":
                    case "modifiedon":
                    case "modifiedby":
                    case "createdbyyominame":
                    case "modifiedbyyominame":
                    case "versionnumber":
                    case "importsequencenumber":
                    case "overriddencreatedon":
                    case "timezoneruleversionnumber":
                    case "utcconversiontimezonecode":
                        continue;
                }
                newQuote[attribute.Key] = attribute.Value;
            }

            if (oldQuote.Contains("name"))
            {
                newQuote["name"] = oldQuote.GetAttributeValue<string>("name") + " - Copy";
            }

            newQuote["tbs_iscopy"] = true;
            Guid newQuoteId = service.Create(newQuote);

            return newQuoteId;
        }
        private void CloneQuoteProducts(Guid oldQuoteId, Guid newQuoteId)
        {
            QueryExpression qe = new QueryExpression("quotedetail");

            qe.ColumnSet = new ColumnSet(true);

            qe.Criteria.AddCondition("quoteid", ConditionOperator.Equal, oldQuoteId);

            EntityCollection quoteProducts = service.RetrieveMultiple(qe);

            tracingService.Trace($"Quote Products Found : {quoteProducts.Entities.Count}");

            foreach (Entity oldProduct in quoteProducts.Entities)
            {
                Entity newProduct = new Entity("quotedetail");

                foreach (var attribute in oldProduct.Attributes)
                {
                    switch (attribute.Key.ToLower())
                    {
                        case "quotedetailid":
                        case "quoteid":
                        case "createdon":
                        case "createdby":
                        case "modifiedon":
                        case "modifiedby":
                        case "versionnumber":
                        case "importsequencenumber":
                            continue;
                    }

                    newProduct[attribute.Key] = attribute.Value;
                }

                newProduct["quoteid"] = new EntityReference("quote", newQuoteId);
                newProduct["quoteid"] = new EntityReference("quote", newQuoteId);

                newProduct["skippricecalculation"] =new OptionSetValue(1);

                Guid newQuoteProductId = service.Create(newProduct);

                tracingService.Trace($"Quote Product Created : {newQuoteProductId}");

                CreateQuoteLineItems(oldProduct.Id, newQuoteProductId);

                CreateQuotePanelAccessories(oldProduct.Id, newQuoteProductId);

                CreateQuotePanelTrims(oldProduct.Id, newQuoteProductId);

                Entity quoteProductUpdate = new Entity("quotedetail", newQuoteProductId);

                quoteProductUpdate["skippricecalculation"] = new OptionSetValue(0);

                service.Update(quoteProductUpdate);
            }
        }
        private void CreateQuoteLineItems(Guid oldQuoteProductId, Guid newQuoteProductId)
        {
            QueryExpression query = new QueryExpression("tbs_quotelineitem");
            query.ColumnSet = new ColumnSet("tbs_numberofpanels", "tbs_ft", "tbs_in", "tbs_cbmode", "tbs_cblengthinch", "tbs_linearftinch", "tbs_totalsqft", "tbs_widthpanel");
            query.Criteria.AddCondition("tbs_quoteproduct", ConditionOperator.Equal, oldQuoteProductId);

            EntityCollection oldLineitems = service.RetrieveMultiple(query);

            for (int i = 0; i < oldLineitems.Entities.Count; i++)
            {
                Entity quoteLineItems = oldLineitems.Entities[i];

                Entity newQuoteLineItem = new Entity("tbs_quotelineitem");

                foreach (var attribute in quoteLineItems.Attributes)
                {
                    if (attribute.Key == "tbs_quotelineitemid" || attribute.Key == "tbs_quoteproduct")
                        continue;

                    newQuoteLineItem[attribute.Key] = attribute.Value;
                }

                newQuoteLineItem["tbs_quoteproduct"] = new EntityReference("quotedetail", newQuoteProductId);

                Guid createdId = service.Create(newQuoteLineItem);
            }
        }
        private void CreateQuotePanelAccessories(Guid oldQuoteProductId, Guid newQuoteProductId)
        {
            QueryExpression query = new QueryExpression("tbs_quotepanelaccessory");
            query.ColumnSet = new ColumnSet(true);
            query.Criteria.AddCondition("tbs_quoteproduct", ConditionOperator.Equal, oldQuoteProductId);

            EntityCollection oldAccessories = service.RetrieveMultiple(query);

            tracingService.Trace($"Accessories Found : {oldAccessories.Entities.Count}");

            foreach (Entity oldQuoteAccessory in oldAccessories.Entities)
            {
                if (oldQuoteAccessory.Contains("tbs_quoteproduct"))
                {
                    EntityReference parent = oldQuoteAccessory.GetAttributeValue<EntityReference>("tbs_quoteproduct");
                }
                Entity newQuoteAccessory = new Entity("tbs_quotepanelaccessory");

                foreach (var attribute in oldQuoteAccessory.Attributes)
                {
                    if (attribute.Key == "tbs_quotepanelaccessoryid" || attribute.Key == "tbs_quoteproduct")
                        continue;

                    newQuoteAccessory[attribute.Key] = attribute.Value;
                }

                newQuoteAccessory["tbs_quoteproduct"] = new EntityReference("quotedetail", newQuoteProductId);

                service.Create(newQuoteAccessory);
            }
        }

        private void CreateQuotePanelTrims(Guid oldQuoteProductId, Guid newQuoteProductId)
        {
            QueryExpression query = new QueryExpression("tbs_quotepaneltrim");
            query.ColumnSet = new ColumnSet(true);
            query.Criteria.AddCondition("tbs_quoteproduct", ConditionOperator.Equal, oldQuoteProductId);

            EntityCollection oldTrims = service.RetrieveMultiple(query);

            tracingService.Trace($"Trims Found : {oldTrims.Entities.Count}");

            foreach (Entity oldQuoteTrim in oldTrims.Entities)
            {
                if (oldQuoteTrim.Contains("tbs_quoteproduct"))
                {
                    EntityReference parent = oldQuoteTrim.GetAttributeValue<EntityReference>("tbs_quoteproduct");
                }
                Entity newQuoteTrim = new Entity("tbs_quotepaneltrim");

                foreach (var attribute in oldQuoteTrim.Attributes)
                {
                    if (attribute.Key == "tbs_quotepaneltrimid" || attribute.Key == "tbs_quoteproduct")
                        continue;

                    newQuoteTrim[attribute.Key] = attribute.Value; 
                }

                newQuoteTrim["tbs_quoteproduct"] = new EntityReference("quotedetail", newQuoteProductId);

                service.Create(newQuoteTrim);
            }
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
