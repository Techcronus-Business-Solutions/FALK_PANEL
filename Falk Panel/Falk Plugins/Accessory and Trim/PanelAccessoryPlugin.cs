using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Falk_Plugins.Accessory_and_Trim
{
    public class PanelAccessoryPlugin : PluginBase
    {
        public PanelAccessoryPlugin() : base(typeof(PanelAccessoryPlugin)) { }

        #region Private Variables
        private IOrganizationService service { get; set; }
        private IPluginExecutionContext context { get; set; }
        private ITracingService tracingService { get; set; }
        private Entity targetEntity { get; set; }
        #endregion

        protected override void ExecuteCrmPlugin(LocalPluginContext localcontext)
        {
            if (localcontext == null)
                throw new ArgumentNullException(nameof(localcontext));

            InitProperties(localcontext);

            try
            {
                if (context.InputParameters.Contains(CONST_TARGETENTITY) && context.InputParameters[CONST_TARGETENTITY] is Entity)
                {
                    targetEntity = (Entity)context.InputParameters[CONST_TARGETENTITY];
                    if (targetEntity.LogicalName == "tbs_opppanelaccessory")
                    {
                        // ---------------- CREATE (PreOperation) ----------------
                        if (context.Stage == PreOperation && context.MessageName == CONST_CREATE)
                        {
                            EntityReference accessoryRef = targetEntity.Contains("tbs_accessory") ? targetEntity.GetAttributeValue<EntityReference>("tbs_accessory") : null;
                            EntityReference oppProductRef = targetEntity.Contains("tbs_opportunityproduct") ? targetEntity.GetAttributeValue<EntityReference>("tbs_opportunityproduct") : null;

                            if (accessoryRef != null && oppProductRef != null)
                            {
                                Entity accessory = service.Retrieve("tbs_accessory", accessoryRef.Id, new ColumnSet("tbs_unit", "tbs_price"));
                                Entity opportunityProduct = service.Retrieve("opportunityproduct", oppProductRef.Id, new ColumnSet("tbs_priceleveltier"));

                                Money unitPrice = accessory.Contains("tbs_price") ? accessory.GetAttributeValue<Money>("tbs_price") : new Money(0);

                                targetEntity["tbs_unit"] = accessory.Contains("tbs_unit") ? accessory.GetAttributeValue<EntityReference>("tbs_unit") : null;
                                targetEntity["tbs_unitprice"] = unitPrice;

                                EntityReference tierRef = opportunityProduct.Contains("tbs_priceleveltier") ? opportunityProduct.GetAttributeValue<EntityReference>("tbs_priceleveltier") : null;

                                if (tierRef != null)
                                {
                                    Entity tier = service.Retrieve("tbs_tier", tierRef.Id, new ColumnSet("tbs_multiplier"));

                                    int tierMultiplier = tier.Contains("tbs_multiplier") ? tier.GetAttributeValue<int>("tbs_multiplier") : 0;

                                    decimal totalPrice = unitPrice.Value * tierMultiplier;
                                    targetEntity["tbs_totalprice"] = new Money(totalPrice);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                tracingService.Trace("AccessoryPlugin Exception: {0}", ex.ToString());
                throw new InvalidPluginExecutionException($"Error in AccessoryPlugin: {ex.Message}");
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
