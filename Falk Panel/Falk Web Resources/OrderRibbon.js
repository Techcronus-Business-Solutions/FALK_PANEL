if (typeof Falk === "undefined") {
    Falk = {
        __namespace: true,
    };
}

if (typeof $ === "undefined") {
    $ = parent.$;
    Jquery = parent.Jquery;
}

Falk.OrderRibbon = {

    SubmitOrder: async function (primaryControl) {
        var formContext = primaryControl;
        var orderId = formContext.data.entity.getId();

        if (!orderId) {
            Xrm.Navigation.openAlertDialog({
                text: "Order ID could not be found."
            });
            return;
        }

        var isEmptyField = function (val) {
            if (val === null || val === undefined) {
                return true;
            }
            if (Array.isArray(val)) {
                return val.length === 0;
            }
            if (typeof val === "string") {
                return val.trim() === "";
            }
            return false;
        };

        var customerAttr = formContext.getAttribute("customerid");
        var customerValue = customerAttr ? customerAttr.getValue() : null;

        var missingFields = [];

        if (isEmptyField(customerValue)) {
            missingFields.push("Potential Customer (customerid)");
        }

        var taxAreaValue = null;

        if (!isEmptyField(customerValue)) {
            try {
                var accountId = customerValue[0].id.replace(/[{}]/g, "");
                var accountEntityType = customerValue[0].entityType || "account";

                var accountRecord = await Xrm.WebApi.retrieveRecord(
                    accountEntityType,
                    accountId,
                    "?$select=_tbs_taxarea_value"
                );

                taxAreaValue = accountRecord["_tbs_taxarea_value"] || null;
                console.log("SubmitOrder - Account _tbs_taxarea_value:", taxAreaValue);
            } catch (err) {
                console.log("SubmitOrder - Error fetching Account Tax Area:", err.message);
                Xrm.Navigation.openAlertDialog({
                    title: "Validation Error",
                    text: "Unable to verify Tax Area on the customer record.\n\n" + err.message
                });
                return;
            }
        }

        if (isEmptyField(taxAreaValue)) {
            missingFields.push("Tax Area (on Company)");
        }

        if (missingFields.length > 0) {
            Xrm.Navigation.openAlertDialog({
                title: "Required Fields Missing",
                text: "Please fill the following required fields before submitting the order:\n\n- " + missingFields.join("\n- ")
            });
            return;
        }

        orderId = orderId.replace(/[{}]/g, "");

        Xrm.Utility.showProgressIndicator("Submitting Order...");

        var data = {
            statecode: 1
        };

        Xrm.WebApi.updateRecord("salesorder", orderId, data).then(
            function success(result) {

                Xrm.Navigation.openAlertDialog({
                    text: "Order state changed successfully."
                }).then(function () {
                    Xrm.Utility.closeProgressIndicator();

                    formContext.data.refresh(false);
                });

            },
            function (error) {
                Xrm.Utility.closeProgressIndicator();

                console.log(error.message);

                Xrm.Navigation.openAlertDialog({
                    text: "Failed to change Order state.\n\n" + error.message
                });
            }
        );
    }
};