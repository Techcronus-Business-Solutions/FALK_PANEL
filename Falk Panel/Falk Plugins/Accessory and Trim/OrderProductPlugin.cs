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

namespace Falk_Plugins.Accessory_and_Trim
{
    public class OrderProductPlugin : PluginBase
    {
        public OrderProductPlugin() : base(typeof(OrderProductPlugin)) { }

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
                        if (context.MessageName == CONST_CREATE && context.Stage == PreOperation)
                        {
                            targetEntity["quantity"] = (decimal)0;
                        }
                        if (context.MessageName == CONST_CREATE && context.Stage == PostOperation)
                        {
                            tracingService.Trace("create");
                            string orderProductName = targetEntity.GetAttributeValue<string>("salesorderdetailname");

                            tracingService.Trace(orderProductName);

                            tracingService.Trace(targetEntity.Contains("tbs_panelthickness").ToString());

                            EntityReference panelThickness = targetEntity.GetAttributeValue<EntityReference>("tbs_panelthickness");
                            tracingService.Trace(panelThickness.Id.ToString());

                            EntityReference panelType = targetEntity.GetAttributeValue<EntityReference>("productid");

                            tracingService.Trace("Panel id " + panelType.Id.ToString());

                            CreatePanelAccessories(service, targetEntity.Id, panelType, panelThickness);
                            tracingService.Trace("Panel Accessories created");

                            CreatePanelTrims(service, targetEntity.Id, panelType, panelThickness);
                            tracingService.Trace("Panel Trims created");

                            UpdatePanelTrimQty(service, targetEntity.Id);
                            UpdatePanelAccessQty(service, targetEntity.Id);
                        }
                        if (context.MessageName == CONST_UPDATE && context.Stage == PostOperation)
                        {
                            try
                            {
                                Guid orderProductId = targetEntity.Id;
                                tracingService.Trace("Order ProductId = " + orderProductId.ToString());
                                UpdatePanelTrimQty(service, orderProductId);
                                UpdatePanelAccessQty(service, orderProductId);

                            }
                            catch (Exception ex)
                            {
                                throw new InvalidPluginExecutionException(ex.Message);
                            }
                        }
                        if (context.MessageName == CONST_UPDATE && context.Stage == PreOperation)
                        {
                            try
                            {
                                Entity PreImage = context.PreEntityImages["PreImage"];

                                EntityReference panelThickness = targetEntity.Contains("tbs_panelthickness") ? targetEntity.GetAttributeValue<EntityReference>("tbs_panelthickness") : null;
                                EntityReference prePanelThickness = PreImage.Contains("tbs_panelthickness") ? PreImage.GetAttributeValue<EntityReference>("tbs_panelthickness") : null;
                                EntityReference panelType = PreImage.Contains("productid") ? PreImage.GetAttributeValue<EntityReference>("productid") : null;

                                bool thicknessChanged = (panelThickness == null && prePanelThickness != null) || (panelThickness != null && prePanelThickness == null) || (panelThickness != null && prePanelThickness != null && panelThickness.Id != prePanelThickness.Id);

                                if (!thicknessChanged)
                                {
                                    tracingService.Trace("Panel thickness not changed. Skipping delete logic.");
                                    return;
                                }

                                tracingService.Trace("Panel thickness changed. Deleting related records.");

                                Guid quoteProductId = targetEntity.Id;
                                tracingService.Trace("Order ProductId = " + quoteProductId.ToString());

                                DeleteQuoteAssociatedRecords(quoteProductId);

                                if (panelType != null)
                                {
                                    CreatePanelAccessories(service, targetEntity.Id, panelType, panelThickness);
                                    tracingService.Trace("Panel Accessories created");

                                    CreatePanelTrims(service, targetEntity.Id, panelType, panelThickness);
                                    tracingService.Trace("Panel Trims created");

                                    UpdatePanelTrimQty(service, targetEntity.Id);
                                    UpdatePanelAccessQty(service, targetEntity.Id);
                                }
                            }
                            catch (Exception ex)
                            {
                                throw new InvalidPluginExecutionException(ex.Message);
                            }
                        }
                        if (context.MessageName == CONST_DELETE && context.Stage == PreOperation)
                        {
                            Guid orderProductId = targetEntity.Id;
                            tracingService.Trace("Order ProductId = " + orderProductId.ToString());
                            DeleteQuoteAssociatedRecords(orderProductId);
                        }
                    }
                    else if (targetEntity.LogicalName == "tbs_orderpanelaccessory")
                    {
                        if (context.Depth > 1)
                        {
                            tracingService.Trace("Exiting because plugin depth is greater than 1.");
                            return;
                        }
                        if (context.MessageName == CONST_UPDATE && context.Stage == PostOperation)
                        {
                            try
                            {
                                targetEntity = service.Retrieve("tbs_orderpanelaccessory", targetEntity.Id, new ColumnSet("tbs_orderproduct"));
                                Guid orderProductId = targetEntity.Contains("tbs_orderproduct") ? targetEntity.GetAttributeValue<EntityReference>("tbs_orderproduct").Id : Guid.Empty;
                                UpdatePanelAccessQty(service, orderProductId);
                            }
                            catch (Exception ex)
                            {
                                throw new InvalidPluginExecutionException(ex.Message);
                            }
                        }
                    }
                    else if (targetEntity.LogicalName == "tbs_orderpaneltrim")
                    {
                        if (context.Depth > 1)
                        {
                            tracingService.Trace("Exiting because plugin depth is greater than 1.");
                            return;
                        }
                        if (context.MessageName == CONST_UPDATE && context.Stage == PostOperation)
                        {
                            try
                            {
                                targetEntity = service.Retrieve("tbs_orderpaneltrim", targetEntity.Id, new ColumnSet("tbs_orderproduct"));

                                Guid orderProductId = targetEntity.Contains("tbs_orderproduct") ? targetEntity.GetAttributeValue<EntityReference>("tbs_orderproduct").Id : Guid.Empty;
                                tracingService.Trace("Quote ProductId = " + orderProductId.ToString());

                                UpdatePanelTrimQty(service, orderProductId);
                                UpdatePanelAccessQty(service, orderProductId);
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
                tracingService.Trace($"Create Order Product Panel Accessory and Panel Trim Error : {ex}");
                throw new InvalidPluginExecutionException("Error occurred while creating Panel Accessory and Panel Trim records for Order Product.", ex);
            }
        }

        private void DeleteQuoteAssociatedRecords(Guid quoteProductId)
        {
            //Delete all line items related to quote prod
            QueryExpression queryLineItem = new QueryExpression("tbs_orderlineitem");
            queryLineItem.ColumnSet.AllColumns = false;
            queryLineItem.Criteria.AddCondition("tbs_orderproduct", ConditionOperator.Equal, quoteProductId);
            EntityCollection lineItems = service.RetrieveMultiple(queryLineItem);

            foreach (Entity lineItem in lineItems.Entities)
            {
                service.Delete("tbs_orderlineitem", lineItem.Id);
            }

            //Delete all accessories related to quote prod
            QueryExpression queryAccess = new QueryExpression("tbs_orderpanelaccessory");
            queryAccess.ColumnSet.AllColumns = false;
            queryAccess.Criteria.AddCondition("tbs_orderproduct", ConditionOperator.Equal, quoteProductId);
            EntityCollection accessories = service.RetrieveMultiple(queryAccess);

            foreach (Entity access in accessories.Entities)
            {
                service.Delete("tbs_orderpanelaccessory", access.Id);
            }

            //Delete all trims related to quote prod
            QueryExpression queryTrim = new QueryExpression("tbs_orderpaneltrim");
            queryTrim.ColumnSet.AllColumns = false;
            queryTrim.Criteria.AddCondition("tbs_orderproduct", ConditionOperator.Equal, quoteProductId);
            EntityCollection trims = service.RetrieveMultiple(queryTrim);

            foreach (Entity trim in trims.Entities)
            {
                service.Delete("tbs_orderpaneltrim", trim.Id);
            }
        }

        private void CreatePanelAccessories(IOrganizationService service, Guid orderProductId, EntityReference panelType, EntityReference panelThickness)
        {
            try
            {
                #region List Accessories
                QueryExpression query = new QueryExpression("tbs_accessory");
                query.ColumnSet.AddColumns("tbs_accessoryid");
                LinkEntity pricing = query.AddLink("tbs_accessorypricing", "tbs_accessorypricing", "tbs_accessorypricingid");
                pricing.EntityAlias = "pricing";
                pricing.Columns.AddColumns("tbs_price", "tbs_unit");
                LinkEntity thickness = query.AddLink("tbs_accessory_tbs_thickness", "tbs_accessoryid", "tbs_accessoryid");
                thickness.EntityAlias = "thickness";
                thickness.LinkCriteria.AddCondition("tbs_thicknessid", ConditionOperator.Equal, panelThickness.Id);
                LinkEntity cat = query.AddLink("tbs_itemcategory", "tbs_itemcategory", "tbs_itemcategoryid", JoinOperator.LeftOuter);
                cat.EntityAlias = "cat";

                LinkEntity rule = cat.AddLink("tbs_accessoryrules", "tbs_itemcategoryid", "tbs_category", JoinOperator.LeftOuter);
                rule.EntityAlias = "rule";
                rule.Columns.AddColumn("tbs_ruleclass");
                rule.LinkCriteria.AddCondition("tbs_panel", ConditionOperator.Equal, panelType.Id);


                EntityCollection accessories = service.RetrieveMultiple(query);
                tracingService.Trace($"accessories count: {accessories.Entities.Count}");
                #endregion

                #region Get User Info
                QueryExpression userQuery = new QueryExpression("systemuser");
                userQuery.ColumnSet = new ColumnSet(false);
                userQuery.Criteria.AddCondition("systemuserid", ConditionOperator.Equal, context.InitiatingUserId);
                LinkEntity teretory = userQuery.AddLink("territory", "territoryid", "territoryid");
                teretory.EntityAlias = "teretory";
                teretory.Columns.AddColumn("name");
                LinkEntity parentteretory = teretory.AddLink("territory", "parentterritoryid", "territoryid", JoinOperator.LeftOuter);
                parentteretory.EntityAlias = "parentteretory";
                parentteretory.Columns.AddColumn("name");

                Entity user = service.RetrieveMultiple(userQuery).Entities.FirstOrDefault();
                tracingService.Trace($"User: {user?.Id}");
                bool isUSA = false;

                if (user != null)
                {
                    var territory =
                        user.GetAttributeValue<AliasedValue>("teretory.name")?.Value as string;

                    var parent =
                        user.GetAttributeValue<AliasedValue>("parentteretory.name")?.Value as string;

                    isUSA =
                        territory == "USA" ||
                        parent == "USA";
                }
                #endregion

                #region GetTier
                int multiplier = 100;

                QueryExpression tierQuery = new QueryExpression("tbs_tier");
                tierQuery.ColumnSet.AddColumn("tbs_multiplier");
                tierQuery.Criteria.AddCondition("tbs_name", ConditionOperator.Equal, "Tier1");
                tierQuery.Criteria.AddCondition("tbs_type", ConditionOperator.Equal, 1);

                Entity tierEnt = service.RetrieveMultiple(tierQuery).Entities.FirstOrDefault();
                tracingService.Trace($"tier entity: {tierEnt?.Id}");
                if (tierEnt != null)
                {
                    multiplier = tierEnt.GetAttributeValue<int>("tbs_multiplier");
                }
                #endregion

                #region Create Accessories
                foreach (Entity accessory in accessories.Entities)
                {
                    Entity panelAccessory = new Entity("tbs_orderpanelaccessory");
                    panelAccessory["tbs_orderproduct"] = new EntityReference("salesorderdetail", orderProductId);
                    panelAccessory["tbs_paneltype"] = panelType;
                    panelAccessory["tbs_panelthickness"] = panelThickness;
                    panelAccessory["tbs_accessory"] = accessory.ToEntityReference();
                    panelAccessory["tbs_category"] = tierEnt.ToEntityReference();
                    panelAccessory["tbs_isquantitycalculated"] = accessory.Contains("rule.tbs_ruleclass") && accessory.GetAttributeValue<AliasedValue>("rule.tbs_ruleclass").Value != null ? true : false;

                    var unit = accessory.GetAttributeValue<AliasedValue>("pricing.tbs_unit")?.Value as EntityReference;
                    var price = accessory.GetAttributeValue<AliasedValue>("pricing.tbs_price")?.Value as Money;

                    panelAccessory["tbs_unit"] = unit;

                    if (price != null)
                    {
                        panelAccessory["tbs_unitprice"] = new Money(price.Value * multiplier / 100m);
                    }

                    Guid panelAccesId = service.Create(panelAccessory);
                    tracingService.Trace("accesory created" + panelAccesId);
                }

                #endregion
            }
            catch (Exception ex)
            {
                tracingService.Trace($"CreatePanelAccessoryAndTrim Error : {ex}");
                throw new InvalidPluginExecutionException("Error occurred while creating Panel Accessory and Panel Trim records.", ex);
            }
        }

        private void CreatePanelTrims(IOrganizationService service, Guid orderProductId, EntityReference panelType, EntityReference panelThickness)
        {
            try
            {
                QueryExpression query = new QueryExpression("tbs_trim");
                query.ColumnSet.AddColumns("tbs_trimid", "tbs_name", "tbs_unit");
                LinkEntity query_tbs_trim_tbs_thickness = query.AddLink("tbs_trim_tbs_thickness", "tbs_trimid", "tbs_trimid");
                query_tbs_trim_tbs_thickness.LinkCriteria.AddCondition("tbs_thicknessid", ConditionOperator.Equal, panelThickness.Id);

                LinkEntity cat = query.AddLink("tbs_itemcategory", "tbs_itemcategory", "tbs_itemcategoryid", JoinOperator.LeftOuter);
                cat.EntityAlias = "cat";

                LinkEntity rule = cat.AddLink("tbs_trimrules", "tbs_itemcategoryid", "tbs_category", JoinOperator.LeftOuter);
                rule.EntityAlias = "rule";
                rule.Columns.AddColumn("tbs_ruleclass");
                rule.LinkCriteria.AddCondition("tbs_panel", ConditionOperator.Equal, panelType.Id);

                EntityCollection trims = service.RetrieveMultiple(query);

                foreach (Entity trim in trims.Entities)
                {
                    Entity panelTrim = new Entity("tbs_orderpaneltrim");
                    panelTrim["tbs_orderproduct"] = new EntityReference("salesorderdetail", orderProductId);
                    panelTrim["tbs_paneltype"] = panelType;
                    panelTrim["tbs_panelthickness"] = panelThickness;
                    panelTrim["tbs_unit"] = trim.Contains("tbs_unit") ? trim.GetAttributeValue<EntityReference>("tbs_unit") : new EntityReference();
                    panelTrim["tbs_trim"] = trim.ToEntityReference();
                    panelTrim["tbs_iscustomtrim"] = false;
                    panelTrim["tbs_isquantitycalculated"] = trim.Contains("rule.tbs_ruleclass") && trim.GetAttributeValue<AliasedValue>("rule.tbs_ruleclass").Value != null ? true : false;

                    Guid trimId = service.Create(panelTrim);
                    tracingService.Trace("trim created" + trimId);
                }
            }
            catch (Exception ex)
            {
                tracingService.Trace($"CreatePanelAccessoryAndTrim Error : {ex}");
                throw new InvalidPluginExecutionException("Error occurred while creating Panel Accessory and Panel Trim records.", ex);
            }
        }

        private void UpdatePanelAccessQty(IOrganizationService service, Guid orderProductId)
        {
            tracingService.Trace("Order ProductId = " + orderProductId.ToString());

            OrganizationRequest customApiRequest = new OrganizationRequest("tbs_CalculateOrderAccessoryQty");

            customApiRequest["tbs_orderProduct"] = orderProductId;

            OrganizationResponse customApiResponse = service.Execute(customApiRequest);
        }

        private void UpdatePanelTrimQty(IOrganizationService service, Guid orderProductId)
        {
            tracingService.Trace("Updating Order Panel Trim Quantity");
            tracingService.Trace("Order ProductId = " + orderProductId.ToString());

            OrganizationRequest customApiRequest = new OrganizationRequest("tbs_CalculateOrderTrimQty");

            customApiRequest["tbs_orderProduct"] = orderProductId;

            OrganizationResponse customApiResponse = service.Execute(customApiRequest);
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