using ClosedXML.Excel;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Tooling.Connector;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Falk_Console
{
    internal class Program
    {
        public static void Main(string[] args)
        {
            try
            {
                using (CrmServiceClient organizationService = Connection.CreateConnection())
                {
                    Console.WriteLine("Console Application Started!");

                    //string excelPath = @"C:\Users\admin\Desktop\Book1.xlsx";

                    //var workbook = new XLWorkbook(excelPath);
                    //var sheet = workbook.Worksheet(1);

                    //foreach (var row in sheet.RowsUsed().Skip(1))
                    //{
                    //    string accessoryDescription = row.Cell("E").GetString().Trim();
                    //    string legacyDescription = row.Cell("D").GetString().Trim();
                    //    string thicknessName = row.Cell("A").GetString().Trim();

                    //    Guid accessoryId = GetAccessory(organizationService, accessoryDescription, legacyDescription);

                    //    Guid thicknessId = GetThickness(organizationService, thicknessName);

                    //    if (accessoryId == Guid.Empty)
                    //    {
                    //        Console.WriteLine($"Accessory not found : {accessoryDescription}");
                    //        continue;
                    //    }

                    //    if (thicknessId == Guid.Empty)
                    //    {
                    //        Console.WriteLine($"Thickness not found : {thicknessName}");
                    //        continue;
                    //    }

                    //    Associate(organizationService, accessoryId, thicknessId);

                    //    Console.WriteLine($"Associated {accessoryDescription} -> {thicknessName}");
                    //}

                    //Console.WriteLine("Completed.");
                    //Console.ReadKey();

                    AccessoriesQtyConsole.CalculateQty(organizationService);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        static Guid GetAccessory(IOrganizationService service, string description, string legacyDescription)
        {
            QueryExpression qe = new QueryExpression("tbs_accessory");
            qe.ColumnSet = new ColumnSet(false);

            qe.Criteria.AddCondition("tbs_name",
                                     ConditionOperator.Equal,
                                     description);
            qe.Criteria.AddCondition("tbs_legacydescription", ConditionOperator.Equal, legacyDescription);

            EntityCollection result = service.RetrieveMultiple(qe);

            if(result.Entities.Count > 1)
            {
                Console.WriteLine("skipped");
                Console.WriteLine("description - " + description);
                Console.WriteLine("legacyDescription - " + legacyDescription);
            }
            return result.Entities.Count > 0
                ? result.Entities[0].Id
                : Guid.Empty;
        }

        static Guid GetThickness(IOrganizationService service, string thickness)
        {
            QueryExpression qe = new QueryExpression("tbs_thickness");
            qe.ColumnSet = new ColumnSet(false);

            qe.Criteria.AddCondition("tbs_name",
                                     ConditionOperator.Equal,
                                     thickness);

            EntityCollection result = service.RetrieveMultiple(qe);

            return result.Entities.Count > 0
                ? result.Entities[0].Id
                : Guid.Empty;
        }

        static void Associate(IOrganizationService service,
                              Guid accessoryId,
                              Guid thicknessId)
        {
            EntityReferenceCollection related = new EntityReferenceCollection();

            related.Add(new EntityReference("tbs_thickness", thicknessId));

            service.Associate(
                "tbs_accessory",
                accessoryId,
                new Relationship("tbs_accessory_tbs_thickness_tbs_thickness"),
                related);
        }
    }
}


