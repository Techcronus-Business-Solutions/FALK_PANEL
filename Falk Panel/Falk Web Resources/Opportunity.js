if (typeof (CrmJS) === "undefined") {
    var CrmJS = { __namespace: true };
}

CrmJS.OpportunityFunctions = {

    // ==================== On Load ====================
    onLoad: function (executionContext) {
        CrmJS.OpportunityFunctions.initialize(executionContext);
        CrmJS.OpportunityFunctions.process(executionContext);
    },

    // ==================== Initialize ====================
    initialize: function (executionContext) {
        CrmJS.globalFormContext = executionContext.getFormContext();
    },

    // ==================== Process ====================
    process: function (executionContext) {
        var formContext = executionContext.getFormContext();

        CrmJS.OpportunityFunctions.setTerritoryFromOwner(formContext);
        CrmJS.OpportunityFunctions.registerStageChange(executionContext);
    },

    // ==================== Owner OnChange ====================
    onCurrencyChange: function (executionContext) {
        var formContext = executionContext.getFormContext();

        CrmJS.OpportunityFunctions.setTerritoryFromOwner(formContext);
    },

    // ==================== Register BPF Stage Change ====================
    registerStageChange: function (executionContext) {
        var formContext = executionContext.getFormContext();

        // Fires after the user moves to another stage and the BPF saves it
        formContext.data.process.addOnStageChange(
            CrmJS.OpportunityFunctions.onStageChange
        );
    },

    // ==================== On Stage Change ====================
    onStageChange: function (executionContext) {
        var formContext = executionContext.getFormContext();

        var stage = formContext.data.process.getActiveStage();
        if (!stage) return;

        var stageName = stage.getName();

        // Compare by stage name. Change "Review" if the name differs
        if (stageName === "Review") {
            var tab = formContext.ui.tabs.get("QUOTES");
            if (tab) {
                tab.setFocus();
            }
        }
    },

    // ==================== Set Territory From Owner ====================
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