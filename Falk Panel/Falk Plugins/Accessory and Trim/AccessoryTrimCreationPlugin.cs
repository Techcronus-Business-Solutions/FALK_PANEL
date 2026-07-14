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

                            CreatePanelAccessories(service, targetEntity, panelType, panelThickness);

                            CreatePanelTrims(service, targetEntity.Id, opportunityProductName, panelType, panelThickness);
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
        private void CreatePanelAccessories(IOrganizationService service, Entity opportunityProduct, EntityReference panelType, EntityReference panelThickness)
        {
            try
            {
                QueryExpression query = new QueryExpression("tbs_accessory");
                query.ColumnSet.AddColumns("tbs_accessoryid", "tbs_accessorypricing", "tbs_legacydescription", "tbs_name", "tbs_salesid", "tbs_unit");
                LinkEntity pricing = query.AddLink("tbs_accessorypricing", "tbs_accessorypricing", "tbs_accessorypricingid");
                pricing.EntityAlias = "pricing";
                pricing.Columns.AddColumns("tbs_price", "tbs_unit");
                LinkEntity thickness = query.AddLink("tbs_accessory_tbs_thickness", "tbs_accessoryid", "tbs_accessoryid");
                thickness.EntityAlias = "thickness";
                thickness.LinkCriteria.AddCondition("tbs_thicknessid", ConditionOperator.Equal, panelThickness.Id);

                EntityCollection accessories = service.RetrieveMultiple(query);

                foreach (Entity accessory in accessories.Entities)
                {
                    Entity panelAccessory = new Entity("tbs_opppanelaccessory");
                    panelAccessory["tbs_opportunityproduct"] = new EntityReference("opportunityproduct", opportunityProduct.Id);
                    panelAccessory["tbs_paneltype"] = panelType;
                    panelAccessory["tbs_panelthickness"] = panelThickness;
                    panelAccessory["tbs_accessory"] = accessory.ToEntityReference();
                    panelAccessory["tbs_unit"] = (EntityReference)accessory.GetAttributeValue<AliasedValue>("pricing.tbs_unit").Value;
                    //Take category from login user - if us - tier 1 else tier 2
                    //panelAccessory["tbs_category"] = 
                    panelAccessory["tbs_unitprice"] = (Money)accessory.GetAttributeValue<AliasedValue>("pricing.tbs_price").Value;
                    service.Create(panelAccessory);
                }
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
                string fetchXml = $@"
                <fetch distinct='true'>
                  <entity name='tbs_trim'>
                    <attribute name='tbs_trimid' />
                    <attribute name='tbs_name' />
                    <link-entity
                        name='tbs_trim_tbs_thickness'
                        from='tbs_trimid'
                        to='tbs_trimid'
                        intersect='true'>
                      <filter>
                        <condition
                          attribute='tbs_thicknessid'
                          operator='eq'
                          value='{panelThickness.Id}' />
                      </filter>
                    </link-entity>
                  </entity>
                </fetch>";

                EntityCollection trims = service.RetrieveMultiple(new FetchExpression(fetchXml));

                foreach (Entity trim in trims.Entities)
                {
                    Entity panelTrim = new Entity("tbs_opppaneltrim");

                    panelTrim["tbs_name"] = $"{opportunityProductName} | {trim.GetAttributeValue<string>("tbs_name")}";

                    panelTrim["tbs_opportunityproduct"] = new EntityReference("opportunityproduct", opportunityProductId);

                    panelTrim["tbs_paneltype"] = panelType;

                    panelTrim["tbs_panelthickness"] = panelThickness;

                    panelTrim["tbs_trim"] = trim.ToEntityReference();

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