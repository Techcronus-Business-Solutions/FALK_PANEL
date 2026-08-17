if (typeof Falk === "undefined") {
    Falk = {
        __namespace: true
    };
}

if (typeof $ === "undefined") {
    $ = parent.$;
    Jquery = parent.Jquery;
}

Falk.QuoteLineItem = {
    OnLoad: async function (executionContext) {
        var formContext = executionContext.getFormContext();

        await Falk.QuoteLineItem.SetWidth(formContext);
        formContext.getAttribute("tbs_ft")
            ?.addOnChange(Falk.QuoteLineItem.OnChangeFeetInch);

        formContext.getAttribute("tbs_in")
            ?.addOnChange(Falk.QuoteLineItem.OnChangeFeetInch);

        formContext.getAttribute("tbs_numberofpanels")
            ?.addOnChange(Falk.QuoteLineItem.SetSqft);
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
                .setValue(Falk.QuoteLineItem.CalculateLinearFtInch(feet, inches));
        }

        Falk.QuoteLineItem.SetSqft(executionContext);
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
        var lookup = formContext.getAttribute("tbs_quoteproduct").getValue();

        if (!lookup || lookup.length === 0)
            return;

        try {
            var oppProductId = lookup[0].id.replace(/[{}]/g, "");

            var result = await Xrm.WebApi.retrieveRecord(
                "quotedetail",
                oppProductId,
                "?$select=quotedetailid&$expand=tbs_panelthickness($select=tbs_visiblepanelwidth)"
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
    }
};