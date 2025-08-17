using System.Text.Json;
using Shared;

namespace NaturalLanguageGenerator;

public static class NLParametersLoader
{
    private static readonly string nlParametersPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../nl-parameters.json"));

    public static string ParametersFilePath => nlParametersPath;

    public static Dictionary<string, string>? LoadParameters()
    {
        if (!File.Exists(nlParametersPath))
        {
            Console.WriteLine($"Warning: nl-parameters.json file not found at '{nlParametersPath}'.");
            return null;
        }

        try
        {
            var jsonArray = JsonSerializer.Deserialize<List<MappedParameter>>(File.ReadAllText(nlParametersPath));
            if (jsonArray == null)
            {
                return null;
            }

            // Convert the list of objects into a dictionary
            var nlParameters = jsonArray.ToDictionary(item => item.Parameter, item => item.NaturalLanguage);
            return nlParameters;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading nl-parameters.json: {ex.Message}");
            return null;
        }
    }
}
