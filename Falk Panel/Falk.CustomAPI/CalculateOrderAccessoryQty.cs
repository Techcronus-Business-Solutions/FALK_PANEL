using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Sdk;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Crm.Sdk.Messages;
using System.Activities.Statements;

namespace Falk.CustomAPI
{
    public class CalculateOrderAccessoryQty : CustomAPIBase
    {
        public CalculateOrderAccessoryQty() : base(typeof(CalculateOrderAccessoryQty)) { }

        #region Private Variables
        private IOrganizationService service { get; set; }
        private IPluginExecutionContext context { get; set; }
        private ITracingService tracingService { get; set; }
        #endregion

        protected override void ExecuteCrmPlugin(LocalPluginContext localcontext)
        {
            if (localcontext == null)
                throw new ArgumentNullException(nameof(localcontext));

            InitProperties(localcontext);

            try
            {
                tracingService.Trace("execution started");
                Guid orderProductId = GetInputGuid("tbs_orderProduct");
                tracingService.Trace("Order ProductId = " + orderProductId.ToString());

                Entity orderdetail = GetOrderProduct(orderProductId);

                EntityCollection accessories = GetAccessories(orderProductId);

                CalculationContext calccontext = BuildCalculationContext(orderdetail);

                AddTrimsIntoDictionary(orderProductId, calccontext);

                CalculateAllAccessories(accessories, calccontext);

                try
                {
                    CalculateRollupFieldRequest calcularRollupAcces = new CalculateRollupFieldRequest
                    {
                        Target = new EntityReference("salesorderdetail", orderProductId),
                        FieldName = "tbs_accessoriespricetotal"
                    };
                    CalculateRollupFieldResponse calcularRollupAccesResult = (CalculateRollupFieldResponse)service.Execute(calcularRollupAcces);

                    CalculateRollupFieldRequest calcularRollupTrim = new CalculateRollupFieldRequest
                    {
                        Target = new EntityReference("salesorderdetail", orderProductId),
                        FieldName = "tbs_trimspricetotal"
                    };
                    CalculateRollupFieldResponse calcularRollupTrimResult = (CalculateRollupFieldResponse)service.Execute(calcularRollupTrim);
                }
                catch (Exception e)
                {
                    tracingService.Trace($"Error Occurred in Calculating Rollup :{e.Message}");
                    throw new InvalidPluginExecutionException(e.Message);
                }

            }
            catch (Exception ex)
            {
                tracingService.Trace("CalculateAccessoryQty Custom API Exception: {0}", ex.ToString());
                throw new InvalidPluginExecutionException($"Error in CalculateAccessoryQty Custom API: {ex.Message}");
            }
        }

        private Entity GetOrderProduct(Guid orderProductId)
        {
            return service.Retrieve("salesorderdetail", orderProductId, new ColumnSet("quantity", "tbs_linearfeet"));
        }

