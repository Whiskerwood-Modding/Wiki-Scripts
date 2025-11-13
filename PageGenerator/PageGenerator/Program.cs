using CommunityToolkit.HighPerformance.Helpers;
using CUE4Parse.Encryption.Aes;
using CUE4Parse.FileProvider;
using CUE4Parse.GameTypes.FF7.Assets.Exports;
using CUE4Parse.MappingsProvider;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Engine;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Assets.Objects.Properties;
using CUE4Parse.UE4.Objects.Core.i18N;
using CUE4Parse.UE4.Objects.Core.Misc;
using CUE4Parse.UE4.Objects.UObject;
using CUE4Parse.UE4.Versions;
using Newtonsoft.Json;
using System.Data;
using static Org.BouncyCastle.Math.EC.ECCurve;

namespace PageGenerator;

class Program
{
    //This is the default steam install location
    private const string _pakDir = @"C:\Program Files (x86)\Steam\steamapps\common\Whiskerwood\Whiskerwood\Content\Paks";
    private const EGame _version = EGame.GAME_UE5_6;
    private const bool _replaceFiles = true;
    // The following paths assume you run this script from the REPO_TOP/PageGenerator/bin/debug/net8.0 folder (the default location visual studio runs it from when you use the debugger)     
    private const string _templateDir = @"..\..\..\..\PageTemplates";
    private const string _outputDir = @"..\..\..\..\Output";
    // This path assumes the Whiskerwood.usmap file is placed in the REPO_TOP/PageGenerator folder
    private const string _mapping = @"../../../../Whiskerwood.usmap";
    
    static async Task Main(string[] args)
    {
        Console.WriteLine("Starting DataTable extraction for MediaWiki templates...");
        
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
                Console.WriteLine("Warning: Mappings file not found at: " + _mapping);
            }
            
            // Initialize and mount the provider
            provider.Initialize();
            await provider.MountAsync();
            
            textLookup.setup(provider); 


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