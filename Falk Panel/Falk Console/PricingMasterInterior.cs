using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Falk_Console
{
    public class PricingMasterInterior
    {
        public static void ImportPricingMasterInteriorData(IOrganizationService service)
        {
             var excelData = ReadExcelData();

            if (excelData == null || !excelData.Any())
                return;

            var productLookup = GetLookupDictionary(service, "product", "name");
            var thicknessLookup = GetLookupDictionary(service, "tbs_thickness", "tbs_name");
            var colorLookup = GetLookupDictionary(service, "tbs_color", "tbs_name");
            var finishLookup = GetLookupDictionary(service, "tbs_finish", "tbs_name");
            var gaugeLookup = GetLookupDictionary(service, "tbs_gauge", "tbs_name");

            foreach (var item in excelData)
            {
                try
                {
                    Entity pricingMaster = new Entity("tbs_pricingmasterexterior");

                    if (productLookup.ContainsKey(item.PanelType))
                        pricingMaster["tbs_paneltype"] = new EntityReference("product", productLookup[item.PanelType]);

                    if (thicknessLookup.ContainsKey(item.PanelThickness))
                        pricingMaster["tbs_panelthickness"] = new EntityReference("tbs_thickness", thicknessLookup[item.PanelThickness]);

                    if (colorLookup.ContainsKey(item.InteriorColorCategory))
                        pricingMaster["tbs_exteriorcolorcategory"] = new EntityReference("tbs_color", colorLookup[item.InteriorColorCategory]);

                    if (finishLookup.ContainsKey(item.InteriorFinish))
                        pricingMaster["tbs_exteriorfinish"] = new EntityReference("tbs_finish", finishLookup[item.InteriorFinish]);

                    if (gaugeLookup.ContainsKey(item.InteriorGauge))
                        pricingMaster["tbs_exteriorgauge"] = new EntityReference("tbs_gauge", gaugeLookup[item.InteriorGauge]);

                    if (decimal.TryParse(item.InteriorPrice, out decimal price))
                        pricingMaster["tbs_exteriorprice"] = new Money(price);

                    service.Create(pricingMaster);

                    //Entity pricingMaster = new Entity("tbs_pricingmasterinterior");

                    //if (productLookup.ContainsKey(item.PanelType))
                    //    pricingMaster["tbs_paneltype"] = new EntityReference("product", productLookup[item.PanelType]);

                    //if (thicknessLookup.ContainsKey(item.PanelThickness))
                    //    pricingMaster["tbs_panelthickness"] = new EntityReference("tbs_thickness", thicknessLookup[item.PanelThickness]);

                    //if (colorLookup.ContainsKey(item.InteriorColorCategory))
                    //    pricingMaster["tbs_interiorcolorcategory"] = new EntityReference("tbs_color", colorLookup[item.InteriorColorCategory]);

                    //if (finishLookup.ContainsKey(item.InteriorFinish))
                    //    pricingMaster["tbs_interiorfinish"] = new EntityReference("tbs_finish", finishLookup[item.InteriorFinish]);

                    //if (gaugeLookup.ContainsKey(item.InteriorGauge))
                    //    pricingMaster["tbs_interiorgauge"] = new EntityReference("tbs_gauge", gaugeLookup[item.InteriorGauge]);

                    //if (decimal.TryParse(item.InteriorPrice, out decimal price))
                    //    pricingMaster["tbs_interiorprice"] = new Money(price);

                    //service.Create(pricingMaster);

                    Console.WriteLine($"Imported : {item.PanelThickness} - {item.InteriorFinish} - {item.InteriorGauge} - {item.InteriorColorCategory}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error importing {item.PanelThickness} - {item.InteriorFinish} - {item.InteriorGauge} - {item.InteriorColorCategory} : {ex.Message}");
                }
            }
        }

        public static List<PricingMasterInteriorModel> ReadExcelData()
        {
            try
            {
                List<PricingMasterInteriorModel> PricingMasterInteriorList = new List<PricingMasterInteriorModel>();
                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
                string FilePath = @"C:\Users\Niki Patel\Desktop\PricingMasterExteriorsData.xlsx";
                using (var Package = new ExcelPackage(new FileInfo(FilePath)))
                {
                    var Worksheet = Package.Workbook.Worksheets.FirstOrDefault();
                    if (Worksheet != null)
                    {
                        int RowCount = Worksheet.Dimension.Rows;
                        for (int Row = 2; Row <= RowCount; Row++) // First row is header
                        {
                            Console.WriteLine("Row: " + Row);
                            var pricingMasterInterior = new PricingMasterInteriorModel
                            {
                                PanelType = Worksheet.Cells[Row, 5].Text,
                                PanelThickness = Worksheet.Cells[Row, 6].Text,
                                InteriorColorCategory = Worksheet.Cells[Row, 7].Text,
                                InteriorFinish = Worksheet.Cells[Row, 8].Text,
                                InteriorGauge = Worksheet.Cells[Row, 9].Text,
                                InteriorPrice = Worksheet.Cells[Row, 10].Text,
                            };
                            PricingMasterInteriorList.Add(pricingMasterInterior);
                        }
                    }
                }
                return PricingMasterInteriorList;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error in reading data: " + ex.Message);
                return null;
            }
        }

        private static Dictionary<string, Guid> GetLookupDictionary(IOrganizationService service, string entityName, string primaryField)
        {
            Dictionary<string, Guid> dictionary =
                new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

            QueryExpression query = new QueryExpression(entityName);
            query.ColumnSet = new ColumnSet(primaryField);

            EntityCollection result = service.RetrieveMultiple(query);

            foreach (Entity entity in result.Entities)
            {
                if (entity.Contains(primaryField))
                {
                    string name = entity.GetAttributeValue<string>(primaryField);

                    if (!dictionary.ContainsKey(name))
                        dictionary.Add(name.Trim(), entity.Id);
                }
            }

            return dictionary;
        }

        public class PricingMasterInteriorModel
        {
            public string PanelType { get; set; }
            public string PanelThickness { get; set; }
            public string InteriorColorCategory { get; set; }
            public string InteriorFinish { get; set; }
            public string InteriorGauge { get; set; }
            public string InteriorPrice { get; set; }
        }
    }
}
