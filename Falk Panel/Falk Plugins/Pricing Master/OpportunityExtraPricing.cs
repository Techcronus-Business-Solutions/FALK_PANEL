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
    public class OpportunityExtraPricing : PluginBase
    {
        public OpportunityExtraPricing() : base(typeof(OpportunityExtraPricing)) { }

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
             Entity traceTarget = context.InputParameters.Contains(CONST_TARGETENTITY) ? context.InputParameters[CONST_TARGETENTITY] as Entity : null;
                    tracingService.Trace("FIRED | Msg={0} Stage={1} Depth={2} Entity={3} Fields={4}",
                        context.MessageName,
                        context.Stage,
                        context.Depth,
                        traceTarget != null ? traceTarget.LogicalName : "ref",
                        traceTarget != null ? string.Join(",", traceTarget.Attributes.Keys) : "");

            try
            {
                if (context.InputParameters.Contains(CONST_TARGETENTITY) && context.InputParameters[CONST_TARGETENTITY] is Entity)
                {
                    targetEntity = (Entity)context.InputParameters[CONST_TARGETENTITY];
                    if (targetEntity.LogicalName == "opportunityproduct")
                    {
                        if (context.MessageName == CONST_UPDATE && context.Stage == PostOperation)
                        {
                            try
                            {
                                if (targetEntity.Contains("quantity"))
                                {
                                    Entity PostImage = context.PostEntityImages["PostImage"];
                                    Guid oppId = PostImage.GetAttributeValue<EntityReference>("opportunityid").Id;

                                    int totalSQFT = GetTotalSQFTFromOppProducts(oppId);
                                    int totalFilteredSQFT = GetFilteredSQFTFromOppProducts(oppId);

                                    Entity updateOpp = new Entity("opportunity");
                                    updateOpp.Id = oppId;
                                    updateOpp["tbs_totalsqft"] = totalSQFT;
                                    updateOpp["tbs_totalfilteredsqft"] = totalFilteredSQFT;

                                    service.Update(updateOpp);
                                }
                            }
                            catch (Exception ex)
                            {
                                throw new InvalidPluginExecutionException(ex.Message);
                            }
                        }
                    }

                    if (targetEntity.LogicalName == "opportunity")
                    {
                        if (context.MessageName == CONST_UPDATE && context.Stage == PreOperation)
                        {
                            Entity PreImage = context.PreEntityImages["PreImage"];

                            int totalSQFT = targetEntity.Contains("tbs_totalsqft") ? GetIntAttributeValue(targetEntity, "tbs_totalsqft") : GetIntAttributeValue(PreImage, "tbs_totalsqft");
                            int totalFilteredSQFT = targetEntity.Contains("tbs_totalfilteredsqft") ? GetIntAttributeValue(targetEntity, "tbs_totalfilteredsqft") : GetIntAttributeValue(PreImage, "tbs_totalfilteredsqft");
                            Currency currencyCode = GetOpportunityCurrencyIsoCode(targetEntity, PreImage);
                            tracingService.Trace(string.Format("Currency Code: {0}, Exchange Rate: {1}", currencyCode.isoCode, currencyCode.exchangeRate));
                            decimal baseAmount = (currencyCode.isoCode == "CAD") ? 3000m : 5000m;
                            tracingService.Trace(string.Format("Base Amount: {0}", baseAmount));

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

                                SyncShopDrawingsRowSafe(
                                    targetEntity.Id,
                                    shopDrawings,
                                    targetEntity.GetAttributeValue<Money>("tbs_shopdrawingsprice"),
                                    targetEntity.Contains("tbs_shopdrawings"));

                            }
                            if (targetEntity.Contains("tbs_totalsqft") || targetEntity.Contains("tbs_stampedandsealeddrawings"))
                            {
                                tracingService.Trace(string.Format("Total SQFT: {0}", totalSQFT));
                                bool stampedSealedDrawings = targetEntity.Contains("tbs_stampedandsealeddrawings") ? GetBoolAttributeValue(targetEntity, "tbs_stampedandsealeddrawings") : GetBoolAttributeValue(PreImage, "tbs_stampedandsealeddrawings");

                                if (stampedSealedDrawings)
                                {
                                    targetEntity["tbs_stampedandsealedshopdrawingsprice"] = new Money(RoundUpToNearest50((totalSQFT * 0.06m + baseAmount)) * 1.75m * currencyCode.exchangeRate);
                                }
                                else
                                {
                                    targetEntity["tbs_stampedandsealedshopdrawingsprice"] = null;
                                }

                                SyncOpportunityProductRowSafe(
                                    targetEntity.Id,
                                    stampedSealedDrawings,
                                    targetEntity.GetAttributeValue<Money>(
                                        "tbs_stampedandsealedshopdrawingsprice"),
                                    targetEntity.Contains("tbs_stampedandsealeddrawings"),
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
                                SyncOpportunityProductRowSafe(
                                                    targetEntity.Id,
                                                    warranty,
                                                    targetEntity.GetAttributeValue<Money>("tbs_roofwarrantyprice"),
                                                    targetEntity.Contains("tbs_weathertightwarranty"),
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
                    }
                }
                else if (context.InputParameters.Contains(CONST_TARGETENTITY) && (context.InputParameters[CONST_TARGETENTITY] is EntityReference))
                {
                    EntityReference targetEntityRef = (EntityReference)context.InputParameters["Target"];

                    if (targetEntityRef.LogicalName == "opportunityproduct")
                    {
                        if (context.MessageName == CONST_DELETE && context.Stage == PostOperation)
                        {
                            try

                            {
                                  if (!context.PreEntityImages.Contains("PreImage"))
                                           {
                                                tracingService.Trace("PreImage not found.");
                                                    return;
                                            }


                                //if (targetEntity.Contains("quantity"))
                                //{
                                Entity PreImage = context.PreEntityImages["PreImage"];

                                
                                EntityReference oppRef =PreImage.GetAttributeValue<EntityReference>("opportunityid");
                                                            
                                Guid oppId = oppRef.Id;

                                bool isWriteIn = PreImage.GetAttributeValue<bool?>("isproductoverridden") ?? false;
                                String rowName = (PreImage.GetAttributeValue<string>("productdescription") ?? "").Trim();
                                bool isShopDrawingsRow = isWriteIn && string.Equals(rowName, ShopDrawingsRowName,StringComparison.OrdinalIgnoreCase); 

                                 bool isStampedSealedRow =
                                    isWriteIn &&
                                    string.Equals(
                                        rowName,
                                        StampedSealedDrawingsRowName,
                                        StringComparison.OrdinalIgnoreCase);

                                 bool isRoofWarrantyRow =
                                    isWriteIn &&
                                    string.Equals(
                                        rowName,
                                        RoofWarrantyRowName,
                                        StringComparison.OrdinalIgnoreCase);
                                                                                    
                                int totalSQFT = GetTotalSQFTFromOppProducts(oppId);
                                int totalFilteredSQFT = GetFilteredSQFTFromOppProducts(oppId);

                                            Entity updateOpp = new Entity("opportunity", oppId);

                                updateOpp["tbs_totalsqft"] = totalSQFT;
                                updateOpp["tbs_totalfilteredsqft"] = totalFilteredSQFT;

                                if (isShopDrawingsRow)
                                        {
                                            updateOpp["tbs_shopdrawings"] = false;
                                            updateOpp["tbs_shopdrawingsprice"] = null;

                                            tracingService.Trace(
                                                "Manual Shop Drawings deletion detected. " +
                                                "Toggle set to No and price cleared.");
                                        }

                                if (isStampedSealedRow)
                                {
                                    updateOpp["tbs_stampedandsealeddrawings"] = false;
                                    updateOpp["tbs_stampedandsealedshopdrawingsprice"] = null;

                                    tracingService.Trace(
                                        "Manual Stamped and Sealed Drawings deletion detected. Toggle set to No and price cleared.");
                                }

                                if (isRoofWarrantyRow)
                                {
                                    updateOpp["tbs_weathertightwarranty"] = false;
                                    updateOpp["tbs_roofwarrantyprice"] = null;

                                    tracingService.Trace(
                                        "Manual Roof Warranty deletion detected. Toggle set to No and price cleared.");
                                }
                                    service.Update(updateOpp);

                                
                                
                            

                                
                               // if (isWriteIn && string.Equals(rowName, ShopDrawingsRowName, StringComparison.OrdinalIgnoreCase))
                                    //{
                                        // skip if the opportunity itself is being deleted (cascade delete)
                                       // QueryExpression exists = new QueryExpression("opportunity");
                                      // Entity updateOpportunity = new Entity("opportunity", oppId);
                                       // updateOpportunity["tbs_shopdrawings"] = false;
                                        //updateOpportunity["tbs_shopdrawingsprice"] = null;

                                            //service.Update(updateOpportunity);
                                    
                                  //tracingService.Trace(
                                    //"Manual Shop Drawings deletion detected. " +
                                   // "Shop Drawings toggle and price reset.");
                                       
                                  //  }
                                 //return;

                                //int totalSQFT = GetTotalSQFTFromOppProducts(oppId);
                                //int totalFilteredSQFT = GetFilteredSQFTFromOppProducts(oppId);



                                //Entity updateOpp = new Entity("opportunity");
                               // updateOpp.Id = oppId;
                                //updateOpp["tbs_totalsqft"] = totalSQFT;
                                //updateOpp["tbs_totalfilteredsqft"] = totalFilteredSQFT;

                                 //if (isShopDrawingsRow)
                                        //{
                                           // updateOpp["tbs_shopdrawings"] = false;      // toggle -> No
                                           // updateOpp["tbs_shopdrawingsprice"] = null;  // price -> blank
                                           // tracingService.Trace("Manual Shop Drawings deletion detected. Toggle set to No, price                       cleared.");
                                        

                                //service.Update(updateOpp);
                                 
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
                tracingService.Trace($"Extra Pricing Calculation Error : {ex}");
                throw new InvalidPluginExecutionException("Error occurred while Extra Pricing Calculation.", ex);
            }
        }

        private int GetTotalSQFTFromOppProducts(Guid oppId)
        {
            int totalSQFT = 0;
            string excludeShopDrawingsRow = BuildShopDrawingsRowExclusion(oppId);

            string fetchXml = $@"
           <fetch aggregate='true'>
              <entity name='opportunityproduct'>
                <attribute name='quantity' alias='qty' aggregate='sum' />
                <filter>
                  <condition attribute='opportunityid' operator='eq' value='{oppId}' />
                  {excludeShopDrawingsRow}
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

        private int GetFilteredSQFTFromOppProducts(Guid oppId)
        {
            // 2, 2.5, 3, 4, 5, 6, 8
            // RRP40, SSR42, RDEK40, FM-RRP, FM-SSR, FM-RDEK

            int totalSQFT = 0;
            string fetchXml = $@"
                   <fetch aggregate='true'>
                      <entity name='opportunityproduct'>
                        <attribute name='quantity' alias='qty' aggregate='sum' />
                        <filter>
                          <condition attribute='opportunityid' operator='eq' value='{oppId}' />
                          <condition attribute='opportunityproductname' operator='in'>
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
        private Currency GetOpportunityCurrencyIsoCode(Entity target, Entity preImage)
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

        private const string ShopDrawingsRowName = "Shop Drawings";
        private const string StampedSealedDrawingsRowName = "Stamped and Sealed Shop Drawings";
        private const string RoofWarrantyRowName = "Roof Warranty";


        private List<Entity> GetOpportunityProductRows(Guid oppId, string rowName)
        {
            QueryExpression query = new QueryExpression("opportunityproduct");

            query.ColumnSet = new ColumnSet(
                "opportunityproductid",
                "productdescription",
                "priceperunit",
                "quantity");

            query.Criteria.AddCondition(
                "opportunityid",
                ConditionOperator.Equal,
                oppId);

            query.Criteria.AddCondition(
                "isproductoverridden",
                ConditionOperator.Equal,
                true);

            return service.RetrieveMultiple(query).Entities
                .Where(e => string.Equals(
                    (e.GetAttributeValue<string>("productdescription") ?? "").Trim(),
                    rowName,
                    StringComparison.OrdinalIgnoreCase))
                .ToList();
        }



       private List<Entity> GetShopDrawingsRows(Guid oppId)
        {
            return GetOpportunityProductRows(oppId, ShopDrawingsRowName);
        }

        private string BuildShopDrawingsRowExclusion(Guid oppId)
        {
            string[] excludedRows =
            {
                ShopDrawingsRowName,
                StampedSealedDrawingsRowName,
                RoofWarrantyRowName
            };

            return string.Concat(
                excludedRows
                    .SelectMany(rowName =>
                        GetOpportunityProductRows(oppId, rowName))
                    .Select(e =>
                        $"<condition attribute='opportunityproductid' operator='ne' value='{e.Id}' />"));
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
            DeleteRequest request = new DeleteRequest { Target = new EntityReference(logicalName, id) };
            request.Parameters["BypassCustomPluginExecution"] = true;
            service.Execute(request);
        }


        private void SyncShopDrawingsRowSafe(Guid oppId, bool shopDrawings, Money priceMoney, bool allowCreate)
        {
            decimal price = priceMoney != null ? priceMoney.Value : 0m;
            List<Entity> matches = GetShopDrawingsRows(oppId);
                    tracingService.Trace("SYNC | shopDrawings={0} price={1} existingRows={2}", shopDrawings, price, matches.Count);

            if (shopDrawings && price > 0)
            {
                if (matches.Count > 0)
                {
                    Entity existing = matches[0];
                    Money existingPrice = existing.GetAttributeValue<Money>("priceperunit");
                    decimal existingQty = existing.GetAttributeValue<decimal?>("quantity") ?? 0m;

                    // update only when something changed
                    if (existingPrice == null || existingPrice.Value != price || existingQty != 1m)
                    {
                        Entity updateProduct = new Entity("opportunityproduct", existing.Id);
                        updateProduct["ispriceoverridden"] = true;
                        updateProduct["priceperunit"] = new Money(price);
                        updateProduct["quantity"] = 1m;
                        updateProduct["tbs_baseprice"] = new Money(price);
                        UpdateBypassingCustomLogic(updateProduct);
                        tracingService.Trace("Shop Drawings row updated.");
                    }

                    // remove accidental duplicates
                    foreach (Entity duplicate in matches.Skip(1))
                    {
                        DeleteBypassingCustomLogic("opportunityproduct", duplicate.Id);
                    }
                }

                else if (allowCreate)
                {
                    Entity newProduct = new Entity("opportunityproduct");
                    newProduct["opportunityid"] = new EntityReference("opportunity", oppId);
                    newProduct["isproductoverridden"] = true;               // write-in product
                    newProduct["productdescription"] = ShopDrawingsRowName; // "Write In Product" field
                    newProduct["quantity"] = 1m;
                    newProduct["ispriceoverridden"] = true;
                    newProduct["priceperunit"] = new Money(price);
                    newProduct["tbs_baseprice"] = new Money(price);
                    CreateBypassingCustomLogic(newProduct);
                    tracingService.Trace("Shop Drawings row created.");
                
                 }
                else
                {
                    Entity newProduct = new Entity("opportunityproduct");
                    newProduct["opportunityid"] = new EntityReference("opportunity", oppId);
                    newProduct["isproductoverridden"] = true;               // write-in product
                    newProduct["productdescription"] = ShopDrawingsRowName; // "Write In Product" field
                    newProduct["quantity"] = 1m;
                    newProduct["ispriceoverridden"] = true;
                    newProduct["priceperunit"] = new Money(price);
                    newProduct["tbs_baseprice"] = new Money(price);
                    CreateBypassingCustomLogic(newProduct);
                    tracingService.Trace("Shop Drawings row created.");
                }
            }
            else
            {
                // Shop Drawings = No -> delete the row
                foreach (Entity existing in matches)
                {
                    DeleteBypassingCustomLogic("opportunityproduct", existing.Id);
                    tracingService.Trace("Shop Drawings row deleted.");
                }
            }
        }

        private void SyncOpportunityProductRowSafe(
            Guid oppId,
            bool enabled,
            Money priceMoney,
            bool allowCreate,
            string rowName)
        {
            decimal price = priceMoney != null ? priceMoney.Value : 0m;

            List<Entity> matches =
                GetOpportunityProductRows(oppId, rowName);

            tracingService.Trace(
                "SYNC | Row={0} Enabled={1} Price={2} ExistingRows={3}",
                rowName,
                enabled,
                price,
                matches.Count);

            if (enabled && price > 0)
            {
                if (matches.Count > 0)
                {
                    Entity existing = matches[0];

                    Money existingPrice =
                        existing.GetAttributeValue<Money>("priceperunit");

                    decimal existingQty =
                        existing.GetAttributeValue<decimal?>("quantity") ?? 0m;

                    if (existingPrice == null ||
                        existingPrice.Value != price ||
                        existingQty != 1m)
                    {
                        Entity updateProduct =
                            new Entity("opportunityproduct", existing.Id);

                        updateProduct["ispriceoverridden"] = true;
                        updateProduct["priceperunit"] = new Money(price);
                        updateProduct["quantity"] = 1m;
                        updateProduct["tbs_baseprice"] = new Money(price);

                        UpdateBypassingCustomLogic(updateProduct);

                        tracingService.Trace("{0} row updated.", rowName);
                    }

                    // Remove duplicate rows

                    foreach (Entity duplicate in matches.Skip(1))
                    {
                        DeleteBypassingCustomLogic(
                            "opportunityproduct",
                            duplicate.Id);

                        tracingService.Trace(
                            "{0} duplicate row deleted.", rowName);
                    }
                }
                else
                {
                    Entity newProduct = new Entity("opportunityproduct");

                    newProduct["opportunityid"] =
                        new EntityReference("opportunity", oppId);

                    newProduct["isproductoverridden"] = true;
                    newProduct["productdescription"] = rowName;
                    newProduct["quantity"] = 1m;
                    newProduct["ispriceoverridden"] = true;
                    newProduct["priceperunit"] = new Money(price);
                    newProduct["tbs_baseprice"] = new Money(price);

                    CreateBypassingCustomLogic(newProduct);

                    tracingService.Trace("{0} row created.", rowName);
                }
            }
            else
            {
                // Toggle No or price is zero: delete the row

                foreach (Entity existing in matches)
                {
                    DeleteBypassingCustomLogic(
                        "opportunityproduct",
                        existing.Id);

                    tracingService.Trace("{0} row deleted.", rowName);
                }
            }
        }


    }
}



    


