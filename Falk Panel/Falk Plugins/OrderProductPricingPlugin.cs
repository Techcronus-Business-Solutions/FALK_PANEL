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
    public class OrderProductPricingPlugin : PluginBase
    {
        public OrderProductPricingPlugin() : base(typeof(OrderProductPricingPlugin)) { }
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
                            CalculatePricing(targetEntity, new Entity("salesorderdetail"));
                        }

                        if (context.MessageName == CONST_UPDATE && context.Stage == PreOperation)
                        {
                            Entity preImage = context.PreEntityImages["PreImage"];

                            CalculatePricing(targetEntity, preImage);
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

        private decimal roundValues(decimal value)
        {
            return Math.Round(value, 2, MidpointRounding.AwayFromZero);
        }
        private void CalculatePricing(Entity target, Entity preImage)
        {
            decimal basePrice = target.GetAttributeValue<Money>("tbs_baseprice")?.Value
                ?? preImage.GetAttributeValue<Money>("tbs_baseprice")?.Value
                ?? 0;

            decimal totalPropertyPrice = target.GetAttributeValue<Money>("tbs_totalpropertiesprice")?.Value
                ?? preImage.GetAttributeValue<Money>("tbs_totalpropertiesprice")?.Value
                ?? 0;

            decimal usPrice = roundValues(basePrice) + roundValues(totalPropertyPrice);
            target["tbs_usprice"] = new Money(roundValues(usPrice));

            decimal sqft = target.Contains("quantity")
                ? target.GetAttributeValue<decimal>("quantity")
                : preImage.GetAttributeValue<decimal>("quantity");

            decimal usdAdjustment = target.GetAttributeValue<Money>("tbs_usdpriceadjustment")?.Value
                ?? preImage.GetAttributeValue<Money>("tbs_usdpriceadjustment")?.Value
                ?? 0;

            decimal upcharge = 0;

            if (sqft > 0)
            {
                if (sqft < 1000)
                {
                    upcharge = (usPrice * 0.10m) + (1200m / sqft);
                }
                else if (sqft < 3500)
                {
                    upcharge = (usPrice * 0.10m) + (750m / sqft);
                }
            }

            target["tbs_smallorderupcharge"] = new Money(roundValues(upcharge));

            decimal pricePerUnit = roundValues(usPrice) + roundValues(usdAdjustment) + roundValues(upcharge);

            target["ispriceoverridden"] = true;
            target["priceperunit"] = new Money(roundValues(pricePerUnit));
            target["extendedamount"] = new Money(roundValues(pricePerUnit) * roundValues(sqft));
        }
    }
}