        private void AddTrimsIntoDictionary(Guid orderProductId, CalculationContext context)
        {
            QueryExpression query = new QueryExpression("tbs_orderpaneltrim");
            query.ColumnSet = new ColumnSet("tbs_quantity", "tbs_trim");

            query.Criteria.AddCondition("tbs_orderproduct", ConditionOperator.Equal, orderProductId);

            LinkEntity trimLink = query.AddLink("tbs_trim", "tbs_trim", "tbs_trimid");

            trimLink.EntityAlias = "trim";
            trimLink.Columns = new ColumnSet("tbs_itemcategory");

            EntityCollection trims = service.RetrieveMultiple(query);

            tracingService.Trace($"Trim Count : {trims.Entities.Count}");

            foreach (Entity trim in trims.Entities)
            {
                EntityReference category =
                    trim.GetAttributeValue<AliasedValue>("trim.tbs_itemcategory")?.Value as EntityReference;

                if (category == null)
                    continue;

                decimal qty = 0;

                if (trim.Contains("tbs_quantity"))
                    qty = trim.GetAttributeValue<int>("tbs_quantity");

                string key = BuildKey(new OptionSetValue(2), category);

                context.Values[key] = qty;

                tracingService.Trace($"Trim Added : {key} = {qty}");
            }
        }
        private EntityCollection GetAccessories(Guid OrderProductId)
        {
            EntityCollection accessories = new EntityCollection();

            try
            {
                QueryExpression query = new QueryExpression("tbs_orderpanelaccessory");
                query.ColumnSet = new ColumnSet("tbs_quantity", "tbs_accessory", "tbs_category", "tbs_unitprice", "tbs_paneltype", "tbs_usecustomquantity", "tbs_isquantitycalculated");
                query.Criteria.AddCondition("tbs_orderproduct", ConditionOperator.Equal, OrderProductId);
                query.AddOrder("tbs_accessory", OrderType.Ascending);
                LinkEntity accessoryLink = query.AddLink("tbs_accessory", "tbs_accessory", "tbs_accessoryid");
                accessoryLink.EntityAlias = "acc";
                accessoryLink.Columns = new ColumnSet("tbs_itemcategory");
                accessories = service.RetrieveMultiple(query);
                tracingService.Trace(accessories.Entities.Count.ToString());
            }
            catch (Exception e)
            {
                throw e;
            }
            return accessories;
        }
        private int? CalculateQuantity(AccessoryConfiguration config, CalculationContext context)
        {
            try
            {
                string rule = config.RuleClass;

                if (string.IsNullOrEmpty(rule))
                {
                    return null;
                }
                tracingService.Trace(rule);

                decimal div1 = config.Mult1;
                decimal div2 = config.Mult2;

                switch (rule)
                {
                    case "Count / Div1":
                        return (int)Math.Ceiling(
                            GetDependencyValue(context, config.DepTbl1, config.DepCat1) / div1);

                    case "SF / Div1":
                        return (int)Math.Ceiling(context.SqFt / div1);

                    case "LFT / Div1":
                        int? LFT = GetLineerFeetTrim(context.OrderProdId);
                        if (LFT.HasValue)
                        {
                            return (int)Math.Ceiling(LFT.Value / div1);
                        }
                        return null;

                    case "(SF / Div1) + ((CountA + CountB) / Div2)":
                        return (int)Math.Ceiling(
                            (context.SqFt / config.Mult1) + (GetDependencyValue(context, config.DepTbl1, config.DepCat1) +
                                GetDependencyValue(context, config.DepTbl2, config.DepCat2) / config.Mult2));

                    case "SF / Div1 / Div2":
                        return (int)Math.Ceiling(context.SqFt / div1 / div2);

                    case "LFP / Div1 / Div2":
                        return (int)Math.Ceiling(context.LinearFeet / div1 / div2);

                    case "(Count * Mult1) / Div1":
                        return (int)Math.Ceiling(
                            (GetDependencyValue(context, config.DepTbl1, config.DepCat1) * div1) / div2);

                    case "(CountA + CountB) / Div1":
                        return (int)Math.Ceiling(
                            (
                                GetDependencyValue(context, config.DepTbl1, config.DepCat1) +
                                GetDependencyValue(context, config.DepTbl2, config.DepCat2)
                            ) / div1);

                    case "(LFT (Perim) * Mult1) / Div1":
                        int? LFTP = GetLineerFeetPerim(context.OrderProdId);
                        if (LFTP.HasValue)
                        {
                            return (int)Math.Ceiling((LFTP.Value * div1) / div2);
                        }
                        return null;

                    case "(2CountA + 2CountB + 4CountC) / Div1 / Div2":
                        return (int)Math.Ceiling(
                            (
                                (2 * GetDependencyValue(context, config.DepTbl1, config.DepCat1)) +
                                (2 * GetDependencyValue(context, config.DepTbl2, config.DepCat2)) +
                                (4 * GetDependencyValue(context, config.DepTbl3, config.DepCat3))
                            ) / div1 / div2);

                    case "Count / Div1 / Div2":
                        return (int)Math.Ceiling(
                            GetDependencyValue(context, config.DepTbl1, config.DepCat1) / div1 / div2);

                    case "(CountA + CountB + 2CountC) / Div1 / Div2":
                        return (int)Math.Ceiling(
                            (
                                GetDependencyValue(context, config.DepTbl1, config.DepCat1) +
                                GetDependencyValue(context, config.DepTbl2, config.DepCat2) +
                                (2 * GetDependencyValue(context, config.DepTbl3, config.DepCat3))
                            ) / div1 / div2);

                    case "Count":
                        return (int)GetDependencyValue(
                            context,
                            config.DepTbl1,
                            config.DepCat1);

                    case "(2CountA + CountB) / Div1 / Div2":
                        return (int)Math.Ceiling(
                            (
                                (2 * GetDependencyValue(context, config.DepTbl1, config.DepCat1)) +
                                GetDependencyValue(context, config.DepTbl2, config.DepCat2)
                            ) / div1 / div2);

                    case "RDEK (cntPurlin * cntPanel) / Div1":
                        return null;

                    case "RDEK Perimeter / Div1":
                        return null;

                    case "RDEK (cntPurlin * lenPurlin) / Div1":
                        return null;

                    case "Count * Mult1":
                        return (int)Math.Ceiling(
                            GetDependencyValue(context, config.DepTbl1, config.DepCat1) * div1);

                    case "RDEK (cntJoint * lenJoint * Mult1) / Div1":
                        return null;

                    case "RDEK ((cntJoint * lenJoint) + LFP) / Div1 / Div2":
                        return null;

                    default:
                        return null;
                }
            }
            catch (Exception e)
            {
                throw e;
            }
        }
        private int? GetLineerFeetTrim(EntityReference OrderProd)
        {
            if (OrderProd != null)
            {
                try
                {
                    string fetchXml = $@"
                    <fetch aggregate='true'>
                      <entity name='tbs_orderpaneltrim'>
                        <attribute name='tbs_quantity' alias='quantity' aggregate='sum' />
                        <filter>
                          <condition attribute='tbs_orderproduct' operator='eq' value='{OrderProd.Id}' />
                        </filter>
                        <link-entity name='tbs_trim' from='tbs_trimid' to='tbs_trim'>
                          <link-entity name='tbs_itemcategory' from='tbs_itemcategoryid' to='tbs_itemcategory'>
                            <filter>
                              <condition attribute='tbs_categoryname' operator='not-in'>
                                <value>Box</value>
                                <value>Skid</value>
                                <value>Flat Stock</value>
                              </condition>
                            </filter>
                          </link-entity>
                        </link-entity>
                      </entity>
                    </fetch>";

                    Entity LFT = service.RetrieveMultiple(new FetchExpression(fetchXml)).Entities.FirstOrDefault();
                    if (LFT != null)
                    {
                        int qty = (int)LFT.GetAttributeValue<AliasedValue>("quantity").Value;
                        return qty;
                    }
                }
                catch (Exception e)
                {
                    throw e;
                }
            }
            return null;
        }
        private int? GetLineerFeetPerim(EntityReference OrderProd)
        {
            if (OrderProd != null)
            {
                try
                {
                    string fetchXml = $@"
                    <fetch aggregate='true'>
                      <entity name='tbs_orderpaneltrim'>
                        <attribute name='tbs_quantity' alias='quantity' aggregate='sum' />
                        <filter>
                          <condition attribute='tbs_orderproduct' operator='eq' value='{OrderProd.Id}' />
                        </filter>
                        <link-entity name='tbs_trim' from='tbs_trimid' to='tbs_trim'>
                          <link-entity name='tbs_itemcategory' from='tbs_itemcategoryid' to='tbs_itemcategory'>
                            <filter>
                                <condition attribute='tbs_categoryname' operator='in'>
                                  <value>High Eave</value>
                                  <value>Rake Zee</value>
                                  <value>Low Eave</value>
                                  <value>Rake</value>
                                  <value>Rake Transition</value>
                                  <value>Ridge Closure</value>
                                  <value>Ext Ridge</value>
                                  <value>Int Ridge</value>
                                  <value>Transition</value>
                                  <value>Valley</value>
                                  <value>Counter</value>
                                </condition>
                            </filter>
                          </link-entity>
                        </link-entity>
                      </entity>
                    </fetch>";

                    Entity LFTP = service.RetrieveMultiple(new FetchExpression(fetchXml)).Entities.FirstOrDefault();
                    if (LFTP != null)
                    {
                        int qty = (int)LFTP.GetAttributeValue<AliasedValue>("quantity").Value;
                        return qty;
                    }
                }
                catch (Exception e)
                {
                    throw e;
                }
            }
            return null;
        }
        private decimal GetDependencyValue(CalculationContext context, OptionSetValue table, EntityReference category)
        {
            return context.Values[BuildKey(table, category)];
        }
        private void UpdateAccessoryQuantity(Entity orderPanelAcc, int qty)
        {
            try
            {
                Entity update =
                        new Entity("tbs_orderpanelaccessory");

                update.Id = orderPanelAcc.Id;

                update["tbs_quantity"] = qty;

                service.Update(update);
            }
            catch (Exception e)
            {
                throw e;
            }
        }
        private void CalculateAllAccessories(EntityCollection accessories, CalculationContext context)
        {
            try
            {
                Dictionary<Guid, AccessoryConfiguration> configs = new Dictionary<Guid, AccessoryConfiguration>();

                foreach (Entity accessory in accessories.Entities)
                {
                    AccessoryConfiguration config = BuildAccessoryConfiguration(accessory);

                    if (config != null)
                    {
                        config.Accessory = accessory;      // new property
                    }
                    configs.Add(accessory.Id, config);
                }

                foreach (var config in configs.Values)
                {
                    if (config != null)
                    {
                        tracingService.Trace(config.Accessory.ToString());
                        CalculateAccessory(config, configs, context);
                    }
                }
            }
            catch (Exception e)
            {
                throw e;
            }
        }
        private void CalculateAccessory(AccessoryConfiguration config, Dictionary<Guid, AccessoryConfiguration> configs, CalculationContext context)
        {
            try
            {
                if (config.Calculated)
                {
                    return;
                }
                ResolveDependency(config.DepTbl1, config.DepCat1, configs, context);

                ResolveDependency(config.DepTbl2, config.DepCat2, configs, context);

                ResolveDependency(config.DepTbl3, config.DepCat3, configs, context);

                bool useCustomQty = config.Accessory.GetAttributeValue<bool>("tbs_usecustomquantity");

                int? qty;

                if (useCustomQty)
                {
                    qty = config.Accessory.GetAttributeValue<int?>("tbs_quantity") ?? 0;
                }
                else
                {
                    qty = CalculateQuantity(config, context);
                }

                tracingService.Trace($"Accessory: {config.ItemCategory.Id}");

                tracingService.Trace($"Rule: {config.RuleClass}");

                tracingService.Trace($"Calculated Qty: {(qty.HasValue ? qty.Value.ToString() : "NULL")}");

                if (qty.HasValue)
                {
                    tracingService.Trace("Inside qty.HasValue");
                    context.Values[BuildKey(config.CurrentTable, config.ItemCategory)] = qty.Value;

                    config.Calculated = true;
                    tracingService.Trace(config.Accessory.ToString());

                    int currentQty = config.Accessory.GetAttributeValue<int?>("tbs_quantity") ?? 0;

                    tracingService.Trace($"Current Qty : {currentQty}");
                    tracingService.Trace($"Calculated Qty : {qty.Value}");

                    if (currentQty != qty.Value)
                    {
                        tracingService.Trace("Updating Accessory Quantity");

                        UpdateAccessoryQuantity(config.Accessory, qty.Value);
                    }
                }
            }
            catch (Exception e)
            {
                throw e;
            }
        }
        private void ResolveDependency(OptionSetValue table, EntityReference category, Dictionary<Guid, AccessoryConfiguration> configs, CalculationContext context)
        {
            try
            {
                if (table == null || category == null)
                {
                    return;
                }
                string key = BuildKey(table, category);

                if (context.Values.ContainsKey(key))
                {
                    return;
                }

                var dependency = FindConfiguration(configs, table, category);

                if (dependency == null)
                {
                    throw new InvalidPluginExecutionException($"Dependency not found : {key}");
                }
                CalculateAccessory(dependency, configs, context);
            }
            catch (Exception e)
            {
                throw e;
            }
        }
        private AccessoryConfiguration FindConfiguration(Dictionary<Guid, AccessoryConfiguration> configs, OptionSetValue table, EntityReference category)
        {
            return configs.Values.Where(x => x != null).ToList().FirstOrDefault(x => x.CurrentTable.Value == table.Value && x.ItemCategory != null && x.ItemCategory.Id == category.Id);
        }
        private CalculationContext BuildCalculationContext(Entity orderdetail)
        {
            return new CalculationContext
            {
                OrderProdId = new EntityReference("salesorderdetail", orderdetail.Id),

                SqFt = orderdetail.GetAttributeValue<decimal>("quantity"),

                LinearFeet = orderdetail.GetAttributeValue<decimal>("tbs_linearfeet")
            };
        }
        private AccessoryConfiguration BuildAccessoryConfiguration(Entity accessory)
        {
            try
            {
                AccessoryConfiguration accConfig = new AccessoryConfiguration();

                EntityReference panelType = accessory.GetAttributeValue<EntityReference>("tbs_paneltype");
                EntityReference itemCategory = accessory.Contains("acc.tbs_itemcategory") ? accessory.GetAttributeValue<AliasedValue>("acc.tbs_itemcategory").Value as EntityReference : null;
                if (itemCategory == null)
                {
                    return null;
                }
                if (itemCategory != null)
                {
                    accConfig.Accessory = accessory;
                    accConfig.ItemCategory = itemCategory;
                    accConfig.CurrentTable = new OptionSetValue(1);

                    QueryExpression query = new QueryExpression("tbs_accessoryrules");
                    query.ColumnSet.AddColumns("tbs_category", "tbs_dependenttablecategory1", "tbs_dependenttablecategory2", "tbs_dependenttablecategory3", "tbs_dependenttabletype1", "tbs_dependenttabletype2", "tbs_dependenttabletype3", "tbs_multiplier1", "tbs_multiplier2", "tbs_panel", "tbs_ruleclass");
                    query.Criteria.AddCondition("tbs_panel", ConditionOperator.Equal, panelType.Id);
                    query.Criteria.AddCondition("tbs_category", ConditionOperator.Equal, itemCategory.Id);
                    EntityCollection rules = service.RetrieveMultiple(query);

                    if (rules.Entities.Count > 1)
                    {
                        tracingService.Trace("Multiple Rules Found - Confused");
                    }
                    else if (rules.Entities.Count > 0)
                    {
                        Entity rule = rules.Entities.FirstOrDefault();
                        accConfig.RuleClass = rule.GetAttributeValue<string>("tbs_ruleclass");
                        accConfig.Mult1 = rule.GetAttributeValue<decimal>("tbs_multiplier1");
                        accConfig.Mult2 = rule.GetAttributeValue<decimal>("tbs_multiplier2");
                        accConfig.DepTbl1 = rule.GetAttributeValue<OptionSetValue>("tbs_dependenttabletype1");
                        accConfig.DepCat1 = rule.GetAttributeValue<EntityReference>("tbs_dependenttablecategory1");
                        accConfig.DepTbl2 = rule.GetAttributeValue<OptionSetValue>("tbs_dependenttabletype2");
                        accConfig.DepCat2 = rule.GetAttributeValue<EntityReference>("tbs_dependenttablecategory2");
                        accConfig.DepTbl3 = rule.GetAttributeValue<OptionSetValue>("tbs_dependenttabletype3");
                        accConfig.DepCat3 = rule.GetAttributeValue<EntityReference>("tbs_dependenttablecategory3");
                    }
                }
                return accConfig;
            }
            catch (Exception e)
            {
                throw e;
            }
        }
        private string BuildKey(OptionSetValue table, EntityReference category)
        {
            if (table == null || category == null)
                return string.Empty;

            return $"{table.Value}|{category.Id}";
        }

