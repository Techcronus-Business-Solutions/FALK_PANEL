using System;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace Falk_Plugins.Quote
{
    public class PostQuoteCreateAutoNumberByOwnerPrefix : IPlugin
    {
        private const int PostOperation = 40;

        public void Execute(IServiceProvider serviceProvider)
        {
            ITracingService tracingService =
                (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            IPluginExecutionContext context =
                (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));

            if (context.MessageName != "Create" || context.PrimaryEntityName != "quote")
                return;

            try
            {
                // Stage 40: add the user's prefix
                if (context.Stage == PostOperation)
                {
                    IOrganizationServiceFactory factory =
                        (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
                    IOrganizationService service = factory.CreateOrganizationService(context.UserId);

                    HandlePostOperation(context, service, tracingService);
                }
            }
            catch (InvalidPluginExecutionException)
            {
                throw;
            }
            catch (Exception ex)
            {
                tracingService.Trace("Quote number plugin error: {0}", ex.ToString());
                throw new InvalidPluginExecutionException(
                    "Error generating Quote Number: " + ex.Message, ex);
            }
        }

        private void HandlePostOperation(
            IPluginExecutionContext context,
            IOrganizationService service,
            ITracingService tracingService)
        {
            Guid quoteId = context.PrimaryEntityId;

            Entity quote = service.Retrieve(
                "quote",
                quoteId,
                new ColumnSet("tbs_quoteno", "tbs_iscopy", "revisionnumber"));

            bool isCopy = quote.GetAttributeValue<bool?>("tbs_iscopy") ?? false;
            int revisionNumber = quote.GetAttributeValue<int?>("revisionnumber") ?? 0;

            tracingService.Trace("Stage 40. isCopy={0}, revision={1}, quoteno='{2}'",
    isCopy, revisionNumber, quote.GetAttributeValue<string>("tbs_quoteno"));

            // Revise: keep the same quote number
            if (revisionNumber > 0 && !isCopy)
            {
                tracingService.Trace("Revise detected. Quote number not changed.");
                return;
            }

            string autoNumber = quote.GetAttributeValue<string>("tbs_quoteno");

            if (string.IsNullOrWhiteSpace(autoNumber))
                throw new InvalidPluginExecutionException("Auto Number was not generated.");

            Entity user = service.Retrieve(
                "systemuser",
                context.InitiatingUserId,
                new ColumnSet("tbs_prefix"));

            string prefix = user.GetAttributeValue<string>("tbs_prefix");

            if (string.IsNullOrWhiteSpace(prefix))
                throw new InvalidPluginExecutionException(
                    "Prefix is not configured for the logged-in user.");

            // Already has the prefix, nothing to do
            if (autoNumber.StartsWith(prefix + "-", StringComparison.OrdinalIgnoreCase))
                return;

            string finalQuoteNumber = prefix + "-" + autoNumber;
            tracingService.Trace("Final Quote Number: {0}", finalQuoteNumber);

            Entity update = new Entity("quote", quoteId);
            update["tbs_quoteno"] = finalQuoteNumber;
            service.Update(update);
        }
    }
}