using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Falk_Plugins.Pricing_Master
{
    public class ColorPlugin : PluginBase
    {
        public ColorPlugin() : base(typeof(ColorPlugin)) { }

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
                    if (targetEntity.LogicalName == "tbs_color")
                    {
                        // ---------------- CREATE (PreOperation) ----------------
                        if (context.Stage == PreOperation && context.MessageName == CONST_CREATE)
                        {
                            string colorName = targetEntity.GetAttributeValue<string>("tbs_name");

                            OptionSetValue colorCategory = targetEntity.GetAttributeValue<OptionSetValue>("tbs_colorcategory");

                            if (colorCategory != null)
                            {
                                // Get Option Set Label
                                string colorCategoryLabel = GetOptionSetLabel(
                                    "tbs_color",
                                    "tbs_colorcategory",
                                    colorCategory.Value);

                                // Set combined value
                                targetEntity["tbs_name"] = $"{colorName} {colorCategoryLabel}";
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                tracingService.Trace("ColorPlugin Exception: {0}", ex.ToString());
                throw new InvalidPluginExecutionException($"Error in ColorPlugin: {ex.Message}");
            }
        }

        private string GetOptionSetLabel(string entityName, string attributeName, int optionValue)
        {
            RetrieveAttributeRequest request = new RetrieveAttributeRequest
            {
                EntityLogicalName = entityName,
                LogicalName = attributeName,
                RetrieveAsIfPublished = true
            };

            RetrieveAttributeResponse response = (RetrieveAttributeResponse)service.Execute(request);

            EnumAttributeMetadata metadata = (EnumAttributeMetadata)response.AttributeMetadata;

            var option = metadata.OptionSet.Options.FirstOrDefault(o => o.Value == optionValue);

            return option?.Label?.UserLocalizedLabel?.Label ?? string.Empty;
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
