// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

internal class Program
{
    private static async Task<int> Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Usage: CSharpGenerator <mode> [arguments...]");
            Console.Error.WriteLine("Modes:");
            Console.Error.WriteLine("  template <template-file> <data-file> <output-file> [additional-context-json]");
            Console.Error.WriteLine("  generate-docs <cli-output-json> <output-dir> [--index] [--common] [--commands]");
            return 1;
        }

        var mode = args[0];

        try
        {
            switch (mode)
            {
                case "template":
                    return await ProcessTemplate(args[1..]);
                case "generate-docs":
                    return await GenerateDocumentation(args[1..]);
                default:
                    Console.Error.WriteLine($"Unknown mode: {mode}");
                    return 1;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> ProcessTemplate(string[] args)
    {
        if (args.Length < 3)
        {
            Console.Error.WriteLine("Usage: CSharpGenerator template <template-file> <data-file> <output-file> [additional-context-json]");
            return 1;
        }

        var templateFile = args[0];
        var dataFile = args[1];
        var outputFile = args[2];
        var additionalContext = args.Length > 3 ? args[3] : null;

        // Configure Handlebars template engine

        // Read data
        var dataJson = await File.ReadAllTextAsync(dataFile);
        var data = JsonSerializer.Deserialize<Dictionary<string, object>>(dataJson, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (data == null)
        {
            Console.Error.WriteLine($"Failed to parse data file: {dataFile}");
            return 1;
        }

        // Add additional context if provided
        if (!string.IsNullOrEmpty(additionalContext))
        {
            var additionalData = JsonSerializer.Deserialize<Dictionary<string, object>>(additionalContext, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            
            if (additionalData != null)
            {
                foreach (var kvp in additionalData)
                {
                    data[kvp.Key] = kvp.Value;
                }
            }
        }

        // Add current timestamp
        data["generatedAt"] = DateTime.UtcNow;

        // Process template using the template engine
        var result = await HandlebarsTemplateEngine.ProcessTemplateAsync(templateFile, data);

        // Ensure output directory exists
        var outputDir = Path.GetDirectoryName(outputFile);
        if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        // Write output
        await File.WriteAllTextAsync(outputFile, result);
        
        Console.WriteLine($"Generated: {outputFile}");
        return 0;
    }

    private static async Task<int> GenerateDocumentation(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: CSharpGenerator generate-docs <cli-output-json> <output-dir> [--index] [--common] [--commands]");
            return 1;
        }

        var cliOutputFile = args[0];
        var outputDir = args[1];
        var generateIndex = args.Contains("--index");
        var generateCommon = args.Contains("--common");
        var generateCommands = args.Contains("--commands");

        // Read CLI output
        var cliOutputJson = await File.ReadAllTextAsync(cliOutputFile);
        var cliOutput = JsonSerializer.Deserialize<CliOutput>(cliOutputJson, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (cliOutput?.Results == null)
        {
            Console.Error.WriteLine("Failed to parse CLI output or no results found");
            return 1;
        }

        // Transform CLI output to expected format
        var transformedData = TransformCliOutput(cliOutput);

        // Add source code discovered common parameters
        var sourceCommonParams = await OptionsDiscovery.DiscoverCommonParametersFromSource();
        
        // Merge source-discovered parameters with CLI-discovered ones
        transformedData = MergeCommonParameters(transformedData, sourceCommonParams);

        // Ensure output directory exists
        Directory.CreateDirectory(outputDir);

        // Generate area pages
        var templatesDir = Path.Combine("..", "templates");
        var areaTemplate = Path.Combine(templatesDir, "area-template.hbs");
        
        foreach (var area in transformedData.Areas)
        {
            await GenerateAreaPage(area.Key, area.Value, transformedData, outputDir, areaTemplate);
        }

        // Generate common tools page if requested
        if (generateCommon)
        {
            var commonTemplate = Path.Combine(templatesDir, "common-tools.hbs");
            await GenerateCommonToolsPage(transformedData, outputDir, commonTemplate);
        }

        // Generate index page if requested
        if (generateIndex)
        {
            await GenerateIndexPage(transformedData, outputDir, areaTemplate);
        }

        // Generate commands page if requested
        if (generateCommands)
        {
            var commandsTemplate = Path.Combine(templatesDir, "commands-template.hbs");
            await GenerateCommandsPage(transformedData, outputDir, commandsTemplate);
        }

        return 0;
    }

    private static TransformedData TransformCliOutput(CliOutput cliOutput)
    {
        var tools = cliOutput.Results;
        var areaGroups = new Dictionary<string, AreaData>();

        foreach (var tool in tools)
        {
            var commandParts = tool.Command?.Split(' ') ?? Array.Empty<string>();
            if (commandParts.Length >= 2)
            {
                var area = commandParts[1]; // e.g., "azmcp storage blob ..." -> "storage"
                
                if (!areaGroups.ContainsKey(area))
                {
                    areaGroups[area] = new AreaData
                    {
                        Description = $"{area} area tools",
                        ToolCount = 0,
                        Tools = new List<Tool>()
                    };
                }
                
                areaGroups[area].ToolCount++;
                areaGroups[area].Tools.Add(tool);
                
                // Add area property to tool for compatibility
                tool.Area = area;
            }
        }

        return new TransformedData
        {
            Version = "1.0.0",
            Tools = tools,
            Areas = areaGroups,
            GeneratedAt = DateTime.UtcNow
        };
    }

    private static async Task GenerateAreaPage(string areaName, AreaData areaData, TransformedData data, string outputDir, string templateFile)
    {
        var areaNameForFile = areaName.ToLowerInvariant().Replace(" ", "-");
        var fileName = $"{areaNameForFile}.md";
        var outputFile = Path.Combine(outputDir, fileName);

        // Get common parameter names to filter them out - use source-discovered if available
        var commonParameters = data.SourceDiscoveredCommonParams.Any() 
            ? data.SourceDiscoveredCommonParams 
            : ExtractCommonParameters(data.Tools);
        var commonParameterNames = new HashSet<string>(commonParameters.Select(p => p.Name));

        // Filter out common parameters from tools for area pages
        var toolsWithFilteredParams = areaData.Tools.Select(tool => new Tool
        {
            Name = tool.Name,
            Command = tool.Command,
            Description = tool.Description,
            SourceFile = tool.SourceFile,
            Area = tool.Area,
            Option = tool.Option?.Where(opt => !commonParameterNames.Contains(opt.Name ?? "")).ToList()
        }).ToList();

        var areaPageData = new Dictionary<string, object>
        {
            ["areaName"] = areaName,
            ["areaData"] = areaData,
            ["tools"] = toolsWithFilteredParams,
            ["version"] = data.Version,
            ["generatedAt"] = data.GeneratedAt,
            ["generateAreaPage"] = true
        };

        var result = await HandlebarsTemplateEngine.ProcessTemplateAsync(templateFile, areaPageData);

        await File.WriteAllTextAsync(outputFile, result);
        Console.WriteLine($"Generated area page: {fileName}");
    }

    private static async Task GenerateCommonToolsPage(TransformedData data, string outputDir, string templateFile)
    {
        // Use source-discovered parameters if available, otherwise fall back to CLI-discovered
        var commonParameters = data.SourceDiscoveredCommonParams.Any() 
            ? data.SourceDiscoveredCommonParams 
            : ExtractCommonParameters(data.Tools);
        
        var commonPageData = new Dictionary<string, object>
        {
            ["version"] = data.Version,
            ["generatedAt"] = data.GeneratedAt,
            ["commonParameters"] = commonParameters
        };

        var result = await HandlebarsTemplateEngine.ProcessTemplateAsync(templateFile, commonPageData);

        var outputFile = Path.Combine(outputDir, "common-tools.md");
        await File.WriteAllTextAsync(outputFile, result);
        Console.WriteLine($"Generated common tools page: common-tools.md");
    }

    private static async Task GenerateIndexPage(TransformedData data, string outputDir, string templateFile)
    {
        var indexPageData = new Dictionary<string, object>
        {
            ["version"] = data.Version,
            ["generatedAt"] = data.GeneratedAt,
            ["tools"] = data.Tools,
            ["areas"] = data.Areas,
            ["generateIndex"] = true
        };

        var result = await HandlebarsTemplateEngine.ProcessTemplateAsync(templateFile, indexPageData);

        var outputFile = Path.Combine(outputDir, "index.md");
        await File.WriteAllTextAsync(outputFile, result);
        Console.WriteLine($"Generated index page: index.md");
    }

    private static async Task GenerateCommandsPage(TransformedData data, string outputDir, string templateFile)
    {
        // Use source-discovered parameters if available, otherwise fall back to CLI-discovered
        var commonParameters = data.SourceDiscoveredCommonParams.Any() 
            ? data.SourceDiscoveredCommonParams 
            : ExtractCommonParameters(data.Tools);
        
        var commandsPageData = new Dictionary<string, object>
        {
            ["version"] = data.Version,
            ["generatedAt"] = data.GeneratedAt,
            ["tools"] = data.Tools,
            ["areas"] = data.Areas,
            ["commonParameters"] = commonParameters
        };

        var result = await HandlebarsTemplateEngine.ProcessTemplateAsync(templateFile, commandsPageData);

        var outputFile = Path.Combine(outputDir, "azmcp-commands.md");
        await File.WriteAllTextAsync(outputFile, result);
        Console.WriteLine($"Generated commands page: azmcp-commands.md");
    }

    private static List<CommonParameter> ExtractCommonParameters(List<Tool> tools)
    {
        var parameterCounts = new Dictionary<string, int>();
        var parameterDetails = new Dictionary<string, Option>();
        var totalTools = tools.Count;

        // Analyze all tools to find common parameters
        foreach (var tool in tools)
        {
            if (tool.Option != null)
            {
                foreach (var param in tool.Option)
                {
                    var paramName = param.Name ?? "";
                    if (!parameterCounts.ContainsKey(paramName))
                    {
                        parameterCounts[paramName] = 0;
                        parameterDetails[paramName] = param;
                    }
                    parameterCounts[paramName]++;
                }
            }
        }

        var commonParameters = new List<CommonParameter>();

        // Add parameters that appear in at least 50% of tools
        var threshold = Math.Floor(totalTools * 0.5);
        var commonFromTools = parameterCounts.Where(p => p.Value >= threshold).OrderByDescending(p => p.Value);

        foreach (var param in commonFromTools)
        {
            var paramDetail = parameterDetails[param.Key];
            commonParameters.Add(new CommonParameter
            {
                Name = param.Key,
                Type = paramDetail.Type ?? "string",
                IsRequired = paramDetail.Required == true,
                Description = paramDetail.Description ?? "",
                UsagePercent = Math.Round((param.Value / (double)totalTools) * 100, 1)
            });
        }

        // Sort by usage percentage, then by name
        return commonParameters.OrderByDescending(p => p.UsagePercent).ThenBy(p => p.Name).ToList();
    }

    private static TransformedData MergeCommonParameters(TransformedData data, List<CommonParameter> sourceCommonParams)
    {
        // Get existing common parameters from CLI discovery
        var cliCommonParams = ExtractCommonParameters(data.Tools);
        
        // Create a merged list, prioritizing source-discovered parameters
        var allCommonParams = new Dictionary<string, CommonParameter>();
        
        // Add CLI-discovered parameters first
        foreach (var param in cliCommonParams)
        {
            allCommonParams[param.Name] = param;
        }
        
        // Add/override with source-discovered parameters
        foreach (var param in sourceCommonParams)
        {
            allCommonParams[param.Name] = param;
        }
        
        // Store the merged common parameters
        data.SourceDiscoveredCommonParams = allCommonParams.Values.OrderBy(p => p.Name).ToList();
        
        return data;
    }

    private static (string toolFamily, string operation) ParseCommand(string command)
    {
        // Expected format: "azmcp <area> [<subarea>] <operation>"
        // Examples: 
        // - "azmcp aks cluster get" -> ("aks cluster", "get")
        // - "azmcp storage account list" -> ("storage account", "list") 
        // - "azmcp subscription list" -> ("subscription", "list")
        
        if (string.IsNullOrEmpty(command))
            return ("", "");
            
        var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        
        if (parts.Length < 3) // Need at least "azmcp area operation"
            return ("", "");
            
        // Skip "azmcp" prefix
        var relevantParts = parts.Skip(1).ToArray();
        
        if (relevantParts.Length == 2)
        {
            // Format: "azmcp area operation"
            return (relevantParts[0], relevantParts[1]);
        }
        else if (relevantParts.Length >= 3)
        {
            // Format: "azmcp area subarea operation" or longer
            // Take everything except the last part as tool family
            var operation = relevantParts.Last();
            var toolFamily = string.Join(" ", relevantParts.Take(relevantParts.Length - 1));
            return (toolFamily, operation);
        }
        
        return ("", "");
    }
}

// Data models
public class CliOutput
{
    public List<Tool> Results { get; set; } = new();
}

public class Tool
{
    public string? Name { get; set; }
    public string? Command { get; set; }
    public string? Description { get; set; }
    public string? SourceFile { get; set; }
    public List<Option>? Option { get; set; }
    public string? Area { get; set; }
}

public class Option
{
    public string? Name { get; set; }
    public string? Type { get; set; }
    public bool Required { get; set; }
    public string? Description { get; set; }
}

public class AreaData
{
    public string Description { get; set; } = "";
    public int ToolCount { get; set; }
    public List<Tool> Tools { get; set; } = new();
}

public class TransformedData
{
    public string Version { get; set; } = "";
    public List<Tool> Tools { get; set; } = new();
    public Dictionary<string, AreaData> Areas { get; set; } = new();
    public DateTime GeneratedAt { get; set; }
    public List<CommonParameter> SourceDiscoveredCommonParams { get; set; } = new();
}

public class CommonParameter
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public bool IsRequired { get; set; }
    public string Description { get; set; } = "";
    public double UsagePercent { get; set; }
    public bool IsHidden { get; set; }
    public string Source { get; set; } = "";
}

// Extension method for regex replacement
public static class StringExtensions
{
    public static string RegularExpressionReplace(this string input, string pattern, string replacement)
    {
        return System.Text.RegularExpressions.Regex.Replace(input, pattern, replacement);
    }
}
