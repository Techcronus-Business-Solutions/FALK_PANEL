if (typeof Falk === "undefined") {
    Falk = {
        __namespace: true
    };
}

if (typeof $ === "undefined") {
    $ = parent.$;
    Jquery = parent.Jquery;
}

Falk.OrderLineItem = {
    OnLoad: async function (executionContext) {
        var formContext = executionContext.getFormContext();

        await Falk.OrderLineItem.SetWidth(formContext);
        formContext.getAttribute("tbs_ft")?.addOnChange(Falk.OrderLineItem.OnChangeFeetInch);
        formContext.getAttribute("tbs_ft")?.addOnChange(Falk.OrderLineItem.ValidateMinimumLength);

        formContext.getAttribute("tbs_in")
            ?.addOnChange(Falk.OrderLineItem.OnChangeFeetInch);

        formContext.getAttribute("tbs_numberofpanels")
            ?.addOnChange(Falk.OrderLineItem.SetSqft);
    },

    OnChangeFeetInch: function (executionContext) {
        var formContext = executionContext.getFormContext();

        var feet = formContext.getAttribute("tbs_ft").getValue() ?? 0;
        var inches = formContext.getAttribute("tbs_in").getValue() ?? 0;

        if (feet === 0 && inches === 0) {
            formContext.getAttribute("tbs_linearftinch").setValue(null);
        }
        else {
            formContext.getAttribute("tbs_linearftinch")
                .setValue(Falk.OrderLineItem.CalculateLinearFtInch(feet, inches));
        }

        Falk.OrderLineItem.SetSqft(executionContext);
    },

    SetSqft: function (executionContext) {
        var formContext = executionContext.getFormContext();

        var feet = formContext.getAttribute("tbs_ft").getValue() ?? 0;
        var inches = formContext.getAttribute("tbs_in").getValue() ?? 0;
        var panels = formContext.getAttribute("tbs_numberofpanels").getValue();
        var width = formContext.getAttribute("tbs_widthpanel").getValue();

        if (panels == null || width == null)
            return;

        var totalSqFt = (panels * ((feet * 12) + inches) * width) / 144;

        formContext.getAttribute("tbs_totalsqft")
            .setValue(Math.round(totalSqFt));
    },

    SetWidth: async function (formContext) {
        var lookup = formContext.getAttribute("tbs_orderproduct").getValue();

        if (!lookup || lookup.length === 0)
            return;

        try {
            var oppProductId = lookup[0].id.replace(/[{}]/g, "");

            var result = await Xrm.WebApi.retrieveRecord(
                "salesorderdetail",
                oppProductId,
                "?$select=salesorderdetailid&$expand=tbs_panelthickness($select=tbs_visiblepanelwidth)"
            );

            if (result.tbs_panelthickness &&
                result.tbs_panelthickness.tbs_visiblepanelwidth != null) {
                formContext.getAttribute("tbs_widthpanel")
                    .setValue(result.tbs_panelthickness.tbs_visiblepanelwidth);
            }

        } catch (e) {
            console.log(e.message);
        }
    },

    CalculateLinearFtInch: function (feet, inches) {
        var wholeFeet = Math.trunc(feet);
        var wholeInches = Math.trunc(inches);

        var fraction = inches - wholeInches;
        var quarter = Math.round(fraction * 4);

        var fractionText = "";

        if (quarter === 4) {
            wholeInches++;
            quarter = 0;
        }

        if (wholeInches === 12) {
            wholeFeet++;
            wholeInches = 0;
        }

        switch (quarter) {
            case 1:
                fractionText = "1/4";
                break;
            case 2:
                fractionText = "1/2"; 
                break;
            case 3:
                fractionText = "3/4";
                break;
        }

        var result = wholeFeet + "' " + wholeInches;

        if (fractionText) {
            result += " " + fractionText;
        }

        result += '"';

        return result;
    },

    ValidateMinimumLength: async function (executionContext) {

        var formContext = executionContext.getFormContext();

        try {

            var lengthAttr = formContext.getAttribute("tbs_ft");

            if (!lengthAttr) {
                console.log("tbs_ft attribute not found.");
                return;
            }

            var length = lengthAttr.getValue();

            console.log("Entered Length: " + length);

            if (length === null || length === undefined) {
                return;
            }

            var orderProductAttr =
                formContext.getAttribute("tbs_orderproduct");

            if (!orderProductAttr) {
                console.log("Order Product attribute not found.");
                return;
            }

            var orderProductValue = orderProductAttr.getValue();

            if (!orderProductValue || orderProductValue.length === 0) {

                console.log("Order Product not selected.");
                return;
            }

            var orderProductId = orderProductValue[0].id.replace(/[{}]/g, "");

            console.log("Order Product ID: " + orderProductId);

            // Get Thickness lookup from Opportunity Product
            var orderProduct = await Xrm.WebApi.retrieveRecord(
                "salesorderdetail",
                orderProductId,
                "?$select=_tbs_panelthickness_value"
            );

            var thicknessId =
                orderProduct["_tbs_panelthickness_value"];

            console.log("Thickness ID: " + thicknessId);

            if (!thicknessId) {
                console.log("Thickness not selected.");
                return;
            }

            // Get Minimum Length from Thickness
            var thickness = await Xrm.WebApi.retrieveRecord(
                "tbs_thickness",
                thicknessId,
                "?$select=tbs_minimumlength"
            );

            var minimumLength =
                thickness["tbs_minimumlength"];

            console.log("Minimum Length: " + minimumLength);

            if (minimumLength === null ||
                minimumLength === undefined) {

                console.log("Minimum Length is empty.");
                return;
            }

            // Validate
            if (length < minimumLength) {

                await Xrm.Navigation.openAlertDialog(
                    {
                        title: "Minimum Length Required",
                        text:
                            "The minimum length for the selected thickness is "
                            + minimumLength
                            + " ft.\n\nPlease enter a length of at least "
                            + minimumLength
                            + " ft."
                    },
                    {
                        height: 220,
                        width: 450
                    }
                );

                // Clear invalid value
                lengthAttr.setValue(null);

                // Recalculate dependent fields
                var linearFtAttr =
                    formContext.getAttribute("tbs_linearftinch");

                if (linearFtAttr) {
                    linearFtAttr.setValue(null);
                }

                return;
            }

            console.log("Minimum length validation passed.");

        }
        catch (error) {

            console.error(
                "ValidateMinimumLength Error: ",
                error
            );

            await Xrm.Navigation.openAlertDialog({
                title: "Validation Error",
                text:
                    "Unable to validate minimum length.\n\n"
                    + error.message
            });
        }
    }
};