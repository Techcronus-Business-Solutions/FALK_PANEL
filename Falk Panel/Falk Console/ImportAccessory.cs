using ClosedXML.Excel;
using DocumentFormat.OpenXml.Drawing.Diagrams;
using ExcelDataReader.Log;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Falk_Console
{
    public class ImportAccessory
    {

        public static string inputFile = "C:\\Users\\admin\\Desktop\\Book1.xlsx";
        //public static string outputFile = "C:\\Users\\admin\\Desktop\\Thrive Tribe\\Migration 18-4\\Output\\quit-swaptostop-update-report-" + DateTime.Now.ToString("yyyyMMdd_HHmm") + ".xlsx";

        public static void importAccessory(IOrganizationService service)
        {
            //QueryExpression query = new QueryExpression("tbs_accessory");
            //query.ColumnSet = new ColumnSet(true);
            //EntityCollection AccessoriesCRM = service.RetrieveMultiple(query);

            List<Accessory> accessories = ReadExcel();

            int i = 1;

            foreach (Accessory Accessory in accessories)
            {
                i++;
                Console.WriteLine("Row: " + i);

                try
                {
                    Console.WriteLine("Currenty processing - Description:- " + Accessory.Description);

                    QueryExpression query = new QueryExpression("tbs_accessory");
                    query.ColumnSet = new ColumnSet("tbs_accessoryid", "tbs_salesid", "tbs_name", "tbs_legacydescription", "tbs_itemcategory", "tbs_accessorypricing");
                    query.Criteria.AddCondition("tbs_name", ConditionOperator.Equal, Accessory.Description);

                    EntityCollection AccessoriesCRM = service.RetrieveMultiple(query);

                    if (AccessoriesCRM.Entities.Count == 0)
                    {
                        Console.WriteLine("Accessory not found: " + Accessory.Description);

                        QueryExpression query1 = new QueryExpression("tbs_accessorypricing");
                        query1.Criteria.AddCondition("tbs_itemid", ConditionOperator.Equal, Accessory.ItemId);
                        query1.ColumnSet = new ColumnSet(true);
                        EntityCollection accessoryPricing = service.RetrieveMultiple(query1);

                        if(accessoryPricing.Entities.Count > 0)
                        {
                            Entity accessoryNew = new Entity("tbs_accessory");

                            accessoryNew["tbs_salesid"] = Accessory.SalesId;
                            accessoryNew["tbs_name"] = Accessory.Description;
                            accessoryNew["tbs_accessorypricing"] = accessoryPricing.Entities.FirstOrDefault().ToEntityReference();
                            Guid accessoryId = service.Create(accessoryNew);

                            Console.WriteLine("New Accessory Created - " + accessoryId);
                            continue;
                        }
                        else
                        {
                            continue;
                        }
                    }

                    if (AccessoriesCRM.Entities.Count > 1)
                    {
                        Console.WriteLine("Multiple accessories found: " + Accessory.Description);

                        foreach (Entity entity in AccessoriesCRM.Entities)
                        {
                            Console.WriteLine("ID: " + entity.Id);
                        }
                        continue;
                    }

                    Entity accessoryCRM = AccessoriesCRM.Entities.First();

                    Console.WriteLine("Accessory found: " + accessoryCRM.Id);

                    QueryExpression query3 = new QueryExpression("tbs_accessorypricing");
                    query3.Criteria.AddCondition("tbs_itemid", ConditionOperator.Equal, Accessory.ItemId);
                    query3.ColumnSet = new ColumnSet(true);
                    EntityCollection accessoryPricing1 = service.RetrieveMultiple(query3);

                    EntityCollection category = null;
                    if (Accessory.Category != string.Empty)
                    {
                        QueryExpression query2 = new QueryExpression("tbs_itemcategory");
                        query2.ColumnSet = new ColumnSet(true);
                        query2.Criteria.AddCondition("tbs_categoryname", ConditionOperator.Equal, Accessory.Category);
                        query2.Criteria.AddCondition("tbs_type", ConditionOperator.Equal, 1);
                        category = service.RetrieveMultiple(query2);
                    }

                    Entity accessoryUpdate = new Entity("tbs_accessory", accessoryCRM.Id);

                    accessoryUpdate["tbs_salesid"] = Accessory.SalesId;
                    accessoryUpdate["tbs_name"] = Accessory.Description;
                    accessoryUpdate["tbs_legacydescription"] = Accessory.LeagcyDescription;
                    if (category != null)
                    {

                        accessoryUpdate["tbs_itemcategory"] = category.Entities.FirstOrDefault().ToEntityReference();
                    }
                    accessoryUpdate["tbs_accessorypricing"] = accessoryPricing1.Entities.FirstOrDefault().ToEntityReference();

                    service.Update(accessoryUpdate);

                    Console.WriteLine("Updated successfully - " + accessoryCRM.Id);
                }
                catch (Exception e)
                {
                    continue;
                }
            }
        }

        public static List<Accessory> ReadExcel()
        {
            Console.WriteLine("Starting");

            var List = new List<Accessory>();

            using (var workbook = new XLWorkbook(inputFile))
            {
                var worksheet = workbook.Worksheet(1);
                var rows = worksheet.RangeUsed().RowsUsed();

                int total = rows.Count();
                int processedCount = 0;

                foreach (var row in rows.Skip(1)) // skip header
                {
                    processedCount++;
                    var description = row.Cell(5).GetValue<string>();
                    var salesid = row.Cell(7).GetValue<string>();
                    var itemid = row.Cell(8).GetValue<string>();
                    var legacyDescription = row.Cell(4).GetValue<string>();
                    var category = row.Cell(6).GetValue<string>();
                    List.Add(new Accessory
                    {
                        Description = description,
                        SalesId = salesid,
                        ItemId = itemid,
                        LeagcyDescription = legacyDescription,
                        Category = category
                        //MethodOfQuit = new OptionSetValue(methodOfQuit)
                    });
                }
            }

            return List;
        }

        public class Accessory
        {
            public string Description { get; set; }
            public string LeagcyDescription { get; set; }
            public string Category { get; set; }
            public string SalesId { get; set; }
            public string ItemId { get; set; }
        }
    }
}

