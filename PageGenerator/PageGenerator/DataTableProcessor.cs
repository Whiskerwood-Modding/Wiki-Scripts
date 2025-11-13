using System.Text;
using System.Linq;
using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Assets.Exports.Engine;
using CUE4Parse.UE4.Assets.Objects;

namespace PageGenerator
{
    public class DataTableProcessor(
        IFileProvider provider,
        string templateDir,
        string outputDir,
        bool replaceFiles)
    {
        public async Task ProcessAllDataTablesAsync()
        {
            Console.WriteLine("Processing all configured DataTables...");

            foreach (var config in DataTableConfigs.Templates.Values)
            {
                try
                {
                    await ProcessDataTableAsync(config);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing {config.TemplateName}: {ex.Message}");
                }
            }

            Console.WriteLine("Completed processing all DataTables!");
        }

        public async Task ProcessDataTableAsync(DataTableConfig config)
        {
            Console.WriteLine($"\n--- Processing {config.TemplateName} ---");
            
            // Load the DataTable
            UDataTable dataTable;
            try
            {
                dataTable = provider.LoadPackageObject<UDataTable>(config.DataTablePath);
                Console.WriteLine($"Successfully loaded DataTable: {config.DataTablePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load DataTable: {config.DataTablePath} - {ex.Message}");
                return;
            }

            Console.WriteLine($"Found DataTable with {dataTable.RowMap.Count} rows");

            // Load template
            string templatePath = Path.Combine(templateDir, config.TemplateName);
            if (!File.Exists(templatePath))
            {
                Console.WriteLine($"Template file not found at: {templatePath}");
                return;
            }

            string template = File.ReadAllText(templatePath);
            Console.WriteLine($"Loaded template: {config.TemplateName}");

            // Create output directory for this DataTable
            string outputFolderPath = Path.Combine(outputDir, config.OutputFolderName);
            Directory.CreateDirectory(outputFolderPath);

            // Process each row
            int processedCount = 0;
            foreach (var row in dataTable.RowMap)
            {
                string rowName = row.Key.Text;
                var rowData = row.Value;

                Console.WriteLine($"Processing row: {rowName}");

                try
                {
                    // Generate MediaWiki content for this row
                    string wikiContent = GenerateWikiPage(rowName, rowData, template, config);

                    // Write to file
                    string fileName = $"{SanitizeFileName(rowName)}.txt";
                    string filePath = Path.Combine(outputFolderPath, fileName);

                    if (!replaceFiles && File.Exists(filePath))
                    {
                        Console.WriteLine($"Skipping {fileName} (file already exists)");
                        continue;
                    }

                    await File.WriteAllTextAsync(filePath, wikiContent, Encoding.UTF8);
                    processedCount++;

                    Console.WriteLine($"Generated: {fileName}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing row {rowName}: {ex.Message}");
                }
            }

            Console.WriteLine($"Completed {config.TemplateName}! Generated {processedCount} pages in {outputFolderPath}");
        }

        private string GenerateWikiPage(string rowName, FStructFallback rowData, string template, DataTableConfig config)
        {
            // Extract properties based on struct definition
            var properties = PropertyExtractor.ExtractPropertiesForTemplate(config.TemplateName, rowData, config.StructName);

            // Filter out empty/none values and remove corresponding template rows
            string result = RemoveEmptyFieldRows(template, properties);

            // Replace remaining template placeholders
            foreach (var prop in properties)
            {
                //attempt to lookup our value in the language mapping before writing it out
                string value = textLookup.TryLookupEnglishMapping(prop.Value, out var mappedValue) ? mappedValue : prop.Value;

                result = result.Replace("{{{" + prop.Key + "}}}", value);
            }

            return result;
        }

        private string RemoveEmptyFieldRows(string template, Dictionary<string, string> properties)
        {
            var lines = template.Split('\n');
            var resultLines = new List<string>();
            
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                bool shouldRemoveLine = false;
                
                // Check if this line contains any template placeholders
                var placeholders = ExtractPlaceholders(line);
                
                if (placeholders.Count > 0)
                {
                    // Special handling for cost rows (they have both cost and count placeholders)
                    var costPlaceholder = placeholders.FirstOrDefault(p => p.StartsWith("cost") && char.IsDigit(p.Last()));
                    var countPlaceholder = placeholders.FirstOrDefault(p => p.StartsWith("ct") && char.IsDigit(p.Last()));
                    
                    if (costPlaceholder != null && countPlaceholder != null)
                    {
                        // This is a cost row - check if either cost is None or count is 0
                        var costValue = properties.GetValueOrDefault(costPlaceholder, "");
                        var countValue = properties.GetValueOrDefault(countPlaceholder, "");
                        
                        if (IsEmptyValue(costValue, costPlaceholder) || IsEmptyValue(countValue, countPlaceholder))
                        {
                            shouldRemoveLine = true;
                        }
                    }
                    else
                    {
                        // Regular field row - check if any of the placeholders have empty/none values
                        foreach (var placeholder in placeholders)
                        {
                            if (properties.TryGetValue(placeholder, out var value) && IsEmptyValue(value, placeholder))
                            {
                                shouldRemoveLine = true;
                                break;
                            }
                        }
                    }
                }
                
                if (shouldRemoveLine)
                {
                    // Skip this line and also skip the next line if it's a table row separator (|-)
                    if (i + 1 < lines.Length && lines[i + 1].Trim() == "|-")
                    {
                        i++; // Skip the next line too
                    }
                }
                else
                {
                    resultLines.Add(line);
                }
            }
            
            return string.Join('\n', resultLines);
        }
        
        private List<string> ExtractPlaceholders(string line)
        {
            var placeholders = new List<string>();
            var startIndex = 0;
            
            while (startIndex < line.Length)
            {
                var start = line.IndexOf("{{{", startIndex, StringComparison.Ordinal);
                if (start == -1) break;
                
                var end = line.IndexOf("}}}", start + 3, StringComparison.Ordinal);
                if (end == -1) break;
                
                var placeholder = line.Substring(start + 3, end - start - 3);
                placeholders.Add(placeholder);
                startIndex = end + 3;
            }
            
            return placeholders;
        }
        
        private bool IsEmptyValue(string value, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(value))
                return true;
                
            // Handle "None" values
            if (value.Equals("None", StringComparison.OrdinalIgnoreCase))
                return true;
                
            // Handle "N/A" values
            if (value.Equals("N/A", StringComparison.OrdinalIgnoreCase))
                return true;
                
            // Handle empty arrays
            if (value.Equals("[]", StringComparison.OrdinalIgnoreCase) || 
                value.Equals("", StringComparison.OrdinalIgnoreCase))
                return true;
                
            // Handle cost fields with "None x0" pattern
            if (value.Contains("None x0"))
                return true;
                
            // Handle zero values for count fields (ct1, ct2, etc.)
            if (fieldName.StartsWith("ct") && (value == "0" || value == "0.00"))
                return true;
                
            // Handle empty/whitespace strings
            if (string.IsNullOrEmpty(value.Trim()))
                return true;
                
            return false;
        }

        private static string SanitizeFileName(string fileName)
        {
            // Remove invalid characters for file names
            string invalidChars = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());
            foreach (char c in invalidChars)
            {
                fileName = fileName.Replace(c.ToString(), "");
            }
            return fileName;
        }
    }
}
