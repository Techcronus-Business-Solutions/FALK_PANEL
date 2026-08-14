if (typeof Falk === "undefined") {
    Falk = {
        __namespace: true,
    };
}
if (typeof $ === "undefined") {
    $ = parent.$;
    Jquery = parent.Jquery;
}

Falk.OpportunityProduct = {
    FRW42PanelId: null,
    FalkHPSFinishId: null,

    OnLoad: async function (executionContext) {
        const formContext = executionContext.getFormContext();

        if (formContext.ui.getFormType() === 1) {
            formContext.getAttribute("quantity").setValue(0);
        }

        // Cache Environment Variables
        this.FRW42PanelId = await this.GetEnvironmentVariableValue("tbs_FRW42PanelId");

        const jsonText = await Falk.OpportunityProduct.GetEnvironmentVariableValue(
            "tbs_FalkHPSFinishId"
        );
        Falk.OpportunityProduct.FalkHPSFinishId = JSON.parse(jsonText);

        formContext.getControl("tbs_exteriorfinish")
            .addPreSearch(function () {
                formContext.getAttribute("tbs_exteriorcolor").setValue(null);
                Falk.OpportunityProduct.addExteriorFinishView(formContext);
            });

        formContext.getControl("tbs_interiorfinish")
            .addPreSearch(function () {
                formContext.getAttribute("tbs_interiorcolor").setValue(null);
                Falk.OpportunityProduct.addInteriorFinishView(formContext);
            });

        formContext.getControl("tbs_exteriorgauge").addPreSearch(function () {
            Falk.OpportunityProduct.addExteriorGaugeView(formContext);
        });

        formContext.getControl("tbs_interiorgauge").addPreSearch(function () {
            Falk.OpportunityProduct.addInteriorGaugeView(formContext);
        });

        formContext.getControl("tbs_exteriorprofile").addPreSearch(function () {
            Falk.OpportunityProduct.addExteriorProfileView(formContext);
        });

        formContext.getControl("tbs_interiorprofile").addPreSearch(function () {
            Falk.OpportunityProduct.addInteriorProfileView(formContext);
        });

        formContext.getControl("tbs_exteriorcolor").addPreSearch(function () {
            Falk.OpportunityProduct.addExteriorColorView(formContext);
        });

        formContext.getControl("tbs_interiorcolor").addPreSearch(function () {
            Falk.OpportunityProduct.addInteriorColorView(formContext);
        });

        formContext.getAttribute("productid").addOnChange(async function () {
            formContext.getAttribute("tbs_panelthickness").setValue(null);

            formContext.getAttribute("tbs_exteriorfinish").setValue(null);
            Falk.OpportunityProduct.addExteriorFinishView(formContext);

            formContext.getAttribute("tbs_interiorfinish").setValue(null);
            Falk.OpportunityProduct.addInteriorFinishView(formContext);

            formContext.getAttribute("tbs_exteriorgauge").setValue(null);
            Falk.OpportunityProduct.addExteriorGaugeView(formContext);

            formContext.getAttribute("tbs_interiorgauge").setValue(null);
            Falk.OpportunityProduct.addInteriorGaugeView(formContext);

            formContext.getAttribute("tbs_exteriorprofile").setValue(null);
            Falk.OpportunityProduct.addExteriorProfileView(formContext);

            formContext.getAttribute("tbs_interiorprofile").setValue(null);
            Falk.OpportunityProduct.addInteriorProfileView(formContext);

            formContext.getAttribute("tbs_exterioremboss").setValue(false);
            await Falk.OpportunityProduct.EnableDisableExteriorEmboss(formContext);

            formContext.getAttribute("tbs_interioremboss").setValue(false);
            await Falk.OpportunityProduct.EnableDisableInteriorEmboss(formContext);

            formContext.getAttribute("tbs_exteriorcolor").setValue(null);
            formContext.getAttribute("tbs_interiorcolor").setValue(null);
        });

        formContext.getAttribute("tbs_panelthickness").addOnChange(async function () {
            await Falk.OpportunityProduct.SetFieldsFromThickness(formContext);
        });

        formContext.getAttribute("tbs_priceleveltier").addOnChange(async function () {
            const thickness = formContext.getAttribute("tbs_panelthickness")?.getValue();
            if (!thickness) {
                formContext.getAttribute("tbs_baseprice").setValue(null);
                return;
            }
            const thicknessId = thickness[0].id.replace(/[{}]/g, "");
            const thicknessRecord = await Xrm.WebApi.retrieveRecord("tbs_thickness", thicknessId, "?$select=tbs_baseprice");

            const basePrice = thicknessRecord.tbs_baseprice || 0;

            await Falk.OpportunityProduct.SetFieldsFromThickness(formContext, basePrice);
        });

        formContext.getAttribute("tbs_interiorfinish").addOnChange(function (executionContext) {
            const formContext = executionContext.getFormContext();

            formContext.getAttribute("tbs_interiorcolor").setValue(null);

            Falk.OpportunityProduct.addInteriorColorView(formContext);
            Falk.OpportunityProduct.FinishOnChange(executionContext);
        });

        formContext.getAttribute("tbs_exteriorfinish").addOnChange(function (executionContext) {
            const formContext = executionContext.getFormContext();

            formContext.getAttribute("tbs_exteriorcolor").setValue(null);

            Falk.OpportunityProduct.addExteriorColorView(formContext);
            Falk.OpportunityProduct.FinishOnChange(executionContext);
        });
    },

    addExteriorFinishView: function (formContext) {
        const product = formContext.getAttribute("productid").getValue();

        if (!product)
            return;

        const productId = product[0].id.replace(/[{}]/g, "");

        const fetchXml =
            "<fetch version='1.0' mapping='logical' distinct='true'>" +
            "  <entity name='tbs_finish'>" +
            "    <attribute name='tbs_name' />" +
            "    <order attribute='tbs_name' />" +
            "    <filter>" +
            "       <condition attribute='tbs_type' operator='eq' value='1' />" +
            "    </filter>" +
            "    <link-entity name='tbs_finish_product' from='tbs_finishid' to='tbs_finishid' link-type='inner'>" +
            "       <filter>" +
            "           <condition attribute='productid' operator='eq' value='" + productId + "' />" +
            "       </filter>" +
            "    </link-entity>" +
            "  </entity>" +
            "</fetch>";

        const layoutXml =
            "<grid name='resultset' object='1' jump='tbs_name' select='1' icon='1' preview='1'>" +
            "   <row name='result' id='tbs_finishid'>" +
            "      <cell name='tbs_name' width='250' />" +
            "   </row>" +
            "</grid>";

        formContext.getControl("tbs_exteriorfinish").addCustomView(
            "{541578de-e063-f111-a848-6045bd042c69}",
            "tbs_finish",
            "Filtered Exterior Finish",
            fetchXml,
            layoutXml,
            true
        );
    },

    addInteriorFinishView: function (formContext) {
        const product = formContext.getAttribute("productid").getValue();

        if (!product)
            return;

        const productId = product[0].id.replace(/[{}]/g, "");

        const fetchXml =
            "<fetch version='1.0' mapping='logical' distinct='true'>" +
            "  <entity name='tbs_finish'>" +
            "    <attribute name='tbs_name' />" +
            "    <order attribute='tbs_name' />" +
            "    <filter>" +
            "       <condition attribute='tbs_type' operator='eq' value='0' />" +
            "    </filter>" +
            "    <link-entity name='tbs_finish_product' from='tbs_finishid' to='tbs_finishid' link-type='inner'>" +
            "       <filter>" +
            "           <condition attribute='productid' operator='eq' value='" + productId + "' />" +
            "       </filter>" +
            "    </link-entity>" +
            "  </entity>" +
            "</fetch>";

        const layoutXml =
            "<grid name='resultset' object='1' jump='tbs_name' select='1' icon='1' preview='1'>" +
            "   <row name='result' id='tbs_finishid'>" +
            "      <cell name='tbs_name' width='250' />" +
            "   </row>" +
            "</grid>";

        formContext.getControl("tbs_interiorfinish").addCustomView(
            "{541578de-e063-f111-a848-6045bd042c70}",
            "tbs_finish",
            "Filtered Interior Finish",
            fetchXml,
            layoutXml,
            true
        );
    },

    addExteriorGaugeView: function (formContext) {
        const exteriorFinish = formContext.getAttribute("tbs_exteriorfinish").getValue();

        if (!exteriorFinish)
            return;

        const exteriorFinishId = exteriorFinish[0].id.replace(/[{}]/g, "");

        const fetchXml =
            "<fetch version='1.0' mapping='logical' distinct='true'>" +
            "  <entity name='tbs_gauge'>" +
            "    <attribute name='tbs_name' />" +
            "    <order attribute='tbs_name' />" +
            "    <link-entity name='tbs_gauge_tbs_finish' from='tbs_gaugeid' to='tbs_gaugeid' link-type='inner'>" +
            "       <filter>" +
            "           <condition attribute='tbs_finishid' operator='eq' value='" + exteriorFinishId + "' />" +
            "       </filter>" +
            "    </link-entity>" +
            "  </entity>" +
            "</fetch>";

        const layoutXml =
            "<grid name='resultset' object='1' jump='tbs_name' select='1' icon='1' preview='1'>" +
            "   <row name='result' id='tbs_gaugeid'>" +
            "      <cell name='tbs_name' width='250' />" +
            "   </row>" +
            "</grid>";

        formContext.getControl("tbs_exteriorgauge").addCustomView(
            "{541578de-e063-f111-a848-6045bd042c71}",
            "tbs_gauge",
            "Filtered Exterior Gauge",
            fetchXml,
            layoutXml,
            true
        );
    },

    addInteriorGaugeView: function (formContext) {
        const interiorFinish = formContext.getAttribute("tbs_interiorfinish").getValue();

        if (!interiorFinish)
            return;

        const interiorFinishId = interiorFinish[0].id.replace(/[{}]/g, "");

        const fetchXml =
            "<fetch version='1.0' mapping='logical' distinct='true'>" +
            "  <entity name='tbs_gauge'>" +
            "    <attribute name='tbs_name' />" +
            "    <order attribute='tbs_name' />" +
            "    <link-entity name='tbs_gauge_tbs_finish' from='tbs_gaugeid' to='tbs_gaugeid' link-type='inner'>" +
            "       <filter>" +
            "           <condition attribute='tbs_finishid' operator='eq' value='" + interiorFinishId + "' />" +
            "       </filter>" +
            "    </link-entity>" +
            "  </entity>" +
            "</fetch>";

        const layoutXml =
            "<grid name='resultset' object='1' jump='tbs_name' select='1' icon='1' preview='1'>" +
            "   <row name='result' id='tbs_gaugeid'>" +
            "      <cell name='tbs_name' width='250' />" +
            "   </row>" +
            "</grid>";

        formContext.getControl("tbs_interiorgauge").addCustomView(
            "{541578de-e063-f111-a848-6045bd042c72}",
            "tbs_gauge",
            "Filtered Interior Gauge",
            fetchXml,
            layoutXml,
            true
        );
    },

    addExteriorProfileView: function (formContext) {
        const product = formContext.getAttribute("productid").getValue();

        if (!product)
            return;

        const productId = product[0].id.replace(/[{}]/g, "");

        const fetchXml =
            "<fetch version='1.0' mapping='logical' distinct='true'>" +
            "  <entity name='tbs_profile'>" +
            "    <attribute name='tbs_name' />" +
            "    <order attribute='tbs_name' />" +
            "    <filter>" +
            "       <condition attribute='tbs_type' operator='eq' value='1' />" +
            "    </filter>" +
            "    <link-entity name='tbs_profile_product' from='tbs_profileid' to='tbs_profileid' link-type='inner'>" +
            "       <filter>" +
            "           <condition attribute='productid' operator='eq' value='" + productId + "' />" +
            "       </filter>" +
            "    </link-entity>" +
            "  </entity>" +
            "</fetch>";

        const layoutXml =
            "<grid name='resultset' object='1' jump='tbs_name' select='1' icon='1' preview='1'>" +
            "   <row name='result' id='tbs_profileid'>" +
            "      <cell name='tbs_name' width='250' />" +
            "   </row>" +
            "</grid>";

        formContext.getControl("tbs_exteriorprofile").addCustomView(
            "{541578de-e063-f111-a848-6045bd042c73}",
            "tbs_profile",
            "Filtered Exterior Profile",
            fetchXml,
            layoutXml,
            true
        );
    },

    addInteriorProfileView: function (formContext) {
        const product = formContext.getAttribute("productid").getValue();

        if (!product)
            return;

        const productId = product[0].id.replace(/[{}]/g, "");

        const fetchXml =
            "<fetch version='1.0' mapping='logical' distinct='true'>" +
            "  <entity name='tbs_profile'>" +
            "    <attribute name='tbs_name' />" +
            "    <order attribute='tbs_name' />" +
            "    <filter>" +
            "       <condition attribute='tbs_type' operator='eq' value='0' />" +
            "    </filter>" +
            "    <link-entity name='tbs_profile_product' from='tbs_profileid' to='tbs_profileid' link-type='inner'>" +
            "       <filter>" +
            "           <condition attribute='productid' operator='eq' value='" + productId + "' />" +
            "       </filter>" +
            "    </link-entity>" +
            "  </entity>" +
            "</fetch>";

        const layoutXml =
            "<grid name='resultset' object='1' jump='tbs_name' select='1' icon='1' preview='1'>" +
            "   <row name='result' id='tbs_profileid'>" +
            "      <cell name='tbs_name' width='250' />" +
            "   </row>" +
            "</grid>";

        formContext.getControl("tbs_interiorprofile").addCustomView(
            "{541578de-e063-f111-a848-6045bd042c74}",
            "tbs_profile",
            "Filtered Interior Profile",
            fetchXml,
            layoutXml,
            true
        );
    },

    addExteriorColorView: function (formContext) {
        const product = formContext.getAttribute("productid").getValue();
        const finish = formContext.getAttribute("tbs_exteriorfinish").getValue();

        if (!product || !finish)
            return;

        const productId = product[0].id.replace(/[{}]/g, "");
        const finishId = finish[0].id.replace(/[{}]/g, "");

        const FRW42PanelId = Falk.OpportunityProduct.FRW42PanelId;

        let fetchXml = "";

        if (FRW42PanelId && FRW42PanelId.replace(/[{}]/g, "").toLowerCase() === productId.toLowerCase()) {
            fetchXml =
                "<fetch version='1.0' mapping='logical'>" +
                " <entity name='tbs_color'>" +
                "   <attribute name='tbs_name' />" +
                "   <order attribute='tbs_name' />" +
                "   <filter>" +
                "      <condition attribute='tbs_paneltype' operator='eq' value='" + productId + "' />" +
                "   </filter>" +
                " </entity>" +
                "</fetch>";

        } else {
            fetchXml =
                "<fetch version='1.0' mapping='logical'>" +
                " <entity name='tbs_color'>" +
                "   <attribute name='tbs_name' />" +
                "   <order attribute='tbs_name' />" +
                "    <link-entity name='tbs_color_tbs_finish' from='tbs_colorid' to='tbs_colorid' link-type='inner'>" +
                "       <filter>" +
                "           <condition attribute='tbs_finishid' operator='eq' value='" + finishId + "' />" +
                "       </filter>" +
                "    </link-entity>" +
                " </entity>" +
                "</fetch>";
        }

        const layoutXml =
            "<grid name='resultset' object='1' jump='tbs_name' select='1' icon='1' preview='1'>" +
            "   <row name='result' id='tbs_colorid'>" +
            "      <cell name='tbs_name' width='250' />" +
            "   </row>" +
            "</grid>";

        formContext.getControl("tbs_exteriorcolor").addCustomView(
            "{36e15928-2987-f111-ab0e-70a8a59a342d}",
            "tbs_color",
            "Filtered Exterior Colors",
            fetchXml,
            layoutXml,
            true
        );
    },

    addInteriorColorView: async function (formContext) {
        const product = formContext.getAttribute("productid").getValue();
        const finish = formContext.getAttribute("tbs_interiorfinish").getValue();

        if (!product || !finish)
            return;

        const productId = product[0].id.replace(/[{}]/g, "");
        const finishId = finish[0].id.replace(/[{}]/g, "");

        const FRW42PanelId = Falk.OpportunityProduct.FRW42PanelId;

        let fetchXml = "";

        if (FRW42PanelId && FRW42PanelId.replace(/[{}]/g, "").toLowerCase() === productId.toLowerCase()) {
            fetchXml =
                "<fetch version='1.0' mapping='logical'>" +
                " <entity name='tbs_color'>" +
                "   <attribute name='tbs_name' />" +
                "   <order attribute='tbs_name' />" +
                "   <filter>" +
                "      <condition attribute='tbs_paneltype' operator='eq' value='" + productId + "' />" +
                "   </filter>" +
                " </entity>" +
                "</fetch>";

        } else {
            fetchXml =
                "<fetch version='1.0' mapping='logical'>" +
                " <entity name='tbs_color'>" +
                "   <attribute name='tbs_name' />" +
                "   <order attribute='tbs_name' />" +
                "    <link-entity name='tbs_color_tbs_finish' from='tbs_colorid' to='tbs_colorid' link-type='inner'>" +
                "       <filter>" +
                "           <condition attribute='tbs_finishid' operator='eq' value='" + finishId + "' />" +
                "       </filter>" +
                "    </link-entity>" +
                " </entity>" +
                "</fetch>";
        }

        const layoutXml =
            "<grid name='resultset' object='1' jump='tbs_name' select='1' icon='1' preview='1'>" +
            "   <row name='result' id='tbs_colorid'>" +
            "      <cell name='tbs_name' width='250' />" +
            "   </row>" +
            "</grid>";

        formContext.getControl("tbs_interiorcolor").addCustomView(
            "{ceb6d2b6-858a-f111-ab0f-70a8a59d3f85}",
            "tbs_color",
            "Filtered Exterior Colors",
            fetchXml,
            layoutXml,
            true
        );
    },

    EnableDisableExteriorEmboss: async function (formContext) {
        const product = formContext.getAttribute("productid").getValue();

        if (!product)
            return;

        const productId = product[0].id.replace(/[{}]/g, "");

        try {
            const product = await Xrm.WebApi.retrieveRecord("product", productId, "?$select=tbs_exteriorembossavailable");

            const isExteriorembossAvailable = product["tbs_exteriorembossavailable"];
            if (!isExteriorembossAvailable) {
                formContext.getAttribute("tbs_exterioremboss").setValue(false);
                formContext.getControl("tbs_exterioremboss").setDisabled(true);
            }
            else {
                formContext.getControl("tbs_exterioremboss").setDisabled(false);
            }
        }
        catch (error) {
            console.error(error.message);
        }
    },

    EnableDisableInteriorEmboss: async function (formContext) {
        const product = formContext.getAttribute("productid").getValue();

        if (!product)
            return;

        const productId = product[0].id.replace(/[{}]/g, "");

        try {
            const product = await Xrm.WebApi.retrieveRecord("product", productId, "?$select=tbs_interiorembossavailable");

            const isInteriorembossAvailable = product["tbs_interiorembossavailable"];
            if (!isInteriorembossAvailable) {
                formContext.getAttribute("tbs_interioremboss").setValue(false);
                formContext.getControl("tbs_interioremboss").setDisabled(true);
            }
            else {
                formContext.getControl("tbs_interioremboss").setDisabled(false);
            }
        }
        catch (error) {
            console.error(error.message);
        }
    },

    FinishOnChange: async function (executionContext) {
        const formContext = executionContext.getFormContext();

        const hpsFinishIds = Falk.OpportunityProduct.FalkHPSFinishId;

        if (!hpsFinishIds)
            return;

        const interiorFinish = formContext.getAttribute("tbs_interiorfinish")?.getValue();
        const exteriorFinish = formContext.getAttribute("tbs_exteriorfinish")?.getValue();

        const normalizeGuid = (id) => id ? id.replace(/[{}]/g, "").toLowerCase() : null;

        const interiorHpsId = normalizeGuid(hpsFinishIds.Interior);
        const exteriorHpsId = normalizeGuid(hpsFinishIds.Exterior);

        // Interior
        if (interiorFinish && interiorHpsId && normalizeGuid(interiorFinish[0].id) === interiorHpsId) {
            formContext.getAttribute("tbs_interioremboss").setValue(2); //HPS
            formContext.getControl("tbs_interioremboss").setDisabled(true);
        }
        else {
            formContext.getControl("tbs_interioremboss").setDisabled(false);
            formContext.getAttribute("tbs_interioremboss").setValue(null);
        }

        // Exterior
        if (exteriorFinish && exteriorHpsId && normalizeGuid(exteriorFinish[0].id) === exteriorHpsId) {
            formContext.getAttribute("tbs_exterioremboss").setValue(2); //HPS
            formContext.getControl("tbs_exterioremboss").setDisabled(true);
        }
        else {
            formContext.getControl("tbs_exterioremboss").setDisabled(false);
            formContext.getAttribute("tbs_exterioremboss").setValue(null);
        }
    },

    GetEnvironmentVariableValue: async function (schemaName) {
        const defResult = await Xrm.WebApi.retrieveMultipleRecords(
            "environmentvariabledefinition",
            `?$select=environmentvariabledefinitionid,defaultvalue&$filter=schemaname eq '${schemaName}'`
        );
        if (!defResult.entities || defResult.entities.length === 0) {
            console.warn(
                `No EnvironmentVariableDefinition found for schema '${schemaName}'`
            );
            return null;
        }
        const def = defResult.entities[0];
        const definitionId = def.environmentvariabledefinitionid;
        const defaultValue = def.defaultvalue;

        // Query the value entity for the current environment override
        const valResult = await Xrm.WebApi.retrieveMultipleRecords(
            "environmentvariablevalue",
            `?$select=value&$filter=_environmentvariabledefinitionid_value eq ${definitionId}`
        );
        if (valResult.entities && valResult.entities.length > 0) {
            return valResult.entities[0].value;
        }
        // If no override exists, fall back to defaultValue
        return defaultValue;
    },

    SetFieldsFromThickness: async function (formContext) {
        const thickness = formContext.getAttribute("tbs_panelthickness")?.getValue();
        if (!thickness) {
            formContext.getAttribute("tbs_stackheight").setValue(null);
            formContext.getAttribute("tbs_panelsperstack").setValue(null);
            formContext.getAttribute("tbs_widthpanel").setValue(null);
            formContext.getAttribute("tbs_baseprice").setValue(null);
            return;
        }

        const thicknessId = thickness[0].id.replace(/[{}]/g, "");
        const thicknessRecord = await Xrm.WebApi.retrieveRecord("tbs_thickness", thicknessId, "?$select=tbs_stackheight,tbs_maxpanelperstack,tbs_visiblepanelwidth,tbs_baseprice");

        const stackHeight = thicknessRecord["tbs_stackheight"];
        const panelsPerStack = thicknessRecord["tbs_maxpanelperstack"];
        const widthPanel = thicknessRecord["tbs_visiblepanelwidth"];
        const basePrice = thicknessRecord.tbs_baseprice || 0;

        formContext.getAttribute("tbs_stackheight").setValue(stackHeight);
        formContext.getAttribute("tbs_panelsperstack").setValue(panelsPerStack);
        formContext.getAttribute("tbs_widthpanel").setValue(widthPanel);
        await Falk.OpportunityProduct.SetBasePrice(formContext, basePrice);
    },

    SetBasePrice: async function (formContext, basePrice) {
        const tier = formContext.getAttribute("tbs_priceleveltier").getValue();

        if (!tier) {
            formContext.getAttribute("tbs_baseprice").setValue(basePrice);
            return;
        }
        const tierId = tier[0].id.replace(/[{}]/g, "");
        const tierRecord = await Xrm.WebApi.retrieveRecord("tbs_tier", tierId, "?$select=tbs_multiplier");
        const multiplier = tierRecord.tbs_multiplier;
        const calculatedPrice = Number((basePrice * multiplier / 100).toFixed(2));
        formContext.getAttribute("tbs_baseprice").setValue(calculatedPrice);
    }
}