        #region Classes
        private class CalculationContext
        {
            public EntityReference OrderProdId { get; set; }
            public decimal SqFt { get; set; }

            public decimal LinearFeet { get; set; }

            public string PanelFamily { get; set; }

            public Dictionary<string, decimal> Values
                = new Dictionary<string, decimal>();
        }
        public class AccessoryConfiguration
        {
            public Entity Accessory { get; set; }
            public bool Calculated { get; set; }
            public OptionSetValue CurrentTable { get; set; }
            public string RuleClass { get; set; }
            public decimal Mult1 { get; set; }
            public decimal Mult2 { get; set; }
            public OptionSetValue DepTbl1 { get; set; }
            public EntityReference DepCat1 { get; set; }
            public OptionSetValue DepTbl2 { get; set; }
            public EntityReference DepCat2 { get; set; }
            public OptionSetValue DepTbl3 { get; set; }
            public EntityReference DepCat3 { get; set; }
            public EntityReference ItemCategory { get; set; }
        }
        #endregion

        private Guid GetInputGuid(string parameterName)
        {
            if (!context.InputParameters.Contains(parameterName))
                throw new InvalidPluginExecutionException(
                    $"Input parameter '{parameterName}' is missing.");

            if (!(context.InputParameters[parameterName] is Guid id))
                throw new InvalidPluginExecutionException(
                    $"Input parameter '{parameterName}' is not a Guid.");

            return id;
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

