using CUE4Parse.FileProvider;
using CUE4Parse.MappingsProvider;
using CUE4Parse.UE4.Versions;

namespace TableGraph;

class Program
{
    // Default values
    private const string _defaultPakDir = @"C:\Program Files (x86)\Steam\steamapps\common\Whiskerwood\Whiskerwood\Content\Paks";
    private const EGame _defaultVersion = EGame.GAME_UE5_6;
    private const string _defaultMapping = @"../Whiskerwood.usmap";
    
    static async Task Main(string[] args)
    {
        // Check for help flag
        if (args.Contains("--help") || args.Contains("-h"))
        {
            PrintHelp();
            return;
        }
        
        Console.WriteLine("=== DataTable Reference Graph Tool ===\n");
        Console.WriteLine("This tool scans all DataTables and lets you search for key references.\n");
        
        try
        {
            // Parse command line arguments
            string pakDir = GetArgValue(args, "--pak-dir", _defaultPakDir);
            string mapping = GetArgValue(args, "--mappings", _defaultMapping);
            string versionStr = GetArgValue(args, "--version", _defaultVersion.ToString());
            
            // Parse game version
            EGame version = _defaultVersion;
            if (!Enum.TryParse<EGame>(versionStr, true, out version))
            {
                Console.WriteLine($"Warning: Invalid game version '{versionStr}', using default: {_defaultVersion}");
                version = _defaultVersion;
            }
            
            // Show configuration
            Console.WriteLine($"Pak Directory: {pakDir}");
            Console.WriteLine($"Mappings File: {mapping}");
            Console.WriteLine($"Game Version: {version}\n");
            
            // Initialize file provider
            DefaultFileProvider provider = new DefaultFileProvider(pakDir, SearchOption.TopDirectoryOnly, new VersionContainer(version), StringComparer.OrdinalIgnoreCase);
            
            // Load mappings if available
            if (File.Exists(mapping))
            {
                provider.MappingsContainer = new FileUsmapTypeMappingsProvider(mapping);
                Console.WriteLine("Loaded mappings successfully");
            }
            else
            {
                Console.WriteLine("Error: Mappings file not found at: " + mapping);
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
                Console.WriteLine("Excluding localization tables (/Data/TextDB/) and Engine tables (/Engine/). Use --include-loc to include localization.\n");
            }
            else
            {
                Console.WriteLine("Excluding Engine tables (/Engine/).\n");
            }
            
            graphGenerator.BuildIndex(pathFilter, includeLoc);
            
            // Check for export flag
            var exportIndex = Array.IndexOf(args, "--export");
            if (exportIndex >= 0)
            {
                var outputPath = exportIndex + 1 < args.Length ? args[exportIndex + 1] : "DataTableIndex.json";
                graphGenerator.ExportToJson(outputPath);
                return;
            }
            
            // If a search term was provided as argument, search and exit
            if (args.Length > 0 && args[0] != "--filter" && args[0] != "--include-loc")
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
    
    private static string GetArgValue(string[] args, string flag, string defaultValue)
    {
        var index = Array.IndexOf(args, flag);
        if (index >= 0 && index + 1 < args.Length)
        {
            return args[index + 1];
        }
        return defaultValue;
    }
    
    private static void PrintHelp()
    {
        Console.WriteLine("=== DataTable Reference Graph Tool - Help ===\n");
        Console.WriteLine("Usage: dotnet run [options] [search-term]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --pak-dir <path>      Path to game Paks folder");
        Console.WriteLine("                        Default: C:\\Program Files (x86)\\Steam\\steamapps\\common\\Whiskerwood\\Whiskerwood\\Content\\Paks");
        Console.WriteLine();
        Console.WriteLine("  --mappings <path>     Path to .usmap mappings file");
        Console.WriteLine("                        Default: ../Whiskerwood.usmap");
        Console.WriteLine();
        Console.WriteLine("  --version <version>   Game engine version (e.g., GAME_UE5_6)");
        Console.WriteLine("                        Default: GAME_UE5_6");
        Console.WriteLine();
        Console.WriteLine("  --filter <path>       Only scan DataTables matching path filter");
        Console.WriteLine();
        Console.WriteLine("  --include-loc         Include localization tables from /Data/TextDB/");
        Console.WriteLine("                        (excluded by default)");
        Console.WriteLine();
        Console.WriteLine("  --export [path]       Export index to JSON file and exit");
        Console.WriteLine("                        Default filename: DataTableIndex.json");
        Console.WriteLine();
        Console.WriteLine("  --help, -h            Show this help message");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  dotnet run");
        Console.WriteLine("  dotnet run recipe.lumbermill");
        Console.WriteLine("  dotnet run --export MyIndex.json");
        Console.WriteLine("  dotnet run --pak-dir \"D:\\Games\\Whiskerwood\\Paks\"");
        Console.WriteLine("  dotnet run --filter Data --export");
        Console.WriteLine("  dotnet run --include-loc recipe.bread");
        Console.WriteLine();
        Console.WriteLine("Interactive Commands:");
        Console.WriteLine("  search <key>          Search for partial matches (alias: s)");
        Console.WriteLine("  exact <key>           Search for exact matches (alias: e)");
        Console.WriteLine("  keys <pattern>        List all keys matching pattern (alias: k)");
        Console.WriteLine("  tables                List all indexed DataTables (alias: t)");
        Console.WriteLine("  rows <table>          List all rows in a DataTable (alias: r)");
        Console.WriteLine("  row <table> <row>     Show full data for a specific row");
        Console.WriteLine("  export <path>         Export index to JSON file (alias: dump)");
        Console.WriteLine("  help                  Show available commands");
        Console.WriteLine("  quit                  Exit the tool (alias: q, exit)");
    }
}