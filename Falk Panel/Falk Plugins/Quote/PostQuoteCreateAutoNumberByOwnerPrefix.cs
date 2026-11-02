using System;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace Falk_Plugins.Quote
{
    public class PostQuoteCreateAutoNumberByOwnerPrefix : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            // Initialize tracing service
            ITracingService tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            tracingService.Trace("GenerateQuoteNumber plugin execution started.");

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
                IOrganizationServiceFactory factory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));

                IOrganizationService service = factory.CreateOrganizationService(context.UserId);

                tracingService.Trace("Organization service initialized.");

                // Run only on Quote Create
                if (context.MessageName != "Create" ||
                    context.PrimaryEntityName != "quote")
                {
                    tracingService.Trace(
                        "Plugin stopped because the message or entity did not match.");

                    return;
                }

                tracingService.Trace(
                    "Quote Create message verified.");

                // Get Quote ID
                Guid quoteId = context.PrimaryEntityId;

                tracingService.Trace(
                    "Quote ID: {0}", quoteId);

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

                // Retrieve Quote Auto Number
                tracingService.Trace(
                    "Retrieving Quote Auto Number.");

                Entity quote = service.Retrieve(
                    "quote",
                    quoteId,
                    new ColumnSet("tbs_quoteno"));

                string autoNumber =
                    quote.GetAttributeValue<string>("tbs_quoteno");

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

                // Generate Final Quote Number
                string finalQuoteNumber = $"{prefix}-{autoNumber}";

                tracingService.Trace(
                    "Final Quote Number: {0}", finalQuoteNumber);

                // Update Quote
                Entity updateQuote = new Entity("quote");
                updateQuote.Id = quoteId;
                updateQuote["tbs_quoteno"] = finalQuoteNumber;

                tracingService.Trace(
                    "Updating Quote Number.");

                service.Update(updateQuote);

                tracingService.Trace(
                    "Quote Number update completed successfully.");
            }
            catch (Exception ex)
            {
                tracingService.Trace(
                    "ERROR in GenerateQuoteNumber: {0}", ex.ToString());

                throw new InvalidPluginExecutionException(
                    "Error generating Quote Number: " + ex.Message, ex);
            }
            finally
            {
                tracingService.Trace(
                    "GenerateQuoteNumber plugin execution finished.");
            }
        }
    }
}