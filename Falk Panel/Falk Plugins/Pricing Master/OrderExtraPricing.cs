using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Activities.Expressions;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.UI.WebControls;

namespace Falk_Plugins.Pricing_Master
{
    public class OrderExtraPricing : PluginBase
    {
        public OrderExtraPricing() : base(typeof(OrderExtraPricing)) { }

        #region Private Variables
        private IOrganizationService service { get; set; }
        private IPluginExecutionContext context { get; set; }
        private ITracingService tracingService { get; set; }
        private IOrganizationServiceFactory factory { get; set; }
        private Entity targetEntity { get; set; }
        #endregion

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
                    if (targetEntity.LogicalName == "salesorderdetail")
                    {
                        if (context.MessageName == CONST_UPDATE && context.Stage == PostOperation)
                        {
                            try
                            {
                                if (targetEntity.Contains("quantity"))
                                {
                                    Entity PostImage = context.PostEntityImages["PostImage"];
                                    Guid salesorderId = PostImage.GetAttributeValue<EntityReference>("salesorderid").Id;

                                    Entity salesorder = service.Retrieve("salesorder", salesorderId, new ColumnSet("skippricecalculation"));
                                    int? skipPriceCalculation = GetOptionSetAttributeValue(salesorder, "skippricecalculation");

                                    if (skipPriceCalculation.HasValue && skipPriceCalculation.Value == 1)
                                    {
                                        tracingService.Trace("Converted salesorder");
                                        return;
                                    }

                                    int totalSQFT = GetTotalSQFTFromOrderProducts(salesorderId);
                                    int totalFilteredSQFT = GetFilteredSQFTFromOrderProducts(salesorderId);

                                    Entity updatesalesorder = new Entity("salesorder");
                                    updatesalesorder.Id = salesorderId;
                                    updatesalesorder["tbs_totalsqft"] = totalSQFT;
                                    updatesalesorder["tbs_totalfilteredsqft"] = totalFilteredSQFT;

                                    service.Update(updatesalesorder);
                                }
                            }
                            catch (Exception ex)
                            {
                                throw new InvalidPluginExecutionException(ex.Message);
                            }
                        }
                    }

