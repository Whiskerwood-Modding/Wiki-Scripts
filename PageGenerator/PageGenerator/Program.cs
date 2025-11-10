using CUE4Parse.FileProvider;
using CUE4Parse.MappingsProvider;
using CUE4Parse.UE4.Versions;

namespace PageGenerator;

class Program
{
    private const string _pakDir = @"C:\Program Files (x86)\Steam\steamapps\common\Whiskerwood\Whiskerwood\Content\Paks";
    private const string _mapping = @"F:\Whiskerwood Modding\Whiskerwood.usmap"; 
    private const EGame  _version = EGame.GAME_UE5_6;
    private const string _templateDir = @"F:\Github Projects\Other\Whiskerwood-Wiki-Scripts\PageGenerator\PageTemplates";
    private const string _outputDir = @"F:\Github Projects\Other\Whiskerwood-Wiki-Scripts\PageGenerator\Output";
    private const bool   _replaceFiles = true;
    
    static async Task Main(string[] args)
    {
        Console.WriteLine("Starting DataTable extraction for MediaWiki templates...");
        
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
            
            // Create processor and process all configured DataTables
            var processor = new DataTableProcessor(provider, _templateDir, _outputDir, _replaceFiles);
            await processor.ProcessAllDataTablesAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }
}