using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Falk_Plugins.Pricing_Master
{
    public class PricingMasterInteriorPlugin : PluginBase
    {
        public PricingMasterInteriorPlugin() : base(typeof(PricingMasterInteriorPlugin)) { }

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
                    if (targetEntity.LogicalName == "tbs_pricingmasterinterior")
                    {
                        // ---------------- CREATE (PreOperation) ----------------
                        if (context.Stage == PreOperation && context.MessageName == CONST_CREATE)
                        {
                            string panelThickness = GetLookupName(targetEntity, "tbs_panelthickness");
                            string interiorFinish = GetLookupName(targetEntity, "tbs_interiorfinish");
                            string interiorGauge = GetLookupName(targetEntity, "tbs_interiorgauge");
                            string interiorColorCategory = GetLookupName(targetEntity, "tbs_interiorcolorcategory");

                            StringBuilder nameBuilder = new StringBuilder();

                            if (!string.IsNullOrWhiteSpace(panelThickness))
                                nameBuilder.Append(panelThickness);

                            if (!string.IsNullOrWhiteSpace(interiorFinish))
                                nameBuilder.Append(" ").Append(interiorFinish);

                            if (!string.IsNullOrWhiteSpace(interiorGauge))
                                nameBuilder.Append(" ").Append(interiorGauge);

                            // Optional field
                            if (!string.IsNullOrWhiteSpace(interiorColorCategory))
                                nameBuilder.Append(" ").Append(interiorColorCategory);

                            string pricingMasterInteriorName = nameBuilder.ToString().Trim();

                            tracingService.Trace(pricingMasterInteriorName);
                            targetEntity["tbs_name"] = pricingMasterInteriorName;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                tracingService.Trace("PricingMasterInteriorPlugin Exception: {0}", ex.ToString());
                throw new InvalidPluginExecutionException($"Error in PricingMasterInteriorPlugin: {ex.Message}");
            }
        }

        private string GetLookupName(Entity entity, string attributeName)
        {
            if (!entity.Contains(attributeName))
                return string.Empty;

            EntityReference lookup = entity.GetAttributeValue<EntityReference>(attributeName);

            if (lookup == null)
                return string.Empty;

            // If Name is already available, use it
            if (!string.IsNullOrWhiteSpace(lookup.Name))
                return lookup.Name;

            // Otherwise retrieve the record
            Entity record = service.Retrieve(
                lookup.LogicalName,
                lookup.Id,
                new ColumnSet("tbs_name"));

            return record.GetAttributeValue<string>("tbs_name") ?? string.Empty;
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
