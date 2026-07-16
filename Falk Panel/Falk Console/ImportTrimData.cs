using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LicenseContext = OfficeOpenXml.LicenseContext;

namespace Falk_Console
{
    public class ImportTrimData
    {
        public static void ImportData(IOrganizationService service)
        {
            var ExcelData = ReadExcelData();

            foreach(var Trim in ExcelData)
            {
                string trimName = Trim.Description;
                string trimDescription = Trim.LegacyDescription;
                string thicknessName = Trim.Panel;

                Guid trimId = GetTrim(service, trimName, trimDescription);
                Guid thicknessId = GetThickness(service, thicknessName);

                if (trimId == Guid.Empty)
                {
                    Console.WriteLine($"Trim not found : {trimName}");
                    continue;
                }

                if (thicknessId == Guid.Empty)
                {
                    Console.WriteLine($"Thickness not found : {thicknessName}");
                    continue;
                }

                Associate(service, trimId, thicknessId);

                Console.WriteLine($"Associated {trimName} -> {thicknessName}");
            }
        }

        public static List<TrimModel> ReadExcelData()
        {
            try
            {
                List<TrimModel> TrimList = new List<TrimModel>();
                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
                string FilePath = @"C:\Users\Niki Patel\Desktop\TrimData.xlsx";
                using (var Package = new ExcelPackage(new FileInfo(FilePath)))
                {
                    var Worksheet = Package.Workbook.Worksheets.FirstOrDefault();
                    if (Worksheet != null)
                    {
                        int RowCount = Worksheet.Dimension.Rows;
                        for (int Row = 2; Row <= RowCount; Row++) // First row is header
                        {
                            Console.WriteLine("Row: " + Row);
                            var Trim = new TrimModel
                            {
                                Panel = Worksheet.Cells[Row, 1].Text,
                                LegacyDescription = Worksheet.Cells[Row, 2].Text,
                                Description = Worksheet.Cells[Row, 3].Text,
                                ItemCat = Worksheet.Cells[Row, 4].Text
                            };
                            TrimList.Add(Trim);
                        }
                    }
                }
                return TrimList;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error in reading data: " + ex.Message);
                return null;
            }
        }

        static Guid GetTrim(IOrganizationService service, string description, string legacyDescription)
        {
            QueryExpression qe = new QueryExpression("tbs_trim");
            qe.ColumnSet = new ColumnSet("tbs_trimid");

            qe.Criteria.AddCondition("tbs_name", ConditionOperator.Equal, description);
            qe.Criteria.AddCondition("tbs_description", ConditionOperator.Equal, legacyDescription);

            var result = service.RetrieveMultiple(qe);

            if (result.Entities.Count > 1)
            {
                Console.WriteLine("skipped");
                Console.WriteLine("description - " + description);
                Console.WriteLine("legacyDescription - " + legacyDescription);
            }
            return result.Entities.Count > 0 ? result.Entities[0].Id : Guid.Empty;
        }

        static Guid GetThickness(IOrganizationService service, string thickness)
        {
            QueryExpression qe = new QueryExpression("tbs_thickness");
            qe.ColumnSet = new ColumnSet(false);

            qe.Criteria.AddCondition("tbs_name", ConditionOperator.Equal, thickness);

            EntityCollection result = service.RetrieveMultiple(qe);

            return result.Entities.Count > 0 ? result.Entities[0].Id : Guid.Empty;
        }

        static void Associate(IOrganizationService service, Guid trimId, Guid thicknessId)
        {
            EntityReferenceCollection related = new EntityReferenceCollection();

            related.Add(new EntityReference("tbs_thickness", thicknessId));

            service.Associate(
                "tbs_trim",
                trimId,
                new Relationship("tbs_trim_tbs_thickness_tbs_thickness"),
                related);
        }

        public class TrimModel
        {
            public string Panel { get; set; }
            public string LegacyDescription { get; set; }
            public string Description { get; set; }
            public string ItemCat { get; set; }
        }
    }
}