                    if (targetEntity.LogicalName == "salesorder")
                    {
                        if (context.MessageName == CONST_UPDATE && context.Stage == PreOperation)
                        {
                            Entity PreImage = context.PreEntityImages["PreImage"];

                            int totalSQFT = targetEntity.Contains("tbs_totalsqft") ? GetIntAttributeValue(targetEntity, "tbs_totalsqft") : GetIntAttributeValue(PreImage, "tbs_totalsqft");
                            int totalFilteredSQFT = targetEntity.Contains("tbs_totalfilteredsqft") ? GetIntAttributeValue(targetEntity, "tbs_totalfilteredsqft") : GetIntAttributeValue(PreImage, "tbs_totalfilteredsqft");

                            if (targetEntity.Contains("tbs_totalsqft") || targetEntity.Contains("tbs_shopdrawings"))
                            {
                                bool shopDrawings = targetEntity.Contains("tbs_shopdrawings") ? GetBoolAttributeValue(targetEntity, "tbs_shopdrawings") : GetBoolAttributeValue(PreImage, "tbs_shopdrawings");
                                if (shopDrawings)
                                {
                                    targetEntity["tbs_shopdrawingsprice"] = new Money(RoundUpToNearest50((totalSQFT * 0.06m + 5000m)));
                                }
                                else
                                {
                                    targetEntity["tbs_shopdrawingsprice"] = null;
                                }
                            }
                            if (targetEntity.Contains("tbs_totalsqft") || targetEntity.Contains("tbs_stampedandsealeddrawings"))
                            {
                                bool stampedSealedDrawings = targetEntity.Contains("tbs_stampedandsealeddrawings") ? GetBoolAttributeValue(targetEntity, "tbs_stampedandsealeddrawings") : GetBoolAttributeValue(PreImage, "tbs_stampedandsealeddrawings");
                                if (stampedSealedDrawings)
                                {
                                    targetEntity["tbs_stampedandsealedshopdrawingsprice"] = new Money(RoundUpToNearest50((totalSQFT * 0.06m + 5000m) * 1.75m));
                                }
                                else
                                {
                                    targetEntity["tbs_stampedandsealedshopdrawingsprice"] = null;
                                }
                            }
                            if (targetEntity.Contains("tbs_totalfilteredsqft") || targetEntity.Contains("tbs_weathertightwarranty"))
                            {
                                bool warranty = targetEntity.Contains("tbs_weathertightwarranty") ? GetBoolAttributeValue(targetEntity, "tbs_weathertightwarranty") : GetBoolAttributeValue(PreImage, "tbs_weathertightwarranty");
                                if (warranty)
                                {
                                    if (totalFilteredSQFT != null && totalFilteredSQFT > 0)
                                    {
                                        targetEntity["tbs_roofwarrantyprice"] = new Money((totalFilteredSQFT * 0.3m + 10000) * 1.4m);
                                    }
                                    else
                                    {
                                        targetEntity["tbs_roofwarrantyprice"] = null;
                                    }
                                }
                                else
                                {
                                    targetEntity["tbs_roofwarrantyprice"] = null;
                                }
                            }
                            if (targetEntity.Contains("tbs_fmapproval"))
                            {
                                bool fmApproval = targetEntity.Contains("tbs_fmapproval") ? GetBoolAttributeValue(targetEntity, "tbs_fmapproval") : GetBoolAttributeValue(PreImage, "tbs_fmapproval");
                                if (!fmApproval)
                                {
                                    targetEntity["tbs_factorymutualspecifiedrequirementsprice"] = null;
                                }
                            }
                            if (targetEntity.Contains("tbs_buyamerican"))
                            {
                                bool buyAmerican = targetEntity.Contains("tbs_buyamerican") ? GetBoolAttributeValue(targetEntity, "tbs_buyamerican") : GetBoolAttributeValue(PreImage, "tbs_buyamerican");
                                if (!buyAmerican)
                                {
                                    targetEntity["tbs_buyamericanprice"] = null;
                                }
                            }
                        }
                        if (context.MessageName == CONST_UPDATE && context.Stage == PostOperation)
                        {
                            if (targetEntity.Contains("tbs_shopdrawingsprice") || targetEntity.Contains("tbs_stampedandsealedshopdrawingsprice") || targetEntity.Contains("tbs_roofwarrantyprice") || targetEntity.Contains("tbs_factorymutualspecifiedrequirementsprice") || targetEntity.Contains("tbs_buyamericanprice"))
                            {
                                Entity PostImage = context.PostEntityImages["PostImage"];
                                Decimal totalExtraPricing = GetDecimalAttributeValue(PostImage, "tbs_totalextrapricing");
                                tracingService.Trace("Extra Pricing : " + totalExtraPricing.ToString());

                                Entity updatesalesorder = new Entity("salesorder", targetEntity.Id);
                                updatesalesorder["freightamount"] = new Money(totalExtraPricing);
                                service.Update(updatesalesorder);
                            }
                        }
                    }
                }
                else if (context.InputParameters.Contains(CONST_TARGETENTITY) && (context.InputParameters[CONST_TARGETENTITY] is EntityReference))
                {
                    EntityReference targetEntityRef = (EntityReference)context.InputParameters["Target"];
                    if (targetEntityRef.LogicalName == "salesorderdetail")
                    {
                        if (context.MessageName == CONST_DELETE && context.Stage == PostOperation)
                        {
                            try
                            {
                                Entity PreImage = context.PreEntityImages["PreImage"];
                                Guid salesorderId = PreImage.GetAttributeValue<EntityReference>("salesorderid").Id;

                                int totalSQFT = GetTotalSQFTFromOrderProducts(salesorderId);
                                int totalFilteredSQFT = GetFilteredSQFTFromOrderProducts(salesorderId);

                                Entity updatesalesorder = new Entity("salesorder");
                                updatesalesorder.Id = salesorderId;
                                updatesalesorder["tbs_totalsqft"] = totalSQFT;
                                updatesalesorder["tbs_totalfilteredsqft"] = totalFilteredSQFT;
                                service.Update(updatesalesorder);
                            }
                            catch (Exception ex)
                            {
                                throw new InvalidPluginExecutionException(ex.Message);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                tracingService.Trace($"Extra Pricing Calculation: {ex}");
                throw new InvalidPluginExecutionException("Error occurred while Extra Pricing Calculation.", ex);
            }
        }

        private int GetTotalSQFTFromOrderProducts(Guid salesorderId)
        {
            int totalSQFT = 0;

            string fetchXml = $@"
                   <fetch aggregate='true'>
                      <entity name='salesorderdetail'>
                        <attribute name='quantity' alias='qty' aggregate='sum' />
                        <filter>
                          <condition attribute='salesorderid' operator='eq' value='{salesorderId}' />
                        </filter>
                      </entity>
                    </fetch>";

            Entity SQFT = service.RetrieveMultiple(new FetchExpression(fetchXml)).Entities.FirstOrDefault();
            if (SQFT != null)
            {
                int qty = Convert.ToInt32((decimal?)SQFT.GetAttributeValue<AliasedValue>("qty").Value);
                return qty;
            }

            return totalSQFT;
        }

        private int GetFilteredSQFTFromOrderProducts(Guid salesorderId)
        {
            // 2, 2.5, 3, 4, 5, 6, 8
            // RRP40, SSR42, RDEK40, FM-RRP, FM-SSR, FM-RDEK

            int totalSQFT = 0;
            string fetchXml = $@"
                   <fetch aggregate='true'>
                      <entity name='salesorderdetail'>
                        <attribute name='quantity' alias='qty' aggregate='sum' />
                        <filter>
                          <condition attribute='salesorderid' operator='eq' value='{salesorderId}' />
                          <condition attribute='productname' operator='in'>
                            <value>RRP40</value>
                            <value>SSR42</value>
                            <value>RDEK40</value>
                            <value>FM-RRP</value>
                            <value>FM-SSR</value>
                            <value>FM-RDEK</value>
                          </condition>
                          <condition entityname='tbs_thickness' attribute='tbs_thicknessnumber' operator='in'>
                            <value>2</value>
                            <value>2.5</value>
                            <value>3</value>
                            <value>4</value>
                            <value>5</value>
                            <value>6</value>
                            <value>8</value>
                          </condition>
                        </filter>
                        <link-entity name='tbs_thickness' from='tbs_thicknessid' to='tbs_panelthickness' link-type='inner' />
                      </entity>
                    </fetch>";

            Entity SQFT = service.RetrieveMultiple(new FetchExpression(fetchXml)).Entities.FirstOrDefault();
            if (SQFT != null)
            {
                int qty = Convert.ToInt32((decimal?)SQFT.GetAttributeValue<AliasedValue>("qty").Value);
                return qty;
            }
            return totalSQFT;
        }

        public static decimal RoundUpToNearest50(decimal value)
        {
            return Math.Ceiling(value / 50) * 50;
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