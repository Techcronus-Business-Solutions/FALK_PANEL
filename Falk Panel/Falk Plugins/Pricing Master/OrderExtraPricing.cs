using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Activities.Expressions;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;
using System.Web.UI.WebControls;

namespace Falk_Plugins.Pricing_Master
{
    /// <summary>
    /// Registration:
    ///   salesorder        Update  PreOperation   (PreImage  "PreImage"  : all attributes)
    ///   salesorder        Update  PostOperation  (PostImage "PostImage" : tbs_totalextrapricing)
    ///   salesorderdetail  Update  PostOperation  (PostImage "PostImage" : salesorderid, quantity)
    ///   salesorderdetail  Delete  PostOperation  (PreImage  "PreImage"  : salesorderid, productdescription, isproductoverridden)
    /// </summary>
    public class OrderExtraPricing : PluginBase
    {
        public OrderExtraPricing() : base(typeof(OrderExtraPricing)) { }

        #region Private Variables
        private IOrganizationService service { get; set; }
        private IPluginExecutionContext context { get; set; }
        private ITracingService tracingService { get; set; }
        private IOrganizationServiceFactory factory { get; set; }
        private Entity targetEntity { get; set; }


        private const string ShopDrawingsRowName = "Shop Drawings";
        private const string StampedSealedDrawingsRowName = "Stamped and Sealed Shop Drawings";
        private const string RoofWarrantyRowName = "Weather Tight Warranty";
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
                            Currency currencyCode = GetOrderCurrencyIsoCode(targetEntity, PreImage);
                            decimal baseAmount = (currencyCode.isoCode == "CAD") ? 3000m : 5000m;

