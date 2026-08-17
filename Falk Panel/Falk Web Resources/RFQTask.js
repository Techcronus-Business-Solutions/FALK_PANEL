if (typeof Falk === "undefined") {
    Falk = {
        __namespace: true,
    };
}
if (typeof $ === "undefined") {
    $ = parent.$;
    Jquery = parent.Jquery;
}

Falk.RFQTask = {

    OnLoad: function (executionContext) {
        var formContext = executionContext.getFormContext();

        formContext.data.process.addOnPreStageChange(Falk.RFQTask.preStageChange);
    },

    preStageChange: function (executionContext) {

        var eventArgs = executionContext.getEventArgs();
        var formContext = executionContext.getFormContext();

        // Check only when moving forward
        if (eventArgs.getDirection() !== "Next")
            return;

        var activeStage = formContext.data.process.getActiveStage();

        // Restrict only from Manager Review stage
        if (activeStage.getName() !== "Manager Review")
            return;

        // Get Approval Status
        var approvalStatus = formContext.getAttribute("tbs_approvalstatus").getValue();

        // Replace 123456 with your Approved option set value
        if (approvalStatus !== 1) {

            eventArgs.preventDefault();

            Xrm.Navigation.openAlertDialog({
                title: "Approval Required",
                text: "You cannot move to the next stage until the Approval Status is Approved."
            });
        }
    }
}