using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Falk.CustomAPI
{
    public class OpportunityProductPricingCalculation : PluginBase
    {
        public OpportunityProductPricingCalculation() : base(typeof(OpportunityProductPricingCalculation)) { }

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
                var product = GetInputRef("Product");
                var panelThickness = GetInputRef("PanelThickness");
                var exteriorFinish = GetInputRef("ExteriorFinish");
                var interiorFinish = GetInputRef("InteriorFinish");
                var exteriorGauge = GetInputRef("ExteriorGauge");
                var interiorGauge = GetInputRef("InteriorGauge");
                var exteriorColor = GetInputRef("ExteriorColor");
                var interiorColor = GetInputRef("InteriorColor");

                var interiorColorCategory = GetColorCategory(interiorColor.Id);
                var exteriorColorCategory = GetColorCategory(exteriorColor.Id);

                decimal interiorPrice = GetPrice(
                    "tbs_pricingmasterinterior",
                    "tbs_interiorprice",
                    new Dictionary<string, object>
                    {
                        { "tbs_paneltype", product.Id },
                        { "tbs_panelthickness", panelThickness.Id },
                        { "tbs_interiorfinish", interiorFinish.Id },
                        { "tbs_interiorgauge", interiorGauge.Id },
                        { "tbs_interiorcolorcategory", interiorColorCategory.Value }
                    },
                    out bool interiorFound);

                decimal exteriorPrice = GetPrice(
                    "tbs_pricingmasterexterior",
                    "tbs_exteriorprice",
                    new Dictionary<string, object>
                    {
                        { "tbs_paneltype", product.Id },
                        { "tbs_panelthickness", panelThickness.Id },
                        { "tbs_exteriorfinish", exteriorFinish.Id },
                        { "tbs_exteriorgauge", exteriorGauge.Id },
                        { "tbs_exteriorcolorcategory", exteriorColorCategory.Value }
                    },
                    out bool exteriorFound);

                context.OutputParameters["InteriorPrice"] = new Money(interiorPrice);
                context.OutputParameters["ExteriorPrice"] = new Money(exteriorPrice);

                //throw new InvalidPluginExecutionException(exteriorFinish.Id + " " + exteriorColor.Id + " " + exteriorGauge.Id + " Color Category: " + exteriorColorCategory.Value);

                //List<string> errors = new List<string>();

                //if (!interiorFound)
                //    errors.Add("Interior pricing not found.");

                //if (!exteriorFound)
                //    errors.Add("Exterior pricing not found.");

                //if (errors.Any())
                //{
                //    throw new InvalidPluginExecutionException(string.Join(Environment.NewLine, errors));
                //}                
            }
            catch (Exception ex)
            {
                tracingService.Trace("OpportunityProductPricingCalculation Custom API Exception: {0}", ex.ToString());
                throw new InvalidPluginExecutionException($"Error in OpportunityProductPricingCalculation Custom API: {ex.Message}");
            }
        }

        private EntityReference GetInputRef(string parameterName)
        {
            if (!context.InputParameters.Contains(parameterName))
                throw new InvalidPluginExecutionException($"Input parameter '{parameterName}' is missing.");

            if (!(context.InputParameters[parameterName] is EntityReference entityReference))
                throw new InvalidPluginExecutionException($"Input parameter '{parameterName}' is invalid.");

            return entityReference;
        }

        private decimal GetPrice(string entityName, string priceField, Dictionary<string, object> conditions, out bool found)
        {
            QueryExpression query = new QueryExpression(entityName)
            {
                ColumnSet = new ColumnSet(priceField),
                TopCount = 1
            };

            foreach (var item in conditions)
            {
                query.Criteria.AddCondition(item.Key, ConditionOperator.Equal, item.Value);
            }

            Entity record = service.RetrieveMultiple(query).Entities.FirstOrDefault();

            if (record == null)
            {
                found = false;
                return 0;
            }

            found = true;
            return record.GetAttributeValue<Money>(priceField)?.Value ?? 0;
        }
        private OptionSetValue GetColorCategory(Guid colorId)
        {
            Entity color = service.Retrieve(
                "tbs_color",
                colorId,
                new ColumnSet("tbs_colorcategory"));

            OptionSetValue category = color.GetAttributeValue<OptionSetValue>("tbs_colorcategory");

            if (category == null)
                throw new InvalidPluginExecutionException("Color Category is missing.");

            return category;
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