                            if (targetEntity.Contains("tbs_totalsqft") || targetEntity.Contains("tbs_shopdrawings"))
                            {
                                bool shopDrawings = targetEntity.Contains("tbs_shopdrawings") ? GetBoolAttributeValue(targetEntity, "tbs_shopdrawings") : GetBoolAttributeValue(PreImage, "tbs_shopdrawings");
                                if (shopDrawings)
                                {
                                    targetEntity["tbs_shopdrawingsprice"] = new Money(RoundUpToNearest50((totalSQFT * 0.06m + baseAmount)) * currencyCode.exchangeRate);
                                }
                                else
                                {
                                    targetEntity["tbs_shopdrawingsprice"] = null;
                                }

                                SyncOrderProductRowSafe(
                                    targetEntity.Id,
                                    shopDrawings,
                                    targetEntity.GetAttributeValue<Money>("tbs_shopdrawingsprice"),
                                    ShopDrawingsRowName);
                            }
                            if (targetEntity.Contains("tbs_totalsqft") || targetEntity.Contains("tbs_stampedandsealeddrawings"))
                            {
                                bool stampedSealedDrawings = targetEntity.Contains("tbs_stampedandsealeddrawings") ? GetBoolAttributeValue(targetEntity, "tbs_stampedandsealeddrawings") : GetBoolAttributeValue(PreImage, "tbs_stampedandsealeddrawings");
                                if (stampedSealedDrawings)
                                {
                                    targetEntity["tbs_stampedandsealedshopdrawingsprice"] = new Money(RoundUpToNearest50((totalSQFT * 0.06m + baseAmount)) * 1.75m * currencyCode.exchangeRate);
                                }
                                else
                                {
                                    targetEntity["tbs_stampedandsealedshopdrawingsprice"] = null;
                                }

                                SyncOrderProductRowSafe(
                                    targetEntity.Id,
                                    stampedSealedDrawings,
                                    targetEntity.GetAttributeValue<Money>("tbs_stampedandsealedshopdrawingsprice"),
                                    StampedSealedDrawingsRowName);
                            }
                            if (targetEntity.Contains("tbs_totalfilteredsqft") || targetEntity.Contains("tbs_weathertightwarranty"))
                            {
                                bool warranty = targetEntity.Contains("tbs_weathertightwarranty") ? GetBoolAttributeValue(targetEntity, "tbs_weathertightwarranty") : GetBoolAttributeValue(PreImage, "tbs_weathertightwarranty");
                                if (warranty)
                                {
                                    if (totalFilteredSQFT != null && totalFilteredSQFT > 0)
                                    {
                                        targetEntity["tbs_roofwarrantyprice"] = new Money(totalFilteredSQFT * 0.3m + 10000);
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

                                SyncOrderProductRowSafe(
                                    targetEntity.Id,
                                    warranty,
                                    targetEntity.GetAttributeValue<Money>("tbs_roofwarrantyprice"),
                                    RoofWarrantyRowName);
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
                                // Whole order is being deleted (cascade) - nothing to update.
                                if (IsParentDelete())
                                {
                                    tracingService.Trace("Parent delete detected - skipping.");
                                    return;
                                }

                                Entity PreImage = context.PreEntityImages["PreImage"];
                                Guid salesorderId = PreImage.GetAttributeValue<EntityReference>("salesorderid").Id;

                                bool isWriteIn = PreImage.GetAttributeValue<bool?>("isproductoverridden") ?? false;
                                string rowName = (PreImage.GetAttributeValue<string>("productdescription") ?? "").Trim();

                                Entity updatesalesorder = new Entity("salesorder");
                                updatesalesorder.Id = salesorderId;

                                if (isWriteIn && string.Equals(rowName, ShopDrawingsRowName, StringComparison.OrdinalIgnoreCase))
                                {
                                    updatesalesorder["tbs_shopdrawings"] = false;
                                    updatesalesorder["tbs_shopdrawingsprice"] = null;
                                    tracingService.Trace("Manual Shop Drawings line deletion detected.");
                                }
                                if (isWriteIn && string.Equals(rowName, StampedSealedDrawingsRowName, StringComparison.OrdinalIgnoreCase))
                                {
                                    updatesalesorder["tbs_stampedandsealeddrawings"] = false;
                                    updatesalesorder["tbs_stampedandsealedshopdrawingsprice"] = null;
                                    tracingService.Trace("Manual Stamped and Sealed line deletion detected.");
                                }
                                if (isWriteIn && string.Equals(rowName, RoofWarrantyRowName, StringComparison.OrdinalIgnoreCase))
                                {
                                    updatesalesorder["tbs_weathertightwarranty"] = false;
                                    updatesalesorder["tbs_roofwarrantyprice"] = null;
                                    tracingService.Trace("Manual Weather Tight Warranty line deletion detected.");
                                }

                                updatesalesorder["tbs_totalsqft"] = GetTotalSQFTFromOrderProducts(salesorderId);
                                updatesalesorder["tbs_totalfilteredsqft"] = GetFilteredSQFTFromOrderProducts(salesorderId);

                                SafeUpdateParent(updatesalesorder);
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

        #region Delete helpers
        private bool IsParentDelete()
        {
            IPluginExecutionContext parent = context.ParentContext;
            while (parent != null)
            {
                if (parent.MessageName == CONST_DELETE && parent.PrimaryEntityName == "salesorder")
                {
                    return true;
                }
                parent = parent.ParentContext;
            }
            return false;
        }

        private void SafeUpdateParent(Entity entity)
        {
            try
            {
                service.Update(entity);
            }
            catch (FaultException<OrganizationServiceFault> ex) when (ex.Detail != null && ex.Detail.ErrorCode == -2147220969)
            {
                // Parent record no longer exists (deleted) - ignore.
                tracingService.Trace("Parent record not found - update skipped.");
            }
        }
        #endregion

        #region SQFT queries
        private string BuildWriteInRowExclusion(Guid salesorderId)
        {
            string[] excludedRows =
            {
                ShopDrawingsRowName,
                StampedSealedDrawingsRowName,
                RoofWarrantyRowName
            };

            return string.Concat(
                excludedRows
                    .SelectMany(rowName => GetOrderProductRows(salesorderId, rowName))
                    .Select(e => $"<condition attribute='salesorderdetailid' operator='ne' value='{e.Id}' />"));
        }

        private int GetTotalSQFTFromOrderProducts(Guid salesorderId)
        {
            string excludeWriteIns = BuildWriteInRowExclusion(salesorderId);

            string fetchXml = $@"
                   <fetch aggregate='true'>
                      <entity name='salesorderdetail'>
                        <attribute name='quantity' alias='qty' aggregate='sum' />
                        <filter>
                          <condition attribute='salesorderid' operator='eq' value='{salesorderId}' />
                          {excludeWriteIns}
                        </filter>
                      </entity>
                    </fetch>";

            return ReadAggregateQty(fetchXml);
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

            private int ReadAggregateQty(string fetchXml)
            {
                Entity result = service.RetrieveMultiple(new FetchExpression(fetchXml)).Entities.FirstOrDefault();
                if (result != null && result.Contains("qty"))
                {
                    object value = result.GetAttributeValue<AliasedValue>("qty").Value;
                    if (value != null)
                    {
                        return Convert.ToInt32(value);
                    }
                }
                return 0;
            }
            #endregion

            #region Write-in row sync
            /// <summary>
            /// Write-in rows are identified by isproductoverridden = true AND
            /// productdescription = row name.
            /// </summary>
            private List<Entity> GetOrderProductRows(Guid salesorderId, string rowName)
            {
                QueryExpression query = new QueryExpression("salesorderdetail")
                {
                    ColumnSet = new ColumnSet("salesorderdetailid", "productdescription", "priceperunit", "quantity")
                };

                query.Criteria.AddCondition("salesorderid", ConditionOperator.Equal, salesorderId);
                query.Criteria.AddCondition("isproductoverridden", ConditionOperator.Equal, true);

                return service.RetrieveMultiple(query).Entities
                    .Where(e => string.Equals(
                        (e.GetAttributeValue<string>("productdescription") ?? "").Trim(),
                        rowName,
                        StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            private void SyncOrderProductRowSafe(Guid salesorderId, bool enabled, Money priceMoney, string rowName)
            {
                decimal price = priceMoney != null ? priceMoney.Value : 0m;
                List<Entity> matches = GetOrderProductRows(salesorderId, rowName);

                tracingService.Trace("ORDER SYNC | Row={0} Enabled={1} Price={2} ExistingRows={3}",
                    rowName, enabled, price, matches.Count);

                if (enabled && price > 0m)
                {
                    if (matches.Count > 0)
                    {
                        Entity existing = matches[0];
                        Money existingPrice = existing.GetAttributeValue<Money>("priceperunit");
                        decimal existingQty = existing.GetAttributeValue<decimal?>("quantity") ?? 0m;

                        if (existingPrice == null || existingPrice.Value != price || existingQty != 1m)
                        {
                            Entity updateProduct = new Entity("salesorderdetail", existing.Id);
                            updateProduct["ispriceoverridden"] = true;
                            updateProduct["priceperunit"] = new Money(price);
                            updateProduct["quantity"] = 1m;
                            updateProduct["tbs_baseprice"] = new Money(price);
                            UpdateBypassingCustomLogic(updateProduct);
                            tracingService.Trace("{0} order line updated.", rowName);
                        }

                        foreach (Entity duplicate in matches.Skip(1))
                        {
                            DeleteBypassingCustomLogic("salesorderdetail", duplicate.Id);
                            tracingService.Trace("{0} duplicate order line deleted.", rowName);
                        }
                    }
                    else
                    {
                        Entity newProduct = new Entity("salesorderdetail");
                        newProduct["salesorderid"] = new EntityReference("salesorder", salesorderId);
                        newProduct["isproductoverridden"] = true;
                        newProduct["productdescription"] = rowName;
                        newProduct["quantity"] = 1m;
                        newProduct["ispriceoverridden"] = true;
                        newProduct["priceperunit"] = new Money(price);
                        newProduct["tbs_baseprice"] = new Money(price);
                        CreateBypassingCustomLogic(newProduct);
                        tracingService.Trace("{0} order line created.", rowName);
                    }
                }
                else
                {
                    // Toggle = No, or no price -> remove the line.
                    foreach (Entity existing in matches)
                    {
                        DeleteBypassingCustomLogic("salesorderdetail", existing.Id);
                        tracingService.Trace("{0} order line deleted.", rowName);
                    }
                }
            }

            private void CreateBypassingCustomLogic(Entity entity)
            {
                CreateRequest request = new CreateRequest { Target = entity };
                request.Parameters["BypassCustomPluginExecution"] = true;
                service.Execute(request);
            }

            private void UpdateBypassingCustomLogic(Entity entity)
            {
                UpdateRequest request = new UpdateRequest { Target = entity };
                request.Parameters["BypassCustomPluginExecution"] = true;
                service.Execute(request);
            }

            private void DeleteBypassingCustomLogic(string logicalName, Guid id)
            {
                DeleteRequest request = new DeleteRequest
                {
                    Target = new EntityReference(logicalName, id)
                };
                request.Parameters["BypassCustomPluginExecution"] = true;
                service.Execute(request);
            }
            #endregion

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
            private Currency GetOrderCurrencyIsoCode(Entity target, Entity preImage)
            {
                EntityReference currencyRef = null;

                if (target.Contains("transactioncurrencyid"))
                {
                    currencyRef = target.GetAttributeValue<EntityReference>("transactioncurrencyid");
                }
                else if (preImage.Contains("transactioncurrencyid"))
                {
                    currencyRef = preImage.GetAttributeValue<EntityReference>("transactioncurrencyid");
                }

                Currency currencyEntity = PluginBase.GetCurrencyExchangeRate(service, currencyRef);
                return currencyEntity;

            }
        }
    }