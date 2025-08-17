using System.Text.Json;

namespace NaturalLanguageGenerator;

public static class NLP_Name
{
    public static string ToNaturalLanguage(string programmaticName)
    {
        if (string.IsNullOrWhiteSpace(programmaticName))
        {
            Console.WriteLine("Warning: Programmatic name is null or empty. Returning 'TBD'.");
            return "TBD";
        }

        // Use the NLParametersLoader class to load parameters
        var nlParameters = NLParametersLoader.LoadParameters();
        if (nlParameters == null)
        {
            var path = NLParametersLoader.ParametersFilePath;

            Console.WriteLine($"Warning: nl-parameters.json file not found or could not be loaded from '{path ?? "unknown"}'. Proceeding with default conversion.");
            return "TBD";
        }

        if (nlParameters.TryGetValue(programmaticName, out var naturalLanguageName))
        {
            Console.WriteLine($"Found natural language name for '{programmaticName}': {naturalLanguageName}");
            return naturalLanguageName;
        }

        Console.WriteLine($"No natural language name found for '{programmaticName}'. Using default conversion.");

        // Define a list of words to treat as acronyms
        var acronyms = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "ID", "AI", "URL" };

        // Replace hyphens with spaces and capitalize only the first word
        var words = programmaticName.Split('-');
        words[0] = char.ToUpper(words[0][0]) + words[0].Substring(1);

        // Capitalize acronyms
        for (int i = 1; i < words.Length; i++)
        {
            if (acronyms.Contains(words[i].ToUpper()))
            {
                words[i] = words[i].ToUpper();
            }
        }

        Console.WriteLine($"Converted '{programmaticName}' to natural language: {string.Join(" ", words)}");
        return string.Join(" ", words);
    }
}
