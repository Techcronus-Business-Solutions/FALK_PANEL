if (typeof Falk === "undefined") {
    Falk = {
        __namespace: true,
    };
}
if (typeof $ === "undefined") {
    $ = parent.$;
    Jquery = parent.Jquery;
}

Falk.OpportunityPanelTrim = {
    OnLoad: function (executionContext) {
        const formContext = executionContext.getFormContext();

        Falk.OpportunityPanelTrim.ToggleCustomTrimSection(executionContext);

        formContext.getAttribute("tbs_iscustomtrim").addOnChange(Falk.OpportunityPanelTrim.ToggleCustomTrimSection);
    },

    ToggleCustomTrimSection: function (executionContext) {
        const formContext = executionContext.getFormContext();

        const isCustomTrim = formContext.getAttribute("tbs_iscustomtrim").getValue();

        const section = formContext.ui.tabs
            .get("General")
            .sections.get("CustomTrim_Section");

        if (section) {
            section.setVisible(isCustomTrim === true);
        }
    }
}