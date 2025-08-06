# Documentation Generation

This project automatically generates multi-page documentation for Azure MCP (Model Context Protocol) tools using a C# generator with Handlebars templates.

## Overview

The documentation generation system consists of:

- **PowerShell Orchestrator** (`Generate-MultiPageDocs.ps1`) - Main entry point that coordinates the generation process
- **C# Generator** (`CSharpGenerator/`) - .NET 9.0 console application that processes CLI output and generates documentation using Handlebars templates
- **Handlebars Templates** (`templates/`) - Template files that define the structure and format of generated documentation

## Architecture

```
docs-generation/
├── Generate-MultiPageDocs.ps1     # Main orchestration script
├── CSharpGenerator/               # C# console application
│   ├── CSharpGenerator.csproj    # Project file with Handlebars.Net dependency
│   └── Program.cs                # Main generator logic
└── templates/                    # Handlebars template files
    ├── area-template.hbs         # Template for area-specific documentation
    └── common-tools.hbs          # Template for common tools documentation
```

## Process Flow

1. **Data Extraction**: The PowerShell script calls the Azure MCP CLI (`dotnet run -- tools list`) to extract tool information
2. **Data Processing**: CLI output is saved as JSON and passed to the C# generator
3. **Template Processing**: The C# generator uses Handlebars.Net to process templates with the extracted data
4. **Documentation Generation**: Multi-page Markdown documentation is generated in the `generated/multi-page/` directory

## Dependencies

### Global Dependencies (Central Package Management)

This project uses Central Package Management (CPM) as configured in the solution's `Directory.Packages.props`. The following dependency must be defined globally:

- **Handlebars.Net** (currently version 2.1.6) - Required for template processing in the C# generator

**Important**: When using CPM, package versions must be defined in `Directory.Packages.props`, not in individual project files. The `CSharpGenerator.csproj` contains only the package reference without a version number:

```xml
<PackageReference Include="Handlebars.Net" />
```

The version is centrally managed in `Directory.Packages.props`:

```xml
<PackageVersion Include="Handlebars.Net" Version="2.1.6" />
```

### Local Dependencies

- **.NET 9.0 SDK** - Required to build and run the C# generator
- **PowerShell** - Required to run the orchestration script
- **Azure MCP CLI** - Must be built and available at `../core/src/AzureMcp.Cli`

## Usage

### Basic Generation

```powershell
./Generate-MultiPageDocs.ps1
```

### Advanced Options

```powershell
# Generate only JSON format (YAML not yet implemented)
./Generate-MultiPageDocs.ps1 -Format json

# Skip index page generation
./Generate-MultiPageDocs.ps1 -CreateIndex $false

# Skip common tools page generation
./Generate-MultiPageDocs.ps1 -CreateCommon $false
```

## Generated Output

The script generates documentation in the `generated/` directory:

```
generated/
├── cli-output.json              # Raw CLI output data
└── multi-page/                 # Generated Markdown documentation
    ├── index.md                # Main index page (if enabled)
    ├── common-tools.md          # Common tools documentation (if enabled)
    └── [area-name].md           # Area-specific documentation pages
```

## Templates

### area-template.hbs

Generates documentation for each Azure service area (e.g., storage, compute, etc.). Includes:
- Area description and metadata
- Quick navigation links
- Detailed tool documentation

### common-tools.hbs

Generates documentation for common tools that span multiple service areas.

## Development

### Building the C# Generator

```bash
cd CSharpGenerator
dotnet build --configuration Release
```

### Adding New Templates

1. Create a new `.hbs` file in the `templates/` directory
2. Update the C# generator logic in `Program.cs` to use the new template
3. Test the generation process

### Troubleshooting

**Error: NU1008 - Projects that use central package version management should not define the version on the PackageReference**

This error occurs when a package version is defined in the project file instead of `Directory.Packages.props`. Ensure all package versions are centrally managed.

**Build Failures**

Ensure the Azure MCP CLI is built and available:
```bash
cd ../core/src/AzureMcp.Cli
dotnet build
```

## Integration

This documentation generation system is designed to be integrated into CI/CD pipelines. The generated documentation can be:
- Committed to the repository
- Published to documentation sites
- Used as input for further processing

## Contributing

When modifying this system:
1. Follow the coding guidelines in `.github/copilot-instructions.md`
2. Ensure all tests pass with `dotnet build`
3. Update templates and generator logic together
4. Test with representative Azure MCP CLI data