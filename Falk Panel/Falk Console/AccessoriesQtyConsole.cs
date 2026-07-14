using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Sdk;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Services.Description;
using Microsoft.Xrm.Tooling.Connector;

namespace Falk_Console
{
    public class AccessoriesQtyConsole
    {
        public static Entity targetEntity = new Entity();
        public static CrmServiceClient service;

        public static void CalculateQty(CrmServiceClient serviceParam)
        {
            try
            {
                service = serviceParam;
                targetEntity = service.Retrieve("tbs_opppanelaccessory", new Guid("afc90b0f-607c-f111-ab0f-6045bd042674"), new ColumnSet(true));

                Guid oppProductId = GetOpportunityProductId();
                Console.WriteLine("Opportunity ProductId = " + oppProductId.ToString());

                Entity opportunityProduct = GetOpportunityProduct(oppProductId);

                EntityCollection accessories = GetAccessories(oppProductId);

                CalculationContext context = BuildCalculationContext(opportunityProduct);

                CalculateAllAccessories(accessories, context);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        private static Guid GetOpportunityProductId()
        {
            return targetEntity.GetAttributeValue<EntityReference>("tbs_opportunityproduct").Id;
        }

        private static Entity GetOpportunityProduct(Guid oppProductId)
        {
            return service.Retrieve("opportunityproduct", oppProductId, new ColumnSet("quantity", "tbs_linearfeet"));
        }
        private static EntityCollection GetAccessories(Guid opportunityProductId)
        {
            QueryExpression query = new QueryExpression("tbs_opppanelaccessory");
            query.ColumnSet = new ColumnSet("tbs_quantity", "tbs_accessory");
            query.Criteria.AddCondition("tbs_opportunityproduct", ConditionOperator.Equal, opportunityProductId);
            LinkEntity accessoryLink = query.AddLink("tbs_accessory", "tbs_accessory", "tbs_accessoryid");
            accessoryLink.EntityAlias = "acc";
            accessoryLink.Columns = new ColumnSet("tbs_itemcategory");
            EntityCollection accessories = service.RetrieveMultiple(query);
            Console.WriteLine(accessories.Entities.Count.ToString());
            return accessories;
        }

        private static int CalculateQuantity(AccessoryConfiguration config, CalculationContext context)
        {
            string rule = config.RuleClass;
            Console.WriteLine(rule);

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
            public decimal DepCat1 { get; set; }
            public string DepTbl2 { get; set; }
            public decimal DepCat2 { get; set; }
            public string DepTbl3 { get; set; }
            public decimal DepCat3 { get; set; }
            public EntityReference ItemCategory { get; set; }
        }

        private static decimal GetDependencyValue(CalculationContext context, string table, string category)
        {
            string key = $"{table}|{category}";

            if (!context.Values.ContainsKey(key))
                throw new InvalidPluginExecutionException(
                    $"Dependency not calculated : {key}");

            return context.Values[key];
        }
        private static void UpdateAccessoryQuantity(Guid id, int qty)
        {
            Entity update =
                new Entity("tbs_opppanelaccessory");

            update.Id = id;

            update["tbs_quantity"] = qty;

            service.Update(update);
        }
        private static void CalculateAllAccessories(EntityCollection accessories, CalculationContext context)
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
        private static CalculationContext BuildCalculationContext(Entity opportunityProduct)
        {
            return new CalculationContext
            {
                SqFt = opportunityProduct.GetAttributeValue<decimal>("quantity"),

                LinearFeet = opportunityProduct.GetAttributeValue<decimal>("tbs_linearfeet")
            };
        }
        private static AccessoryConfiguration BuildAccessoryConfiguration(Entity accessory)
        {



            AccessoryConfiguration accConfig = new AccessoryConfiguration
            {
                OpportunityAccessoryId = accessory.Id,
                ItemCategory = GetAliasedValue<EntityReference>(accessory, "acc.tbs_itemcategory"),
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
            return accConfig;
        }
        private static T GetAliasedValue<T>(Entity entity, string alias)
        {
            if (!entity.Contains(alias))
                return default(T);

            AliasedValue value =
                entity.GetAttributeValue<AliasedValue>(alias);

            if (value == null)
                return default(T);

            return (T)value.Value;
        }
    }
}
