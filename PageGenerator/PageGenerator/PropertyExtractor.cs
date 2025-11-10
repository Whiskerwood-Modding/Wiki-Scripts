using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Objects.UObject;

namespace PageGenerator
{
    public static class PropertyExtractor
    {
        // The following paths assume you run this script from the REPO_TOP/PageGenerator/bin/debug/net8.0 folder (the default location visual studio runs it from when you use the debugger)
        private const string StructDefinitionsDir = @"..\..\..\..\StructDefinitions";

        // Generic property extraction method that uses struct definitions
        public static Dictionary<string, string> ExtractPropertiesForTemplate(string templateName, FStructFallback data, string structName)
        {
            // Try to use struct-based extraction first
            if (!string.IsNullOrEmpty(structName))
            {
                return ExtractPropertiesFromStruct(data, structName);
            }
            
            // Fall back to generic extraction if no struct name provided
            return templateName switch
            {
                _ => ExtractGenericProperties(data)
            };
        }

        private static Dictionary<string, string> ExtractPropertiesFromStruct(FStructFallback data, string structName)
        {
            try
            {
                string structFilePath = Path.Combine(StructDefinitionsDir, $"{structName}.txt");
                var fields = StructParser.ParseStructDefinition(structFilePath);
                
                if (fields.Count == 0)
                {
                    Console.WriteLine($"No fields found in struct definition for {structName}, falling back to generic extraction");
                    return ExtractGenericProperties(data);
                }
                
                Console.WriteLine($"Using struct definition for {structName} with {fields.Count} fields");
                return StructParser.GeneratePropertyDictionary(fields, data);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error using struct definition for {structName}: {ex.Message}");
                Console.WriteLine("Falling back to generic property extraction");
                return ExtractGenericProperties(data);
            }
        }

        // Generic fallback that extracts all properties as strings
        private static Dictionary<string, string> ExtractGenericProperties(FStructFallback data)
        {
            var properties = new Dictionary<string, string>();
            
            foreach (var property in data.Properties)
            {
                try
                {
                    var propertyName = property.Name.Text;
                    var value = property.Tag?.GenericValue?.ToString() ?? "N/A";
                    properties[propertyName.ToLowerInvariant()] = value;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error extracting property {property.Name.Text}: {ex.Message}");
                    properties[property.Name.Text.ToLowerInvariant()] = "N/A";
                }
            }
            
            return properties;
        }

        // Public helper methods that can be used by StructParser
        public static string GetFloatValue(FStructFallback data, string propertyName)
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

        public static string GetByteValue(FStructFallback data, string propertyName)
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

        public static string GetFNameValue(FStructFallback data, string propertyName)
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

        // Generic value extractor that tries different types
        public static string GetGenericValue(FStructFallback data, string propertyName)
        {
            try
            {
                // Try different types in order of likelihood
                if (data.TryGetValue<string>(out var stringVal, propertyName))
                    return stringVal;
                if (data.TryGetValue<FName>(out var fnameVal, propertyName))
                    return fnameVal.Text;
                if (data.TryGetValue<float>(out var floatVal, propertyName))
                    return floatVal.ToString("F2", CultureInfo.InvariantCulture);
                if (data.TryGetValue<int>(out var intVal, propertyName))
                    return intVal.ToString();
                if (data.TryGetValue<bool>(out var boolVal, propertyName))
                    return boolVal.ToString();
                if (data.TryGetValue<byte>(out var byteVal, propertyName))
                    return byteVal.ToString();
                
                // If none of the above work, try to get the raw value
                foreach (var property in data.Properties)
                {
                    if (property.Name.Text == propertyName)
                    {
                        return property.Tag?.GenericValue?.ToString() ?? "N/A";
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting generic value for {propertyName}: {ex.Message}");
            }
            return "N/A";
        }

        public static string GetStringValue(FStructFallback data, string propertyName)
        {
            try
            {
                if (data.TryGetValue<string>(out var stringVal, propertyName))
                {
                    return stringVal;
                }
                if (data.TryGetValue<FName>(out var fnameVal, propertyName))
                {
                    return fnameVal.Text;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting string value for {propertyName}: {ex.Message}");
            }
            return "N/A";
        }

        public static string GetIntValue(FStructFallback data, string propertyName)
        {
            try
            {
                if (data.TryGetValue<int>(out var intVal, propertyName))
                {
                    return intVal.ToString();
                }
                if (data.TryGetValue<uint>(out var uintVal, propertyName))
                {
                    return uintVal.ToString();
                }
                if (data.TryGetValue<long>(out var longVal, propertyName))
                {
                    return longVal.ToString();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting int value for {propertyName}: {ex.Message}");
            }
            return "N/A";
        }

        public static string GetBoolValue(FStructFallback data, string propertyName)
        {
            try
            {
                if (data.TryGetValue<bool>(out var boolVal, propertyName))
                {
                    return boolVal.ToString();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting bool value for {propertyName}: {ex.Message}");
            }
            return "N/A";
        }

        public static string GetArrayValue(FStructFallback data, string propertyName)
        {
            try
            {
                // Try to get as a generic array first
                if (data.TryGetValue<object[]>(out var objectArray, propertyName))
                {
                    var items = objectArray.Select(item => item?.ToString() ?? "").Where(s => !string.IsNullOrEmpty(s));
                    return string.Join(", ", items);
                }

                // Try to get as a list
                if (data.TryGetValue<List<object>>(out var objectList, propertyName))
                {
                    var items = objectList.Select(item => item?.ToString() ?? "").Where(s => !string.IsNullOrEmpty(s));
                    return string.Join(", ", items);
                }

                // Try to get as FName array for common cases like childUnlocks, prerequisiteUnlocks
                if (data.TryGetValue<FName[]>(out var fnameArray, propertyName))
                {
                    var items = fnameArray.Select(fname => fname.Text).Where(s => !string.IsNullOrEmpty(s));
                    return string.Join(", ", items);
                }

                // Try to get as List<FName>
                if (data.TryGetValue<List<FName>>(out var fnameList, propertyName))
                {
                    var items = fnameList.Select(fname => fname.Text).Where(s => !string.IsNullOrEmpty(s));
                    return string.Join(", ", items);
                }

                // Fall back to generic extraction
                foreach (var property in data.Properties)
                {
                    if (property.Name.Text == propertyName)
                    {
                        var genericValue = property.Tag?.GenericValue;
                        if (genericValue != null)
                        {
                            // Try to parse as array-like string
                            var stringValue = genericValue.ToString();
                            if (stringValue?.Contains('[') == true || stringValue?.Contains(',') == true)
                            {
                                return stringValue;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting array value for {propertyName}: {ex.Message}");
            }
            return "[]";
        }

        public static string GetColorValue(FStructFallback data, string propertyName)
        {
            try
            {
                // Try to get as a color structure
                if (data.TryGetValue<object>(out var colorObj, propertyName))
                {
                    // FColor might be represented as a struct with R, G, B, A components
                    var colorString = colorObj?.ToString();
                    if (!string.IsNullOrEmpty(colorString))
                    {
                        return colorString;
                    }
                }

                // Try to get as individual components if it's a struct
                foreach (var property in data.Properties)
                {
                    if (property.Name.Text == propertyName)
                    {
                        var genericValue = property.Tag?.GenericValue;
                        if (genericValue != null)
                        {
                            return genericValue.ToString() ?? "N/A";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting color value for {propertyName}: {ex.Message}");
            }
            return "N/A";
        }
    }
}
