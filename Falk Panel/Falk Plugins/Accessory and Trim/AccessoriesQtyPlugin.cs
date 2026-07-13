using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Activities.Statements;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Caching;
using System.Web.UI.WebControls;
using System.Windows.Forms;

namespace Falk_Plugins.Accessory_and_Trim
{
    public class AccessoriesQtyPlugin : PluginBase
    {
        public AccessoriesQtyPlugin() : base(typeof(AccessoriesQtyPlugin)) { }

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
                    if (targetEntity.LogicalName == "tbs_opppanelaccessory")
                    {
                        if (context.MessageName == CONST_CREATE && context.Stage == PreOperation)
                        {

                            Guid oppProductId = GetOpportunityProductId();
                            tracingService.Trace("Opportunity ProductId = " + oppProductId.ToString());

                            Entity opportunityProduct = GetOpportunityProduct(oppProductId);

                            EntityCollection accessories = GetAccessories(oppProductId);

                            CalculationContext context = BuildCalculationContext(opportunityProduct);

                            CalculateAllAccessories(accessories, context);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                throw new InvalidPluginExecutionException("exception : " + e.Message);
            }
        }

        private Guid GetOpportunityProductId()
        {
            return targetEntity
                .GetAttributeValue<EntityReference>("tbs_opportunityproduct")
                .Id;
        }

        private Entity GetOpportunityProduct(Guid oppProductId)
        {
            return service.Retrieve(
                "opportunityproduct",
                oppProductId,
                new ColumnSet(
                    "quantity",
                    "tbs_linearfeet"));
        }
        private EntityCollection GetAccessories(Guid opportunityProductId)
        {
            QueryExpression query = new QueryExpression("tbs_opppanelaccessory");

            query.ColumnSet = new ColumnSet("tbs_quantity", "tbs_accessory");

            query.Criteria.AddCondition("tbs_opportunityproduct", ConditionOperator.Equal, opportunityProductId);

            LinkEntity accessoryLink = query.AddLink("tbs_accessory", "tbs_accessory", "tbs_accessoryid");

            accessoryLink.EntityAlias = "acc";

            accessoryLink.Columns = new ColumnSet("tbs_ruleclass","tbs_multiplier1","tbs_multiplier2","tbs_itemcategory","tbs_deptbl1","tbs_depcat1","tbs_deptbl2","tbs_depcat2","tbs_deptbl3","tbs_depcat3");
            EntityCollection accessories = service.RetrieveMultiple(query);
            tracingService.Trace(accessories.Entities.Count.ToString());
            return accessories;
        }

        private int CalculateQuantity(AccessoryConfiguration config, CalculationContext context)
        {
            string rule = config.RuleClass;
            tracingService.Trace(rule);

            decimal div1 = config.Mult1;
            decimal div2 = config.Mult2;

            switch (rule)
            {
                case "SF / Div1":
                    return (int)Math.Ceiling(context.SqFt / div1);

                case "SF / Div1 / Div2":
                    return (int)Math.Ceiling(context.SqFt / div1 / div2);

                case "Count / Div1":
                    return (int)Math.Ceiling(
                        GetDependencyValue(context, config.DepTbl1, config.DepCat1) / div1);

                case "Count / Div1 / Div2":
                    return (int)Math.Ceiling(
                        GetDependencyValue(context, config.DepTbl1, config.DepCat1) / div1 / div2);

                case "Count":
                    return (int)GetDependencyValue(
                        context,
                        config.DepTbl1,
                        config.DepCat1);

                case "LFT / Div1":
                    return (int)Math.Ceiling(context.LinearFeet / div1);

                // Uncomment once Perimeter is available in CalculationContext
                /*
                case "LFP / Div1 / Div2":
                    return (int)Math.Ceiling(context.Perimeter / div1 / div2);
                */

                case "(CountA + CountB) / Div1":
                    return (int)Math.Ceiling(
                        (
                            GetDependencyValue(context, config.DepTbl1, config.DepCat1) +
                            GetDependencyValue(context, config.DepTbl2, config.DepCat2)
                        ) / div1);

                case "(CountA + CountB) / Div2":
                    return (int)Math.Ceiling(
                        (
                            GetDependencyValue(context, config.DepTbl1, config.DepCat1) +
                            GetDependencyValue(context, config.DepTbl2, config.DepCat2)
                        ) / div2);

                case "(CountA + CountB) / Div1 / Div2":
                    return (int)Math.Ceiling(
                        (
                            GetDependencyValue(context, config.DepTbl1, config.DepCat1) +
                            GetDependencyValue(context, config.DepTbl2, config.DepCat2)
                        ) / div1 / div2);

                case "(2CountA + CountB) / Div1 / Div2":
                    return (int)Math.Ceiling(
                        (
                            (2 * GetDependencyValue(context, config.DepTbl1, config.DepCat1)) +
                            GetDependencyValue(context, config.DepTbl2, config.DepCat2)
                        ) / div1 / div2);

                case "(CountA + CountB + 2CountC) / Div1 / Div2":
                    return (int)Math.Ceiling(
                        (
                            GetDependencyValue(context, config.DepTbl1, config.DepCat1) +
                            GetDependencyValue(context, config.DepTbl2, config.DepCat2) +
                            (2 * GetDependencyValue(context, config.DepTbl3, config.DepCat3))
                        ) / div1 / div2);

                case "(2CountA + 2CountB + 4CountC) / Div1 / Div2":
                    return (int)Math.Ceiling(
                        (
                            (2 * GetDependencyValue(context, config.DepTbl1, config.DepCat1)) +
                            (2 * GetDependencyValue(context, config.DepTbl2, config.DepCat2)) +
                            (4 * GetDependencyValue(context, config.DepTbl3, config.DepCat3))
                        ) / div1 / div2);

                case "(Count * Mult1) / Div1":
                    return (int)Math.Ceiling(
                        (
                            GetDependencyValue(context, config.DepTbl1, config.DepCat1) * div2
                        ) / div1);

                case "Count * Mult1":
                    return (int)Math.Ceiling(
                        GetDependencyValue(context, config.DepTbl1, config.DepCat1) * div1);

                default:
                    throw new InvalidPluginExecutionException(
                        $"Unknown RuleClass : {rule}");
            }
        }
        public class AccessoryConfiguration
        {
            public Guid OpportunityAccessoryId { get; set; }
            public string RuleClass { get; set; }
            public decimal Mult1 { get; set; }   
            public decimal Mult2 { get; set; }
            public string DepTbl1 { get; set; }
            public string DepCat1 { get; set; }
            public string DepTbl2 { get; set; }
            public string DepCat2 { get; set; }
            public string DepTbl3 { get; set; }
            public string DepCat3 { get; set; }
            public string ItemCategory { get; set; }
        }

