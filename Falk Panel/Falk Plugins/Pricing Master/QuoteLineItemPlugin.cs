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
    public class QuoteLineItemPlugin : PluginBase
    {
        public QuoteLineItemPlugin() : base(typeof(QuoteLineItemPlugin)) { }

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
                    if (targetEntity.LogicalName == "tbs_quotelineitem")
                    {
                        if (context.MessageName == CONST_CREATE && context.Stage == PostOperation)
                        {
                            #region Calculate Opportunity Product Rollups
                            try
                            {
                                decimal width = targetEntity.Contains("tbs_widthpanel") ? targetEntity.GetAttributeValue<decimal>("tbs_widthpanel") : 0;
                                tracingService.Trace("quoteProduct exists: " + targetEntity.Contains("tbs_quoteproduct"));
                                Guid quoteProd  = targetEntity.Contains("tbs_quoteproduct") ? targetEntity.GetAttributeValue<EntityReference>("tbs_quoteproduct").Id : Guid.Empty;

                                string fetchXml = $@"
                                            <fetch aggregate='true'>
                                              <entity name='tbs_quotelineitem'>
                                                <attribute name='tbs_totalsqft' alias='sqft' aggregate='sum' />
                                                <filter>
                                                  <condition attribute='tbs_quoteproduct' operator='eq' value='{quoteProd}' />
                                                </filter>
                                              </entity>
                                            </fetch>";

                                Entity SQFT = service.RetrieveMultiple(new FetchExpression(fetchXml)).Entities.FirstOrDefault();

                                tracingService.Trace("Starting SQFT value extraction.");

                                decimal totalSQFT = 0;

                                if (SQFT == null)
                                {
                                    tracingService.Trace("SQFT entity is NULL.");
                                }
                                else if (!SQFT.Contains("sqft"))
                                {
                                    tracingService.Trace("SQFT entity does NOT contain alias 'sqft'.");
                                }
                                else
                                {
                                    tracingService.Trace("SQFT entity contains alias 'sqft'.");

                                    AliasedValue sqftAlias =
                                        SQFT.GetAttributeValue<AliasedValue>("sqft");

                                    tracingService.Trace(
                                        "sqftAlias null: " + (sqftAlias == null)
                                    );

                                    if (sqftAlias != null)
                                    {
                                        tracingService.Trace(
                                            "sqftAlias.Value null: " +
                                            (sqftAlias.Value == null)
                                        );

                                        if (sqftAlias.Value != null)
                                        {
                                            tracingService.Trace(
                                                "sqftAlias.Value type: " +
                                                sqftAlias.Value.GetType().FullName
                                            );

                                            totalSQFT = Convert.ToDecimal(sqftAlias.Value);

                                            tracingService.Trace(
                                                "totalSQFT: " + totalSQFT
                                            );
                                        }
                                    }
                                }

                                tracingService.Trace("Finished SQFT value extraction.");

                                decimal linearFt = width > 0
                                    ? (totalSQFT * 12) / width
                                    : 0;

                                tracingService.Trace(
                                    "width: " + width +
                                    " | totalSQFT: " + totalSQFT +
                                    " | linearFt: " + linearFt
                                );

                                EntityReference quoteProductRef =
                                    targetEntity.GetAttributeValue<EntityReference>("tbs_quoteproduct");

                                tracingService.Trace(
                                    "quoteProductRef null: " +
                                    (quoteProductRef == null)
                                );

                                if (quoteProductRef == null)
                                {
                                    throw new InvalidPluginExecutionException(
                                        "tbs_quoteproduct is null while updating quotedetail."
                                    );
                                }

                                Entity QuoteProductToUpdate =
                                    new Entity("quotedetail", quoteProductRef.Id);

                                QuoteProductToUpdate["quantity"] = totalSQFT;
                                QuoteProductToUpdate["tbs_linearfeet"] = linearFt;

                                tracingService.Trace("Before service.Update(quotedetail).");

                                service.Update(QuoteProductToUpdate);

                                tracingService.Trace("After service.Update(quotedetail).");
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
                                    Target = new EntityReference("quotedetail", targetEntity.GetAttributeValue<EntityReference>("tbs_quoteproduct").Id),
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
                                    Guid quoteProd = PreImage.Contains("tbs_quoteproduct") ? PreImage.GetAttributeValue<EntityReference>("tbs_quoteproduct").Id : Guid.Empty;

                                    string fetchXml = $@"
                                            <fetch aggregate='true'>
                                              <entity name='tbs_quotelineitem'>
                                                <attribute name='tbs_totalsqft' alias='sqft' aggregate='sum' />
                                                <filter>
                                                  <condition attribute='tbs_quoteproduct' operator='eq' value='{quoteProd}' />
                                                </filter>
                                              </entity>
                                            </fetch>";

                                    Entity SQFT = service.RetrieveMultiple(new FetchExpression(fetchXml)).Entities.FirstOrDefault();
                                    decimal totalSQFT = 0;
                                    if (SQFT != null)
                                    {
                                        totalSQFT = (decimal)SQFT.GetAttributeValue<AliasedValue>("sqft").Value;
                                    }
                                    tracingService.Trace("SQFT");

                                    //decimal totalSqFtSum = GetDecimalAttributeValue(QuoteProduct, "quantity") + GetDecimalAttributeValue(targetEntity, "tbs_totalsqft") - GetDecimalAttributeValue(PreImage, "tbs_totalsqft");
                                    decimal linearFt = width > 0 ? (totalSQFT * 12) / width : 0;

                                    Entity QuoteProductToUpdate = new Entity("quotedetail", quoteProd);
                                    QuoteProductToUpdate["quantity"] = totalSQFT;
                                    QuoteProductToUpdate["tbs_linearfeet"] = linearFt;
                                    service.Update(QuoteProductToUpdate);
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
                                        Target = new EntityReference("quotedetail", PreImage.GetAttributeValue<EntityReference>("tbs_quoteproduct").Id),
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
                    if (targetEntityRef.LogicalName == "tbs_quotelineitem")
                    {
                        if (context.MessageName == CONST_DELETE && context.Stage == PostOperation)
                        {
                            Entity PreImage = context.PreEntityImages["PreImage"];

                            #region Calculate Opportunity Product Rollups
                            // Calculate total SQFT & Linear Ft of all line items on the opportunity product
                            try
                            {
                                decimal width = PreImage.GetAttributeValue<decimal>("tbs_widthpanel");
                                Guid quoteProd = targetEntity.Contains("tbs_quoteproduct") ? targetEntity.GetAttributeValue<EntityReference>("tbs_quoteproduct").Id : Guid.Empty;

                                string fetchXml = $@"
                                            <fetch aggregate='true'>
                                              <entity name='tbs_quotelineitem'>
                                                <attribute name='tbs_totalsqft' alias='sqft' aggregate='sum' />
                                                <filter>
                                                  <condition attribute='tbs_quoteproduct' operator='eq' value='{quoteProd}' />
                                                </filter>
                                              </entity>
                                            </fetch>";

                                Entity SQFT = service.RetrieveMultiple(new FetchExpression(fetchXml)).Entities.FirstOrDefault();
                                decimal totalSQFT = 0;

                                if (SQFT != null && SQFT.Contains("sqft"))
                                {
                                    AliasedValue sqftAlias =
                                        SQFT.GetAttributeValue<AliasedValue>("sqft");

                                    if (sqftAlias != null && sqftAlias.Value != null)
                                    {
                                        totalSQFT = Convert.ToDecimal(sqftAlias.Value);
                                    }
                                }
                                tracingService.Trace("SQFT");

                                decimal linearFt = width > 0 ? (totalSQFT * 12) / width : 0;

                                Entity QuoteProductToUpdate = new Entity("quotedetail", quoteProd);
                                QuoteProductToUpdate["quantity"] = totalSQFT;
                                QuoteProductToUpdate["tbs_linearfeet"] = linearFt;
                                service.Update(QuoteProductToUpdate);
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
                                    Target = new EntityReference("quotedetail", PreImage.GetAttributeValue<EntityReference>("tbs_quoteproduct").Id),
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

