if (typeof Falk === "undefined") {
    Falk = {
        __namespace: true,
    };
}
if (typeof $ === "undefined") {
    $ = parent.$;
    Jquery = parent.Jquery;
}

Falk.Lead = {
    OnLoad: {},

    OnSave: {},

    OnChange: function (executionContext) {
        var formContext = executionContext.getFormContext();
        var isExistingCustomer = formContext.getAttribute("tbs_existingcustomer").getValue();
        var tab = formContext.ui.tabs.get("Summary");

        if (isExistingCustomer == true) {
            tab.sections.get("Existing_Customer_Section").setVisible(true);
        } else {
            tab.sections.get("Existing_Customer_Section").setVisible(false);
        }
    }
}