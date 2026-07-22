using System;
using System.Activities;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Workflow;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using System.Linq;
using System.Collections.Generic;

namespace Falk_Code_Activity
{
    public abstract class CodeActivityBase : CodeActivity
    {
        protected CodeActivityContext _codeActivityContext; // Store context for reuse
        protected IOrganizationService _service; // Store context for reuse

        protected class LocalWorkflowContext
        {
            internal CodeActivityContext CodeActivityContext { get; private set; }
            internal IWorkflowContext WorkflowContext { get; private set; }
            internal IOrganizationService OrganizationService { get; private set; }
            internal ITracingService TracingService { get; private set; }

            internal LocalWorkflowContext(CodeActivityContext context)
            {
                CodeActivityContext = context ?? throw new ArgumentNullException(nameof(context));
                WorkflowContext = context.GetExtension<IWorkflowContext>() ?? throw new InvalidPluginExecutionException("Failed to retrieve WorkflowContext.");
                TracingService = context.GetExtension<ITracingService>() ?? throw new InvalidPluginExecutionException("Failed to retrieve TracingService.");

                IOrganizationServiceFactory serviceFactory = context.GetExtension<IOrganizationServiceFactory>();
                OrganizationService = serviceFactory.CreateOrganizationService(WorkflowContext.UserId);
            }

            internal void Trace(string message)
            {
                if (!string.IsNullOrWhiteSpace(message))
                {
                    TracingService.Trace($"{message} | Correlation Id: {WorkflowContext.CorrelationId}, Initiating User: {WorkflowContext.InitiatingUserId}");
                }
            }
        }

        protected abstract void ExecuteWorkflowLogic(LocalWorkflowContext localContext);

        protected override void Execute(CodeActivityContext context)
        {
            _codeActivityContext = context; // Set protected context field

            LocalWorkflowContext localContext = new LocalWorkflowContext(context);
            localContext.Trace($"Entered {GetType().Name}.Execute()");

            _service = localContext.OrganizationService;

            try
            {
                ExecuteWorkflowLogic(localContext);
            }
            catch (Exception ex)
            {
                localContext.Trace(GetFormattedExceptionTraceString(ex));
                throw;
            }

            localContext.Trace($"Exiting {GetType().Name}.Execute()");
        }

        #region Parameter Methods
        protected T GetInputValue<T>(InArgument<T> argument) => argument.Get(_codeActivityContext);

        protected void SetOutputValue<T>(OutArgument<T> argument, T value) => argument?.Set(_codeActivityContext, value);
        #endregion

        #region Utility Method
        protected static string GetFormattedExceptionTraceString(Exception ex)
        {
            return $"Exception: {ex.GetType().FullName}, Message: {ex.Message}, StackTrace: {ex}";
        }

        public string GetStringAttributeValue(Entity entity, string attributeName)
        {
            var attributeValue = string.Empty;
            if (entity.Contains(attributeName))
                attributeValue = entity[attributeName] as string;
            return attributeValue;
        }

        public static int GetIntAttributeValue(Entity entity, string attributeName)
        {
            var attributeValue = 0;
            if (entity.Contains(attributeName) && entity[attributeName] is int)
                attributeValue = (int)entity[attributeName];
            return attributeValue;
        }

        public double GetDoubleAttributeValue(Entity entity, string attributeName)
        {
            var attributeValue = 0.00;
            if (entity.Contains(attributeName) && entity[attributeName] is double)
                attributeValue = (double)entity[attributeName];
            return attributeValue;
        }

        public static decimal GetDecimalAttributeValue(Entity entity, string attributeName)
        {
            decimal attributeValue = 0;
            if (entity.Contains(attributeName) && entity[attributeName] is decimal)
                attributeValue = (decimal)entity[attributeName];
            return attributeValue;
        }

        public float GetFloatAttributeValue(Entity entity, string attributeName)
        {
            float attributeValue = 0;
            //if (entity.Contains(attributeName) && entity[attributeName] is float)
            //    attributeValue = (float)entity[attributeName];
            if (entity.Contains(attributeName))
                attributeValue = Convert.ToInt32(entity[attributeName]);
            return attributeValue;
        }

        public static decimal GetMoneyAttributeValue(Entity entity, string attributeName)
        {
            decimal attributeValue = 0.0m;
            if (entity.Contains(attributeName) && entity[attributeName] is Money)
                return ((Money)entity[attributeName]).Value;
            return attributeValue;
        }

        public DateTime GetDateTimeAttributeValue(Entity entity, string attributeName)
        {
            var attributeValue = new DateTime();
            if (entity.Contains(attributeName))
                attributeValue = entity[attributeName] is DateTime ? (DateTime)entity[attributeName] : new DateTime();
            return attributeValue;
        }

