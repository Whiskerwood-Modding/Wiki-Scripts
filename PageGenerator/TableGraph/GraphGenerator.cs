using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Assets.Exports.Engine;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Assets.Objects.Properties;
using CUE4Parse.Utils;

namespace TableGraph;

/// <summary>
/// Represents a reference found in a DataTable
/// </summary>
public class DataTableReference
{
    public string DataTablePath { get; set; } = string.Empty;
    public string RowKey { get; set; } = string.Empty;
    public string PropertyPath { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    
    public override string ToString() => 
        $"[{DataTablePath}] Row: {RowKey}, Property: {PropertyPath}, Value: {Value}";
}

/// <summary>
/// Index entry for fast lookups
/// </summary>
public class IndexEntry
{
    public string DataTablePath { get; set; } = string.Empty;
    public string RowKey { get; set; } = string.Empty;
    public string PropertyPath { get; set; } = string.Empty;
}

public class GraphGenerator
{
    private readonly IFileProvider _provider;
    
    // Index: maps string values to where they appear
    private readonly Dictionary<string, List<IndexEntry>> _valueIndex = new(StringComparer.OrdinalIgnoreCase);
    
    // Store all extracted data for detailed lookups
    private readonly Dictionary<string, Dictionary<string, Dictionary<string, object>>> _tableData = new();
    
    public GraphGenerator(IFileProvider provider)
    {
        _provider = provider;
    }
    
    /// <summary>
    /// Scans all DataTables in the game and builds an index of all values
    /// </summary>
    public void BuildIndex(string? pathFilter = null, bool includeLoc = false)
    {
        Console.WriteLine("Scanning for DataTables...");
        
        var dataTableFiles = _provider.Files.Values
            .Where(f => f.Extension.Equals("uasset", StringComparison.OrdinalIgnoreCase))
            .Where(f => pathFilter == null || f.Path.Contains(pathFilter, StringComparison.OrdinalIgnoreCase))
            .Where(f => includeLoc || !f.Path.Contains("/Data/TextDB/", StringComparison.OrdinalIgnoreCase))
            .ToList();
        
        Console.WriteLine($"Found {dataTableFiles.Count} potential asset files to scan");
        
        int processed = 0;
        int dataTables = 0;
        
        foreach (var file in dataTableFiles)
        {
            try
            {
                // Try to load as DataTable
                var pathWithoutExtension = file.Path.SubstringBeforeLast('.');
                if (_provider.TryLoadPackageObject<UDataTable>(pathWithoutExtension, out var dataTable))
                {
                    IndexDataTable(pathWithoutExtension, dataTable);
                    dataTables++;
                }
            }
            catch
            {
                // Not a DataTable or failed to load - skip
            }
            
            processed++;
            if (processed % 100 == 0)
            {
                Console.WriteLine($"Processed {processed}/{dataTableFiles.Count} files, found {dataTables} DataTables...");
            }
        }
        
        Console.WriteLine($"Indexing complete! Found {dataTables} DataTables with {_valueIndex.Count} unique values.");
    }
    
    /// <summary>
    /// Index a single DataTable
    /// </summary>
    private void IndexDataTable(string tablePath, UDataTable dataTable)
    {
        var tableEntries = new Dictionary<string, Dictionary<string, object>>();
        
        foreach (var row in dataTable.RowMap)
        {
            var rowKey = row.Key.Text;
            var rowData = new Dictionary<string, object>();
            
            // Index the row key itself
            AddToIndex(rowKey, tablePath, rowKey, "RowKey");
            
            // Extract and index all properties
            ExtractAndIndexProperties(row.Value.Properties, tablePath, rowKey, "", rowData);
            
            tableEntries[rowKey] = rowData;
        }
        
        _tableData[tablePath] = tableEntries;
    }
    
    /// <summary>
    /// Recursively extract properties and add them to the index
    /// </summary>
    private void ExtractAndIndexProperties(List<FPropertyTag> properties, string tablePath, string rowKey, string pathPrefix, Dictionary<string, object> rowData)
    {
        foreach (var prop in properties)
        {
            var propName = prop.Name.Text;
            var currentPath = string.IsNullOrEmpty(pathPrefix) ? propName : $"{pathPrefix}.{propName}";
            
            ExtractAndIndexValue(prop.Tag, tablePath, rowKey, currentPath, rowData, propName);
        }
    }
    
