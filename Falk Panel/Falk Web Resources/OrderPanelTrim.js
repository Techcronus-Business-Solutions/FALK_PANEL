if (typeof Falk === "undefined") {
    Falk = {
        __namespace: true,
    };
}
if (typeof $ === "undefined") {
    $ = parent.$;
    Jquery = parent.Jquery;
}

Falk.OrderPanelTrim = {
    OnLoad: function (executionContext) {
        const formContext = executionContext.getFormContext();

        Falk.OrderPanelTrim.ToggleCustomTrimSection(executionContext);

        formContext.getAttribute("tbs_iscustomtrim").addOnChange(Falk.OrderPanelTrim.ToggleCustomTrimSection);
    },

    ToggleCustomTrimSection: function (executionContext) {
        const formContext = executionContext.getFormContext();

        const isCustomTrim = formContext.getAttribute("tbs_iscustomtrim").getValue();

        const section = formContext.ui.tabs
            .get("General_Tab")
            .sections.get("CustomTrim_Section");

        if (section) {
            section.setVisible(isCustomTrim === true);
        }
    }
}