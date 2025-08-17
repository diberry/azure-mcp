using System.Text.Json;

namespace NaturalLanguageGenerator;

public static class NLParametersLoader
{
    private static readonly string nlParametersPath = Path.Combine("./docs-generation", "nl-parameters.json");

    public static Dictionary<string, string>? LoadParameters()
    {
        if (!File.Exists(nlParametersPath))
        {
            Console.WriteLine($"Warning: nl-parameters.json file not found at '{Path.GetFullPath(nlParametersPath)}'.");
            return null;
        }

        try
        {
            var nlParameters = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(nlParametersPath));
            return nlParameters;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading nl-parameters.json: {ex.Message}");
            return null;
        }
    }
}
