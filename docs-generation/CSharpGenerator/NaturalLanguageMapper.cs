using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.IO;
using Shared;

public static class NaturalLanguageMapper
{
    private static readonly Dictionary<string, string> ParameterMappings = new();

    static NaturalLanguageMapper()
    {
        // Load mappings generated/mapped-parameters.json file
        var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        var projectRoot = Path.GetFullPath(Path.Combine(baseDirectory, "../../../.."));
        var filePath = Path.Combine(projectRoot,"nl-parameters.json");
        try
        {
            if (File.Exists(filePath))
            {
                var jsonContent = File.ReadAllText(filePath);
                var mappings = JsonSerializer.Deserialize<List<MappedParameter>>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (mappings != null)
                {
                    foreach (var mapping in mappings)
                    {
                        ParameterMappings[mapping.Parameter] = mapping.NaturalLanguage;
                    }
                }
            }
            else
            {
                Console.WriteLine($"Error: Mapping file not found at {filePath}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading parameter mappings: {ex.Message}");
        }
    }

    public static string GetNaturalLanguage(string parameterName)
    {

        if (string.IsNullOrEmpty(parameterName)) return "TBD";

        if (ParameterMappings.TryGetValue(parameterName, out var naturalLanguage))
        {
            return naturalLanguage;
        }

        Console.WriteLine($"Missing NaturalLanguage for parameter: {parameterName}");
        return "TBD";
    }
}
