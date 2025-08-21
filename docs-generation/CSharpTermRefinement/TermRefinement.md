Read all markdown files at https://github.com/MicrosoftDocs/azure-dev-docs/blob/main/articles/azure-mcp-server/tools. Come up with all the parameters in the parameters tables across the files. Put this into a JSON object where it is the parameter name, what file and line it is located at. This needs to be a repeatable script so create an easy to understand and well-factored script. 

Only create files in this Term-refinement subdirectory.

## Execution Instructions

You can run the program in three modes:

1. **Full Run** (default): Downloads markdown files and processes them.
   ```bash
   dotnet run --project /workspaces/azure-mcp/docs-generation/Term-refinement/extract_parameters.csproj
   ```

2. **Download Only**: Fetches markdown files and saves them.
   ```bash
   dotnet run --project /workspaces/azure-mcp/docs-generation/Term-refinement/extract_parameters.csproj download
   ```

3. **Process Only**: Processes previously downloaded markdown files.
   ```bash
   dotnet run --project /workspaces/azure-mcp/docs-generation/Term-refinement/extract_parameters.csproj process
   ```