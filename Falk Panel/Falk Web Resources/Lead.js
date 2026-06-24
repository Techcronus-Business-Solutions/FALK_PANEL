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
        OnLoad: function (executionContext) {
            var formContext = executionContext.getFormContext();

            formContext.getAttribute("tbs_existingcustomer")
                .addOnChange(Falk.Lead.OnChange);

            Falk.Lead.OnChange(executionContext);
        },

        OnChange: function (executionContext) {
            var formContext = executionContext.getFormContext();
            var isExistingCustomer = formContext.getAttribute("tbs_existingcustomer").getValue();
            var section = formContext.ui.tabs.get("Summary")
                .sections.get("Existing_Customer_Section");
            section.setVisible(isExistingCustomer === true);
        }
    }