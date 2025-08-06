// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.RegularExpressions;
using HandlebarsDotNet;

internal class Program
{
    private static async Task<int> Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Usage: CSharpGenerator <mode> [arguments...]");
            Console.Error.WriteLine("Modes:");
            Console.Error.WriteLine("  template <template-file> <data-file> <output-file> [additional-context-json]");
            Console.Error.WriteLine("  generate-docs <cli-output-json> <output-dir> [--index] [--common]");
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

        // Configure Handlebars
        var handlebars = Handlebars.Create();
        RegisterHelpers(handlebars);

        // Read and compile template
        var templateContent = await File.ReadAllTextAsync(templateFile);
        var template = handlebars.Compile(templateContent);

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

        // Process template
        var result = template(data);

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
            Console.Error.WriteLine("Usage: CSharpGenerator generate-docs <cli-output-json> <output-dir> [--index] [--common]");
            return 1;
        }

        var cliOutputFile = args[0];
        var outputDir = args[1];
        var generateIndex = args.Contains("--index");
        var generateCommon = args.Contains("--common");

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
        var sourceCommonParams = await DiscoverCommonParametersFromSource();
        
        // Merge source-discovered parameters with CLI-discovered ones
        transformedData = MergeCommonParameters(transformedData, sourceCommonParams);

        // Ensure output directory exists
        Directory.CreateDirectory(outputDir);

        // Generate area pages
        var templatesDir = "templates";
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

        var handlebars = Handlebars.Create();
        RegisterHelpers(handlebars);

        var templateContent = await File.ReadAllTextAsync(templateFile);
        var template = handlebars.Compile(templateContent);
        var result = template(areaPageData);

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

        var handlebars = Handlebars.Create();
        RegisterHelpers(handlebars);

        var templateContent = await File.ReadAllTextAsync(templateFile);
        var template = handlebars.Compile(templateContent);
        var result = template(commonPageData);

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

        var handlebars = Handlebars.Create();
        RegisterHelpers(handlebars);

        var templateContent = await File.ReadAllTextAsync(templateFile);
        var template = handlebars.Compile(templateContent);
        var result = template(indexPageData);

        var outputFile = Path.Combine(outputDir, "index.md");
        await File.WriteAllTextAsync(outputFile, result);
        Console.WriteLine($"Generated index page: index.md");
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

    private static async Task<List<CommonParameter>> DiscoverCommonParametersFromSource()
    {
        var commonParams = new List<CommonParameter>();
        
        // Dynamically discover all option definitions from OptionDefinitions.cs
        var optionDefinitionsPath = Path.Combine("..", "core", "src", "AzureMcp.Core", "Models", "Option", "OptionDefinitions.cs");
        
        if (!File.Exists(optionDefinitionsPath))
        {
            Console.WriteLine($"Warning: OptionDefinitions.cs not found at {optionDefinitionsPath}");
            return commonParams;
        }
        
        var optionDefinitionsSource = await File.ReadAllTextAsync(optionDefinitionsPath);
        
        // Step 1: Extract ALL static classes and their options dynamically
        var allOptionsFromClasses = ExtractAllOptionsFromClasses(optionDefinitionsSource);
        Console.WriteLine($"Debug: Found {allOptionsFromClasses.Count} option definitions from static classes");
        
        // Step 2: Find all GlobalOptions and RetryPolicyOptions properties dynamically
        var optionsClassMappings = await DiscoverOptionsClassMappings();
        Console.WriteLine($"Debug: Found {optionsClassMappings.Count} option class property mappings");
        
        // Step 3: Cross-reference to create final parameter list
        foreach (var mapping in optionsClassMappings)
        {
            var matchingOption = allOptionsFromClasses.FirstOrDefault(opt => 
                opt.ParameterName.Equals(mapping.ParameterName, StringComparison.OrdinalIgnoreCase));
            
            if (matchingOption != null)
            {
                Console.WriteLine($"Debug: Matched {mapping.PropertyName} -> {matchingOption.ParameterName}");
                commonParams.Add(new CommonParameter
                {
                    Name = matchingOption.ParameterName,
                    Type = MapCSharpTypeToJsonType(mapping.PropertyType.Replace("?", "")),
                    IsRequired = matchingOption.IsRequired,
                    Description = matchingOption.Description,
                    UsagePercent = 100,
                    IsHidden = matchingOption.IsHidden
                });
            }
        }
        
        // Step 4: Add any remaining options that might not be mapped to properties
        foreach (var option in allOptionsFromClasses)
        {
            if (!commonParams.Any(p => p.Name.Equals(option.ParameterName, StringComparison.OrdinalIgnoreCase)))
            {
                Console.WriteLine($"Debug: Adding unmapped option: {option.ParameterName}");
                commonParams.Add(new CommonParameter
                {
                    Name = option.ParameterName,
                    Type = MapCSharpTypeToJsonType(option.Type),
                    IsRequired = option.IsRequired,
                    Description = option.Description,
                    UsagePercent = 100,
                    IsHidden = option.IsHidden
                });
            }
        }
        
        Console.WriteLine($"Debug: Total discovered parameters: {commonParams.Count}");
        return commonParams.OrderBy(p => p.Name).ToList();
    }
    
    private static List<OptionDefinition> ExtractAllOptionsFromClasses(string sourceCode)
    {
        var options = new List<OptionDefinition>();
        
        // Step 1: Extract all constants that map to parameter names
        var constPattern = @"public\s+const\s+string\s+(\w+)\s*=\s*""([^""]+)"";";
        var constMatches = Regex.Matches(sourceCode, constPattern);
        var constantMap = new Dictionary<string, string>();
        
        foreach (Match constMatch in constMatches)
        {
            var constName = constMatch.Groups[1].Value;
            var paramName = constMatch.Groups[2].Value;
            constantMap[constName] = paramName;
            Console.WriteLine($"Debug: Found constant {constName} = {paramName}");
        }
        
        // Step 2: Extract option definitions using a simpler pattern
        // Look for: public static readonly Option<TYPE> NAME = new(
        var optionPattern = @"public\s+static\s+readonly\s+Option<([^>]+)>\s+(\w+)\s*=\s*new\s*\(";
        var optionMatches = Regex.Matches(sourceCode, optionPattern);
        
        foreach (Match optionMatch in optionMatches)
        {
            var type = optionMatch.Groups[1].Value.Trim();
            var propertyName = optionMatch.Groups[2].Value;
            
            // Try to find the corresponding constant by pattern matching
            var paramName = "";
            var description = "";
            
            // Look for a constant that ends with "Name" and matches this property
            var possibleConstName = propertyName + "Name";
            if (constantMap.ContainsKey(possibleConstName))
            {
                paramName = constantMap[possibleConstName];
            }
            else
            {
                // Fall back to converting property name
                paramName = InferParameterNameFromProperty(propertyName);
            }
            
            // For now, set default description - could be enhanced later
            description = $"Parameter for {propertyName}";
            
            Console.WriteLine($"Debug: Found option: {propertyName} -> {paramName} ({type})");
            
            options.Add(new OptionDefinition
            {
                ClassName = "Unknown", // Could be enhanced to detect class context
                PropertyName = propertyName,
                ParameterName = paramName,
                Type = type,
                Description = description,
                IsRequired = false, // Default, could be enhanced
                IsHidden = false    // Default, could be enhanced
            });
        }
        
        Console.WriteLine($"Debug: Found {constMatches.Count} constants and {optionMatches.Count} options");
        return options;
    }
    
    private static async Task<List<OptionsClassMapping>> DiscoverOptionsClassMappings()
    {
        var mappings = new List<OptionsClassMapping>();
        
        // Discover GlobalOptions properties
        var globalOptionsPath = Path.Combine("..", "core", "src", "AzureMcp.Core", "Models", "Option", "GlobalOptions.cs");
        if (File.Exists(globalOptionsPath))
        {
            var globalOptionsSource = await File.ReadAllTextAsync(globalOptionsPath);
            mappings.AddRange(ExtractPropertiesFromOptionsClass(globalOptionsSource, "GlobalOptions"));
        }
        
        // Discover RetryPolicyOptions properties
        var retryPolicyPath = Path.Combine("..", "core", "src", "AzureMcp.Core", "Models", "Option", "RetryPolicyOptions.cs");
        if (File.Exists(retryPolicyPath))
        {
            var retryPolicySource = await File.ReadAllTextAsync(retryPolicyPath);
            mappings.AddRange(ExtractPropertiesFromOptionsClass(retryPolicySource, "RetryPolicyOptions"));
        }
        
        return mappings;
    }
    
    private static List<OptionsClassMapping> ExtractPropertiesFromOptionsClass(string sourceCode, string className)
    {
        var mappings = new List<OptionsClassMapping>();
        
        // Extract properties that might map to option definitions
        var propertyPattern = @"public\s+([^?\s]+\??)\s+(\w+)\s*\{\s*get;\s*set;\s*\}";
        var propertyMatches = Regex.Matches(sourceCode, propertyPattern);
        
        foreach (Match match in propertyMatches)
        {
            var propertyType = match.Groups[1].Value;
            var propertyName = match.Groups[2].Value;
            
            // Try to infer parameter name from property name
            var parameterName = InferParameterNameFromProperty(propertyName);
            
            Console.WriteLine($"Debug: Found property in {className}: {propertyName} ({propertyType}) -> inferred param: {parameterName}");
            
            mappings.Add(new OptionsClassMapping
            {
                ClassName = className,
                PropertyName = propertyName,
                PropertyType = propertyType,
                ParameterName = parameterName
            });
        }
        
        return mappings;
    }
    
    private static string InferParameterNameFromProperty(string propertyName)
    {
        // Convert PascalCase property names to kebab-case parameter names
        // Examples: TenantId -> tenant-id, AuthMethod -> auth-method
        return Regex.Replace(propertyName, @"([a-z])([A-Z])", "$1-$2").ToLowerInvariant();
    }
    
    private static string MapCSharpTypeToJsonType(string csharpType)
    {
        return csharpType.ToLowerInvariant() switch
        {
            "string" => "string",
            "int" => "integer", 
            "integer" => "integer",
            "double" => "number",
            "float" => "number",
            "decimal" => "number",
            "bool" => "boolean",
            "boolean" => "boolean",
            "timespan" => "number",
            _ => "string" // Default to string for unknown types
        };
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

    private static void RegisterHelpers(IHandlebars handlebars)
    {
        // Format date helper
        handlebars.RegisterHelper("formatDate", (context, arguments) =>
        {
            if (arguments.Length == 0 || arguments[0] == null)
                return DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC");

            if (arguments[0] is DateTime dateTime)
                return dateTime.ToString("yyyy-MM-dd HH:mm:ss UTC");

            if (DateTime.TryParse(arguments[0].ToString(), out var parsedDate))
                return parsedDate.ToString("yyyy-MM-dd HH:mm:ss UTC");

            return arguments[0].ToString();
        });

        // Kebab case helper
        handlebars.RegisterHelper("kebabCase", (context, arguments) =>
        {
            if (arguments.Length == 0 || arguments[0] == null)
                return string.Empty;

            var str = arguments[0].ToString();
            return str?.ToLowerInvariant()
                .Replace(' ', '-')
                .Replace('_', '-')
                .RegularExpressionReplace("[^a-z0-9-]", "") ?? string.Empty;
        });

        // Get area count helper
        handlebars.RegisterHelper("getAreaCount", (context, arguments) =>
        {
            if (arguments.Length == 0 || arguments[0] == null)
                return 0;

            if (arguments[0] is JsonElement element && element.ValueKind == JsonValueKind.Object)
                return element.EnumerateObject().Count();

            if (arguments[0] is Dictionary<string, object> dict)
                return dict.Count;

            if (arguments[0] is Dictionary<string, AreaData> areaDict)
                return areaDict.Count;

            return 0;
        });

        // Math helpers
        handlebars.RegisterHelper("add", (context, arguments) =>
        {
            if (arguments.Length < 2) return 0;
            
            if (double.TryParse(arguments[0]?.ToString(), out var a) && 
                double.TryParse(arguments[1]?.ToString(), out var b))
                return a + b;
            
            return 0;
        });

        handlebars.RegisterHelper("divide", (context, arguments) =>
        {
            if (arguments.Length < 2) return 0;
            
            if (double.TryParse(arguments[0]?.ToString(), out var a) && 
                double.TryParse(arguments[1]?.ToString(), out var b) && b != 0)
                return a / b;
            
            return 0;
        });

        handlebars.RegisterHelper("round", (context, arguments) =>
        {
            if (arguments.Length < 1) return 0;
            
            if (!double.TryParse(arguments[0]?.ToString(), out var num))
                return 0;

            var precision = 1;
            if (arguments.Length > 1 && int.TryParse(arguments[1]?.ToString(), out var p))
                precision = p;

            return Math.Round(num, precision);
        });

        // Filter tools by area helper
        handlebars.RegisterHelper("filterToolsByArea", (context, arguments) =>
        {
            if (arguments.Length < 2) return new object[0];

            var tools = arguments[0];
            var areaName = arguments[1]?.ToString();

            if (tools is JsonElement toolsElement && toolsElement.ValueKind == JsonValueKind.Array)
            {
                var filteredTools = new List<object>();
                foreach (var tool in toolsElement.EnumerateArray())
                {
                    if (tool.TryGetProperty("area", out var areaProperty) && 
                        areaProperty.GetString() == areaName)
                    {
                        filteredTools.Add(tool);
                    }
                }
                return filteredTools;
            }

            return new object[0];
        });

        // JSON helper
        handlebars.RegisterHelper("toJson", (context, arguments) =>
        {
            if (arguments.Length == 0 || arguments[0] == null)
                return "null";

            return JsonSerializer.Serialize(arguments[0], new JsonSerializerOptions
            {
                WriteIndented = true
            });
        });

        // Required helper for boolean display
        handlebars.RegisterHelper("requiredIcon", (context, arguments) =>
        {
            if (arguments.Length == 0) return "❌";
            
            var value = arguments[0];
            if (value is bool boolValue)
                return boolValue ? "✅" : "❌";
            
            if (bool.TryParse(value?.ToString(), out var parsedBool))
                return parsedBool ? "✅" : "❌";
            
            return "❌";
        });

        // Parse tool family from command
        handlebars.RegisterHelper("toolFamily", (context, arguments) =>
        {
            if (arguments.Length == 0 || arguments[0] == null)
                return string.Empty;

            var command = arguments[0].ToString() ?? string.Empty;
            var (toolFamily, _) = ParseCommand(command);
            return toolFamily;
        });

        // Parse operation from command
        handlebars.RegisterHelper("operation", (context, arguments) =>
        {
            if (arguments.Length == 0 || arguments[0] == null)
                return string.Empty;

            var command = arguments[0].ToString() ?? string.Empty;
            var (_, operation) = ParseCommand(command);
            return operation;
        });

        // Concatenate strings
        handlebars.RegisterHelper("concat", (context, arguments) =>
        {
            return string.Join("", arguments.Select(arg => arg?.ToString() ?? string.Empty));
        });
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
}

public class OptionDefinition
{
    public string ClassName { get; set; } = "";
    public string PropertyName { get; set; } = "";
    public string ParameterName { get; set; } = "";
    public string Type { get; set; } = "";
    public string Description { get; set; } = "";
    public bool IsRequired { get; set; }
    public bool IsHidden { get; set; }
}

public class OptionsClassMapping
{
    public string ClassName { get; set; } = "";
    public string PropertyName { get; set; } = "";
    public string PropertyType { get; set; } = "";
    public string ParameterName { get; set; } = "";
}

// Extension method for regex replacement
public static class StringExtensions
{
    public static string RegularExpressionReplace(this string input, string pattern, string replacement)
    {
        return System.Text.RegularExpressions.Regex.Replace(input, pattern, replacement);
    }
}
