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
    public class ImportaccesoryData
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
                        string ItemID = Trim.ItemID;
                        string Description = Trim.Description;
                        string SalesID = Trim.SalesId;

                        QueryExpression queryExpression = new QueryExpression("tbs_accessorypricing");
                        queryExpression.ColumnSet = new ColumnSet(false);
                        queryExpression.Criteria.AddCondition("tbs_itemid", ConditionOperator.Equal, ItemID);
                        EntityCollection ItemId = service.RetrieveMultiple(queryExpression);

                        if (ItemId.Entities.Count > 0)
                        {
                            QueryExpression query = new QueryExpression("tbs_accessory");
                            query.ColumnSet = new ColumnSet(true);
                            query.Criteria.AddCondition("tbs_salesid", ConditionOperator.Equal, SalesID);
                            query.Criteria.AddCondition("tbs_name", ConditionOperator.Equal, Description);

                            query.Criteria.AddCondition("tbs_accessorypricing", ConditionOperator.Equal, ItemId.Entities.FirstOrDefault().Id);

                            EntityCollection acc = service.RetrieveMultiple(query);

                            if (acc.Entities.Count > 1)
                            {
                                Console.WriteLine(ItemID);
                                Console.WriteLine("Multiple Records found");
                                foreach (var entity in acc.Entities)
                                {
                                    Console.WriteLine(entity.Id);
                                }
                                continue;
                            }

                            Entity accEnt = acc.Entities.FirstOrDefault();

                            if (accEnt != null)
                            {
                                Console.WriteLine("Match Found for - " + Description);
                            }
                            else
                            {
                                Console.WriteLine("Match Not Found for - " + Description);
                            }
                        }
                        else
                        {
                            Console.WriteLine("Item Id not found " + ItemID);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                }
                Console.WriteLine("Complete");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        public static List<AccessoryPricingModel> ReadExcelData()
        {
            try
            {
                List<AccessoryPricingModel> TrimList = new List<AccessoryPricingModel>();
                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
                string FilePath = @"C:\Users\admin\Downloads\Old vs new accessories.xlsx";
                using (var Package = new ExcelPackage(new FileInfo(FilePath)))
                {
                    var Worksheet = Package.Workbook.Worksheets.FirstOrDefault();
                    if (Worksheet != null)
                    {
                        int RowCount = Worksheet.Dimension.Rows;
                        for (int Row = 2; Row <= RowCount; Row++) // First row is header
                        {
                            Console.WriteLine("Row: " + Row);
                            var accessory = new AccessoryPricingModel
                            {
                                Description = Worksheet.Cells[Row, 1].Text,
                                SalesId = Worksheet.Cells[Row, 2].Text,
                                ItemID = Worksheet.Cells[Row, 3].Text
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

        public class AccessoryPricingModel
        {
            public string ItemID { get; set; }
            public string Description { get; set; }
            public string SalesId { get; set; }
        }
    }
}