        public bool GetBoolAttributeValue(Entity entity, string attributeName)
        {
            var attributeValue = false;
            if (entity.Contains(attributeName) && (entity[attributeName] is bool && (bool)entity[attributeName]))
                attributeValue = (bool)entity[attributeName];
            return attributeValue;
        }

        public static EntityReference GetLookupAttributeValue(Entity entity, string attributeName)
        {
            if (entity == null) return null;
            EntityReference attributeValue = null;
            if (entity.Contains(attributeName) && (entity[attributeName] is EntityReference))
                attributeValue = (EntityReference)entity[attributeName];
            return attributeValue;
        }

        public static object GetAliasedAttributeValue(Entity entity, string attributeName)
        {
            if (entity == null) return null;
            object attributeValue = null;
            if (entity.Contains(attributeName) && (entity[attributeName] is AliasedValue))
                attributeValue = ((AliasedValue)entity[attributeName]).Value;
            return attributeValue;
        }

        public Guid GetPrimaryKeyAttributeValue(Entity entity)
        {
            return entity != null ? entity.Id : new Guid();
        }

        public int? GetOptionSetAttributeValue(Entity entity, string attributeName)
        {
            int? attributeValue = null;
            if (!entity.Contains(attributeName) || (!(entity[attributeName] is OptionSetValue))) return attributeValue;
            var optionSetValue = entity[attributeName] as OptionSetValue;
            attributeValue = optionSetValue.Value;
            return attributeValue;
        }

        public ExecuteMultipleRequest GetExeculteMultipleRequest()
        {
            var multipleExecuteRequest = new ExecuteMultipleRequest
            {
                Settings = new ExecuteMultipleSettings
                {
                    ContinueOnError = true,
                    ReturnResponses = true
                },
                Requests = new OrganizationRequestCollection()
            };

            return multipleExecuteRequest;
        }
        #endregion

        #region General Methods
        protected string GetEnvironmentVariable(string schemaName)
        {
            QueryExpression query = new QueryExpression("environmentvariabledefinition")
            {
                ColumnSet = new ColumnSet("defaultvalue"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("schemaname", ConditionOperator.Equal, schemaName)
                    }
                },
                LinkEntities =
                {
                    new LinkEntity
                    {
                        LinkFromEntityName = "environmentvariabledefinition",
                        LinkFromAttributeName = "environmentvariabledefinitionid",
                        LinkToEntityName = "environmentvariablevalue",
                        LinkToAttributeName = "environmentvariabledefinitionid",
                        Columns = new ColumnSet("value"),
                        EntityAlias = "val",
                        JoinOperator = JoinOperator.LeftOuter
                    }
                }
            };

            Entity result = _service.RetrieveMultiple(query).Entities.FirstOrDefault();

            if (result != null)
            {
                return result.Contains("val.value")
                    ? (string)((AliasedValue)result["val.value"]).Value
                    : result.GetAttributeValue<string>("defaultvalue");
            }

            return null;
        }

        public Tuple<string, string> GetPrimaryAttributes(string entityLogicalName)
        {
            RetrieveEntityRequest request = new RetrieveEntityRequest
            {
                EntityFilters = EntityFilters.Entity,
                LogicalName = entityLogicalName
            };

            RetrieveEntityResponse response = (RetrieveEntityResponse)_service.Execute(request);

            EntityMetadata metadata = response.EntityMetadata;

            string PrimaryIdAttribute = metadata.PrimaryIdAttribute;
            string PrimaryNameAttribute = metadata.PrimaryNameAttribute;

            Tuple<string, string> PrimaryAttributes = new Tuple<string, string>(PrimaryIdAttribute, PrimaryNameAttribute);

            return PrimaryAttributes;
        }

        public Entity CloneWithoutSystemFields(Entity entity)
        {
            Entity clone = new Entity(entity.LogicalName);
            Tuple<string, string> PrimaryAttributes = GetPrimaryAttributes(entity.LogicalName);

            foreach (KeyValuePair<string, object> attr in entity.Attributes)
            {
                if (attr.Key != PrimaryAttributes.Item1 &&
                    attr.Key != "createdon" &&
                    attr.Key != "modifiedon" &&
                    attr.Key != "createdby" &&
                    attr.Key != "modifiedby" &&
                    attr.Key != "ownerid" &&
                    attr.Key != "owningbusinessunit" &&
                    attr.Key != "statecode" &&
                    attr.Key != "statuscode")
                {
                    clone[attr.Key] = attr.Value;
                }
            }
            return clone;
        }
        #endregion
    }
}
