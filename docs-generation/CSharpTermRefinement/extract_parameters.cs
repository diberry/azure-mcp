using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Linq; // Add this for LINQ support

class Program
{
    // Refactor to separate download and processing logic
    static async Task Main(string[] args)
    {
        string mode = args.Length > 0 ? args[0].ToLower() : "full";
        string repoUrl = "https://github.com/MicrosoftDocs/azure-dev-docs";
        string branch = "main";
        string outputDirectory = "./generated/term-refinement";
        Directory.CreateDirectory(outputDirectory);
        string outputFile = Path.Combine(outputDirectory, "../parameters.json");

        if (mode == "download" || mode == "full")
        {
            Console.WriteLine("Fetching markdown files...");
            var markdownFiles = await FetchMarkdownFiles(repoUrl, branch, outputDirectory);
            await File.WriteAllTextAsync(Path.Combine(outputDirectory, "../markdown_files.json"), JsonSerializer.Serialize(markdownFiles, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine("Markdown files downloaded and saved.");

            if (mode == "download") return;
        }

        if (mode == "process" || mode == "full")
        {
            Console.WriteLine("Processing markdown files...");
            var markdownFiles = JsonSerializer.Deserialize<List<string>>(await File.ReadAllTextAsync(Path.Combine(outputDirectory, "../markdown_files.json")));

            // Ensure markdownFiles is not null before iterating
            if (markdownFiles == null || markdownFiles.Count == 0)
            {
                Console.WriteLine("No markdown files to process.");
                return;
            }

            var allParameters = new List<Dictionary<string, object>>();
            foreach (var fileUrl in markdownFiles)
            {
                Console.WriteLine($"Processing {fileUrl}...");
                var parameters = await ExtractParametersFromMarkdown(fileUrl);
                allParameters.AddRange(parameters);
            }

            // Log the total number of parameters before writing to the file
            Console.WriteLine($"Total parameters to write: {allParameters.Count}");
            foreach (var parameter in allParameters)
            {
                Console.WriteLine(JsonSerializer.Serialize(parameter));
            }

            // Order the parameters by name before writing to the file
            allParameters = allParameters.OrderBy(p => p["name"].ToString()).ToList();

            // Add a final log to confirm the file-writing process
            try
            {
                Console.WriteLine($"Writing parameters to {outputFile}...");
                await File.WriteAllTextAsync(outputFile, JsonSerializer.Serialize(allParameters, new JsonSerializerOptions { WriteIndented = true }));
                Console.WriteLine("Parameters successfully written to file.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error writing to file: {ex.Message}");
            }
            // Print the full file path of the parameters.json file
            Console.WriteLine($"Parameters file written to: {Path.GetFullPath(outputFile)}");
            Console.WriteLine("Processing complete.");
        }
    }

    // Update FetchMarkdownFiles to download and save .md files
    static async Task<List<string>> FetchMarkdownFiles(string repoUrl, string branch, string outputDirectory)
    {
        string apiUrl = $"https://api.github.com/repos/{repoUrl.Split('/')[3]}/{repoUrl.Split('/')[4]}/contents/articles/azure-mcp-server/tools?ref={branch}";
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("User-Agent", "Azure-MCP-Tool");

        var response = await client.GetStringAsync(apiUrl);
        using var doc = JsonDocument.Parse(response);

        var files = new List<string>();
        foreach (var element in doc.RootElement.EnumerateArray())
        {
            if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty("name", out var nameElement) && nameElement.ValueKind == JsonValueKind.String && nameElement.GetString()!.EndsWith(".md"))
            {
                if (element.TryGetProperty("download_url", out var downloadUrlElement) && downloadUrlElement.ValueKind == JsonValueKind.String)
                {
                    string? downloadUrl = downloadUrlElement.GetString();
                    if (!string.IsNullOrWhiteSpace(downloadUrl))
                    {
                        // Ensure nameElement and its value are not null before using them
                        if (nameElement.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(nameElement.GetString()))
                        {
                            string fileName = nameElement.GetString()!; // Safe to use with null-forgiving operator
                            string filePath = Path.Combine(outputDirectory, fileName);

                            // Download the markdown file content
                            var fileContent = await client.GetStringAsync(downloadUrl);
                            await File.WriteAllTextAsync(filePath, fileContent);

                            Console.WriteLine($"Downloaded and saved: {fileName}");

                            files.Add(filePath);
                        }
                    }
                }
            }
        }

        return files;
    }

    // Update ExtractParametersFromMarkdown to handle local file paths
    static async Task<List<Dictionary<string, object>>> ExtractParametersFromMarkdown(string fileUrl)
    {
        string content;

        if (File.Exists(fileUrl))
        {
            // Read content from local file
            content = await File.ReadAllTextAsync(fileUrl);
        }
        else
        {
            // Fetch content from remote URL
            using var client = new HttpClient();
            content = await client.GetStringAsync(fileUrl);
        }

        // Ensure 'parameters' is declared in the correct scope
        var parameters = new List<Dictionary<string, object>>();

        var lines = content.Split('\n');

        int tableStart = -1;
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains("| Parameter |"))
            {
                tableStart = i;
                break;
            }
        }

        // Add debug logs to verify table detection and processing
        Console.WriteLine($"Processing file: {fileUrl}");
        Console.WriteLine("Checking for parameter table...");

        if (tableStart == -1)
        {
            Console.WriteLine("No parameter table found in the file.");
            return parameters;
        }

        // Log the number of parameters detected in the table
        Console.WriteLine($"Parameter table detected. Extracting {parameters.Count} parameters...");

        // Log the number of rows detected in the table
        int rowCount = 0;
        for (int i = tableStart + 2; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]) || !lines[i].StartsWith("|"))
                break;
            rowCount++;
        }
        Console.WriteLine($"Detected {rowCount} rows in the parameter table.");

        // Update parsing logic to handle row structure more robustly
        // Print every column in each row as it is detected
        // Remove bold markdown from parameter values if present
        for (int i = tableStart + 2; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]) || !lines[i].StartsWith("|"))
                break;

            // Split the row into columns
            var parts = lines[i].Trim('|').Split('|')
                .Select(p => p.Trim().Replace("**", "")) // Remove bold markdown
                .Where(p => !string.IsNullOrEmpty(p))
                .ToArray();

            Console.WriteLine($"Row {i - tableStart - 1}:");
            for (int j = 0; j < parts.Length; j++)
            {
                Console.WriteLine($"    Column {j + 1}: {parts[j]}");
            }

            if (parts.Length >= 3 // Adjusted to check for at least 3 columns
            )
            {
                parameters.Add(new Dictionary<string, object>
                {
                    { "name", parts[0] },
                    { "required", parts[1] },
                    { "description", parts[2] },
                    { "file", fileUrl },
                    { "line", Array.IndexOf(lines, lines[i]) + 1 }
                });
            }
            else
            {
                Console.WriteLine($"Skipping malformed row at line {i + 1}: {lines[i]}");
            }
        }

        // Print all extracted parameters at the end of the function
        Console.WriteLine("All extracted parameters:");
        foreach (var parameter in parameters)
        {
            Console.WriteLine(JsonSerializer.Serialize(parameter));
        }

        // Correct the return statement
        return parameters;
    }
}
