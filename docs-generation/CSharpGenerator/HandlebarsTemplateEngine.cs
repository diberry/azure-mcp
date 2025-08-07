// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using HandlebarsDotNet;
using System.Text.Json;

/// <summary>
/// Manages Handlebars template compilation and custom helper registration.
/// Responsible for all template-related operations and rendering logic.
/// </summary>
public static class HandlebarsTemplateEngine
{
    /// <summary>
    /// Creates a configured Handlebars instance with all custom helpers registered.
    /// </summary>
    public static IHandlebars CreateEngine()
    {
        var handlebars = Handlebars.Create();
        RegisterHelpers(handlebars);
        return handlebars;
    }

    /// <summary>
    /// Processes a template file with data and returns the rendered result.
    /// </summary>
    public static async Task<string> ProcessTemplateAsync(string templateFile, Dictionary<string, object> data)
    {
        var handlebars = CreateEngine();
        
        var templateContent = await File.ReadAllTextAsync(templateFile);
        var template = handlebars.Compile(templateContent);
        
        return template(data);
    }

    /// <summary>
    /// Processes a template string with data and returns the rendered result.
    /// </summary>
    public static string ProcessTemplateString(string templateContent, Dictionary<string, object> data)
    {
        var handlebars = CreateEngine();
        var template = handlebars.Compile(templateContent);
        return template(data);
    }

    /// <summary>
    /// Registers all custom Handlebars helpers for documentation generation.
    /// </summary>
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

        // Parse sub-tool family (e.g., "blob" from "azmcp storage blob batch set-tier")
        handlebars.RegisterHelper("subToolFamily", (context, arguments) =>
        {
            if (arguments.Length == 0 || arguments[0] == null)
                return string.Empty;

            var command = arguments[0].ToString() ?? string.Empty;
            var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            
            if (parts.Length < 3) // Need at least "azmcp area operation"
                return string.Empty;
                
            // Skip "azmcp" and area name, get the first sub-component
            if (parts.Length >= 3)
            {
                // For "azmcp storage blob batch set-tier" -> return "blob"
                // For "azmcp storage account list" -> return "account"
                return char.ToUpper(parts[2][0]) + parts[2].Substring(1).ToLower();
            }
            
            return string.Empty;
        });

        // Parse sub-operation (everything after the sub-tool family)
        handlebars.RegisterHelper("subOperation", (context, arguments) =>
        {
            if (arguments.Length == 0 || arguments[0] == null)
                return string.Empty;

            var command = arguments[0].ToString() ?? string.Empty;
            var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            
            if (parts.Length < 4) // Need at least "azmcp area subtool operation"
                return string.Empty;
                
            // For "azmcp storage blob batch set-tier" -> return "batch set-tier"
            // Skip "azmcp", area name, and sub-tool family
            var remainingParts = parts.Skip(3).ToArray();
            return string.Join(" ", remainingParts);
        });

        // Concatenate strings
        handlebars.RegisterHelper("concat", (context, arguments) =>
        {
            return string.Join("", arguments.Select(arg => arg?.ToString() ?? string.Empty));
        });
        
        // Group by property helper
        handlebars.RegisterHelper("groupBy", (context, arguments) =>
        {
            if (arguments.Length < 2) return new Dictionary<string, object>();
            
            var collection = arguments[0];
            var propertyName = arguments[1]?.ToString();
            
            if (propertyName == null) return new Dictionary<string, object>();
            
            var grouped = new Dictionary<string, List<object>>();
            
            if (collection is IEnumerable<CommonParameter> commonParams)
            {
                foreach (var item in commonParams)
                {
                    var keyValue = typeof(CommonParameter).GetProperty(propertyName)?.GetValue(item)?.ToString() ?? "Unknown";
                    
                    if (!grouped.ContainsKey(keyValue))
                        grouped[keyValue] = new List<object>();
                    
                    grouped[keyValue].Add(item);
                }
            }
            else if (collection is System.Collections.IEnumerable enumerable)
            {
                foreach (var item in enumerable)
                {
                    if (item == null) continue;
                    
                    var keyValue = "Unknown";
                    var itemType = item.GetType();
                    var property = itemType.GetProperty(propertyName);
                    
                    if (property != null)
                    {
                        keyValue = property.GetValue(item)?.ToString() ?? "Unknown";
                    }
                    
                    if (!grouped.ContainsKey(keyValue))
                        grouped[keyValue] = new List<object>();
                    
                    grouped[keyValue].Add(item);
                }
            }
            
            return grouped;
        });
    }
}
