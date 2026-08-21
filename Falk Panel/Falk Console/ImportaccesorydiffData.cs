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
    public class ImportaccesorydiffData
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
                        decimal? CostUSD = null;
                        if (Trim.CostUSD != "")
                        {
                            CostUSD = decimal.Parse(Trim.CostUSD.Substring(1));
                        }

                        decimal? Margin = null;
                        if (Trim.Margin != "")
                        {
                            Margin = decimal.Parse(Trim.Margin);
                        }

                        decimal? Price = null;
                        if (Trim.Price != "")
                        {
                            Price = decimal.Parse(Trim.Price.Substring(1));
                        }

                        decimal? CanadaCustomerMargin = null;
                        if (Trim.CanadaCustomerMargin != "")
                        {
                            CanadaCustomerMargin = decimal.Parse(Trim.CanadaCustomerMargin);
                        }

                        decimal? CanadaCustomerPrice = null;
                        if (Trim.CanadaCustomerPrice != "")
                        {
                            CanadaCustomerPrice = decimal.Parse(Trim.CanadaCustomerPrice.Substring(1));
                        }
                        string Unit = Trim.Unit;
                        string Finish = Trim.Finish;
                        string Paint = Trim.Paint;
                        decimal? Weight = null;
                        if (Trim.Weight != "")
                        {
                            decimal.Parse(Trim.Weight);
                        }

                        string ItemType = Trim.ItemType;

                        QueryExpression query = new QueryExpression("tbs_accessorypricing");
                        query.ColumnSet = new ColumnSet("tbs_itemid");
                        query.Criteria.AddCondition("tbs_itemid", ConditionOperator.Equal, ItemID);

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
                            Entity accUpdate = new Entity("tbs_accessorypricing", accEnt.Id);
                            accUpdate["tbs_description"] = Description;
                            if (Trim.CostUSD != "")
                            {
                                accUpdate["tbs_costusd"] = new Money(CostUSD.Value);
                            }
                            accUpdate["tbs_margin"] = Margin;
                            accUpdate["tbs_canadacustomermargin"] = CanadaCustomerMargin;

                            //Unit
                            QueryExpression queryUnit = new QueryExpression("uom");
                            queryUnit.ColumnSet = new ColumnSet(false);
                            queryUnit.Criteria.AddCondition("name", ConditionOperator.Equal, Unit);
                            EntityCollection uom = service.RetrieveMultiple(queryUnit);

                            if (uom.Entities.Count > 0)
                            {
                                accUpdate["tbs_unit"] = new EntityReference("uom", uom.Entities.FirstOrDefault().Id);
                            }

                            //Finish
                            if (Finish == "Exterior Match")
                            {
                                accUpdate["tbs_finish"] = new OptionSetValue(3);
                            }
                            else if (Finish == "Galvanized")
                            {
                                accUpdate["tbs_finish"] = new OptionSetValue(1);
                            }
                            else if (Finish == "Interior Match")
                            {
                                accUpdate["tbs_finish"] = new OptionSetValue(2);
                            }

                            //Paint
                            if (Paint == "FALSE")
                            {
                                accUpdate["tbs_paint"] = false;
                            }
                            else if (Paint == "TRUE")
                            {
                                accUpdate["tbs_paint"] = true;
                            }

                            //Weight
                            accUpdate["tbs_weight"] = Weight;

                            //Item Type
                            if (ItemType == "Item")
                            {
                                accUpdate["tbs_itemtype"] = new OptionSetValue(928530000);
                            }
                            else if (ItemType == "Resource")
                            {
                                accUpdate["tbs_itemtype"] = new OptionSetValue(928530001);

                            }
                            service.Update(accUpdate);
                        }
                        else
                        {
                            Console.WriteLine("item id not found. " + ItemID);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Exception: " + ex.Message);
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
                string FilePath = @"C:\Users\admin\Downloads\Book1.xlsx";
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
                                ItemID = Worksheet.Cells[Row, 1].Text,
                                Description = Worksheet.Cells[Row, 2].Text,
                                CostEUR = Worksheet.Cells[Row, 3].Text,
                                CostUSD = Worksheet.Cells[Row, 4].Text,
                                Margin = Worksheet.Cells[Row, 5].Text,
                                Price = Worksheet.Cells[Row, 6].Text,
                                CanadaCustomerMargin = Worksheet.Cells[Row, 7].Text,
                                CanadaCustomerPrice = Worksheet.Cells[Row, 8].Text,
                                Unit = Worksheet.Cells[Row, 9].Text,
                                Finish = Worksheet.Cells[Row, 10].Text,
                                Paint = Worksheet.Cells[Row, 11].Text,
                                Weight = Worksheet.Cells[Row, 12].Text,
                                ItemType = Worksheet.Cells[Row, 13].Text,
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
            public string CostEUR { get; set; }
            public string CostUSD { get; set; }
            public string Margin { get; set; }
            public string Price { get; set; }
            public string CanadaCustomerMargin { get; set; }
            public string CanadaCustomerPrice { get; set; }
            public string Unit { get; set; }
            public string Finish { get; set; }
            public string Paint { get; set; }
            public string Weight { get; set; }
            public string ItemType { get; set; }
        }
    }
}
