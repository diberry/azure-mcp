using System.Text.Json;
using Shared;

namespace NaturalLanguageGenerator;


public static class TextCleanup
{
    private static string? nlParametersPath = null;

    private static string? textReplacerParametersPath = null;

    public static string? TextReplacerParametersFilePath => textReplacerParametersPath;
    public static string? ParametersFilePath => nlParametersPath;

    public static MappedParameter[]? mappedParameters { get; private set; }

    private static Dictionary<string, string>? mappedParametersDict;

    public static MappedParameter[]? LoadFiles(List<string> RequiredFiles)
    {
        try
        {
            if (RequiredFiles == null || RequiredFiles.Count == 0)
            {
                Console.WriteLine("Warning: RequiredFiles list is null or empty. Returning null.");
                return null;
            }

            List<MappedParameter> combinedParameters = new();

            for (int i = 0; i < RequiredFiles.Count; i++)
            {
                var file = RequiredFiles[i];

                if (file.IndexOf("nl-parameters.json", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    nlParametersPath = file;
                }
                if (file.IndexOf("static-text-replacement.json", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    textReplacerParametersPath = file;
                }
            }

            if (File.Exists(nlParametersPath))
            {
                var nlJsonArray = JsonSerializer.Deserialize<List<MappedParameter>>(File.ReadAllText(nlParametersPath));
                if (nlJsonArray != null)
                {
                    combinedParameters.AddRange(nlJsonArray);
                }
            }
            else
            {
                Console.WriteLine($"Warning: nl-parameters.json file not found at '{nlParametersPath}'.");
            }

            if (File.Exists(textReplacerParametersPath))
            {
                var textReplaceJsonArray = JsonSerializer.Deserialize<List<MappedParameter>>(File.ReadAllText(textReplacerParametersPath));
                if (textReplaceJsonArray != null)
                {
                    combinedParameters.AddRange(textReplaceJsonArray);
                }
            }
            else
            {
                Console.WriteLine($"Warning: static-text-replacement.json file not found at '{textReplacerParametersPath}'.");
            }

            // Combine and deduplicate parameters based on the 'Parameter' property
            mappedParameters = combinedParameters
                .GroupBy(p => p.Parameter)
                .Select(g => g.First())
                .ToArray();

            // Update the dictionary for quick lookups
            mappedParametersDict = mappedParameters.ToDictionary(item => item.Parameter, item => item.NaturalLanguage);

            return mappedParameters;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading parameters: {ex.Message}");
            return null;
        }
    }

    private static string[] SplitAndTransformProgrammaticName(string programmaticName)
    {
        // Split the programmatic name into words
        var words = programmaticName.Split('-');

        // Replace known `bad text` for Docs requirements
        for (int i = 1; i < words.Length; i++)
        {
            if (mappedParametersDict != null && mappedParametersDict.TryGetValue(words[i].ToUpper(), out var naturalLanguageValue))
            {
                words[i] = naturalLanguageValue;
            }
        }

        // Capitalize the first word
        words[0] = char.ToUpper(words[0][0]) + words[0].Substring(1);

        return words;
    }

    public static string NormalizeParameter(string programmaticName)
    {
        if (string.IsNullOrWhiteSpace(programmaticName))
        {
            Console.WriteLine("Warning NormalizeParameterNames: Programmatic name is null or empty. Returning 'TBD'.");
            return "TBD";
        }

        if (mappedParametersDict != null && mappedParametersDict.TryGetValue(programmaticName, out var naturalLanguageName))
        {
            Console.WriteLine($"Found natural language name for '{programmaticName}': {naturalLanguageName}");
            return naturalLanguageName;
        }

        // Word isn't in list - break it apart and fix it
        var words = SplitAndTransformProgrammaticName(programmaticName);

        Console.WriteLine($"Converted '{programmaticName}' to natural language: {string.Join(" ", words)}");
        return "TBD (add to nl-parameters.json) for " + programmaticName + ": " + string.Join(" ", words);
    }
}
