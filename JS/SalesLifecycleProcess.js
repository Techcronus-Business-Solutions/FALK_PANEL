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

    if (eventArgs.getDirection() === "Previous") {

        eventArgs.preventDefault();

        Xrm.Navigation.openErrorDialog({
            message: "Moving to a previous stage is not allowed."
        });
    }
}
};