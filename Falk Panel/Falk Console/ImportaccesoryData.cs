using DocumentFormat.OpenXml.Math;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using OfficeOpenXml;
using System;
using System.Activities.Statements;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static Falk_Console.ImportAccessory;
using LicenseContext = OfficeOpenXml.LicenseContext;

namespace Falk_Console
{
    public class ImportaccesoryData
    {
        public static void ImportData(IOrganizationService service)
        {
            try
            {
                var ExcelData = ReadExcelData();

                foreach (var Accessory in ExcelData)
                {
                    try
                    {
                        string Panel = Accessory.Panel;
                        string LegacyDescription = Accessory.LegacyDescription;
                        string Description = Accessory.Description;
                        string Category = Accessory.Category;
                        string SalesID = Accessory.SalesId;
                        string ItemId = Accessory.ItemId;

                        if (!string.IsNullOrEmpty(ItemId))
                        {
                            QueryExpression queryExpressionPricing = new QueryExpression("tbs_accessorypricing");
                            queryExpressionPricing.ColumnSet = new ColumnSet(false);
                            queryExpressionPricing.Criteria.AddCondition("tbs_itemid", ConditionOperator.Equal, ItemId);
                            EntityCollection ItemIdEntColl = service.RetrieveMultiple(queryExpressionPricing);

                            if (ItemIdEntColl.Entities.Count > 0)
                            {
                                Console.WriteLine("Item Id Found " + ItemId);

                                Entity AccessoryPricing = ItemIdEntColl.Entities[0];

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
                                Entity AccessoryRecord = new Entity("tbs_accessory");

                                AccessoryRecord["tbs_salesid"] = SalesID;

                                AccessoryRecord["tbs_name"] = Description;

                                if (!string.IsNullOrEmpty(LegacyDescription))
                                {
                                    AccessoryRecord["tbs_legacydescription"] = LegacyDescription;
                                }

                                if (CategoryEntity != null)
                                {
                                    AccessoryRecord["tbs_itemcategory"] = new EntityReference("tbs_itemcategory", CategoryEntity.Id);
                                }

                                AccessoryRecord["tbs_accessorypricing"] = new EntityReference("tbs_accessorypricing", AccessoryPricing.Id);
                                Guid AccessoryId = Guid.Empty;

                                try
                                {
                                    AccessoryId = service.Create(AccessoryRecord);

                                    Console.WriteLine("Accessory Created Successfully. ID: " + AccessoryId);
                                }
                                catch (Exception ex)
                                {
                                    if (ex.Message.Contains("Entity Key Legacy Description and Description violated"))
                                    {
                                        Console.WriteLine("Duplicate Accessory found. Finding existing record...");
                                        QueryExpression ExistingAccessoryQuery = new QueryExpression("tbs_accessory");

                                        ExistingAccessoryQuery.ColumnSet = new ColumnSet(false);

                                        ExistingAccessoryQuery.Criteria.AddCondition("tbs_legacydescription",ConditionOperator.Equal,LegacyDescription);

                                        ExistingAccessoryQuery.Criteria.AddCondition("tbs_name",ConditionOperator.Equal,Description);

                                        EntityCollection ExistingAccessoryCollection = service.RetrieveMultiple(ExistingAccessoryQuery);

                                        if (ExistingAccessoryCollection.Entities.Count > 0)
                                        {
                                            AccessoryId = ExistingAccessoryCollection.Entities[0].Id;

                                            Console.WriteLine("Existing Accessory found. ID: "+ AccessoryId);
                                        }
                                        else
                                        {
                                            Console.WriteLine("Duplicate exception occurred, but existing Accessory could not be found.");
                                            continue;
                                        }
                                    }
                                    else
                                    {
                                        // Some other exception
                                        Console.WriteLine("Error creating Accessory: "+ ex.Message);
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
                                                        "tbs_accessory",
                                                        AccessoryId,
                                                        new Relationship("tbs_accessory_tbs_thickness_tbs_thickness"),
                                                        new EntityReferenceCollection { new EntityReference("tbs_thickness", ThicknessEntity.Id) }
                                                    );

                                                    Console.WriteLine("Thickness associated successfully: " + Panel + Accessory.Description);
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

        public static List<AccessoryModel> ReadExcelData()
        {
            try
            {
                List<AccessoryModel> TrimList = new List<AccessoryModel>();
                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
                string FilePath = @"C:\Users\admin\Downloads\Final accessories.xlsx";
                using (var Package = new ExcelPackage(new FileInfo(FilePath)))
                {
                    var Worksheet = Package.Workbook.Worksheets["Accessories"];
                    if (Worksheet != null)
                    {
                        int RowCount = Worksheet.Dimension.Rows;
                        for (int Row = 2; Row <= RowCount; Row++) // First row is header
                        {
                            Console.WriteLine("Row: " + Row);
                            var accessory = new AccessoryModel
                            {
                                Panel = Worksheet.Cells[Row, 1].Text,
                                LegacyDescription = Worksheet.Cells[Row, 2].Text,
                                Description = Worksheet.Cells[Row, 3].Text,
                                Category = Worksheet.Cells[Row, 4].Text,
                                SalesId = Worksheet.Cells[Row, 5].Text,
                                ItemId = Worksheet.Cells[Row, 6].Text
                            };
                            TrimList.Add(accessory);
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

        public class AccessoryModel
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
