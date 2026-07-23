using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Falk_Plugins.Pricing_Master
{
    public class AccessoryTrimCreationPlugin : PluginBase
    {
        public AccessoryTrimCreationPlugin() : base(typeof(AccessoryTrimCreationPlugin)) { }
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
                    if (targetEntity.LogicalName == "opportunityproduct")
                    {
                        if (context.MessageName == CONST_CREATE && context.Stage == PreOperation)
                        {
                            targetEntity["quantity"] = (decimal)0;
                        }
                        if (context.MessageName == CONST_CREATE && context.Stage == PostOperation)
                        {
                            tracingService.Trace("create");
                            string opportunityProductName = targetEntity.GetAttributeValue<string>("opportunityproductname");

                            tracingService.Trace(opportunityProductName);
                            EntityReference panelThickness = targetEntity.GetAttributeValue<EntityReference>("tbs_panelthickness");
                            tracingService.Trace(panelThickness.Id.ToString());

                            EntityReference panelType = targetEntity.GetAttributeValue<EntityReference>("productid");

                            CreatePanelAccessories(service, targetEntity.Id, panelType, panelThickness);

                            CreatePanelTrims(service, targetEntity.Id, opportunityProductName, panelType, panelThickness);

                            OrganizationRequest customApiRequestTrim = new OrganizationRequest("tbs_CalculateTrimQty");

                            customApiRequestTrim["tbs_oppProduct"] = targetEntity.Id;

                            OrganizationResponse customApiResponseTrim = service.Execute(customApiRequestTrim);

                            OrganizationRequest customApiRequestAcces = new OrganizationRequest("tbs_CalculateAccessoryQty");

                            customApiRequestAcces["tbs_oppProduct"] = targetEntity.Id;

                            OrganizationResponse customApiResponseAcces = service.Execute(customApiRequestAcces);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                tracingService.Trace($"CreatePanelAccessoryAndTrim Error : {ex}");
                throw new InvalidPluginExecutionException("Error occurred while creating Panel Accessory and Panel Trim records.", ex);
            }
        }
        private void CreatePanelAccessories(IOrganizationService service, Guid opportunityProductId, EntityReference panelType, EntityReference panelThickness)
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
                tierQuery.Criteria.AddCondition("tbs_name", ConditionOperator.Equal, isUSA ? "Tier1" : "Tier2");
                tierQuery.Criteria.AddCondition("tbs_type", ConditionOperator.Equal, 1);

                Entity tierEnt = service.RetrieveMultiple(tierQuery).Entities.FirstOrDefault();
                tracingService.Trace($"tier entity: {tierEnt?.Id}");
                if (tierEnt != null) {
                    multiplier = tierEnt.GetAttributeValue<int>("tbs_multiplier");
                }
                #endregion

                #region Create Accessories
                foreach (Entity accessory in accessories.Entities)
                {
                    Entity panelAccessory = new Entity("tbs_opppanelaccessory");
                    panelAccessory["tbs_opportunityproduct"] = new EntityReference("opportunityproduct", opportunityProductId);
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

                    service.Create(panelAccessory);
                }

                #endregion
            }
            catch (Exception ex)
            {
                tracingService.Trace($"CreatePanelAccessoryAndTrim Error : {ex}");
                throw new InvalidPluginExecutionException("Error occurred while creating Panel Accessory and Panel Trim records.", ex);
            }
        }
        private void CreatePanelTrims(IOrganizationService service, Guid opportunityProductId, string opportunityProductName, EntityReference panelType, EntityReference panelThickness)
        {
            try
            {
                QueryExpression query = new QueryExpression("tbs_trim");
                query.ColumnSet.AddColumns("tbs_trimid", "tbs_name", "tbs_unit");
                LinkEntity query_tbs_trim_tbs_thickness = query.AddLink("tbs_trim_tbs_thickness", "tbs_trimid", "tbs_trimid");
                query_tbs_trim_tbs_thickness.LinkCriteria.AddCondition("tbs_thicknessid", ConditionOperator.Equal, panelThickness.Id);
                EntityCollection trims = service.RetrieveMultiple(query);

                foreach (Entity trim in trims.Entities)
                {
                    Entity panelTrim = new Entity("tbs_opppaneltrim");
                    panelTrim["tbs_opportunityproduct"] = new EntityReference("opportunityproduct", opportunityProductId);
                    panelTrim["tbs_paneltype"] = panelType;
                    panelTrim["tbs_panelthickness"] = panelThickness;
                    panelTrim["tbs_unit"] = trim.Contains("tbs_unit") ? trim.GetAttributeValue<EntityReference>("tbs_unit") : new EntityReference();
                    panelTrim["tbs_trim"] = trim.ToEntityReference();
                    panelTrim["tbs_iscustomtrim"] = false;
                    service.Create(panelTrim);
                }
            }
            catch (Exception ex)
            {
                tracingService.Trace($"CreatePanelAccessoryAndTrim Error : {ex}");
                throw new InvalidPluginExecutionException("Error occurred while creating Panel Accessory and Panel Trim records.", ex);
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