    /// <summary>
    /// Extract a value from a property tag and index it
    /// </summary>
    private void ExtractAndIndexValue(FPropertyTagType? tag, string tablePath, string rowKey, string propertyPath, Dictionary<string, object> rowData, string propName)
    {
        if (tag == null) return;
        
        var value = tag.GenericValue;
        if (value == null) return;
        
        switch (tag)
        {
            case ArrayProperty arrayProp:
                var arrayValues = new List<object>();
                var array = arrayProp.Value;
                if (array?.Properties != null)
                {
                    for (int i = 0; i < array.Properties.Count; i++)
                    {
                        var elementPath = $"{propertyPath}[{i}]";
                        var elementData = new Dictionary<string, object>();
                        ExtractAndIndexValue(array.Properties[i], tablePath, rowKey, elementPath, elementData, $"{propName}[{i}]");
                        if (elementData.Count > 0)
                            arrayValues.Add(elementData.Values.First());
                        else
                            arrayValues.Add(array.Properties[i].GenericValue?.ToString() ?? "");
                    }
                }
                rowData[propName] = arrayValues;
                break;
                
            case StructProperty structProp:
                var structValue = structProp.Value;
                if (structValue?.StructType is FStructFallback fallback)
                {
                    var structData = new Dictionary<string, object>();
                    ExtractAndIndexProperties(fallback.Properties, tablePath, rowKey, propertyPath, structData);
                    rowData[propName] = structData;
                }
                else
                {
                    // Other struct types - just convert to string
                    var strValue = value.ToString() ?? "";
                    IndexStringValue(strValue, tablePath, rowKey, propertyPath);
                    rowData[propName] = strValue;
                }
                break;
                
            case MapProperty mapProp:
                var map = mapProp.Value;
                if (map?.Properties != null)
                {
                    var mapData = new Dictionary<string, object>();
                    int mapIndex = 0;
                    foreach (var kvp in map.Properties)
                    {
                        var keyStr = kvp.Key.GenericValue?.ToString() ?? $"key{mapIndex}";
                        var keyPath = $"{propertyPath}.Key[{mapIndex}]";
                        var valuePath = $"{propertyPath}.Value[{mapIndex}]";
                        
                        IndexStringValue(keyStr, tablePath, rowKey, keyPath);
                        
                        var valueData = new Dictionary<string, object>();
                        if (kvp.Value != null)
                        {
                            ExtractAndIndexValue(kvp.Value, tablePath, rowKey, valuePath, valueData, keyStr);
                        }
                        mapData[keyStr] = valueData.Count > 0 ? valueData.Values.First() : "";
                        mapIndex++;
                    }
                    rowData[propName] = mapData;
                }
                break;
                
            case SetProperty setProp:
                var set = setProp.Value;
                if (set?.Properties != null)
                {
                    var setValues = new List<object>();
                    for (int i = 0; i < set.Properties.Count; i++)
                    {
                        var elementPath = $"{propertyPath}[{i}]";
                        var elementData = new Dictionary<string, object>();
                        ExtractAndIndexValue(set.Properties[i], tablePath, rowKey, elementPath, elementData, $"{propName}[{i}]");
                        if (elementData.Count > 0)
                            setValues.Add(elementData.Values.First());
                    }
                    rowData[propName] = setValues;
                }
                break;
                
            default:
                // Simple value types - index as string
                var simpleValue = value.ToString() ?? "";
                IndexStringValue(simpleValue, tablePath, rowKey, propertyPath);
                rowData[propName] = simpleValue;
                break;
        }
    }
    
    /// <summary>
    /// Index a string value
    /// </summary>
    private void IndexStringValue(string value, string tablePath, string rowKey, string propertyPath)
    {
        if (string.IsNullOrWhiteSpace(value) || value == "None" || value == "0" || value == "False" || value == "True")
            return;
        
        AddToIndex(value, tablePath, rowKey, propertyPath);
    }
    
    /// <summary>
    /// Add a value to the index
    /// </summary>
    private void AddToIndex(string value, string tablePath, string rowKey, string propertyPath)
    {
        if (!_valueIndex.TryGetValue(value, out var entries))
        {
            entries = new List<IndexEntry>();
            _valueIndex[value] = entries;
        }
        
        entries.Add(new IndexEntry
        {
            DataTablePath = tablePath,
            RowKey = rowKey,
            PropertyPath = propertyPath
        });
    }
    
