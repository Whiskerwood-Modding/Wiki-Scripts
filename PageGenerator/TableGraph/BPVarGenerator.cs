using System.Text;
using System.Text.RegularExpressions;

namespace TableGraph;

/// <summary>
/// Generates UE5 Blueprint Variable copy-paste buffers for DataTable map variables
/// </summary>
public class BPVarGenerator
{
    private readonly GraphGenerator _graphGenerator;
    
    public BPVarGenerator(GraphGenerator graphGenerator)
    {
        _graphGenerator = graphGenerator;
    }
    
    /// <summary>
    /// Generate a friendly name from a variable name (e.g., "WoodCharcoalRecipe" -> "Wood Charcoal Recipe")
    /// </summary>
    private static string MakeFriendlyName(string varName)
    {
        // Insert space before capital letters
        var spaced = Regex.Replace(varName, @"([a-z0-9])([A-Z])", "$1 $2");
        
        // Replace underscores with spaces
        spaced = spaced.Replace("_", " ").Trim();
        
        return string.IsNullOrEmpty(spaced) ? varName : spaced;
    }
    
    /// <summary>
    /// Build the default value string for the map
    /// </summary>
    private static string BuildDefaultValue(List<(string Key, string DefaultValue)> fields)
    {
        var pairs = new List<string>();
        
        foreach (var (key, value) in fields)
        {
            pairs.Add($"(\\\"{key}\\\", \\\"{value}\\\")");
        }
        
        return "(" + string.Join(",", pairs) + ")";
    }
    
    /// <summary>
    /// Generate a new GUID in uppercase hex format
    /// </summary>
    private static string GenerateGuid()
    {
        return Guid.NewGuid().ToString("N").ToUpper();
    }
    
    /// <summary>
    /// Generate the BPVar copy-paste buffer string
    /// </summary>
    public string GenerateBPVar(string varName, string tableName)
    {
        var schema = _graphGenerator.GetTableSchema(tableName, out var _);
        
        if (schema == null)
        {
            throw new ArgumentException($"No DataTable found matching: {tableName}");
        }
        
        var guid = GenerateGuid();
        var friendlyName = MakeFriendlyName(varName);
        var defaultValue = BuildDefaultValue(schema);
        
        var sb = new StringBuilder();
        sb.Append($"BPVar(VarName=\"{varName}\",");
        sb.Append($"VarGuid={guid},");
        sb.Append("VarType=(PinCategory=\"name\",PinSubCategory=\"\",PinSubCategoryObject=None,");
        sb.Append("PinSubCategoryMemberReference=(MemberParent=None,MemberName=\"\",");
        sb.Append("MemberGuid=00000000000000000000000000000000),");
        sb.Append("PinValueType=(TerminalCategory=\"string\",TerminalSubCategory=\"\",");
        sb.Append("TerminalSubCategoryObject=None,bTerminalIsConst=False,bTerminalIsWeakPointer=False,");
        sb.Append("bTerminalIsUObjectWrapper=False),ContainerType=Map,bIsReference=False,bIsConst=False,");
        sb.Append("bIsWeakPointer=False,bIsUObjectWrapper=False,bSerializeAsSinglePrecisionFloat=False),");
        sb.Append($"FriendlyName=\"{friendlyName}\",");
        sb.Append("Category=NSLOCTEXT(\"KismetSchema\", \"Default\", \"Default\"),");
        sb.Append("PropertyFlags=65541,RepNotifyFunc=\"\",ReplicationCondition=COND_None,MetaDataArray=,");
        sb.Append($"DefaultValue=\"{defaultValue}\")");
        
        return sb.ToString();
    }
    
