using CUE4Parse.FileProvider;
using CUE4Parse.MappingsProvider;
using CUE4Parse.UE4.Versions;

namespace TableGraph;

class Program
{
    // This is the default steam install location
    private const string _pakDir = @"C:\Program Files (x86)\Steam\steamapps\common\Whiskerwood\Whiskerwood\Content\Paks";
    private const EGame _version = EGame.GAME_UE5_6;
    // This path assumes the Whiskerwood.usmap file is placed in the PageGenerator folder (next to README.md)
    private const string _mapping = @"../Whiskerwood.usmap";
    
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== DataTable Reference Graph Tool ===\n");
        Console.WriteLine("This tool scans all DataTables and lets you search for key references.\n");
        
        try
        {
            // Initialize file provider
            DefaultFileProvider provider = new DefaultFileProvider(_pakDir, SearchOption.TopDirectoryOnly, new VersionContainer(_version), StringComparer.OrdinalIgnoreCase);
            
            // Load mappings if available
            if (File.Exists(_mapping))
            {
                provider.MappingsContainer = new FileUsmapTypeMappingsProvider(_mapping);
                Console.WriteLine("Loaded mappings from: " + _mapping);
            }
            else
            {
                Console.WriteLine("Error: Mappings file not found at: " + _mapping);
                return;
            }
            
            // Initialize and mount the provider
            provider.Initialize();
            await provider.MountAsync();
            Console.WriteLine("Provider initialized and mounted successfully\n");
            
            // Create graph generator and build index
            var graphGenerator = new GraphGenerator(provider);
            
            // Optional: Filter to only scan certain paths (e.g., "DataTable" or "Data")
            // Pass null to scan all .uasset files
            string? pathFilter = null;
            bool includeLoc = false;
            
            // Parse command line args for initial search or path filter
            if (args.Length > 0)
            {
                if (args[0] == "--filter" && args.Length > 1)
                {
                    pathFilter = args[1];
                    Console.WriteLine($"Using path filter: {pathFilter}");
                }
                else if (args[0] == "--include-loc" || args.Contains("--include-loc"))
                {
                    includeLoc = true;
                    Console.WriteLine("Including localization tables from /Data/TextDB/");
                }
            }
            
            if (!includeLoc)
            {
                Console.WriteLine("Excluding localization tables (/Data/TextDB/). Use --include-loc to include them.\n");
            }
            
            graphGenerator.BuildIndex(pathFilter, includeLoc);
            
            // If a search term was provided as argument, search and exit
            if (args.Length > 0 && args[0] != "--filter")
            {
                var searchTerm = args[0];
                bool exactMatch = args.Length > 1 && args[1] == "--exact";
                graphGenerator.PrintSearchResults(searchTerm, exactMatch);
            }
            else
            {
                // Start interactive mode
                graphGenerator.InteractiveSearch();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }

}