        private decimal GetDependencyValue(CalculationContext context, string table, string category)
        {
            string key = $"{table}|{category}";

            if (!context.Values.ContainsKey(key))
                throw new InvalidPluginExecutionException(
                    $"Dependency not calculated : {key}");

            return context.Values[key];
        }
        private void UpdateAccessoryQuantity(Guid id, int qty)
        {
            Entity update =
                new Entity("tbs_opppanelaccessory");

            update.Id = id;

            update["tbs_quantity"] = qty;

            service.Update(update);
        }
        private void CalculateAllAccessories(EntityCollection accessories, CalculationContext context)
        {
            foreach (Entity accessory in accessories.Entities)
            {
                AccessoryConfiguration config =
                    BuildAccessoryConfiguration(accessory);

                if (string.IsNullOrWhiteSpace(config.RuleClass))
                    continue;

                int qty = CalculateQuantity(config, context);

                context.Values[$"Accessory|{config.ItemCategory}"] = qty;

                UpdateAccessoryQuantity(accessory.Id, qty);
            }
        }
        private class CalculationContext
        {
            public decimal SqFt { get; set; }

            public decimal LinearFeet { get; set; }

            public string PanelFamily { get; set; }

            public Dictionary<string, decimal> Values
                = new Dictionary<string, decimal>();
        }
        private CalculationContext BuildCalculationContext(Entity opportunityProduct)
        {
            return new CalculationContext
            {
                SqFt = opportunityProduct.GetAttributeValue<decimal>("quantity"),

                LinearFeet = opportunityProduct.GetAttributeValue<decimal>("tbs_linearfeet")
            };
        }
        private AccessoryConfiguration BuildAccessoryConfiguration(Entity accessory)
        {
            return new AccessoryConfiguration
            {
                OpportunityAccessoryId = accessory.Id,

                ItemCategory = GetAliasedValue<string>(accessory, "acc.tbs_itemcategory"),

                RuleClass = GetAliasedValue<string>(accessory, "acc.tbs_ruleclass"),

                Mult1 = GetAliasedValue<decimal>(accessory, "acc.tbs_multiplier1"),

                Mult2 = GetAliasedValue<decimal>(accessory, "acc.tbs_multiplier2"),

                DepTbl1 = GetAliasedValue<string>(accessory, "acc.tbs_deptbl1"),

                DepCat1 = GetAliasedValue<string>(accessory, "acc.tbs_depcat1"),

                DepTbl2 = GetAliasedValue<string>(accessory, "acc.tbs_deptbl2"),

                DepCat2 = GetAliasedValue<string>(accessory, "acc.tbs_depcat2"),

                DepTbl3 = GetAliasedValue<string>(accessory, "acc.tbs_deptbl3"),

                DepCat3 = GetAliasedValue<string>(accessory, "acc.tbs_depcat3")
            };
        }
        private T GetAliasedValue<T>(Entity entity, string alias)
        {
            if (!entity.Contains(alias))
                return default(T);

            AliasedValue value =
                entity.GetAttributeValue<AliasedValue>(alias);

            if (value == null)
                return default(T);

            return (T)value.Value;
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

