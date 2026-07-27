using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Falk.CustomAPI
{
    public class CalculateQuoteTrimQty : CustomAPIBase
    {
        public CalculateQuoteTrimQty() : base(typeof(CalculateQuoteTrimQty)) { }

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
                Guid quoteProductId = GetInputGuid("tbs_quoteProduct");
                Console.WriteLine("Quote ProductId = " + quoteProductId.ToString());

                Entity quoteProduct = GetQuoteProduct(quoteProductId);

                EntityCollection trims = GetTrims(quoteProductId);

                CalculationContext calccontext = BuildCalculationContext(quoteProduct);

                CalculateAllTrims(trims, calccontext);

            }
            catch (Exception ex)
            {
                tracingService.Trace("CalculateTrimyQty Custom API Exception: {0}", ex.ToString());
                throw new InvalidPluginExecutionException($"Error in CalculateTrimQty Custom API: {ex.Message}");
            }
        }

        private Entity GetQuoteProduct(Guid quoteProductId)
        {
            return service.Retrieve("quotedetail", quoteProductId, new ColumnSet("quantity", "tbs_linearfeet"));
        }
        private EntityCollection GetTrims(Guid quoteProductId)
        {
            EntityCollection trims = new EntityCollection();

            try
            {
                QueryExpression query = new QueryExpression("tbs_quotepaneltrim");
                query.ColumnSet = new ColumnSet("tbs_quantity", "tbs_trim", "tbs_category", "tbs_unitprice", "tbs_paneltype");
                query.Criteria.AddCondition("tbs_quoteproduct", ConditionOperator.Equal, quoteProductId);
                query.AddOrder("tbs_trim", OrderType.Ascending);
                LinkEntity trimLink = query.AddLink("tbs_trim", "tbs_trim", "tbs_trimid");
                trimLink.EntityAlias = "acc";
                trimLink.Columns = new ColumnSet("tbs_itemcategory");
                trims = service.RetrieveMultiple(query);
                tracingService.Trace(trims.Entities.Count.ToString());
            }
            catch (Exception e)
            {
                throw e;
            }
            return trims;
        }
        private int? CalculateQuantity(TrimConfiguration config, CalculationContext context)
        {
            try
            {
                string rule = config.RuleClass;

                if (string.IsNullOrEmpty(rule))
                {
                    tracingService.Trace("No Rule Identified - Need to fill quantity");

                    if (config.Trim.Contains("tbs_quantity"))
                    {
                        tracingService.Trace("Manual Quantity = " + config.Trim.GetAttributeValue<int>("tbs_quantity"));
                        return config.Trim.GetAttributeValue<int>("tbs_quantity");
                    }



                    return null;
                }
                tracingService.Trace(rule);

                decimal div1 = config.Mult1;
                decimal div2 = config.Mult2;

                switch (rule)
                {
                    case "CountA + CountB + CountC":

                        decimal? val1 = GetDependencyValue(context, config.DepTbl1, config.DepCat1);
                        decimal? val2 = GetDependencyValue(context, config.DepTbl2, config.DepCat2);
                        decimal? val3 = GetDependencyValue(context, config.DepTbl3, config.DepCat3);

                        if (!val1.HasValue && !val2.HasValue && !val3.HasValue)
                            return null;

                        return (int)Math.Ceiling((val1 ?? 0m) + (val2 ?? 0m) + (val3 ?? 0m));

                    case "Count * Mult1":

                        decimal? count = GetDependencyValue(context, config.DepTbl1, config.DepCat1);

                        if (!count.HasValue)
                            return null;

                        return (int)Math.Ceiling(count.Value * div1);

                    case "Count / Div1":
                        decimal? count1 = GetDependencyValue(context, config.DepTbl1, config.DepCat1);

                        if (!count1.HasValue)
                            return null;

                        return (int)Math.Ceiling((double)(count1.Value / div1));

                    case "LFT / Div1":
                        int? LFT = GetLineerFeetTrim(context.QuoteProdId);
                        if (LFT.HasValue)
                        {
                            return (int)Math.Ceiling(LFT.Value / div1);
                        }
                        return null;

                    case "Count":
                        decimal? count2 = GetDependencyValue(context, config.DepTbl1, config.DepCat1);

                        return count2.HasValue ? (int?)count2.Value : null;

                    case "RDEK Perimeter":
                        return null;

                    case "RDEK cntJoint * lenJoint":
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
        private int? GetLineerFeetTrim(EntityReference quoteProd)
        {
            if (quoteProd != null)
            {
                try
                {
                    string fetchXml = $@"
                    <fetch aggregate='true'>
                      <entity name='tbs_quotepaneltrim'>
                        <attribute name='tbs_quantity' alias='quantity' aggregate='sum' />
                        <filter>
                          <condition attribute='tbs_quoteproduct' operator='eq' value='{quoteProd.Id}' />
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

        private decimal? GetDependencyValue(CalculationContext context, OptionSetValue table, EntityReference category)
        {
            if (context.Values.TryGetValue(BuildKey(table, category), out decimal value))
            {
                return value;
            }

            return null;

        }
        private void UpdateTrimQuantity(Guid id, int qty)
        {
            try
            {
                tracingService.Trace("Quantity to update - " + qty);
                Entity update =
                        new Entity("tbs_quotepaneltrim");

                update.Id = id;

                update["tbs_quantity"] = qty;

                service.Update(update);
            }
            catch (Exception e)
            {
                throw e;
            }
        }
        private void CalculateAllTrims(EntityCollection trims, CalculationContext context)
        {
            try
            {
                Dictionary<Guid, TrimConfiguration> configs = new Dictionary<Guid, TrimConfiguration>();

                foreach (Entity trim in trims.Entities)
                {
                    TrimConfiguration config = BuildTrimConfiguration(trim);

                    if (config != null)
                    {
                        config.Trim = trim;      // new property
                    }
                    configs.Add(trim.Id, config);
                }

                foreach (var config in configs.Values)
                {
                    if (config != null)
                    {
                        tracingService.Trace("panel trim = " + config.QuoteTrimId);
                        tracingService.Trace("Category = " + config.ItemCategory.Id);
                        CalculateTrim(config, configs, context);
                    }
                    tracingService.Trace(" \n \n \n \n");
                }
            }
            catch (Exception e)
            {
                throw e;
            }
        }
        private void CalculateTrim(TrimConfiguration config, Dictionary<Guid, TrimConfiguration> configs, CalculationContext context)
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

                int? qty = CalculateQuantity(config, context);

                if (qty.HasValue)
                {
                    context.Values[BuildKey(config.CurrentTable, config.ItemCategory)] = qty.Value;
                    tracingService.Trace("context updated" + context);

                    config.Calculated = true;
                    tracingService.Trace(config.QuoteTrimId.ToString());

                    UpdateTrimQuantity(config.QuoteTrimId, qty.Value);
                }
                else
                {
                    tracingService.Trace("Quantity null");
                }
            }
            catch (Exception e)
            {
                throw e;
            }
        }
        private void ResolveDependency(OptionSetValue table, EntityReference category, Dictionary<Guid, TrimConfiguration> configs, CalculationContext context)
        {
            try
            {
                if (table == null || category == null)
                {
                    return;
                }
                tracingService.Trace("Dependent Category = " + category.Id);
                string key = BuildKey(table, category);

                if (context.Values.ContainsKey(key))
                {
                    return;     // already calculated
                }

                var dependency = FindConfiguration(configs, table, category);

                if (dependency == null)
                    throw new InvalidPluginExecutionException($"Dependency not found : {key}");

                CalculateTrim(dependency, configs, context);
            }
            catch (Exception e)
            {
                throw e;
            }
        }
        private TrimConfiguration FindConfiguration(Dictionary<Guid, TrimConfiguration> configs, OptionSetValue table, EntityReference category)
        {
            return configs.Values.Where(x => x != null).ToList().FirstOrDefault(x => x.CurrentTable.Value == table.Value && x.ItemCategory != null && x.ItemCategory.Id == category.Id);
        }
        private CalculationContext BuildCalculationContext(Entity quoteProduct)
        {
            return new CalculationContext
            {
                SqFt = quoteProduct.GetAttributeValue<decimal>("quantity"),

                LinearFeet = quoteProduct.GetAttributeValue<decimal>("tbs_linearfeet"),

                QuoteProdId = quoteProduct.ToEntityReference()
            };
        }
        private TrimConfiguration BuildTrimConfiguration(Entity trim)
        {
            try
            {
                TrimConfiguration accConfig = new TrimConfiguration();

                EntityReference panelType = trim.GetAttributeValue<EntityReference>("tbs_paneltype");
                EntityReference itemCategory = trim.Contains("acc.tbs_itemcategory") ? trim.GetAttributeValue<AliasedValue>("acc.tbs_itemcategory").Value as EntityReference : null;
                if (itemCategory == null)
                {
                    return null;
                }
                if (itemCategory != null)
                {
                    accConfig.QuoteTrimId = trim.Id;
                    accConfig.ItemCategory = itemCategory;
                    accConfig.CurrentTable = new OptionSetValue(2);

                    QueryExpression query = new QueryExpression("tbs_trimrules");
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

        #region Classes
        private class CalculationContext
        {
            public EntityReference QuoteProdId { get; set; }
            public decimal SqFt { get; set; }

            public decimal LinearFeet { get; set; }

            public string PanelFamily { get; set; }

            public Dictionary<string, decimal> Values
                = new Dictionary<string, decimal>();
        }
        public class TrimConfiguration
        {
            public Entity Trim { get; set; }
            public bool Calculated { get; set; }
            public OptionSetValue CurrentTable { get; set; }
            public Guid QuoteTrimId { get; set; }
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

    }
}
