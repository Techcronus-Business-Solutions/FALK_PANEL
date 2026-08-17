if (typeof Falk === "undefined") {
    Falk = {
        __namespace: true
    };
}

if (typeof $ === "undefined") {
    $ = parent.$;
    Jquery = parent.Jquery;
}

Falk.AccountRibbon = {
    CreateOpportunity: async function (executionContext) {
        try {
            var formContext = executionContext;

            var accountId = formContext.data.entity.getId();
            var accountName = formContext.getAttribute("name")?.getValue();
            var email = formContext.getAttribute("emailaddress1")?.getValue();
            var street1 = formContext.getAttribute("address1_line1")?.getValue();
            var street2 = formContext.getAttribute("address1_line2")?.getValue();
            var street3 = formContext.getAttribute("address1_line3")?.getValue();
            var city = formContext.getAttribute("address1_city")?.getValue();
            var state = formContext.getAttribute("address1_stateorprovince")?.getValue();
            var zip = formContext.getAttribute("address1_postalcode")?.getValue();
            var paymentTerm = formContext.getAttribute("paymenttermscode")?.getValue();

            accountId = accountId.replace(/[{}]/g, "");
            var formParameters = {};

            formParameters["name"] = accountName;
            formParameters["emailaddress"] = email;
            var Address = "";
            if (street1 != null) {
                Address += street1;
            }
            if (street2 != null) {
                Address += " " + street2;
            }
            if (street3 != null) {
                Address += " " + street3; 
            }
            formParameters["tbs_projectstreetaddress"] = Address;
            formParameters["tbs_projectcity"] = city;
            formParameters["tbs_projectstate"] = state;
            formParameters["tbs_projectzip"] = zip;
            formParameters["paymenttermscode"] = paymentTerm;


            // Set Account lookup on Opportunity
            formParameters["parentaccountid"] = accountId;
            formParameters["parentaccountidname"] = accountName;
            formParameters["parentaccountidtype"] = "account";

            // Open new Opportunity form
            var entityFormOptions = {
                entityName: "opportunity",
                useQuickCreateForm: false
            };

            await Xrm.Navigation.openForm(entityFormOptions,formParameters);

        } catch (error) {

            console.error(error);

            Xrm.Navigation.openAlertDialog({
                text: error.message || "An error occurred while opening the Opportunity form."
            });
        }
    }
};