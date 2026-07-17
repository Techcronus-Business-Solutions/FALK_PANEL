
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Falk_Plugins
{
    public class LineItemPlugin : PluginBase
    {
        public LineItemPlugin() : base(typeof(LineItemPlugin)) { }

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
                    if (targetEntity.LogicalName == "tbs_lineitem")
                    {
                        if (context.MessageName == CONST_CREATE && context.Stage == PostOperation)
                        {
                            #region Calculate Opportunity Product Rollups
                            // Calculate total SQFT & Linear Ft of all line items on the opportunity product
                            try
                            {
                                decimal width = targetEntity.Contains("tbs_widthpanel") ? targetEntity.GetAttributeValue<decimal>("tbs_widthpanel") : 0;
                                Entity OpportunityProduct = service.Retrieve("opportunityproduct", targetEntity.GetAttributeValue<EntityReference>("tbs_opportunityproduct").Id, new ColumnSet("quantity"));

                                decimal totalSqFtSum = GetDecimalAttributeValue(OpportunityProduct, "quantity") + GetDecimalAttributeValue(targetEntity, "tbs_totalsqft");
                                decimal linearFt = width > 0 ? (totalSqFtSum * 12) / width : 0;

                                Entity OpportunityProductToUpdate = new Entity("opportunityproduct", targetEntity.GetAttributeValue<EntityReference>("tbs_opportunityproduct").Id);
                                OpportunityProductToUpdate["quantity"] = totalSqFtSum;
                                OpportunityProductToUpdate["tbs_linearfeet"] = linearFt;
                                service.Update(OpportunityProductToUpdate);
                            }
                            catch (Exception e)
                            {
                                tracingService.Trace($"Error Occurred in Calculating Total Sqft :{e.Message}");
                                throw new InvalidPluginExecutionException(e.Message);
                            }

                            // Force rollup total amount of panels on the opportunity product
                            try
                            {
                                CalculateRollupFieldRequest calcularRollup = new CalculateRollupFieldRequest
                                {
                                    Target = new EntityReference("opportunityproduct", targetEntity.GetAttributeValue<EntityReference>("tbs_opportunityproduct").Id),
                                    FieldName = "tbs_totalamountofpanels"
                                };
                                CalculateRollupFieldResponse calcularRollupResult = (CalculateRollupFieldResponse)service.Execute(calcularRollup);
                            }
                            catch (Exception e)
                            {
                                tracingService.Trace($"Error Occurred in Calculating Total Number of Panel :{e.Message}");
                                throw new InvalidPluginExecutionException(e.Message);
                            }
                            #endregion
                        }

                        else if (context.MessageName == CONST_UPDATE && context.Stage == PostOperation)
                        {
                            Entity PreImage = context.PreEntityImages["PreImage"];

                            #region Calculate Opportunity Product Rollups
                            // Calculate total SQFT & Linear Ft of all line items on the opportunity product
                            if (targetEntity.Contains("tbs_totalsqft"))
                            {
                                try
                                {
                                    decimal width = targetEntity.Contains("tbs_widthpanel") ? targetEntity.GetAttributeValue<decimal>("tbs_widthpanel") : PreImage.GetAttributeValue<decimal>("tbs_widthpanel");
                                    Entity OpportunityProduct = service.Retrieve("opportunityproduct", PreImage.GetAttributeValue<EntityReference>("tbs_opportunityproduct").Id, new ColumnSet("quantity"));

                                    decimal totalSqFtSum = GetDecimalAttributeValue(OpportunityProduct, "quantity") + GetDecimalAttributeValue(targetEntity, "tbs_totalsqft") - GetDecimalAttributeValue(PreImage, "tbs_totalsqft");
                                    decimal linearFt = width > 0 ? (totalSqFtSum * 12) / width : 0;

                                    Entity OpportunityProductToUpdate = new Entity("opportunityproduct", OpportunityProduct.Id);
                                    OpportunityProductToUpdate["quantity"] = totalSqFtSum;
                                    OpportunityProductToUpdate["tbs_linearfeet"] = linearFt;
                                    service.Update(OpportunityProductToUpdate);
                                }
                                catch (Exception e)
                                {
                                    tracingService.Trace($"Error Occurred in Calculating Total Sqft :{e.Message}");
                                    throw new InvalidPluginExecutionException(e.Message);
                                }
                            }

                            // Force rollup total amount of panels on the opportunity product if number of panel is changed
                            if (targetEntity.Contains("tbs_numberofpanels"))
                            {
                                try
                                {
                                    CalculateRollupFieldRequest calcularRollup = new CalculateRollupFieldRequest
                                    {
                                        Target = new EntityReference("opportunityproduct", PreImage.GetAttributeValue<EntityReference>("tbs_opportunityproduct").Id),
                                        FieldName = "tbs_totalamountofpanels"
                                    };
                                    CalculateRollupFieldResponse calcularRollupResult = (CalculateRollupFieldResponse)service.Execute(calcularRollup);
                                }
                                catch (Exception e)
                                {
                                    tracingService.Trace($"Error Occurred in Calculating Total Number of Panel :{e.Message}");
                                    throw new InvalidPluginExecutionException(e.Message);
                                }
                            }
                            #endregion
                        }
                    }
                }

                else if (context.InputParameters.Contains("Target") && (context.InputParameters["Target"] is EntityReference))
                {
                    EntityReference targetEntityRef = (EntityReference)context.InputParameters["Target"];
                    if (targetEntityRef.LogicalName == "tbs_lineitem")
                    {
                        if (context.MessageName == CONST_DELETE && context.Stage == PostOperation)
                        {
                            Entity PreImage = context.PreEntityImages["PreImage"];

                            #region Calculate Opportunity Product Rollups
                            // Calculate total SQFT & Linear Ft of all line items on the opportunity product
                            try
                            {
                                decimal width = PreImage.GetAttributeValue<decimal>("tbs_widthpanel");
                                Entity OpportunityProduct = service.Retrieve("opportunityproduct", PreImage.GetAttributeValue<EntityReference>("tbs_opportunityproduct").Id, new ColumnSet("quantity"));

                                decimal totalSqFtSum = GetDecimalAttributeValue(OpportunityProduct, "quantity") - GetDecimalAttributeValue(PreImage, "tbs_totalsqft");
                                decimal linearFt = width > 0 ? (totalSqFtSum * 12) / width : 0;

                                Entity OpportunityProductToUpdate = new Entity("opportunityproduct", OpportunityProduct.Id);
                                OpportunityProductToUpdate["quantity"] = totalSqFtSum;
                                OpportunityProductToUpdate["tbs_linearfeet"] = linearFt;
                                service.Update(OpportunityProductToUpdate);


                            }
                            catch (Exception e)
                            {
                                tracingService.Trace($"Error Occurred in Calculating Total Sqft :{e.Message}");
                                throw new InvalidPluginExecutionException(e.Message);
                            }

                            // Force rollup total amount of panels on the opportunity product if number of panel is changed
                            try
                            {
                                CalculateRollupFieldRequest calcularRollup = new CalculateRollupFieldRequest
                                {
                                    Target = new EntityReference("opportunityproduct", PreImage.GetAttributeValue<EntityReference>("tbs_opportunityproduct").Id),
                                    FieldName = "tbs_totalamountofpanels"
                                };
                                CalculateRollupFieldResponse calcularRollupResult = (CalculateRollupFieldResponse)service.Execute(calcularRollup);
                            }
                            catch (Exception e)
                            {
                                tracingService.Trace($"Error Occurred in Calculating Total Number of Panel :{e.Message}");
                                throw new InvalidPluginExecutionException(e.Message);
                            }
                            #endregion
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
