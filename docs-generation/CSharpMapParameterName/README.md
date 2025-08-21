# MCP Tool: Natural Language Name Mapper

## Overview
This tool maps `origin` parameter names from `cli-output.json` to their corresponding `natural language` names using `parameters.json`. The results are saved in a new JSON file, `mapped-parameters.json`.

## Prerequisites
- .NET SDK installed on your system.
- Ensure the following files exist in the specified locations:
  - `../generated/cli-output.json`: Contains the `origin` parameter names.
  - `../Term-refinement/data/parameters.json`: Contains the `natural language` parameter names.

## How to Run
1. Navigate to the `CSharpMapParameterName` directory:
   ```bash
   cd ./docs-generation/CSharpMapParameterName
   ```

2. Build and run the program using the .NET CLI:
   ```bash
   dotnet run
   ```

3. The program will:
   - Read `cli-output.json` and `parameters.json`.
   - Map the `origin` parameter names to their `natural language` names.
   - Save the results to `mapped-parameters.json` in the same directory.

4. Check the output file:
   ```bash
   cat mapped-parameters.json
   ```

## Output
The output file, `mapped-parameters.json`, will have the following structure:
```json
[
  {
    "origin": "parameter_name",
    "natural_language": "mapped_name_or_unmapped"
  }
]
```

## Error Handling
If any errors occur (e.g., missing files or invalid JSON), they will be printed to the console.

## Notes
- Ensure the input files are correctly formatted JSON.
- The program performs a case-insensitive match for parameter names.