    /// <summary>
    /// Search for all references to a specific key/value
    /// </summary>
    public List<DataTableReference> SearchReferences(string searchKey, bool exactMatch = false, bool caseSensitive = false)
    {
        var results = new List<DataTableReference>();
        var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        
        foreach (var kvp in _valueIndex)
        {
            bool matches = exactMatch 
                ? kvp.Key.Equals(searchKey, comparison)
                : kvp.Key.Contains(searchKey, comparison);
            
            if (matches)
            {
                foreach (var entry in kvp.Value)
                {
                    results.Add(new DataTableReference
                    {
                        DataTablePath = entry.DataTablePath,
                        RowKey = entry.RowKey,
                        PropertyPath = entry.PropertyPath,
                        Value = kvp.Key
                    });
                }
            }
        }
        
        return results;
    }
    
    /// <summary>
    /// Get all unique values that contain a search string
    /// </summary>
    public List<string> FindMatchingKeys(string searchKey, bool caseSensitive = false)
    {
        var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        
        return _valueIndex.Keys
            .Where(k => k.Contains(searchKey, comparison))
            .Distinct()
            .OrderBy(k => k)
            .ToList();
    }
    
    /// <summary>
    /// Get all indexed DataTable paths
    /// </summary>
    public List<string> GetIndexedTables()
    {
        return _tableData.Keys.OrderBy(k => k).ToList();
    }
    
    /// <summary>
    /// Get all row keys for a specific DataTable
    /// </summary>
    public List<string> GetTableRows(string tablePath)
    {
        if (_tableData.TryGetValue(tablePath, out var table))
        {
            return table.Keys.OrderBy(k => k).ToList();
        }
        return new List<string>();
    }
    
    /// <summary>
    /// Get full row data for a specific row
    /// </summary>
    public Dictionary<string, object>? GetRowData(string tablePath, string rowKey)
    {
        if (_tableData.TryGetValue(tablePath, out var table))
        {
            if (table.TryGetValue(rowKey, out var row))
            {
                return row;
            }
        }
        return null;
    }
    
    /// <summary>
    /// Print search results in a formatted way
    /// </summary>
    public void PrintSearchResults(string searchKey, bool exactMatch = false)
    {
        Console.WriteLine($"\n=== Searching for: \"{searchKey}\" (exact={exactMatch}) ===\n");
        
        var results = SearchReferences(searchKey, exactMatch);
        
        if (results.Count == 0)
        {
            Console.WriteLine("No references found.");
            
            // Suggest similar keys
            var similar = FindMatchingKeys(searchKey).Take(10).ToList();
            if (similar.Count > 0)
            {
                Console.WriteLine("\nDid you mean one of these?");
                foreach (var key in similar)
                {
                    Console.WriteLine($"  - {key}");
                }
            }
            return;
        }
        
        // Group by DataTable
        var grouped = results
            .GroupBy(r => r.DataTablePath)
            .OrderBy(g => g.Key)
            .ToList();
        
        Console.WriteLine($"Found {results.Count} references in {grouped.Count} DataTable(s):\n");
        
        foreach (var group in grouped)
        {
            Console.WriteLine($" {group.Key}");
            
            // Group by row within table
            var byRow = group.GroupBy(r => r.RowKey).OrderBy(r => r.Key);
            
            foreach (var rowGroup in byRow)
            {
                Console.WriteLine($"  Row: {rowGroup.Key}");
                foreach (var reference in rowGroup.OrderBy(r => r.PropertyPath))
                {
                    Console.WriteLine($"    └─ {reference.PropertyPath}: {reference.Value}");
                }
            }
            Console.WriteLine();
        }
    }
    
