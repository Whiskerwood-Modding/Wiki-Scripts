namespace PageGenerator
{
    public class DataTableConfig(string templateName, string dataTablePath, string outputFolderName, string structName)
    {
        public string TemplateName { get; set; } = templateName;
        public string DataTablePath { get; set; } = dataTablePath;
        public string OutputFolderName { get; set; } = outputFolderName;
        public string StructName { get; set; } = structName;
    }
    
    public static class DataTableConfigs
    {
        public static readonly Dictionary<string, DataTableConfig> Templates = new Dictionary<string, DataTableConfig>
        {
            {
                "CropsTemplate.txt", 
                new DataTableConfig(
                    "CropsTemplate.txt", 
                    "/Game/Data/AssetLookups/Crops.Crops", 
                    "Crops",
                    "FCropMasterSyncData"
                )
            },
            {
                "TechUnlocksV2Template.txt",
                new DataTableConfig(
                    "TechUnlocksV2Template.txt",
                    "/Game/Data/AssetLookups/TechUnlocksV2.TechUnlocksV2",
                    "TechUnlocksV2",
                    "FTechUnlock_V2"
                )
            },
            {
                "BuildingsTemplate.txt",
                new DataTableConfig(
                    "BuildingsTemplate.txt",
                    "/Game/Data/GridactorDefs_Sync.GridactorDefs_Sync",
                    "Buildings",
                    "FGridActorDefinition_MasterSyncFormat"
                )
            },
            {
                "ResourcesTemplate.txt",
                new DataTableConfig(
                    "ResourcesTemplate.txt",
                    "/Game/Data/AssetLookups/ResourceLookup.ResourceLookup",
                    "Resources",
                    "FResourceDef"
                )
            },
            {
                "BuildingsOverviewTemplate.txt",
                new DataTableConfig(
                    "BuildingsOverviewTemplate.txt",
                    "/Game/Data/GridactorDefs_Sync.GridactorDefs_Sync",
                    "BuildingsOverview",
                    "FGridActorDefinition_MasterSyncFormat"
                )
            }
            
            // EXAMPLE: Add more DataTable configurations here in the future
            // Uncomment and modify the following when you want to add a Buildings DataTable:
            /*
            , {
                "BuildingsTemplate.txt",
                new DataTableConfig(
                    "BuildingsTemplate.txt",
                    "/Game/Data/AssetLookups/Buildings.Buildings",  // Update this path
                    "Buildings",
                    "FBuildingMasterSyncData"  // Update this struct name
                )
            }
            */
        };
    }
}
