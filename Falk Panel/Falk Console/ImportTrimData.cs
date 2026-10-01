using DocumentFormat.OpenXml.Math;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using OfficeOpenXml;
using System;
using System.Activities.Statements;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LicenseContext = OfficeOpenXml.LicenseContext;

namespace Falk_Console
{
    public class ImporttrimData
    {
        public static void ImportData(IOrganizationService service)
        {
            try
            {
                var ExcelData = ReadExcelData();

                foreach (var Trim in ExcelData)
                {
                    try
                    {
                        string Panel = Trim.Panel;
                        string LegacyDescription = Trim.LegacyDescription;
                        string Description = Trim.Description;
                        string Category = Trim.Category;
                        string SalesID = Trim.SalesId;
                        string ItemId = Trim.ItemId;

                        if (!string.IsNullOrEmpty(ItemId))
                        {
                            QueryExpression queryExpressionPricing = new QueryExpression("tbs_trimpricing");
                            queryExpressionPricing.ColumnSet = new ColumnSet(false);
                            queryExpressionPricing.Criteria.AddCondition("tbs_itemid", ConditionOperator.Equal, ItemId);
                            EntityCollection ItemIdEntColl = service.RetrieveMultiple(queryExpressionPricing);

                            if (ItemIdEntColl.Entities.Count > 0)
                            {
                                Console.WriteLine("Item Id Found " + ItemId);

                                Entity TrimPricing = ItemIdEntColl.Entities[0];

                                Entity CategoryEntity = null;

                                if (!string.IsNullOrEmpty(Category))
                                {
                                    QueryExpression queryExpressionCategory = new QueryExpression("tbs_itemcategory");
                                    queryExpressionCategory.ColumnSet = new ColumnSet(false);
                                    queryExpressionCategory.Criteria.AddCondition("tbs_categoryname", ConditionOperator.Equal, Category);
                                    EntityCollection CategoryEntColl = service.RetrieveMultiple(queryExpressionCategory);

                                    if (CategoryEntColl.Entities.Count > 0)
                                    {
                                        CategoryEntity = CategoryEntColl.Entities[0];

                                        Console.WriteLine("Category Found: " + Category);
                                    }
                                    else
                                    {
                                        Console.WriteLine("Category not found: " + Category);
                                    }
                                }
                                Entity TrimRecord = new Entity("tbs_trim");

                                TrimRecord["tbs_salesid"] = SalesID;

                                TrimRecord["tbs_name"] = Description;

                                if (!string.IsNullOrEmpty(LegacyDescription))
                                {
                                    TrimRecord["tbs_description"] = LegacyDescription;
                                }

                                if (CategoryEntity != null)
                                {
                                    TrimRecord["tbs_itemcategory"] = new EntityReference("tbs_itemcategory", CategoryEntity.Id);
                                }

                                TrimRecord["tbs_trimpricing"] = new EntityReference("tbs_trimpricing", TrimPricing.Id);
                                Guid TrimId = Guid.Empty;

                                try
                                {
                                    TrimId = service.Create(TrimRecord);

                                    Console.WriteLine("Trim Created Successfully. ID: " + TrimId);
                                }
                                catch (Exception ex)
                                {
                                    if (ex.Message.Contains("Entity Key Legacy Description and Description violated"))
                                    {
                                        Console.WriteLine("Duplicate trim found. Finding existing record...");
                                        QueryExpression ExistingTrimQuery = new QueryExpression("tbs_trim");

                                        ExistingTrimQuery.ColumnSet = new ColumnSet(false);

                                        ExistingTrimQuery.Criteria.AddCondition("tbs_description", ConditionOperator.Equal, LegacyDescription);

                                        ExistingTrimQuery.Criteria.AddCondition("tbs_name", ConditionOperator.Equal, Description);

                                        EntityCollection ExistingTrimCollection = service.RetrieveMultiple(ExistingTrimQuery);

                                        if (ExistingTrimCollection.Entities.Count > 0)
                                        {
                                            TrimId = ExistingTrimCollection.Entities[0].Id;

                                            Console.WriteLine("Existing Trim found. ID: " + TrimId);
                                        }
                                        else
                                        {
                                            Console.WriteLine("Duplicate exception occurred, but existing Trim could not be found.");
                                            continue;
                                        }
                                    }
                                    else
                                    {
                                        // Some other exception
                                        Console.WriteLine("Error creating Trim: " + ex.Message);
                                        continue;
                                    }
                                }
                                if (!string.IsNullOrEmpty(Panel))
                                {
                                    string[] PanelParts = Panel.Trim().Split(' ');

                                    if (PanelParts.Length >= 2)
                                    {
                                        string ThicknessNumberText = PanelParts[PanelParts.Length - 1];
                                        string PanelTypeName = string.Join(" ", PanelParts.Take(PanelParts.Length - 1));

                                        Console.WriteLine("Panel Type: " + PanelTypeName);

                                        Console.WriteLine("Thickness Number: " + ThicknessNumberText);

                                        // 5. Convert Thickness Number
                                        decimal ThicknessNumber;
                                        if (decimal.TryParse(ThicknessNumberText, out ThicknessNumber))
                                        {
                                            // 6. Find Panel Type
                                            QueryExpression PanelTypeQuery = new QueryExpression("product");

                                            PanelTypeQuery.ColumnSet = new ColumnSet(false);

                                            PanelTypeQuery.Criteria.AddCondition("name", ConditionOperator.Equal, PanelTypeName);

                                            EntityCollection PanelTypeCollection = service.RetrieveMultiple(PanelTypeQuery);

                                            if (PanelTypeCollection.Entities.Count > 0)
                                            {
                                                Entity PanelTypeEntity = PanelTypeCollection.Entities[0];

                                                Console.WriteLine("Panel Type Found: " + PanelTypeName);
                                                // 7. Find Thickness

                                                QueryExpression ThicknessQuery = new QueryExpression("tbs_thickness");

                                                ThicknessQuery.ColumnSet = new ColumnSet(false);
                                                // Thickness Number
                                                ThicknessQuery.Criteria.AddCondition("tbs_thicknessnumber", ConditionOperator.Equal, ThicknessNumber);

                                                // Panel Type Lookup
                                                ThicknessQuery.Criteria.AddCondition("tbs_product", ConditionOperator.Equal, PanelTypeEntity.Id);

                                                EntityCollection ThicknessCollection = service.RetrieveMultiple(ThicknessQuery);

                                                if (ThicknessCollection.Entities.Count > 0)
                                                {
                                                    Entity ThicknessEntity = ThicknessCollection.Entities[0];

                                                    Console.WriteLine("Thickness Found: " + Panel);

                                                    // 8. Establish N:N Relationship

                                                    service.Associate(
                                                        "tbs_trim",
                                                        TrimId,
                                                        new Relationship("tbs_trim_tbs_thickness_tbs_thickness"),
                                                        new EntityReferenceCollection { new EntityReference("tbs_thickness", ThicknessEntity.Id) }
                                                    );

                                                    Console.WriteLine("Thickness associated successfully: " + Panel + Trim.Description);
                                                }
                                                else
                                                {
                                                    Console.WriteLine("Thickness not found. " + "Panel Type: " + PanelTypeName + ", Thickness Number: " + ThicknessNumber);
                                                }
                                            }
                                            else
                                            {
                                                Console.WriteLine("Panel Type not found: " + PanelTypeName);
                                            }
                                        }
                                        else
                                        {
                                            Console.WriteLine("Invalid Thickness Number: " + ThicknessNumberText);
                                        }
                                    }
                                    else
                                    {
                                        Console.WriteLine("Invalid Panel format: " + Panel);
                                    }
                                }
                            }
                            else
                            {
                                Console.WriteLine("Item Id not found " + ItemId);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        if (ex.Message == "Entity Key Legacy Description and Description violated. A record with the same value for Legacy Description, Description already exists. A duplicate record cannot be created. Select one or more unique values and try again.")
                        {
                            Console.WriteLine(ex.Message);
                            continue;
                        }
                    }
                }
                Console.WriteLine("Complete");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        public static List<TrimModel> ReadExcelData()
        {
            try
            {
                List<TrimModel> TrimList = new List<TrimModel>();
                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
                string FilePath = @"C:\Users\admin\Downloads\Final accessories.xlsx";
                using (var Package = new ExcelPackage(new FileInfo(FilePath)))
                {
                    var Worksheet = Package.Workbook.Worksheets["Trims"];
                    if (Worksheet != null)
                    {
                        int RowCount = Worksheet.Dimension.Rows;
                        for (int Row = 2; Row <= RowCount; Row++) // First row is header
                        {
                            Console.WriteLine("Row: " + Row);
                            var trim = new TrimModel
                            {
                                Panel = Worksheet.Cells[Row, 1].Text,
                                LegacyDescription = Worksheet.Cells[Row, 2].Text,
                                Description = Worksheet.Cells[Row, 3].Text,
                                Category = Worksheet.Cells[Row, 4].Text,
                                SalesId = Worksheet.Cells[Row, 5].Text,
                                ItemId = Worksheet.Cells[Row, 6].Text
                            };
                            TrimList.Add(trim);
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

        public class TrimModel
        {
            public string Panel { get; set; }
            public string LegacyDescription { get; set; }
            public string Description { get; set; }
            public string Category { get; set; }
            public string SalesId { get; set; }
            public string ItemId { get; set; }
        }
    }
}