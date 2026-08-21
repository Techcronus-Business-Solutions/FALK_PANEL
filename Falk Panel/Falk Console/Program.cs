using ClosedXML.Excel;
using DocumentFormat.OpenXml.Drawing.Diagrams;
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

                    //string excelPath = @"C:\Users\admin\Desktop\Book2.xlsx";

                    //var workbook = new XLWorkbook(excelPath);
                    //var sheet = workbook.Worksheet(1);

                    //foreach (var row in sheet.RowsUsed().Skip(1))
                    //{
                    //    string trimDescription = row.Cell("E").GetString().Trim();
                    //    string legacyDescription = row.Cell("D").GetString().Trim();
                    //    string thicknessName = row.Cell("A").GetString().Trim();

                    //    Guid trimId = GetAccessory(organizationService, trimDescription, legacyDescription);

                    //    if(trimDescription == "Non Embossed Flat Sheet 10'" && legacyDescription == "48\"x120\" Flat Sheets (HPS COLOR)")
                    //    {
                    //        trimId = new Guid("0776c8c9-5080-f111-ab0f-70a8a5ae5a0e");
                    //    }
                    //    else if (trimDescription == "Embossed Flat Sheet 10'" && legacyDescription == "48\"x120\" Non-Embossed Flat Sheets (PVDF/SMP COLOR)")
                    //    {
                    //        trimId = new Guid("0d76c8c9-5080-f111-ab0f-70a8a5ae5a0e");
                    //    }
                    //    else if(trimDescription == "Interior Non Embossed Flat Sheet 10'" && legacyDescription == "44\"x120\" Non-Embossed Flat Sheets (INTERIOR WHITE)")
                    //    {
                    //        trimId = new Guid("1376c8c9-5080-f111-ab0f-70a8a5ae5a0e");
                    //    }
                    //    else if(trimDescription == "Interior Embossed Flat Sheet 10'" && legacyDescription == "")
                    //    {
                    //        trimId = new Guid("1976c8c9-5080-f111-ab0f-70a8a5ae5a0e");
                    //    }
                    //    else if(trimDescription == "Low Side Eave Flashing" && legacyDescription == "Low Eave Flashing (RRP-FLE1-00.5)")
                    //    {
                    //        Console.WriteLine("Skipped as trim not found");
                    //        continue;
                    //    }
                    //    else if (trimDescription == "Low Side Eave Flashing" && legacyDescription == "Low Eave Flashing (RRP-FLE1-00.5)")
                    //    {
                    //        Console.WriteLine("Skipped as trim not found");
                    //        continue;
                    //    }

                    //    if (trimId == Guid.Empty)
                    //    {

                    //    }
                    //    //{
                    //    //    trimId = new Guid("0d76c8c9-5080-f111-ab0f-70a8a5ae5a0e");
                    //    //}


                    //    Guid thicknessId = GetThickness(organizationService, thicknessName);

                    //    if (trimId == Guid.Empty)
                    //    {
                    //        Console.WriteLine($"trim not found : {trimDescription}");
                    //        continue;
                    //    }

                    //    if (thicknessId == Guid.Empty)
                    //    {
                    //        Console.WriteLine($"Thickness not found : {thicknessName}");
                    //        continue;
                    //    }

                    //    Associate(organizationService, trimId, thicknessId);

                    //    Console.WriteLine($"Associated {trimDescription} -> {thicknessName}");
                    //}

                    //ImportTrimData.ImportData(organizationService);

                    //Console.WriteLine("Completed.");
                    //Console.ReadKey();

                    //AccessoriesQtyConsole.CalculateQty(organizationService);
                    //TrimQtyConsole.CalculateQty(organizationService);

                    //PricingMasterInterior.ImportPricingMasterInteriorData(organizationService);

                    QueryExpression query = new QueryExpression("tbs_thickness");
                    query.ColumnSet = new ColumnSet(true);

                    EntityCollection thicknessEntColl = organizationService.RetrieveMultiple(query);

                    foreach(Entity thickness in thicknessEntColl.Entities)
                    {
                        string panelThickness = thickness.GetAttributeValue<EntityReference>("tbs_product").Name;
                        decimal thicknessNumber = thickness.Contains("tbs_thicknessnumber") ? thickness.GetAttributeValue<decimal>("tbs_thicknessnumber") : 0;

                        string thicknessName = panelThickness + " " + Math.Round(thicknessNumber, 2).ToString();
                        //string thicknessName = thicknessNumber.ToString();

                        thickness["tbs_panelcombo"] = thicknessName;
                        thickness["tbs_name"] = Math.Round(thicknessNumber, 2).ToString();

                        organizationService.Update(thickness);
                    }

                    //ImportaccesoryData.ImportData(organizationService);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        static Guid GetAccessory(IOrganizationService service, string description, string legacyDescription)
        {
            QueryExpression qe = new QueryExpression("tbs_trim");
            qe.ColumnSet = new ColumnSet(false);

            qe.Criteria.AddCondition("tbs_name", ConditionOperator.Equal, description);
            qe.Criteria.AddCondition("tbs_description", ConditionOperator.Equal, legacyDescription);

            EntityCollection result = service.RetrieveMultiple(qe);

            if (result.Entities.Count > 1)
            {
                Console.WriteLine("skipped");
                Console.WriteLine("description - " + description);
                Console.WriteLine("legacyDescription - " + legacyDescription);
                //return new Guid("2d76c8c9-5080-f111-ab0f-70a8a5ae5a0e");
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
            try
            {
                EntityReferenceCollection related = new EntityReferenceCollection();

                related.Add(new EntityReference("tbs_thickness", thicknessId));

                service.Associate(
                    "tbs_trim",
                    accessoryId,
                    new Relationship("tbs_trim_tbs_thickness_tbs_thickness"),
                    related);
            }
            catch(Exception ex)
            {
                
                if(ex.Message != "Cannot insert duplicate key.")
                {
                    Console.WriteLine(ex.Message);
                }
            }
            
        }
    }
}


