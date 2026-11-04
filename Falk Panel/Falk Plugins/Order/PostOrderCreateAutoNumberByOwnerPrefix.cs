using System;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace Falk_Plugins.Order
{
    public class PostOrderCreateAutoNumberByOwnerPrefix : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            // Initialize tracing service
            ITracingService tracingService =(ITracingService)serviceProvider.GetService(typeof(ITracingService));

            tracingService.Trace(
                "GenerateOrderNumber plugin execution started.");

            try
            {
                // Get plugin execution context
                IPluginExecutionContext context =(IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));

                tracingService.Trace(
                    "Message Name: {0}", context.MessageName);

                tracingService.Trace(
                    "Primary Entity: {0}", context.PrimaryEntityName);

                tracingService.Trace(
                    "Execution Stage: {0}", context.Stage);

                tracingService.Trace(
                    "User ID: {0}", context.UserId);

                tracingService.Trace(
                    "Initiating User ID: {0}", context.InitiatingUserId);

                // Initialize organization service
                IOrganizationServiceFactory factory =(IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));

                IOrganizationService service =factory.CreateOrganizationService(context.UserId);

                tracingService.Trace(
                    "Organization service initialized.");

                // Run only on Order Create
                if (context.MessageName != "Create" ||
                    context.PrimaryEntityName != "salesorder")
                {
                    tracingService.Trace(
                        "Plugin stopped because the message or entity did not match.");

                    return;
                }

                tracingService.Trace(
                    "Order Create message verified.");

                // Get Order ID
                Guid orderId = context.PrimaryEntityId;

                tracingService.Trace(
                    "Order ID: {0}", orderId);

                // Get initiating user's ID
                Guid userId = context.InitiatingUserId;

                tracingService.Trace(
                    "Retrieving user prefix for user: {0}", userId);

                // Retrieve User Prefix
                Entity user = service.Retrieve(
                    "systemuser",
                    userId,
                    new ColumnSet("tbs_prefix"));

                string prefix =
                    user.GetAttributeValue<string>("tbs_prefix");

                tracingService.Trace(
                    "Retrieved prefix: {0}",
                    string.IsNullOrWhiteSpace(prefix)
                        ? "<empty>"
                        : prefix);

                if (string.IsNullOrWhiteSpace(prefix))
                {
                    throw new InvalidPluginExecutionException(
                        "Prefix is not configured for the logged-in user.");
                }

                // Retrieve Order Auto Number
                tracingService.Trace(
                    "Retrieving Order Auto Number.");

                Entity order = service.Retrieve(
                    "salesorder",
                    orderId,
                    new ColumnSet("tbs_ordernumber"));

                string autoNumber =
                    order.GetAttributeValue<string>("tbs_ordernumber");

                tracingService.Trace(
                    "Retrieved Auto Number: {0}",
                    string.IsNullOrWhiteSpace(autoNumber)
                        ? "<empty>"
                        : autoNumber);

                if (string.IsNullOrWhiteSpace(autoNumber))
                {
                    throw new InvalidPluginExecutionException(
                        "Auto Number was not generated.");
                }

                // Generate Final Order Number
                string finalOrderNumber = $"{prefix}-{autoNumber}";

                tracingService.Trace(
                    "Final Order Number: {0}", finalOrderNumber);       
                // Update Order
                Entity updateOrder = new Entity("salesorder");
                updateOrder.Id = orderId;
                updateOrder["tbs_ordernumber"] = finalOrderNumber;

                tracingService.Trace(
                    "Updating Order Number.");

                service.Update(updateOrder);

                tracingService.Trace(
                    "Order Number update completed successfully.");
            }
            catch (Exception ex)
            {
                tracingService.Trace(
                    "ERROR in GenerateOrderNumber: {0}", ex.ToString());

                throw new InvalidPluginExecutionException(
                    "Error generating Order Number: " + ex.Message, ex);
            }
            finally
            {
                tracingService.Trace(
                    "GenerateOrderNumber plugin execution finished.");
            }
        }
    }
}