using System.Text;
using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Assets.Exports.Engine;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Assets.Exports.Texture;
using CUE4Parse_Conversion.Textures;

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

            // Check if this is a special single-page template (like ResourcesTemplate)
            if (config.TemplateName == "ResourcesTemplate.txt" || config.TemplateName == "BuildingsOverviewTemplate.txt")
            {
                await ProcessSinglePageTemplate(dataTable, template, config, outputFolderPath);
            }
            else
            {
                // Process each row as individual pages
                await ProcessIndividualPages(dataTable, template, config, outputFolderPath);
            }
        }

        private async Task ProcessSinglePageTemplate(UDataTable dataTable, string template, DataTableConfig config, string outputFolderPath)
        {
            Console.WriteLine($"Processing single-page template for {config.TemplateName}");
            
            // Extract the row template
            var lines = template.Split('\n').ToList();
            int rowStartIndex = -1;
            int rowEndIndex = -1;
            
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].Trim() == "|-" && rowStartIndex == -1)
                {
                    rowStartIndex = i + 1; // Start after the |-
                }
                else if ((lines[i].Trim() == "|-" || lines[i].Trim() == "|}") && rowStartIndex != -1)
                {
                    rowEndIndex = i;
                    break;
                }
            }
            
            if (rowStartIndex == -1 || rowEndIndex == -1)
            {
                Console.WriteLine("Could not find row template in the template file");
                return;
            }
            
            // Extract the row template
            var rowTemplateLines = lines.GetRange(rowStartIndex, rowEndIndex - rowStartIndex);
            string rowTemplate = string.Join('\n', rowTemplateLines);
            Console.WriteLine($"Extracted row template: {rowTemplate}");
            
            // Process all rows and build the final content
            var allRowsContent = new List<string>();
            int processedCount = 0;
            
            foreach (var row in dataTable.RowMap)
            {
                string rowName = row.Key.Text;
                var rowData = row.Value;
                
                Console.WriteLine($"Processing row: {rowName}");
                
                try
                {
                    // Extract properties for this row
                    var properties = PropertyExtractor.ExtractPropertiesForTemplate(config.TemplateName, rowData, config.StructName);
                    
                    // Skip rows for BuildingsOverviewTemplate if description is missing or building name is empty/none
                    if (config.TemplateName == "BuildingsOverviewTemplate.txt")
                    {
                        // Check if description exists and is not empty
                        if (!properties.TryGetValue("description", out var description) || 
                            string.IsNullOrEmpty(description) || 
                            description == "N/A")
                        {
                            Console.WriteLine($"Skipping row {rowName}: No description found");
                            continue;
                        }
                        
                        // Check if stringKey (building name) exists and is not empty/none
                        if (!properties.TryGetValue("stringKey", out var buildingName) ||
                            string.IsNullOrEmpty(buildingName) || 
                            buildingName == "N/A" || 
                            buildingName.ToLowerInvariant() == "none")
                        {
                            Console.WriteLine($"Skipping row {rowName}: Building name is empty or none");
                            continue;
                        }
                    }
                    
                    // Handle icon extraction and PNG generation
                    if (properties.TryGetValue("icon", out var iconPath) && !string.IsNullOrEmpty(iconPath) && iconPath != "N/A")
                    {
                        var extractedIconFileName = await ExtractIconAsPng(iconPath, outputFolderPath, rowName);
                        if (!string.IsNullOrEmpty(extractedIconFileName))
                        {
                            // Set the icon property to the extracted filename without .png extension
                            properties["icon"] = extractedIconFileName.Replace(".png", "");
                            Console.WriteLine($"Successfully extracted icon for {rowName}: {extractedIconFileName}");
                        }
                        else
                        {
                            Console.WriteLine($"Failed to extract icon for {rowName}, will show without icon");
                            properties["icon"] = string.Empty; // Clear icon property if extraction failed
                        }
                    }
                    else
                    {
                        // No icon to extract
                        properties["icon"] = string.Empty;
                    }
                    
                    // For BuildingsOverviewTemplate, regenerate the buildingWithIcon after icon processing
                    if (config.TemplateName == "BuildingsOverviewTemplate.txt")
                    {
                        string buildingName = properties.GetValueOrDefault("stringKey", "Unknown");
                        string iconFileName = properties.TryGetValue("icon", out var icon) ? icon : string.Empty;
                        
                        if (!string.IsNullOrEmpty(iconFileName))
                        {
                            // Building has an extracted icon
                            properties["buildingWithIcon"] = $"[[File:{iconFileName}.png|64px]] {buildingName}";
                        }
                        else
                        {
                            // Building has no icon, just show the name
                            properties["buildingWithIcon"] = buildingName;
                        }
                    }
                    
                    // Replace placeholders in the row template
                    string processedRow = rowTemplate;
                    foreach (var prop in properties)
                    {
                        string value = textLookup.TryLookupEnglishMapping(prop.Value, out var mappedValue) ? mappedValue : prop.Value;
                        processedRow = processedRow.Replace("{{{" + prop.Key + "}}}", value);
                    }
                    
                    allRowsContent.Add(processedRow);
                    processedCount++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing row {rowName}: {ex.Message}");
                }
            }
            
            // Build the final page content
            var finalLines = new List<string>();
            
            // Add everything before the row template
            finalLines.AddRange(lines.GetRange(0, rowStartIndex - 1)); // Don't include the first |-
            
            // Add all processed rows
            for (int i = 0; i < allRowsContent.Count; i++)
            {
                finalLines.Add("|-");
                finalLines.Add(allRowsContent[i]);
            }
            
            // Add the closing table tag
            finalLines.Add("|}");
            
            string finalContent = string.Join('\n', finalLines);
            
            // Write to single file
            string fileName = $"{config.OutputFolderName}.txt";
            string filePath = Path.Combine(outputFolderPath, fileName);
            
            if (!replaceFiles && File.Exists(filePath))
            {
                Console.WriteLine($"Skipping {fileName} (file already exists)");
                return;
            }
            
            await File.WriteAllTextAsync(filePath, finalContent, Encoding.UTF8);
            Console.WriteLine($"Generated single page: {fileName} with {processedCount} resources");
        }

        private async Task ProcessIndividualPages(UDataTable dataTable, string template, DataTableConfig config, string outputFolderPath)
        {
            // Process each row as individual pages (existing logic)
            int processedCount = 0;
            foreach (var row in dataTable.RowMap)
            {
                string rowName = row.Key.Text;
                var rowData = row.Value;

                Console.WriteLine($"Processing row: {rowName}");

                try
                {
                    // Generate MediaWiki content for this row
                    string wikiContent = GenerateWikiPage(rowData, template, config);

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

        private string GenerateWikiPage(FStructFallback rowData, string template, DataTableConfig config)
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

        private async Task<string> ExtractIconAsPng(string iconAssetPath, string outputFolderPath, string rowId)
        {
            try
            {
                Console.WriteLine($"Attempting to extract icon: {iconAssetPath}");
                
                // Clean up the asset path - remove any package references and get just the path
                string cleanPath = iconAssetPath;
                if (cleanPath.Contains('\''))
                {
                    // Extract path from something like "Texture2D'/Game/UI/Icons/Icon_approval.Icon_approval'"
                    int startIndex = cleanPath.IndexOf('\'') + 1;
                    int endIndex = cleanPath.LastIndexOf('\'');
                    if (startIndex > 0 && endIndex > startIndex)
                    {
                        cleanPath = cleanPath.Substring(startIndex, endIndex - startIndex);
                    }
                }
                
                // Remove the duplicate name at the end if present (e.g., "/Game/UI/Icons/Icon_approval.Icon_approval" -> "/Game/UI/Icons/Icon_approval")
                if (cleanPath.Contains('.'))
                {
                    string[] parts = cleanPath.Split('.');
                    if (parts.Length == 2 && parts[0].EndsWith("/" + parts[1]))
                    {
                        cleanPath = parts[0];
                    }
                }
                
                Console.WriteLine($"Cleaned icon path: {cleanPath}");
                
                // Try to load the texture asset
                UTexture2D? texture = null;
                try
                {
                    texture = provider.LoadPackageObject<UTexture2D>(cleanPath);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to load texture directly: {ex.Message}");
                    try
                    {
                        texture = provider.LoadPackageObject<UTexture2D>(cleanPath + ".uasset");
                    }
                    catch
                    {
                        Console.WriteLine($"Also failed with .uasset extension");
                        return string.Empty;
                    }
                }

                Console.WriteLine($"Successfully loaded texture: {texture.Name}");
                
                // Decode the texture using CUE4Parse conversion methods
                var cTexture = texture.Decode();
                if (cTexture == null)
                {
                    Console.WriteLine($"Failed to decode texture: {texture.Name}");
                    return string.Empty;
                }
                
                string iconFileName = SanitizeFileName(rowId) + ".png";
                string iconFilePath = Path.Combine(outputFolderPath, iconFileName);
                var pngData = cTexture.Encode(ETextureFormat.Png, false, out var ext);
                await File.WriteAllBytesAsync(iconFilePath, pngData);
                
                Console.WriteLine($"Successfully extracted icon: {iconFileName}");
                return iconFileName;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error extracting icon {iconAssetPath}: {ex.Message}");
                return string.Empty;
            }
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
