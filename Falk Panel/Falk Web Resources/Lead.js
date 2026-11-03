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

            var ownerAttribute = formContext.getAttribute("ownerid");
            if (ownerAttribute != null) {
                ownerAttribute.addOnChange(Falk.Lead.OnCurrencyChange);
            }

            // 3. Set Territory on initial load
            Falk.Lead.setTerritoryFromOwner(formContext);
        },

        OnChange: function (executionContext) {
            var formContext = executionContext.getFormContext();
            var isExistingCustomer = formContext.getAttribute("tbs_existingcustomer").getValue();
            var section = formContext.ui.tabs.get("Summary")
                .sections.get("Existing_Customer_Section");
            section.setVisible(isExistingCustomer === true);
        },

        OnCurrencyChange: function (executionContext) {
            var formContext = executionContext.getFormContext();
            Falk.Lead.setTerritoryFromOwner(formContext);
        },

        setTerritoryFromOwner: function (formContext) {
            try {
                var ownerAttribute = formContext.getAttribute("ownerid");
                var territoryAttribute = formContext.getAttribute("tbs_territory");

                if (ownerAttribute == null || territoryAttribute == null) {
                    return;
                }

                var ownerValue = ownerAttribute.getValue();

                if (ownerValue == null || ownerValue.length === 0) {
                    territoryAttribute.setValue(null);
                    return;
                }

                if (ownerValue[0].entityType !== "systemuser") {
                    territoryAttribute.setValue(null);
                    return;
                }

                var ownerId = ownerValue[0].id.replace(/[{}]/g, "");

                Xrm.WebApi.retrieveRecord(
                    "systemuser",
                    ownerId,
                    "?$select=_territoryid_value"
                ).then(
                    function success(result) {
                        var territoryId = result["_territoryid_value"];
                        var territoryName = result["_territoryid_value@OData.Community.Display.V1.FormattedValue"];

                        if (territoryId == null) {
                            territoryAttribute.setValue(null);
                            return;
                        }

                        territoryAttribute.setValue([{
                            id: territoryId,
                            name: territoryName,
                            entityType: "territory"
                        }]);
                    },
                    function (error) {
                        console.log(error.message);
                    }
                );
            }
            catch (e) {
                console.log(e.message);
            }
        }
    };
    