    /// <summary>
    /// Interactive search mode
    /// </summary>
    public void InteractiveSearch()
    {
        Console.WriteLine("\n=== DataTable Reference Search Tool ===");
        Console.WriteLine("Commands:");
        Console.WriteLine("  search <key>     - Search for partial matches");
        Console.WriteLine("  exact <key>      - Search for exact matches");
        Console.WriteLine("  keys <pattern>   - List all keys matching pattern");
        Console.WriteLine("  tables           - List all indexed DataTables");
        Console.WriteLine("  rows <table>     - List all rows in a DataTable");
        Console.WriteLine("  row <table> <row> - Show full row data");
        Console.WriteLine("  quit             - Exit");
        Console.WriteLine();
        
        while (true)
        {
            Console.Write("\n> ");
            var input = Console.ReadLine()?.Trim();
            
            if (string.IsNullOrEmpty(input))
                continue;
            
            var parts = input.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            var command = parts[0].ToLowerInvariant();
            var arg = parts.Length > 1 ? parts[1] : "";
            
            switch (command)
            {
                case "search":
                case "s":
                    if (string.IsNullOrEmpty(arg))
                    {
                        Console.WriteLine("Usage: search <key>");
                        continue;
                    }
                    PrintSearchResults(arg, exactMatch: false);
                    break;
                    
                case "exact":
                case "e":
                    if (string.IsNullOrEmpty(arg))
                    {
                        Console.WriteLine("Usage: exact <key>");
                        continue;
                    }
                    PrintSearchResults(arg, exactMatch: true);
                    break;
                    
                case "keys":
                case "k":
                    if (string.IsNullOrEmpty(arg))
                    {
                        Console.WriteLine("Usage: keys <pattern>");
                        continue;
                    }
                    var keys = FindMatchingKeys(arg);
                    Console.WriteLine($"\nFound {keys.Count} matching keys:");
                    foreach (var key in keys.Take(50))
                    {
                        Console.WriteLine($"  {key}");
                    }
                    if (keys.Count > 50)
                        Console.WriteLine($"  ... and {keys.Count - 50} more");
                    break;
                    
                case "tables":
                case "t":
                    var tables = GetIndexedTables();
                    Console.WriteLine($"\nIndexed {tables.Count} DataTables:");
                    foreach (var table in tables)
                    {
                        Console.WriteLine($"  {table}");
                    }
                    break;
                    
                case "rows":
                case "r":
                    if (string.IsNullOrEmpty(arg))
                    {
                        Console.WriteLine("Usage: rows <tablePath>");
                        continue;
                    }
                    // Find matching table
                    var matchingTable = GetIndexedTables()
                        .FirstOrDefault(t => t.Contains(arg, StringComparison.OrdinalIgnoreCase));
                    if (matchingTable == null)
                    {
                        Console.WriteLine($"No table found matching: {arg}");
                        continue;
                    }
                    var rows = GetTableRows(matchingTable);
                    Console.WriteLine($"\nRows in {matchingTable} ({rows.Count}):");
                    foreach (var row in rows.Take(100))
                    {
                        Console.WriteLine($"  {row}");
                    }
                    if (rows.Count > 100)
                        Console.WriteLine($"  ... and {rows.Count - 100} more");
                    break;
                    
                case "row":
                    var rowParts = arg.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                    if (rowParts.Length < 2)
                    {
                        Console.WriteLine("Usage: row <tablePath> <rowKey>");
                        continue;
                    }
                    var tableMatch = GetIndexedTables()
                        .FirstOrDefault(t => t.Contains(rowParts[0], StringComparison.OrdinalIgnoreCase));
                    if (tableMatch == null)
                    {
                        Console.WriteLine($"No table found matching: {rowParts[0]}");
                        continue;
                    }
                    var rowData = GetRowData(tableMatch, rowParts[1]);
                    if (rowData == null)
                    {
                        Console.WriteLine($"Row not found: {rowParts[1]}");
                        continue;
                    }
                    Console.WriteLine($"\nRow data for {tableMatch} -> {rowParts[1]}:");
                    PrintRowData(rowData, "  ");
                    break;
                    
                case "quit":
                case "q":
                case "exit":
                    return;
                    
                default:
                    Console.WriteLine($"Unknown command: {command}");
                    break;
            }
        }
    }
    
    private void PrintRowData(object data, string indent)
    {
        switch (data)
        {
            case Dictionary<string, object> dict:
                foreach (var kvp in dict)
                {
                    if (kvp.Value is Dictionary<string, object> || kvp.Value is List<object>)
                    {
                        Console.WriteLine($"{indent}{kvp.Key}:");
                        PrintRowData(kvp.Value, indent + "  ");
                    }
                    else
                    {
                        Console.WriteLine($"{indent}{kvp.Key}: {kvp.Value}");
                    }
                }
                break;
                
            case List<object> list:
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i] is Dictionary<string, object> || list[i] is List<object>)
                    {
                        Console.WriteLine($"{indent}[{i}]:");
                        PrintRowData(list[i], indent + "  ");
                    }
                    else
                    {
                        Console.WriteLine($"{indent}[{i}]: {list[i]}");
                    }
                }
                break;
                
            default:
                Console.WriteLine($"{indent}{data}");
                break;
        }
    }
}