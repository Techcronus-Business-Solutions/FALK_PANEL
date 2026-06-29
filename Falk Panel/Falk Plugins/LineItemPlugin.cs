
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Falk_Plugins
{
    internal class LineItemPlugin : PluginBase
    {
        public LineItemPlugin() : base(typeof(LineItemPlugin)) { }

        private IOrganizationService service { get; set; }

        private IPluginExecutionContext context { get; set; }

        private ITracingService tracingService { get; set; }

        private Entity targetEntity { get; set; }


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
                    if (targetEntity.LogicalName == "tbs_lineitem")
                    {
                        if (context.MessageName == CONST_CREATE && context.Stage == PreOperation)
                        {
                            //Fill linear FT & Inch
                            #region Fill Linear FT & Inch
                            if (targetEntity.Contains("tbs_ft") && targetEntity.Contains("tbs_in") && targetEntity.GetAttributeValue<decimal>("tbs_ft") > 0 && targetEntity.GetAttributeValue<decimal>("tbs_in") > 0)
                            {
                                decimal feet = targetEntity.GetAttributeValue<decimal>("tbs_ft");
                                decimal inches = targetEntity.GetAttributeValue<decimal>("tbs_in");

                                int wholeFeet = (int)Math.Truncate(feet);
                                int wholeInches = (int)Math.Truncate(inches);

                                decimal fraction = inches - wholeInches;

                                int quarter = (int)Math.Round(fraction * 4);

                                string fractionText = "";

                                if (quarter == 4)
                                {
                                    wholeInches++;
                                    quarter = 0;
                                }

                                if (wholeInches == 12)
                                {
                                    wholeFeet++;
                                    wholeInches = 0;
                                }

                                switch (quarter)
                                {
                                    case 1:
                                        fractionText = "1/4";
                                        break;
                                    case 2:
                                        fractionText = "2/4";   // Change to "1/2" if preferred
                                        break;
                                    case 3:
                                        fractionText = "3/4";
                                        break;
                                }

                                string result = $"{wholeFeet}' {wholeInches}";

                                if (!string.IsNullOrWhiteSpace(fractionText))
                                    result += $" {fractionText}";

                                result += "\"";

                                targetEntity["tbs_linearftinch"] = result;

                            }
                            #endregion

                            #region Get Panel Width
                            if (targetEntity.Contains("tbs_opportunityproduct") && targetEntity.GetAttributeValue<EntityReference>("tbs_opportunityproduct") != null)
                            {
                                QueryExpression query = new QueryExpression("tbs_thickness");
                                query.ColumnSet.AddColumn("tbs_visiblepanelwidth");
                                LinkEntity oppProduct = query.AddLink("opportunityproduct", "tbs_thicknessid", "tbs_panelthickness");
                                oppProduct.EntityAlias = "oppProduct";

                                var LI = oppProduct.AddLink("tbs_lineitem", "opportunityproductid", "tbs_opportunityproduct");
                                LI.EntityAlias = "LI";

                                LI.LinkCriteria.AddCondition("tbs_lineitemid", ConditionOperator.Equal, targetEntity.Id);
                                Entity thickness = service.RetrieveMultiple(query).Entities.FirstOrDefault();

                                if (thickness.Contains("tbs_visiblepanelwidth"))
                                {
                                    decimal width = thickness.GetAttributeValue<decimal>("tbs_visiblepanelwidth");
                                    if (width > 0)
                                    {
                                        targetEntity["tbs_widthpanel"] = width;
                                    }
                                }
                            }
                            #endregion

                            #region Calculate SQFT
                            if (targetEntity.Contains("tbs_numberofpanels") && targetEntity.Contains("tbs_ft") && targetEntity.Contains("tbs_in") && targetEntity.Contains("tbs_widthpanel"))
                            {
                                int noOfPanels = targetEntity.GetAttributeValue<int>("tbs_numberofpanels");
                                decimal feet = targetEntity.GetAttributeValue<decimal>("tbs_ft");
                                decimal inches = targetEntity.GetAttributeValue<decimal>("tbs_in");
                                decimal width = targetEntity.GetAttributeValue<decimal>("tbs_widthpanel");

                                decimal totalSqFt = (noOfPanels * ((feet * 12) + inches) * width) / 144;

                                targetEntity["tbs_totalsqft"] = Math.Round(totalSqFt, 2);
                            }
                            #endregion
                        }
                    }
                }
            }
            catch (Exception e)
            {
                throw new InvalidPluginExecutionException("exception : " + e.Message);
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
