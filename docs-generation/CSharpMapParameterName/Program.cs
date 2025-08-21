using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Shared;

class Program
{
    static void Main(string[] args)
    {
        string cliOutputPath = "./generated/cli-output.json";
        string parametersPath = "./generated/parameters.json";
        string outputPath = "./nl-parameters.json"; // Updated path to save the file in the ./data subdirectory

        try
        {
            // Update the JSON deserialization to handle potential mismatches in structure
            try
            {
                // Read and parse cli-output.json
                var cliOutputJson = File.ReadAllText(cliOutputPath);
                var cliOutputWrapper = JsonSerializer.Deserialize<CliOutput>(cliOutputJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                var cliOutput = cliOutputWrapper?.Results ?? new List<Command>();

                // Read and parse parameters.json
                var parametersJson = File.ReadAllText(parametersPath);
                var parameters = JsonSerializer.Deserialize<List<Parameter>>(parametersJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new List<Parameter>();

                // Map origin to natural language names
                var mappedParameters = new List<MappedParameter>();

                // Add logging to understand why the output file might be empty
                Console.WriteLine("CLI Output parsed: " + cliOutput.Count + " commands found.");
                Console.WriteLine("Parameters parsed: " + parameters.Count + " parameters found.");

                // Define an array of terms that should always be uppercased
                var alwaysUppercase = new[] { "id", "url", "ai" };

                foreach (var command in cliOutput)
                {
                    Console.WriteLine("Processing command: " + command.Name);
                    foreach (var option in command.Option)
                    {
                        string naturalName;
                        if (parameters.Any(p => string.Equals(p.Name, option.Name, StringComparison.OrdinalIgnoreCase)))
                        {
                            naturalName = parameters.FirstOrDefault(p => string.Equals(p.Name, option.Name, StringComparison.OrdinalIgnoreCase))?.Name ?? string.Empty;
                        }
                        else if (alwaysUppercase.Any(term => option.Name.Equals(term, StringComparison.OrdinalIgnoreCase)))
                        {
                            naturalName = string.Join(" ", option.Name.Split('-').Select(word => alwaysUppercase.Contains(word.ToLower()) ? word.ToUpper() : char.ToUpper(word[0]) + word.Substring(1).ToLower()));
                        }
                        else if (!option.Name.Contains('-')) // Single word rule
                        {
                            naturalName = char.ToUpper(option.Name[0]) + option.Name.Substring(1).ToLower();
                        }
                        else // Handle dashed words
                        {
                            // Update dashed words to follow sentence case
                            naturalName = string.Join(" ", option.Name.Split('-').Select((word, index) => index == 0 ? char.ToUpper(word[0]) + word.Substring(1).ToLower() : word.ToLower()));
                        }

                        // Ensure terms in alwaysUppercase are always uppercase in the NaturalLanguage field
                        naturalName = string.Join(" ", naturalName.Split(' ').Select(word => alwaysUppercase.Contains(word.ToLower()) ? word.ToUpper() : word));

                        Console.WriteLine($"Mapping option '{option.Name}' to '{naturalName}'");
                        mappedParameters.Add(new MappedParameter { Parameter = option.Name, NaturalLanguage = naturalName });
                    }
                }

                if (!mappedParameters.Any())
                {
                    Console.WriteLine("No parameters were mapped. The output file will contain an empty array.");
                }

                // Ensure only unique parameters are added
                mappedParameters = mappedParameters
                    .GroupBy(mp => mp.Parameter)
                    .Select(g => g.First())
                    .OrderBy(mp => mp.Parameter)
                    .ToList();

                // Ensure the output directory exists
                var outputDirectory = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(outputDirectory) && !Directory.Exists(outputDirectory))
                {
                    Directory.CreateDirectory(outputDirectory);
                }

                // Write results to output file
                File.WriteAllText(outputPath, JsonSerializer.Serialize(mappedParameters, new JsonSerializerOptions { WriteIndented = true }));

                Console.WriteLine($"Mapped parameters written to {outputPath}");
            }
            catch (JsonException jsonEx)
            {
                Console.WriteLine($"JSON Error: {jsonEx.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }
}

class CliOutput
{
    public int Status { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<Command> Results { get; set; } = new List<Command>();
}

class Command
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string CommandText { get; set; } = string.Empty; // Renamed to avoid conflict
    public List<OptionDetail> Option { get; set; } = new List<OptionDetail>(); // Updated to match JSON structure
}

class OptionDetail
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}

class Parameter
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}
