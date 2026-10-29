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
    },

    // ==================== Owner OnChange ====================
    onOwnerChange: function (executionContext) {
        var formContext = executionContext.getFormContext();

        CrmJS.OpportunityFunctions.setTerritoryFromOwner(formContext);
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