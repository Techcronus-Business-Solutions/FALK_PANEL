using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Tooling.Connector;
using System;
using ClosedXML.Excel;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Sdk.Messages;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Falk_Console
{
    internal class Program
    {
        private const string ThicknessEntity = "tbs_thickness";
        private const string AccessoryEntity = "tbs_accessory";
        private const string NameColumn = "tbs_name";
        private const string RelationshipSchemaName = "tbs_accessory_tbs_thickness_tbs_thickness";

        private sealed class LogRow
        {
            public DateTime Timestamp { get; set; } = DateTime.Now;
            public string Status { get; set; }
            public string Thickness { get; set; }
            public string Accessory { get; set; }
            public string Details { get; set; }
        }

        public static void Main(string[] args)
        {
            try
            {
                using (CrmServiceClient organizationService = Connection.CreateConnection())
                {
                    Console.WriteLine("Console Application Started!");

                    //Console.Write("Enter Quote Product GUID (tbs_quoteProduct): 147d330d-c9c3-f111-aaad-7ced8d70f306");
                    //Guid quoteProductId = new Guid("147d330d-c9c3-f111-aaad-7ced8d70f306");

                    //var logicTrim = new CalculateQuoteTrimQtyLogic(organizationService);
                    //logicTrim.Run(quoteProductId);

                    //var logicAccessory = new Falk.CustomAPI.CalculateQuoteAccessoryQtyLogic();
                    //logicAccessory.Execute((IOrganizationService)organizationService, quoteProductId);


                    // Add the logic here
                    // Read Excel file path
                    Console.Write("Enter Excel file path: ");
                    string excelPath = "C:\\Users\\admin\\Desktop\\Falk issue\\ThicknessAccessoryAssociationConsole\\ThicknessAccessoryAssociationConsole\\Book1.xlsx";

                    string logPath = System.IO.Path.Combine(
                        System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(excelPath)),
                        System.IO.Path.GetFileNameWithoutExtension(excelPath)
                            + "_AssociationLog_"
                            + DateTime.Now.ToString("yyyyMMdd_HHmmss")
                            + ".xlsx");

                    // Read Excel mappings
                    var mappings = new System.Collections.Generic.List<
                        (string Thickness, string Accessory, int ExcelRow)>();

                    using (var workbook = new ClosedXML.Excel.XLWorkbook(excelPath))
                    {
                        var worksheet = workbook.Worksheets.First();

                        foreach (var row in worksheet.RowsUsed().Where(r => r.RowNumber() > 1))
                        {
                            string thicknessName = row.Cell(1).GetString().Trim();
                            string accessoryName = row.Cell(2).GetString().Trim();

                            if (string.IsNullOrWhiteSpace(thicknessName) &&
                                string.IsNullOrWhiteSpace(accessoryName))
                                continue;

                            mappings.Add((thicknessName, accessoryName, row.RowNumber()));
                        }
                    }

                    Console.WriteLine($"Loaded {mappings.Count} Excel rows.");

                    Console.WriteLine(
                        "WARNING: This will remove existing accessory associations from ALL thickness records.");

                    Console.Write("Type YES to continue: ");

                    if (Console.ReadLine() != "YES")
                    {
                        Console.WriteLine("Operation cancelled.");
                        return;
                    }

                    var logs = new System.Collections.Generic.List<LogRow>();

                    // Phase 1: Retrieve all thickness records and remove existing associations
                    var thicknessQuery = new Microsoft.Xrm.Sdk.Query.QueryExpression(ThicknessEntity)
                    {
                        ColumnSet = new Microsoft.Xrm.Sdk.Query.ColumnSet(NameColumn),
                        PageInfo = new Microsoft.Xrm.Sdk.Query.PagingInfo
                        {
                            Count = 5000,
                            PageNumber = 1
                        }
                    };

                    var allThicknesses = new System.Collections.Generic.List<Entity>();
                    Microsoft.Xrm.Sdk.EntityCollection thicknessPage;

                    do
                    {
                        thicknessPage = organizationService.RetrieveMultiple(thicknessQuery);
                        allThicknesses.AddRange(thicknessPage.Entities);

                        if (thicknessPage.MoreRecords)
                        {
                            thicknessQuery.PageInfo.PageNumber++;
                            thicknessQuery.PageInfo.PagingCookie = thicknessPage.PagingCookie;
                        }
                    }
                    while (thicknessPage.MoreRecords);

                    var relationship = new Relationship(RelationshipSchemaName);

                    Console.WriteLine($"Found {allThicknesses.Count} thickness records.");

                    foreach (var thickness in allThicknesses)
                    {
                        string thicknessName =
                            thickness.GetAttributeValue<string>(NameColumn) ?? "(unnamed thickness)";

                        try
                        {
                            var accessoryQuery =
                                new Microsoft.Xrm.Sdk.Query.QueryExpression(AccessoryEntity)
                                {
                                    ColumnSet = new Microsoft.Xrm.Sdk.Query.ColumnSet(NameColumn)
                                };

                            var request = new Microsoft.Xrm.Sdk.Messages.RetrieveRequest
                            {
                                Target = thickness.ToEntityReference(),
                                ColumnSet = new Microsoft.Xrm.Sdk.Query.ColumnSet(NameColumn),
                                RelatedEntitiesQuery = new RelationshipQueryCollection
            {
                { relationship, accessoryQuery }
            }
                            };

                            var response =
                                (Microsoft.Xrm.Sdk.Messages.RetrieveResponse)
                                organizationService.Execute(request);

                            if (!response.Entity.RelatedEntities.TryGetValue(
                                    relationship, out EntityCollection accessories) ||
                                accessories == null)
                                continue;

                            foreach (var accessory in accessories.Entities)
                            {
                                string accessoryName =
                                    accessory.GetAttributeValue<string>(NameColumn)
                                    ?? "(unnamed accessory)";

                                try
                                {
                                    organizationService.Disassociate(
                                        ThicknessEntity,
                                        thickness.Id,
                                        relationship,
                                        new EntityReferenceCollection
                                        {
                        accessory.ToEntityReference()
                                        });

                                    logs.Add(new LogRow
                                    {
                                        Status = "DISASSOCIATED",
                                        Thickness = thicknessName,
                                        Accessory = accessoryName,
                                        Details = "Existing association removed."
                                    });

                                    Console.WriteLine(
                                        $"[DISASSOCIATED] {thicknessName} -> {accessoryName}");
                                }
                                catch (Exception ex)
                                {
                                    logs.Add(new LogRow
                                    {
                                        Status = "ERROR",
                                        Thickness = thicknessName,
                                        Accessory = accessoryName,
                                        Details = "Disassociate failed: " + ex.Message
                                    });

                                    Console.WriteLine(
                                        $"[ERROR DISASSOCIATE] {thicknessName} -> {accessoryName}: {ex.Message}");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            logs.Add(new LogRow
                            {
                                Status = "ERROR",
                                Thickness = thicknessName,
                                Details = "Could not retrieve existing associations: " + ex.Message
                            });

                            Console.WriteLine(
                                $"[ERROR RETRIEVING ASSOCIATIONS] {thicknessName}: {ex.Message}");
                        }
                    }

                    // Phase 2: Associate according to Excel
                    foreach (var mapping in mappings)
                    {
                        Console.WriteLine(
                            $"[ROW {mapping.ExcelRow}] {mapping.Thickness} -> {mapping.Accessory}");

                        try
                        {
                            Entity FindByName(string entityName, string name)
                            {
                                var query = new Microsoft.Xrm.Sdk.Query.QueryExpression(entityName)
                                {
                                    ColumnSet = new Microsoft.Xrm.Sdk.Query.ColumnSet(NameColumn),
                                    TopCount = 2
                                };

                                query.Criteria.AddCondition(
                                    NameColumn,
                                    Microsoft.Xrm.Sdk.Query.ConditionOperator.Equal,
                                    name);

                                var results = organizationService.RetrieveMultiple(query);

                                if (results.Entities.Count > 1)
                                    throw new InvalidOperationException(
                                        $"Multiple {entityName} records found with name '{name}'.");

                                return results.Entities.FirstOrDefault();
                            }

                            Entity thickness = FindByName(ThicknessEntity, mapping.Thickness);
                            Entity accessory = FindByName(AccessoryEntity, mapping.Accessory);

                            if (thickness == null || accessory == null)
                            {
                                string status = thickness == null && accessory == null
                                    ? "MISSING_BOTH"
                                    : thickness == null
                                        ? "MISSING_THICKNESS"
                                        : "MISSING_ACCESSORY";

                                string details = thickness == null && accessory == null
                                    ? "Thickness and accessory were not found."
                                    : thickness == null
                                        ? $"Thickness '{mapping.Thickness}' not found."
                                        : $"Accessory '{mapping.Accessory}' not found.";

                                logs.Add(new LogRow
                                {
                                    Status = status,
                                    Thickness = mapping.Thickness,
                                    Accessory = mapping.Accessory,
                                    Details = $"Excel row {mapping.ExcelRow}. {details}"
                                });

                                Console.WriteLine($"[{status}] Excel row {mapping.ExcelRow}: {details}");
                                continue;
                            }

                            organizationService.Associate(
                                ThicknessEntity,
                                thickness.Id,
                                relationship,
                                new EntityReferenceCollection
                                {
                                    accessory.ToEntityReference()
                                });

                            logs.Add(new LogRow
                            {
                                Status = "ASSOCIATED",
                                Thickness = mapping.Thickness,
                                Accessory = mapping.Accessory,
                                Details = $"Excel row {mapping.ExcelRow}. Association successful."
                            });

                            Console.WriteLine(
                                $"[ASSOCIATED] {mapping.Thickness} -> {mapping.Accessory}");
                        }
                        catch (Exception ex)
                        {
                            logs.Add(new LogRow
                            {
                                Status = "ERROR",
                                Thickness = mapping.Thickness,
                                Accessory = mapping.Accessory,
                                Details = $"Excel row {mapping.ExcelRow}. {ex.Message}"
                            });

                            Console.WriteLine($"[ERROR] Excel row {mapping.ExcelRow}: {ex.Message}");
                        }
                    }

                    // Phase 3: Write Excel log
                    using (var logWorkbook = new ClosedXML.Excel.XLWorkbook())
                    {
                        var allLogsSheet = logWorkbook.Worksheets.Add("All Logs");
                        var missingSheet = logWorkbook.Worksheets.Add("Missing Records");

                        void WriteLogs(ClosedXML.Excel.IXLWorksheet sheet,
                            System.Collections.Generic.IEnumerable<LogRow> rows)
                        {
                            string[] headers = { "Timestamp", "Status", "Thickness", "Accessory", "Details" };

                            for (int i = 0; i < headers.Length; i++)
                                sheet.Cell(1, i + 1).Value = headers[i];

                            int rowNumber = 2;

                            foreach (var log in rows)
                            {
                                sheet.Cell(rowNumber, 1).Value = log.Timestamp;
                                sheet.Cell(rowNumber, 2).Value = log.Status;
                                sheet.Cell(rowNumber, 3).Value = log.Thickness;
                                sheet.Cell(rowNumber, 4).Value = log.Accessory;
                                sheet.Cell(rowNumber, 5).Value = log.Details;
                                rowNumber++;
                            }

                            sheet.Row(1).Style.Font.Bold = true;
                            sheet.SheetView.FreezeRows(1);
                            sheet.Columns().AdjustToContents();
                        }

                        WriteLogs(allLogsSheet, logs);

                        WriteLogs(
                            missingSheet,
                            logs.Where(x =>
                                x.Status == "MISSING_THICKNESS" ||
                                x.Status == "MISSING_ACCESSORY" ||
                                x.Status == "MISSING_BOTH"));

                        logWorkbook.SaveAs(logPath);
                    }

                    Console.WriteLine();
                    Console.WriteLine("Processing completed.");
                    Console.WriteLine($"Successful associations: {logs.Count(x => x.Status == "ASSOCIATED")}");
                    Console.WriteLine($"Missing records: {logs.Count(x => x.Status.StartsWith("MISSING"))}");
                    Console.WriteLine($"Errors: {logs.Count(x => x.Status == "ERROR")}");
                    Console.WriteLine($"Excel log file: {logPath}");

                    Console.WriteLine("Finished. Press any key to exit.");
                    Console.ReadKey();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
    }
}


