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

    SubmitOrder: function (primaryControl) {
        var formContext = primaryControl;
        var orderId = formContext.data.entity.getId();

        if (!orderId) {
            Xrm.Navigation.openAlertDialog({
                text: "Order ID could not be found."
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