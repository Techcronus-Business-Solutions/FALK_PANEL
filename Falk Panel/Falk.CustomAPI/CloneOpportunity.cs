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
    public class CloneOpportunity : CustomAPIBase
    {
        public CloneOpportunity() : base(typeof(CloneOpportunity)) { }

        #region Private Variables
        private IOrganizationService service { get; set; }
        private IPluginExecutionContext context { get; set; }
        private ITracingService tracingService { get; set; }

        private const int StateOpen = 0;
        private const int StateWon = 1;
        private const int StateLost = 2;

        private const int ProductStateActive = 0;
        private const int ProductStatusActive = 1;

        private const bool ReactivateInactiveProducts = true;

         private static readonly HashSet<string> ExcludedAttributes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // system fields
            "opportunityid",
            "createdon",
            "createdby",
            "createdonbehalfby",
            "modifiedon",
            "modifiedby",
            "modifiedonbehalfby",
            "createdbyyominame",
            "modifiedbyyominame",
            "versionnumber",
            "importsequencenumber",
            "overriddencreatedon",
            "timezoneruleversionnumber",
            "utcconversiontimezonecode",

            // status fields - the clone must always start as Open
            "statecode",
            "statuscode",

            // close-related fields (populated when an opportunity is Won/Lost)
            "actualrevenue",
            "actualclosedate",
            "closeprobability",

            // business process flow fields
            "stageid",
            "processid",
            "traversedpath"
        };




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

                CloneOpportunityProducts(oppRef.Id, newOpportunityId);

                tracingService.Trace("New Opportunity Id = " + newOpportunityId);

                context.OutputParameters["ClonedOpportunityId"] = newOpportunityId;
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

            int sourceState = oldOpp.GetAttributeValue<OptionSetValue>("statecode")?.Value ?? StateOpen;

            string sourceStatus = sourceState == StateWon ? "Won"
                                : sourceState == StateLost ? "Lost"
                                : "Open";
             tracingService.Trace($"Source opportunity status : {sourceStatus}");

            Entity newOpp = new Entity("opportunity");

            foreach (var attribute in oldOpp.Attributes)
            {
                   string key = attribute.Key;

                //if (attribute.Key == "opportunityid")
                if (ExcludedAttributes.Contains(key))  
                    continue;

                //switch (attribute.Key.ToLower())
                //{
                  //  case "opportunityid":
                   // case "createdon":
                    //case "createdby":
                   // case "modifiedon":
                   // case "modifiedby":
                    // case "createdbyyominame":
                   // case "modifiedbyyominame":
                    //case "versionnumber":
                    // case "importsequencenumber":
                   // case "overriddencreatedon":
                   // case "timezoneruleversionnumber":
                    //case "utcconversiontimezonecode":
                      //  continue;
                //}

                if (key.EndsWith("_base", StringComparison.OrdinalIgnoreCase))
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
        private void CloneOpportunityProducts(Guid oldOpportunityId, Guid newOpportunityId)
        {
            QueryExpression qe = new QueryExpression("opportunityproduct");

            qe.ColumnSet = new ColumnSet("opportunityproductid","productid","productname");

            qe.Criteria.AddCondition("opportunityid", ConditionOperator.Equal, oldOpportunityId);

            EntityCollection products = service.RetrieveMultiple(qe);

            tracingService.Trace($"Products Found : {products.Entities.Count}");

            Dictionary<Guid, Entity> reactivated = new Dictionary<Guid, Entity>();

            try { 
            foreach (Entity product in products.Entities)
            {
                EntityReference productRef = product.GetAttributeValue<EntityReference>("productid");

                if (productRef != null)
                    {
                        string productName = product.GetAttributeValue<string>("productname") ?? productRef.Id.ToString();

                        EnsureProductActive(productRef.Id, productName, reactivated);
                    }


                OrganizationRequest request = new OrganizationRequest("tbs_CloneOpportunityProduct");

                request["Target"] = new EntityReference("opportunityproduct", product.Id);
                                    request["NewOpportunityId"] = newOpportunityId;


                OrganizationResponse response = service.Execute(request);

                Guid clonedProductId = (Guid)response.Results["ClonedOpportunityProductId"];

                Entity updateProduct = new Entity("opportunityproduct", clonedProductId);

                updateProduct["opportunityid"] = new EntityReference("opportunity", newOpportunityId);

                service.Update(updateProduct);

                tracingService.Trace($"Cloned Product : {clonedProductId}");
            }
            }
            finally
            {
                // Always put products back to their original status, even if cloning failed
                RestoreProducts(reactivated);
            }
        }

        private void EnsureProductActive(Guid productId, string productName, Dictionary<Guid, Entity> reactivated)
        {
            // already handled earlier in this run
            if (reactivated.ContainsKey(productId))
                return;

            Entity prod = service.Retrieve("product", productId, new ColumnSet("statecode", "statuscode", "name"));

            int state = prod.GetAttributeValue<OptionSetValue>("statecode")?.Value ?? ProductStateActive;
            int status = prod.GetAttributeValue<OptionSetValue>("statuscode")?.Value ?? ProductStatusActive;

            if (state == ProductStateActive)
                return;

            if (!ReactivateInactiveProducts)
                throw new InvalidPluginExecutionException($"Cannot clone: product '{productName}' is not active.");

            tracingService.Trace($"Product '{productName}' ({productId}) is not active (state {state}, status {status}). Activating temporarily.");

            SetProductState(productId, ProductStateActive, ProductStatusActive);

            reactivated[productId] = prod;
        }

        private void RestoreProducts(Dictionary<Guid, Entity> reactivated)
        {
            foreach (KeyValuePair<Guid, Entity> item in reactivated)
            {
                Entity original = item.Value;

                int state = original.GetAttributeValue<OptionSetValue>("statecode").Value;
                int status = original.GetAttributeValue<OptionSetValue>("statuscode").Value;
                string name = original.GetAttributeValue<string>("name") ?? item.Key.ToString();

                try
                {
                    SetProductState(item.Key, state, status);

                    tracingService.Trace($"Restored product '{name}' ({item.Key}) to state {state}, status {status}.");
                }
                catch (Exception ex)
                {
                    // never hide the real result of the clone because the restore failed
                    tracingService.Trace($"WARNING: could not restore product '{name}' ({item.Key}) to state {state}, status {status}. {ex.Message}");
                }
            }
        }

         private void SetProductState(Guid productId, int stateCode, int statusCode)
        {
            SetStateRequest setState = new SetStateRequest
            {
                EntityMoniker = new EntityReference("product", productId),
                State = new OptionSetValue(stateCode),
                Status = new OptionSetValue(statusCode)
            };

            service.Execute(setState);
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
