if (typeof Falk === "undefined") {
    Falk = {
        __namespace: true,
    };
}
if (typeof $ === "undefined") {
    $ = parent.$;
    Jquery = parent.Jquery;
}

Falk.Lead = {

    // ---- Country validation config ----
    COUNTRY_FIELD: "address1_country",

    COUNTRY_MAP: {
        AD: "Andorra", AE: "United Arab Emirates", AL: "Albania", AM: "Armenia", AO: "Angola",
        AR: "Argentina", AT: "Austria", AU: "Australia", AZ: "Azerbaijan", BA: "Bosnia and Herzegovina",
        BD: "Bangladesh", BE: "Belgium", BG: "Bulgaria", BH: "Bahrain", BM: "Bermuda",
        BN: "Brunei Darussalam", BO: "Bolivia", BR: "Brazil", BS: "Bahamas", BW: "Botswana",
        CA: "Canada", CH: "Switzerland", CL: "Chile", CM: "Cameroon", CN: "China",
        CO: "Colombia", CR: "Costa Rica", CY: "Cyprus", CZ: "Czechia", DE: "Germany",
        DK: "Denmark", DO: "Dominican Republic", DZ: "Algeria", EC: "Ecuador", EE: "Estonia",
        EG: "Egypt", EL: "Greece", ES: "Spain", ET: "Ethiopia", FI: "Finland",
        FJ: "Fiji Islands", FO: "Faroe Islands", FR: "France", GB: "Great Britain", GE: "Georgia",
        GG: "Guernsey", GH: "Ghana", GL: "Greenland", GT: "Guatemala", HK: "Hong Kong",
        HN: "Honduras", HR: "Croatia", HU: "Hungary", ID: "Indonesia", IE: "Ireland",
        IL: "Israel", IM: "Isle of Man", IN: "India", IS: "Iceland", IT: "Italy",
        JE: "Jersey", JM: "Jamaica", JO: "Jordan", JP: "Japan", KE: "Kenya",
        KH: "Cambodia", KR: "South Korea", KW: "Kuwait", KY: "Cayman Islands", KZ: "Kazakhstan",
        LB: "Lebanon", LI: "Liechtenstein", LK: "Sri Lanka", LT: "Lithuania", LU: "Luxembourg",
        LV: "Latvia", MA: "Morocco", MC: "Monaco", ME: "Montenegro", MG: "Madagascar",
        MK: "North Macedonia", MN: "Mongolia", MO: "Macao", MT: "Malta", MU: "Mauritius",
        MV: "Maldives", MW: "Malawi", MX: "Mexico", MY: "Malaysia", MZ: "Mozambique",
        NA: "Namibia", NG: "Nigeria", NI: "Nothern Ireland", NL: "Netherlands", NO: "Norway",
        NP: "Nepal", NZ: "New Zealand", OM: "Oman", PA: "Panama", PE: "Peru",
        PH: "Philippines", PK: "Pakistan", PL: "Poland", PR: "Puerto Rico", PT: "Portugal",
        PY: "Paraguay", QA: "Qatar", RO: "Romania", RS: "Serbia", RU: "Russia",
        SA: "Saudi Arabia", SB: "Solomon Islands", SE: "Sweden", SG: "Singapore", SI: "Slovenia",
        SK: "Slovakia", SM: "San Marino", SN: "Senegal", ST: "São Tomé and Príncipe", SV: "El Salvador",
        SZ: "Swaziland", TH: "Thailand", TN: "Tunisia", TR: "Türkiye", TT: "Trinidad and Tobago",
        TW: "Taiwan", TZ: "Tanzania", UA: "Ukraine", UG: "Uganda", US: "USA",
        UY: "Uruguay", VG: "British Virgin Islands", VN: "Vietnam", VU: "Vanuatu", WS: "Samoa",
        XK: "Kosovo", ZA: "South Africa", ZM: "Zambia", ZW: "Zimbabwe"
    },

    // Extra common names people may type -> code
    COUNTRY_ALIASES: {
        "UNITED STATES": "US", "UNITED STATES OF AMERICA": "US", "AMERICA": "US",
        "UNITED KINGDOM": "GB", "UK": "GB", "ENGLAND": "GB", "SCOTLAND": "GB", "WALES": "GB",
        "GREECE": "EL", "GR": "EL", "TURKEY": "TR", "SWAZILAND": "SZ", "ESWATINI": "SZ",
        "UAE": "AE", "CZECH REPUBLIC": "CZ", "KOREA": "KR", "REPUBLIC OF KOREA": "KR",
        "NORTHERN IRELAND": "NI", "BRUNEI": "BN", "FIJI": "FJ", "MACAU": "MO",
        "SAO TOME AND PRINCIPE": "ST", "MACEDONIA": "MK"
    },
    OnLoad: function (executionContext) {
        var formContext = executionContext.getFormContext();

        formContext.getAttribute("tbs_existingcustomer")
            .addOnChange(Falk.Lead.OnChange);

        Falk.Lead.OnChange(executionContext);

        var ownerAttribute = formContext.getAttribute("ownerid");
        if (ownerAttribute != null) {
            ownerAttribute.addOnChange(Falk.Lead.OnCurrencyChange);
        }

        // 3. Set Territory on initial load
        Falk.Lead.setTerritoryFromOwner(formContext);

        // 4. Country validation
        var countryAttribute = formContext.getAttribute(Falk.Lead.COUNTRY_FIELD);
        if (countryAttribute != null) {
            countryAttribute.addOnChange(Falk.Lead.OnCountryChange);
            Falk.Lead.validateCountry(formContext);
        }
        formContext.data.entity.addOnSave(Falk.Lead.OnSave);
    },

    OnChange: function (executionContext) {
        var formContext = executionContext.getFormContext();
        var isExistingCustomer = formContext.getAttribute("tbs_existingcustomer").getValue();
        var section = formContext.ui.tabs.get("Summary")
            .sections.get("Existing_Customer_Section");
        section.setVisible(isExistingCustomer === true);
    },

    OnCurrencyChange: function (executionContext) {
        var formContext = executionContext.getFormContext();
        Falk.Lead.setTerritoryFromOwner(formContext);
    },

    OnCountryChange: function (executionContext) {
        var formContext = executionContext.getFormContext();
        Falk.Lead.validateCountry(formContext);
    },

    OnSave: function (executionContext) {
        var formContext = executionContext.getFormContext();
        var isValid = Falk.Lead.validateCountry(formContext);
        if (!isValid) {
            executionContext.getEventArgs().preventDefault();
        }
    },

    validateCountry: function (formContext) {
        var notificationId = "country_validation";
        var attribute = formContext.getAttribute(Falk.Lead.COUNTRY_FIELD);

        if (attribute == null) {
            return true;
        }

        var control = formContext.getControl(Falk.Lead.COUNTRY_FIELD)
            || formContext.getControl("address1_composite_compositionLinkControl_" + Falk.Lead.COUNTRY_FIELD)
            || (attribute.controls.getLength() > 0 ? attribute.controls.get(0) : null);

        var showError = function (msg) {
            if (control != null) {
                control.setNotification(msg, notificationId);
            } else {
                formContext.ui.setFormNotification(msg, "ERROR", notificationId);
            }
        };
        var clearError = function () {
            if (control != null) {
                control.clearNotification(notificationId);
            }
            formContext.ui.clearFormNotification(notificationId);
        };

        clearError();

        var value = attribute.getValue();
        if (value == null || String(value).trim() === "") {
            return true;
        }

        var typed = String(value).trim();
        var upper = typed.toUpperCase();

        if (typed === upper && Falk.Lead.COUNTRY_MAP.hasOwnProperty(upper)) {
            return true;
        }

        if (Falk.Lead.COUNTRY_MAP.hasOwnProperty(upper)) {
            showError("Please use uppercase code: " + upper + " (" + Falk.Lead.COUNTRY_MAP[upper] + ")");
            return false;
        }

        var suggestion = null;
        if (Falk.Lead.COUNTRY_ALIASES.hasOwnProperty(upper)) {
            suggestion = Falk.Lead.COUNTRY_ALIASES[upper];
        } else {
            for (var code in Falk.Lead.COUNTRY_MAP) {
                if (Falk.Lead.COUNTRY_MAP[code].toUpperCase() === upper) {
                    suggestion = code;
                    break;
                }   
            }
        }

        if (suggestion) {
            showError("Please add " + suggestion + " (" + Falk.Lead.COUNTRY_MAP[suggestion] + ") instead of \"" + typed + "\".");
        } else {
            showError("\"" + typed + "\" is not a valid country code. Please enter a valid code from the allowed list (e.g. US, IN, GB).");
        }
        return false;
    },

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
