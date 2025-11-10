using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using CUE4Parse.UE4.Assets.Objects;

namespace PageGenerator
{
    public class StructField
    {
        public string Name { get; set; } = "";
        public string Type { get; set; } = "";
        public string TemplateKey { get; set; } = "";
    }

    public static class StructParser
    {
        private static readonly Regex FieldRegex = new Regex(@"---@field\s+(\w+)\s+(\S+)", RegexOptions.Compiled);

        public static List<StructField> ParseStructDefinition(string structFilePath)
        {
            var fields = new List<StructField>();
            
            if (!File.Exists(structFilePath))
            {
                Console.WriteLine($"Struct definition file not found: {structFilePath}");
                return fields;
            }

            string[] lines = File.ReadAllLines(structFilePath);
            
            foreach (string line in lines)
            {
                var match = FieldRegex.Match(line);
                if (match.Success)
                {
                    string fieldName = match.Groups[1].Value;
                    string fieldType = match.Groups[2].Value;
                    
                    fields.Add(new StructField
                    {
                        Name = fieldName,
                        Type = fieldType,
                        TemplateKey = ConvertToTemplateKey(fieldName)
                    });
                }
            }

            Console.WriteLine($"Parsed {fields.Count} fields from {structFilePath}");
            return fields;
        }

        public static Dictionary<string, string> GeneratePropertyDictionary(List<StructField> fields, FStructFallback data)
        {
            var properties = new Dictionary<string, string>();

            foreach (var field in fields)
            {
                string value = ExtractValueByType(data, field.Name, field.Type);
                properties[field.TemplateKey] = value;
            }

            return properties;
        }

        private static string ConvertToTemplateKey(string fieldName)
        {
            // Convert PascalCase to camelCase for template keys
            if (string.IsNullOrEmpty(fieldName))
                return fieldName;
            
            return char.ToLowerInvariant(fieldName[0]) + fieldName[1..];
        }

        private static string ExtractValueByType(FStructFallback data, string propertyName, string propertyType)
        {
            // Handle TArray types
            if (propertyType.StartsWith("TArray<"))
            {
                return PropertyExtractor.GetArrayValue(data, propertyName);
            }

            return propertyType.ToLowerInvariant() switch
            {
                "float" or "double" => PropertyExtractor.GetFloatValue(data, propertyName),
                "int" or "int32" or "uint" or "uint32" => PropertyExtractor.GetIntValue(data, propertyName),
                "byte" or "uint8" => PropertyExtractor.GetByteValue(data, propertyName),
                "bool" or "boolean" => PropertyExtractor.GetBoolValue(data, propertyName),
                "fname" => PropertyExtractor.GetFNameValue(data, propertyName),
                "string" or "fstring" => PropertyExtractor.GetStringValue(data, propertyName),
                "fcolor" => PropertyExtractor.GetColorValue(data, propertyName),
                _ => PropertyExtractor.GetGenericValue(data, propertyName)
            };
        }
    }
}
