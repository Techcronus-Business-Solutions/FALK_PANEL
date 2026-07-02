if (typeof Falk === "undefined") {
    Falk = {
        __namespace: true,
    };
}

if (typeof $ === "undefined") {
    $ = parent.$;
    Jquery = parent.Jquery;
}

Falk.OpportunityProductRibbon = {

    CalculateLineItemPricing: function (primaryControl) {
        var formContext = primaryControl;

        let panelThickness = Falk.OpportunityProductRibbon.getLookup(formContext, "tbs_panelthickness", "tbs_thickness");
        let exteriorFinish = Falk.OpportunityProductRibbon.getLookup(formContext, "tbs_exteriorfinish", "tbs_finish");
        let interiorFinish = Falk.OpportunityProductRibbon.getLookup(formContext, "tbs_interiorfinish", "tbs_finish");
        let exteriorGauge = Falk.OpportunityProductRibbon.getLookup(formContext, "tbs_exteriorgauge", "tbs_gauge");
        let interiorGauge = Falk.OpportunityProductRibbon.getLookup(formContext, "tbs_interiorgauge", "tbs_gauge");
        let exteriorColor = Falk.OpportunityProductRibbon.getLookup(formContext, "tbs_exteriorcolor", "tbs_color");
        let interiorColor = Falk.OpportunityProductRibbon.getLookup(formContext, "tbs_interiorcolor", "tbs_color");
        let productId = Falk.OpportunityProductRibbon.getLookup(formContext, "productid", "product");
        let priceLevelTier = Falk.OpportunityProductRibbon.getLookup(formContext, "tbs_priceleveltier", "tbs_tier");

        let exteriorEmboss = formContext.getAttribute("tbs_exterioremboss").getValue();
        let interiorEmboss = formContext.getAttribute("tbs_interioremboss").getValue();

        if (!panelThickness || !exteriorFinish || !interiorFinish || !exteriorGauge ||
            !interiorGauge || !exteriorColor || !interiorColor) {
            Xrm.Navigation.openAlertDialog({ text: "Please fill in all pricing attributes before calculating." });
            return;
        }
        var request = {
            entity: { entityType: "opportunityproduct", id: formContext.data.entity.getId().replace(/[{}]/g, "") },
            Product: { "@odata.type": "Microsoft.Dynamics.CRM.product", productid: productId.id },
            PanelThickness: { "@odata.type": "Microsoft.Dynamics.CRM.tbs_thickness", tbs_thicknessid: panelThickness.id },
            PriceLevelTier: { "@odata.type": "Microsoft.Dynamics.CRM.tbs_tier", tbs_tierid: priceLevelTier.id },
            ExteriorFinish: { "@odata.type": "Microsoft.Dynamics.CRM.tbs_finish", tbs_finishid: exteriorFinish.id },
            InteriorFinish: { "@odata.type": "Microsoft.Dynamics.CRM.tbs_finish", tbs_finishid: interiorFinish.id },
            ExteriorGauge: { "@odata.type": "Microsoft.Dynamics.CRM.tbs_gauge", tbs_gaugeid: exteriorGauge.id },
            InteriorGauge: { "@odata.type": "Microsoft.Dynamics.CRM.tbs_gauge", tbs_gaugeid: interiorGauge.id },
            ExteriorColor: { "@odata.type": "Microsoft.Dynamics.CRM.tbs_color", tbs_colorid: exteriorColor.id },
            InteriorColor: { "@odata.type": "Microsoft.Dynamics.CRM.tbs_color", tbs_colorid: interiorColor.id },
            InteriorEmboss: interiorEmboss,
            ExteriorEmboss: exteriorEmboss,

            getMetadata: function () {
                return {
                    boundParameter: "entity",
                    parameterTypes: {
                        entity: { typeName: "mscrm.opportunityproduct", structuralProperty: 5 },
                        Product: { typeName: "mscrm.product", structuralProperty: 5 },
                        PanelThickness: { typeName: "mscrm.tbs_thickness", structuralProperty: 5 },
                        PriceLevelTier: { typeName: "mscrm.tbs_tier", structuralProperty: 5 },
                        ExteriorFinish: { typeName: "mscrm.tbs_finish", structuralProperty: 5 },
                        InteriorFinish: { typeName: "mscrm.tbs_finish", structuralProperty: 5 },
                        ExteriorGauge: { typeName: "mscrm.tbs_gauge", structuralProperty: 5 },
                        InteriorGauge: { typeName: "mscrm.tbs_gauge", structuralProperty: 5 },
                        ExteriorColor: { typeName: "mscrm.tbs_color", structuralProperty: 5 },
                        InteriorColor: { typeName: "mscrm.tbs_color", structuralProperty: 5 },
                        InteriorEmboss: { typeName: "Edm.Int32", structuralProperty: 1 },
                        ExteriorEmboss: { typeName: "Edm.Int32", structuralProperty: 1 }
                    },
                    operationType: 0, operationName: "tbs_FalkCustomAPIOpportunityProductPricingCalculation"
                };
            }
        };

        Xrm.Utility.showProgressIndicator("Calculating line item pricing...");

        Xrm.WebApi.execute(request).then(
            function success(response) {
                if (response.ok) { return response.json(); }
            }
        ).then(function (responseBody) {
            var result = responseBody;
            var interiorprice = result["InteriorPrice"]; 
            var exteriorprice = result["ExteriorPrice"];
            var interiorembossprice = result["InteriorEmbossPrice"];
            var exteriorembossprice = result["ExteriorEmbossPrice"];
            var calculatedbaseprice = result["CalculatedBasePrice"];

            formContext.getAttribute("tbs_interiorfinishprice").setValue(interiorprice);
            formContext.getAttribute("tbs_exteriorfinishprice").setValue(exteriorprice);
            formContext.getAttribute("tbs_ribbingmodelsweatherprice").setValue(0);
            formContext.getAttribute("tbs_ribbingmodeusinteriorprice").setValue(0);
            formContext.getAttribute("tbs_embossinglsweatherprice").setValue(exteriorembossprice);
            formContext.getAttribute("tbs_embossingusinteriorprice").setValue(interiorembossprice);
            formContext.getAttribute("tbs_baseprice").setValue(calculatedbaseprice);

            let TotalPropertyPrice = result.InteriorPrice + result.ExteriorPrice + result.ExteriorEmbossPrice + result.InteriorEmbossPrice;
            formContext.getAttribute("tbs_totalpropertiesprice").setValue(TotalPropertyPrice);
        }).catch(function (error) {
            console.log(error.message);
        }).finally(function () {
            Xrm.Utility.closeProgressIndicator();
        });
    },

    getLookup: function (formContext, fieldName, entityType) {
        var attr = formContext.getAttribute(fieldName);
        var val = attr ? attr.getValue() : null;
        if (!val || !val[0]) return null;
        return { id: val[0].id.replace(/[{}]/g, ""), entityType: entityType };
    },
};