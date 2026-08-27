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
    public class ImportTrim
    {

        public static string inputFile = "C:\\Users\\admin\\Desktop\\Book1.xlsx";
        //public static string outputFile = "C:\\Users\\admin\\Desktop\\Thrive Tribe\\Migration 18-4\\Output\\quit-swaptostop-update-report-" + DateTime.Now.ToString("yyyyMMdd_HHmm") + ".xlsx";

        public static void importTrim(IOrganizationService service)
        {
            //QueryExpression query = new QueryExpression("tbs_accessory");
            //query.ColumnSet = new ColumnSet(true);
            //EntityCollection AccessoriesCRM = service.RetrieveMultiple(query);

            List<Trim> trims = ReadExcel();

            int i = 1;

            foreach (Trim Trim in trims)
            {
                i++;
                Console.WriteLine("Row: " + i);

                try
                {
                    Console.WriteLine("Currenty processing - Description:- " + Trim.Description);

                    QueryExpression query = new QueryExpression("tbs_trim");
                    //query.ColumnSet = new ColumnSet("tbs_trimid", "tbs_salesid", "tbs_name", "tbs_description", "tbs_itemcategory", "tbs_trimpricing");
                    query.Criteria.AddCondition("tbs_name", ConditionOperator.Equal, Trim.Description);

                    EntityCollection trimsCRM = service.RetrieveMultiple(query);

                    if (trimsCRM.Entities.Count == 0)
                    {
                        Console.WriteLine("Trim not found: " + Trim.Description);

                        QueryExpression query1 = new QueryExpression("tbs_trimpricing");
                        query1.Criteria.AddCondition("tbs_itemid", ConditionOperator.Equal, Trim.ItemId);
                        query1.ColumnSet = new ColumnSet(true);
                        EntityCollection trimPricing = service.RetrieveMultiple(query1);

                        if (trimPricing.Entities.Count > 0)
                        {
                            Entity accessoryNew = new Entity("tbs_trim");

                            accessoryNew["tbs_salesid"] = Trim.SalesId;
                            accessoryNew["tbs_name"] = Trim.Description;
                            accessoryNew["tbs_trimpricing"] = trimPricing.Entities.FirstOrDefault().ToEntityReference();
                            Guid accessoryId = service.Create(accessoryNew);

                            Console.WriteLine("New Trim Created - " + accessoryId);
                            continue;
                        }
                        else
                        {
                            Console.WriteLine("Trim Pricing Needed - " + Trim.ItemId);
                            continue;
                        }
                    }

                    if (trimsCRM.Entities.Count > 1)
                    {
                        Console.WriteLine("Multiple accessories found: " + Trim.Description);

                        foreach (Entity entity in trimsCRM.Entities)
                        {
                            Console.WriteLine("ID: " + entity.Id);
                        }
                        continue;
                    }

                    Entity trimCRM = trimsCRM.Entities.First();

                    Console.WriteLine("Trim found: " + trimCRM.Id);

                    QueryExpression query3 = new QueryExpression("tbs_trimpricing");
                    query3.Criteria.AddCondition("tbs_itemid", ConditionOperator.Equal, Trim.ItemId);
                    query3.ColumnSet = new ColumnSet(true);
                    EntityCollection trimPricing1 = service.RetrieveMultiple(query3);

                    EntityCollection category = null;
                    if (Trim.Category != string.Empty)
                    {
                        QueryExpression query2 = new QueryExpression("tbs_itemcategory");
                        query2.ColumnSet = new ColumnSet(true);
                        query2.Criteria.AddCondition("tbs_categoryname", ConditionOperator.Equal, Trim.Category);
                        query2.Criteria.AddCondition("tbs_type", ConditionOperator.Equal, 2);
                        category = service.RetrieveMultiple(query2);
                    }

                    Entity trimUpdate = new Entity("tbs_trim", trimCRM.Id);

                    trimUpdate["tbs_salesid"] = Trim.SalesId;
                    trimUpdate["tbs_name"] = Trim.Description;
                    trimUpdate["tbs_description"] = Trim.LeagcyDescription;
                    if (category != null && category.Entities.Count > 0)
                    {

                        trimUpdate["tbs_itemcategory"] = category.Entities.FirstOrDefault().ToEntityReference();
                    }
                    if (trimPricing1.Entities.Count > 0)
                    {
                        Console.WriteLine("Trim Pricing Found - " + Trim.ItemId);

                        trimUpdate["tbs_trimpricing"] = trimPricing1.Entities.FirstOrDefault().ToEntityReference();
                    }

                    service.Update(trimUpdate);

                    Console.WriteLine("Updated successfully - " + trimCRM.Id);
                }
                catch (Exception e)
                {
                    continue;
                }
            }
        }

        public static List<Trim> ReadExcel()
        {
            Console.WriteLine("Starting");

            var List = new List<Trim>();

            using (var workbook = new XLWorkbook(inputFile))
            {
                var worksheet = workbook.Worksheet(4);
                var rows = worksheet.RangeUsed().RowsUsed();

                int total = rows.Count();
                int processedCount = 0;

                foreach (var row in rows.Skip(1)) // skip header
                {
                    processedCount++;
                    var description = row.Cell(5).GetValue<string>();
                    var salesid = row.Cell(8).GetValue<string>();
                    var itemid = row.Cell(9).GetValue<string>();
                    var legacyDescription = row.Cell(4).GetValue<string>();
                    var category = row.Cell(7).GetValue<string>();
                    List.Add(new Trim
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

        public class Trim
        {
            public string Description { get; set; }
            public string LeagcyDescription { get; set; }
            public string Category { get; set; }
            public string SalesId { get; set; }
            public string ItemId { get; set; }
        }
    }
}

