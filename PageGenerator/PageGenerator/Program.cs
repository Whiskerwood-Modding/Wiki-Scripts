using System.Text;
using System.Globalization;
using CUE4Parse.FileProvider;
using CUE4Parse.MappingsProvider;
using CUE4Parse.UE4.Assets.Exports.Engine;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Versions;
using CUE4Parse.UE4.Objects.UObject;

namespace PageGenerator;

class Program
{
    private const string _pakDir = @"C:\Program Files (x86)\Steam\steamapps\common\Whiskerwood\Whiskerwood\Content\Paks";
    private const string _mapping = @"F:\Whiskerwood Modding\Whiskerwood.usmap"; 
    private const EGame  _version = EGame.GAME_UE5_6;
    private const string _templateDir = @"F:\Github Projects\Other\Whiskerwood-Wiki-Scripts\PageGenerator\PageTemplates";
    private const string _outputDir = @"F:\Github Projects\Other\Whiskerwood-Wiki-Scripts\PageGenerator\Output";
    private const bool   _replaceFiles = false;
    private const bool   _printSuccess = true; 
    
    static async Task Main(string[] args)
    {
        Console.WriteLine("Starting Crops DataTable extraction...");
        
        try
        {
            // Initialize file provider
            var provider = new DefaultFileProvider(_pakDir, SearchOption.TopDirectoryOnly, new VersionContainer(_version), StringComparer.OrdinalIgnoreCase);
            
            // Load mappings if available
            if (File.Exists(_mapping))
            {
                provider.MappingsContainer = new FileUsmapTypeMappingsProvider(_mapping);
                Console.WriteLine("Loaded mappings from: " + _mapping);
            }
            else
            {
                Console.WriteLine("Warning: Mappings file not found at: " + _mapping);
            }
            
            // Initialize and mount the provider
            provider.Initialize();
            await provider.MountAsync();
            Console.WriteLine("Provider initialized and mounted successfully");
            
            // Load the Crops DataTable
            string cropsPath = "/Game/Data/AssetLookups/Crops.Crops";
            
            UDataTable dataTable;
            try
            {
                dataTable = provider.LoadPackageObject<UDataTable>(cropsPath);
                Console.WriteLine($"Successfully loaded DataTable: {cropsPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load DataTable: {cropsPath} - {ex.Message}");
                return;
            }
            
            Console.WriteLine($"Found DataTable with {dataTable.RowMap.Count.ToString()} rows");
            
            // Load template
            string templatePath = Path.Combine(_templateDir, "CropsTemplate.txt");
            if (!File.Exists(templatePath))
            {
                Console.WriteLine($"Template file not found at: {templatePath}");
                return;
            }
            
            string template = File.ReadAllText(templatePath);
            Console.WriteLine("Loaded MediaWiki template");
            
            // Ensure output directory exists
            Directory.CreateDirectory(_outputDir);
            
            // Process each crop
            foreach (var row in dataTable.RowMap)
            {
                string cropName = row.Key.Text;
                var cropData = row.Value;
                
                Console.WriteLine($"Processing crop: {cropName}");
                
                // Generate MediaWiki content for this crop
                string wikiContent = GenerateCropWikiPage(cropName, cropData, template);
                
                // Write to file
                string fileName = $"{SanitizeFileName(cropName)}.txt";
                string filePath = Path.Combine(_outputDir, fileName);
                
                if (!_replaceFiles && File.Exists(filePath))
                {
                    Console.WriteLine($"Skipping {fileName} (file already exists)");
                    continue;
                }
                
                File.WriteAllText(filePath, wikiContent, Encoding.UTF8);
                
                if (_printSuccess)
                {
                    Console.WriteLine($"Generated: {fileName}");
                }
            }
            
            Console.WriteLine($"\nCompleted! Generated {dataTable.RowMap.Count.ToString()} crop pages in {_outputDir}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
        
        Console.WriteLine("\nPress any key to exit...");
        Console.ReadKey();
    }
    
    private static string GenerateCropWikiPage(string cropName, FStructFallback cropData, string template)
    {
        // Extract crop properties with safe value extraction
        var properties = new Dictionary<string, string>
        {
            { "growthPeriodInYears", GetFloatValue(cropData, "GrowthPeriodInYears") },
            { "consumptionRemainder", GetFloatValue(cropData, "ConsumptionRemainder") },
            { "nutrientSaturation", GetFloatValue(cropData, "NutrientSaturation") },
            { "nutrientStarvation", GetFloatValue(cropData, "NutrientStarvation") },
            { "decayPeriodInYears", GetFloatValue(cropData, "DecayPeriodInYears") },
            { "nutrientMinimum", GetFloatValue(cropData, "NutrientMinimum") },
            { "lightTarget", GetFloatValue(cropData, "LightTarget") },
            { "lightPenalty", GetFloatValue(cropData, "LightPenalty") },
            { "rockPenalty", GetFloatValue(cropData, "RockPenalty") },
            { "pollutionPenalty", GetFloatValue(cropData, "PollutionPenalty") },
            { "maximumYield", GetFloatValue(cropData, "MaximumYield") },
            { "yieldGrowthThreshold", GetFloatValue(cropData, "YieldGrowthThreshold") },
            { "frostGrowthThreshold", GetByteValue(cropData, "FrostGrowthThreshold") },
            { "frostRotThreshold", GetByteValue(cropData, "FrostRotThreshold") },
            { "requiredUnlock", GetFNameValue(cropData, "RequiredUnlock") }
        };
        
        // Replace template placeholders
        string result = template;
        foreach (var prop in properties)
        {
            result = result.Replace("{{{" + prop.Key + "}}}", prop.Value);
        }
        
        return result;
    }
    
    private static string GetFloatValue(FStructFallback data, string propertyName)
    {
        try
        {
            if (data.TryGetValue<float>(out var floatVal, propertyName))
            {
                return floatVal.ToString("F2", CultureInfo.InvariantCulture);
            }
            if (data.TryGetValue<double>(out var doubleVal, propertyName))
            {
                return doubleVal.ToString("F2", CultureInfo.InvariantCulture);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting float value for {propertyName}: {ex.Message}");
        }
        return "N/A";
    }
    
    private static string GetByteValue(FStructFallback data, string propertyName)
    {
        try
        {
            if (data.TryGetValue<byte>(out var byteVal, propertyName))
            {
                return byteVal.ToString();
            }
            if (data.TryGetValue<int>(out var intVal, propertyName))
            {
                return intVal.ToString();
            }
            if (data.TryGetValue<uint>(out var uintVal, propertyName))
            {
                return uintVal.ToString();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting byte value for {propertyName}: {ex.Message}");
        }
        return "N/A";
    }
    
    private static string GetFNameValue(FStructFallback data, string propertyName)
    {
        try
        {
            if (data.TryGetValue<FName>(out var fnameVal, propertyName))
            {
                return fnameVal.Text;
            }
            if (data.TryGetValue<string>(out var stringVal, propertyName))
            {
                return stringVal;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting FName value for {propertyName}: {ex.Message}");
        }
        return "N/A";
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