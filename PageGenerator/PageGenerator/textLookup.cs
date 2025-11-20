using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Assets.Exports.Engine;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Objects.Core.i18N;
using CUE4Parse.UE4.Assets.Objects.Properties;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PageGenerator
{
    internal class textLookup
    {
        
        private static UDataTable englishDump;
        private static Dictionary<string, FStructFallback>? byText;
        // You should (theoretically) be able to support other languages by changing this file name to another language's file (such as Loc_De for German)
        private const string _englishFilePath = "/Game/Data/TextDB/Loc_En";

        // Setup our language lookup dictionary, we only need to do this once
        public static void setup(DefaultFileProvider provider)
        {
            englishDump = provider.LoadPackageObject<UDataTable>(_englishFilePath);
            byText = englishDump.RowMap.ToDictionary(k => k.Key.Text, v => v.Value, StringComparer.Ordinal);
        }

        // Attempt to find a key value mapping in the English text database
        // If this returns false, the valueString will be empty
        public static bool TryLookupEnglishMapping(string searchText, out string valueString)
        {
            FStructFallback fallbackValue;
            if (byText == null)
            {   
                valueString = string.Empty;
                Console.WriteLine("text lookup dictionary is not initialized!");
                return false;
            }
            if (byText.TryGetValue(searchText, out fallbackValue))
            {
                return TryExtractFallbackString(fallbackValue, out valueString);
            }
            else
            {
                // cannot find key in dictionary
                valueString = string.Empty;
                return false;
            }
        }

        private static bool TryExtractFallbackString(FStructFallback fallback, out string valueFound)
        {
            if (fallback == null)
            {
                valueFound = string.Empty;
                return false;
            }

            // Try common typed properties first
            if (fallback.TryGetValue<FText>(out var ftext, "Text", "Value"))
            {
                valueFound = ftext.ToString();
                return true;
            }
            if (fallback.TryGetValue<StrProperty>(out var strProp, "Text", "Value"))
            {
                // StrProperty implementations vary; try known property names then ToString()
                var t = strProp.GetType();
                var p = t.GetProperty("Value") ?? t.GetProperty("Text") ?? t.GetProperty("Content");
                if (p != null)
                {
                    var v = p.GetValue(strProp);
                    if (v != null)
                    {
                        valueFound = v.ToString();
                        return true;
                    }
                }
                valueFound = strProp.ToString() ?? string.Empty;
                return true; 
            }

            // Try primitive/string direct unbox
            if (fallback.TryGetValue<string>(out var directString, "Text", "Value"))
            {
                valueFound = directString;
                return true;
            }

            // Generic getter (falls back to any stored object, then ToString)
            var maybe = fallback.GetOrDefault<object>("Text", null!);
            if (maybe != null)
            {
                valueFound = maybe.ToString() ?? string.Empty;
                return true;
            }

            // Give up, the value has a type that this function doesn't support / doesn't know how to parse
            valueFound = string.Empty;
            return false;
        }
    }
}