    /// <summary>
    /// Generate BPVar with custom fields (not from indexed tables)
    /// </summary>
    public static string GenerateBPVarCustom(string varName, List<(string Key, string DefaultValue)> fields)
    {
        var guid = GenerateGuid();
        var friendlyName = MakeFriendlyName(varName);
        var defaultValue = BuildDefaultValue(fields);
        
        var sb = new StringBuilder();
        sb.Append($"BPVar(VarName=\"{varName}\",");
        sb.Append($"VarGuid={guid},");
        sb.Append("VarType=(PinCategory=\"name\",PinSubCategory=\"\",PinSubCategoryObject=None,");
        sb.Append("PinSubCategoryMemberReference=(MemberParent=None,MemberName=\"\",");
        sb.Append("MemberGuid=00000000000000000000000000000000),");
        sb.Append("PinValueType=(TerminalCategory=\"string\",TerminalSubCategory=\"\",");
        sb.Append("TerminalSubCategoryObject=None,bTerminalIsConst=False,bTerminalIsWeakPointer=False,");
        sb.Append("bTerminalIsUObjectWrapper=False),ContainerType=Map,bIsReference=False,bIsConst=False,");
        sb.Append("bIsWeakPointer=False,bIsUObjectWrapper=False,bSerializeAsSinglePrecisionFloat=False),");
        sb.Append($"FriendlyName=\"{friendlyName}\",");
        sb.Append("Category=NSLOCTEXT(\"KismetSchema\", \"Default\", \"Default\"),");
        sb.Append("PropertyFlags=65541,RepNotifyFunc=\"\",ReplicationCondition=COND_None,MetaDataArray=,");
        sb.Append($"DefaultValue=\"{defaultValue}\")");
        
        return sb.ToString();
    }
    
    /// <summary>
    /// Interactive mode for generating BPVars
    /// </summary>
    public void InteractiveGenerate()
    {
        Console.WriteLine("\n=== Blueprint Variable Generator ===");
        Console.WriteLine("\nThis will generate a BPVar based on the schema of an indexed DataTable.");
        Console.WriteLine("Enter 'back' to return to main menu");
        Console.WriteLine();
        
        while (true)
        {
            Console.Write("DataTable name or path: ");
            var tableName = Console.ReadLine()?.Trim();
            
            if (string.IsNullOrEmpty(tableName))
                continue;
                
            if (tableName.Equals("back", StringComparison.OrdinalIgnoreCase))
                return;
            
            // Try to get schema
            var schema = _graphGenerator.GetTableSchema(tableName, out var matchedTable);
            if (schema == null)
            {
                Console.WriteLine($"Error: No DataTable found matching '{tableName}'");
                Console.WriteLine("Use the 'tables' command to see available DataTables.");
                continue;
            }
            
            // Show the detected schema
            Console.WriteLine($"\nDetected {schema.Count} fields from DataTable '{matchedTable}':");
            foreach (var (key, defaultValue) in schema)
            {
                Console.WriteLine($"  {key}  {defaultValue}");
            }
            Console.WriteLine();
            
            Console.Write("Variable name: ");
            var varName = Console.ReadLine()?.Trim();
            
            if (string.IsNullOrEmpty(varName))
            {
                Console.WriteLine("Variable name cannot be empty");
                continue;
            }
            
            try
            {
                var result = GenerateBPVar(varName, tableName);
                Console.WriteLine("\n=== Copy the following to clipboard ===\n");
                Console.WriteLine(result);
                Console.WriteLine("\n=== End of copy buffer ===\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
            
            Console.WriteLine();
        }
    }
    
    /// <summary>
    /// Print schema details for a DataTable
    /// </summary>
    public void PrintTableSchema(string tableName)
    {
        var schema = _graphGenerator.GetTableSchema(tableName, out var _);
        
        if (schema == null)
        {
            Console.WriteLine($"No DataTable found matching: {tableName}");
            return;
        }
        
        Console.WriteLine($"\n=== Schema for {tableName} ===");
        Console.WriteLine($"Fields ({schema.Count}):");
        
        foreach (var (key, value) in schema)
        {
            Console.WriteLine($"  {key}: {value}");
        }
        
        Console.WriteLine();
    }
}

