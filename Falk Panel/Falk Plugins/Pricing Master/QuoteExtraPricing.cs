using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;

namespace Falk_Plugins.Pricing_Master
{
    /// <summary>
    /// Registration (same step set as before, plus Create):
    ///   quote        Update  PreOperation   (PreImage  "PreImage"  : all attributes)
    ///   quote        Update  PostOperation  (PostImage "PostImage" : tbs_totalextrapricing)
    ///   quotedetail  Create  PostOperation
    ///   quotedetail  Update  PostOperation  (PostImage "PostImage" : quoteid, quantity)
    ///   quotedetail  Delete  PostOperation  (PreImage  "PreImage"  : quoteid, productdescription, isproductoverridden)
    /// </summary>
    public class QuoteExtraPricing : PluginBase
    {
        public QuoteExtraPricing() : base(typeof(QuoteExtraPricing)) { }

        #region Private Variables
        private IOrganizationService service { get; set; }
        private IPluginExecutionContext context { get; set; }
        private ITracingService tracingService { get; set; }
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
                if (!context.InputParameters.Contains(CONST_TARGETENTITY))
                {
                    return;
                }

                if (context.InputParameters[CONST_TARGETENTITY] is Entity)
                {
                    targetEntity = (Entity)context.InputParameters[CONST_TARGETENTITY];

                    if (targetEntity.LogicalName == "quotedetail" && context.Stage == PostOperation &&
                        (context.MessageName == CONST_UPDATE || context.MessageName == "Create"))
                    {
                        HandleQuoteDetailChange();
                    }
                    else if (targetEntity.LogicalName == "quote" && context.MessageName == CONST_UPDATE)
                    {
                        if (context.Stage == PreOperation)
                        {
                            HandleQuotePreUpdate();
                        }
                        else if (context.Stage == PostOperation)
                        {
                            HandleQuotePostUpdate();
                        }
                    }
                }
                else if (context.InputParameters[CONST_TARGETENTITY] is EntityReference)
                {
                    EntityReference targetRef = (EntityReference)context.InputParameters[CONST_TARGETENTITY];
                    if (targetRef.LogicalName == "quotedetail" &&
                        context.MessageName == CONST_DELETE &&
                        context.Stage == PostOperation)
                    {
                        HandleQuoteDetailDelete();
                    }
                }
            }
            catch (Exception ex)
            {
                tracingService.Trace($"Extra Pricing Calculation: {ex}");
                throw new InvalidPluginExecutionException("Error occurred while Extra Pricing Calculation.", ex);
            }
        }

        #region quotedetail Create / Update (Post)
        private void HandleQuoteDetailChange()
        {
            Guid quoteId;

            if (context.MessageName == "Create")
            {
                EntityReference quoteRef = targetEntity.GetAttributeValue<EntityReference>("quoteid");
                if (quoteRef == null) return;
                quoteId = quoteRef.Id;
            }
            else
            {
                if (!targetEntity.Contains("quantity")) return;
                if (!context.PostEntityImages.Contains("PostImage")) return;
                EntityReference quoteRef = context.PostEntityImages["PostImage"].GetAttributeValue<EntityReference>("quoteid");
                if (quoteRef == null) return;
                quoteId = quoteRef.Id;
            }

            Entity quote = service.Retrieve("quote", quoteId, new ColumnSet("skippricecalculation"));
            int? skipPriceCalculation = GetOptionSetAttributeValue(quote, "skippricecalculation");
            if (skipPriceCalculation.HasValue && skipPriceCalculation.Value == 1)
            {
                tracingService.Trace("Converted Quote");
                return;
            }

            int totalSQFT = GetTotalSQFTFromQuoteProducts(quoteId);
            int totalFilteredSQFT = GetFilteredSQFTFromQuoteProducts(quoteId);

            Entity updateQuote = new Entity("quote", quoteId);
            updateQuote["tbs_totalsqft"] = totalSQFT;
            updateQuote["tbs_totalfilteredsqft"] = totalFilteredSQFT;
            service.Update(updateQuote);
        }
        #endregion

        #region quotedetail Delete (Post)
        private void HandleQuoteDetailDelete()
        {
            // Whole quote is being deleted (cascade) - nothing to update.
            if (IsParentDelete())
            {
                tracingService.Trace("Parent delete detected - skipping.");
                return;
            }

            if (!context.PreEntityImages.Contains("PreImage"))
            {
                tracingService.Trace("PreImage not found.");
                return;
            }

            Entity preImage = context.PreEntityImages["PreImage"];
            EntityReference quoteRef = preImage.GetAttributeValue<EntityReference>("quoteid");
            if (quoteRef == null) return;
            Guid quoteId = quoteRef.Id;

            bool isWriteIn = preImage.GetAttributeValue<bool?>("isproductoverridden") ?? false;
            string rowName = (preImage.GetAttributeValue<string>("productdescription") ?? "").Trim();

            Entity updateQuote = new Entity("quote", quoteId);

            if (isWriteIn && string.Equals(rowName, ShopDrawingsRowName, StringComparison.OrdinalIgnoreCase))
            {
                updateQuote["tbs_shopdrawings"] = false;
                updateQuote["tbs_shopdrawingsprice"] = null;
                tracingService.Trace("Manual Shop Drawings line deletion detected.");
            }
            if (isWriteIn && string.Equals(rowName, StampedSealedDrawingsRowName, StringComparison.OrdinalIgnoreCase))
            {
                updateQuote["tbs_stampedandsealeddrawings"] = false;
                updateQuote["tbs_stampedandsealedshopdrawingsprice"] = null;
                tracingService.Trace("Manual Stamped and Sealed line deletion detected.");
            }
            if (isWriteIn && string.Equals(rowName, RoofWarrantyRowName, StringComparison.OrdinalIgnoreCase))
            {
                updateQuote["tbs_weathertightwarranty"] = false;
                updateQuote["tbs_roofwarrantyprice"] = null;
                tracingService.Trace("Manual Weather Tight Warranty line deletion detected.");
            }

            updateQuote["tbs_totalsqft"] = GetTotalSQFTFromQuoteProducts(quoteId);
            updateQuote["tbs_totalfilteredsqft"] = GetFilteredSQFTFromQuoteProducts(quoteId);

            SafeUpdateParent(updateQuote);
        }

        private bool IsParentDelete()
        {
            IPluginExecutionContext parent = context.ParentContext;
            while (parent != null)
            {
                if (parent.MessageName == CONST_DELETE && parent.PrimaryEntityName == "quote")
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

        #region quote Update (Pre)
        private void HandleQuotePreUpdate()
        {
            if (!context.PreEntityImages.Contains("PreImage")) return;
            Entity preImage = context.PreEntityImages["PreImage"];

            int totalSQFT = targetEntity.Contains("tbs_totalsqft")
                ? GetIntAttributeValue(targetEntity, "tbs_totalsqft")
                : GetIntAttributeValue(preImage, "tbs_totalsqft");
            int totalFilteredSQFT = targetEntity.Contains("tbs_totalfilteredsqft")
                ? GetIntAttributeValue(targetEntity, "tbs_totalfilteredsqft")
                : GetIntAttributeValue(preImage, "tbs_totalfilteredsqft");

            Currency currencyCode = GetQuoteCurrencyIsoCode(targetEntity, preImage);
            decimal baseAmount = (currencyCode.isoCode == "CAD") ? 3000m : 5000m;

            // ---------------- Shop Drawings ----------------
            if (targetEntity.Contains("tbs_totalsqft") || targetEntity.Contains("tbs_shopdrawings"))
            {
                bool shopDrawings = GetToggle("tbs_shopdrawings", preImage);
                if (shopDrawings)
                {
                    targetEntity["tbs_shopdrawingsprice"] =
                        new Money(RoundUpToNearest50(totalSQFT * 0.06m + baseAmount) * currencyCode.exchangeRate);
                }
                else
                {
                    targetEntity["tbs_shopdrawingsprice"] = null;
                }

                SyncQuoteProductRowSafe(
                    targetEntity.Id,
                    shopDrawings,
                    targetEntity.GetAttributeValue<Money>("tbs_shopdrawingsprice"),
                    ShopDrawingsRowName);
            }

            // ---------------- Stamped and Sealed ----------------
            if (targetEntity.Contains("tbs_totalsqft") || targetEntity.Contains("tbs_stampedandsealeddrawings"))
            {
                bool stampedSealed = GetToggle("tbs_stampedandsealeddrawings", preImage);
                if (stampedSealed)
                {
                    targetEntity["tbs_stampedandsealedshopdrawingsprice"] =
                        new Money(RoundUpToNearest50(totalSQFT * 0.06m + baseAmount) * 1.75m * currencyCode.exchangeRate);
                }
                else
                {
                    targetEntity["tbs_stampedandsealedshopdrawingsprice"] = null;
                }

                SyncQuoteProductRowSafe(
                    targetEntity.Id,
                    stampedSealed,  
                    targetEntity.GetAttributeValue<Money>("tbs_stampedandsealedshopdrawingsprice"),
                    StampedSealedDrawingsRowName);
            }

            // ---------------- Roof Warranty ----------------
            if (targetEntity.Contains("tbs_totalfilteredsqft") || targetEntity.Contains("tbs_weathertightwarranty"))
            {
                bool warranty = GetToggle("tbs_weathertightwarranty", preImage);
                if (warranty && totalFilteredSQFT > 0)
                {
                    targetEntity["tbs_roofwarrantyprice"] = new Money(totalFilteredSQFT * 0.3m + 10000m);
                }
                else
                {
                    targetEntity["tbs_roofwarrantyprice"] = null;
                }

                SyncQuoteProductRowSafe(
                    targetEntity.Id,
                    warranty,
                    targetEntity.GetAttributeValue<Money>("tbs_roofwarrantyprice"),
                    RoofWarrantyRowName);
            }

            // ---------------- FM Approval / Buy American ----------------
            if (targetEntity.Contains("tbs_fmapproval"))
            {
                if (!GetBoolAttributeValue(targetEntity, "tbs_fmapproval"))
                {
                    targetEntity["tbs_factorymutualspecifiedrequirementsprice"] = null;
                }
            }
            if (targetEntity.Contains("tbs_buyamerican"))
            {
                if (!GetBoolAttributeValue(targetEntity, "tbs_buyamerican"))
                {
                    targetEntity["tbs_buyamericanprice"] = null;
                }
            }
        }

        private bool GetToggle(string field, Entity preImage)
        {
            return targetEntity.Contains(field)
                ? GetBoolAttributeValue(targetEntity, field)
                : GetBoolAttributeValue(preImage, field);
        }
        #endregion

        #region quote Update (Post)
        private void HandleQuotePostUpdate()
        {
            if (targetEntity.Contains("tbs_shopdrawingsprice") ||
                targetEntity.Contains("tbs_stampedandsealedshopdrawingsprice") ||
                targetEntity.Contains("tbs_roofwarrantyprice") ||
                targetEntity.Contains("tbs_factorymutualspecifiedrequirementsprice") ||
                targetEntity.Contains("tbs_buyamericanprice"))
            {
                if (!context.PostEntityImages.Contains("PostImage")) return;
                Entity postImage = context.PostEntityImages["PostImage"];
                decimal totalExtraPricing = GetDecimalAttributeValue(postImage, "tbs_totalextrapricing");
                tracingService.Trace("Extra Pricing : " + totalExtraPricing.ToString());

                Entity updateQuote = new Entity("quote", targetEntity.Id);
                updateQuote["freightamount"] = new Money(totalExtraPricing);
                service.Update(updateQuote);
            }
        }
        #endregion

        #region SQFT queries
        private string BuildWriteInRowExclusion(Guid quoteId)
        {
            string[] excludedRows =
            {
                ShopDrawingsRowName,
                StampedSealedDrawingsRowName,
                RoofWarrantyRowName
            };

            return string.Concat(
                excludedRows
                    .SelectMany(rowName => GetQuoteProductRows(quoteId, rowName))
                    .Select(e => $"<condition attribute='quotedetailid' operator='ne' value='{e.Id}' />"));
        }

        private int GetTotalSQFTFromQuoteProducts(Guid quoteId)
        {
            string excludeWriteIns = BuildWriteInRowExclusion(quoteId);

            string fetchXml = $@"
                <fetch aggregate='true'>
                  <entity name='quotedetail'>
                    <attribute name='quantity' alias='qty' aggregate='sum' />
                    <filter>
                      <condition attribute='quoteid' operator='eq' value='{quoteId}' />
                      {excludeWriteIns}
                    </filter>
                  </entity>
                </fetch>";

            return ReadAggregateQty(fetchXml);
        }

        private int GetFilteredSQFTFromQuoteProducts(Guid quoteId)
        {
            // Thickness: 2, 2.5, 3, 4, 5, 6, 8
            // Products : RRP40, SSR42, RDEK40, FM-RRP, FM-SSR, FM-RDEK
            string fetchXml = $@"
                <fetch aggregate='true'>
                  <entity name='quotedetail'>
                    <attribute name='quantity' alias='qty' aggregate='sum' />
                    <filter>
                      <condition attribute='quoteid' operator='eq' value='{quoteId}' />
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

            return ReadAggregateQty(fetchXml);
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
        private List<Entity> GetQuoteProductRows(Guid quoteId, string rowName)
        {
            QueryExpression query = new QueryExpression("quotedetail")
            {
                ColumnSet = new ColumnSet("quotedetailid", "productdescription", "priceperunit", "quantity", "tbs_sqftamount")
            };

            query.Criteria.AddCondition("quoteid", ConditionOperator.Equal, quoteId);
            query.Criteria.AddCondition("isproductoverridden", ConditionOperator.Equal, true);

            return service.RetrieveMultiple(query).Entities
                .Where(e => string.Equals(
                    (e.GetAttributeValue<string>("productdescription") ?? "").Trim(),
                    rowName,
                    StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        private void SyncQuoteProductRowSafe(Guid quoteId, bool enabled, Money priceMoney, string rowName)
        {
            decimal price = priceMoney != null ? priceMoney.Value : 0m;
            List<Entity> matches = GetQuoteProductRows(quoteId, rowName);

            tracingService.Trace("QUOTE SYNC | Row={0} Enabled={1} Price={2} ExistingRows={3}",
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
                        Entity updateProduct = new Entity("quotedetail", existing.Id);
                        updateProduct["ispriceoverridden"] = true;
                        updateProduct["priceperunit"] = new Money(price);
                        updateProduct["quantity"] = 1m;
                        updateProduct["tbs_baseprice"] = new Money(price);
                        updateProduct["tbs_sqftamount"] = new Money(price);
                        UpdateBypassingCustomLogic(updateProduct);
                        tracingService.Trace("{0} quote line updated.", rowName);
                    }

                    foreach (Entity duplicate in matches.Skip(1))
                    {
                        DeleteBypassingCustomLogic("quotedetail", duplicate.Id);
                        tracingService.Trace("{0} duplicate quote line deleted.", rowName);
                    }
                }
                else
                {
                    Entity newProduct = new Entity("quotedetail");
                    newProduct["quoteid"] = new EntityReference("quote", quoteId);
                    newProduct["isproductoverridden"] = true;
                    newProduct["productdescription"] = rowName;
                    //newProduct["productid"] = rowName;// write-in product name (FIXED: was productname)
                    newProduct["quantity"] = 1m;
                    newProduct["ispriceoverridden"] = true;
                    newProduct["priceperunit"] = new Money(price);
                    newProduct["tbs_baseprice"] = new Money(price);
                    newProduct["tbs_sqftamount"] = new Money(price);
                    CreateBypassingCustomLogic(newProduct);
                    tracingService.Trace("{0} quote line created.", rowName);
                }
            }
            else
            {
                // Toggle = No, or no price -> remove the line.
                foreach (Entity existing in matches)
                {
                    DeleteBypassingCustomLogic("quotedetail", existing.Id);
                    tracingService.Trace("{0} quote line deleted.", rowName);
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

        private Currency GetQuoteCurrencyIsoCode(Entity target, Entity preImage)
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

            return PluginBase.GetCurrencyExchangeRate(service, currencyRef);
        }
    }
}