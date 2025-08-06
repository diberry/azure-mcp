// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.RegularExpressions;

public static class OptionsDiscovery
{
    public static async Task<List<CommonParameter>> DiscoverCommonParametersFromSource()
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
}

// Data models for options discovery
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
