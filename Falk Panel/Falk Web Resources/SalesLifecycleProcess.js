if (typeof Falk === "undefined") {
    Falk = {
        __namespace: true,
    };
}

if (typeof $ === "undefined") {
    $ = parent.$;
    Jquery = parent.Jquery;
}

Falk.SalesLifecycleProcess = {

    OnLoad: function (executionContext) {

        var formContext = executionContext.getFormContext();
        // Register the PreStageChange event
        formContext.data.process.addOnPreStageChange(Falk.SalesLifecycleProcess.PreStageChange);
    },

    OnChange: function (executionContext) {

    },

    PreStageChange: function (executionContext) {

        var eventArgs = executionContext.getEventArgs();
        var formContext = executionContext.getFormContext();
        // Check whether user is moving backwards
        if (eventArgs.getDirection() === "Previous") {

            eventArgs.preventDefault();
            

            formContext.ui.setFormNotification(
                "Moving to a previous stage is not allowed",
                "ERROR",
                "BPFError"
            );
        }

        setTimeout(function () {
            formContext.ui.clearFormNotification("BPFError");
        }, 5000);
    }
};