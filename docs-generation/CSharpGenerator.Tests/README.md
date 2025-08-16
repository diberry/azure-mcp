# CSharpGenerator.Tests

## How to Run Tests

1. Navigate to the `docs-generation` folder:
   ```bash
   cd /workspaces/azure-mcp/docs-generation
   ```

2. Run the tests with detailed output and code coverage:
   ```bash
   dotnet test CSharpGenerator.Tests/CSharpGenerator.Tests.csproj --collect:"Code Coverage" --logger "trx;LogFileName=TestResults.trx" --results-directory "CSharpGenerator.Tests/TestResults"
   ```

## Viewing Results

- **Test Results**:
  - The test results will be saved in the `TestResults` folder inside the `CSharpGenerator.Tests` project.
  - Open the `TestResults.trx` file using Visual Studio or any compatible viewer.

- **Code Coverage**:
  - The code coverage report will be saved in the same folder as `coverage.cobertura.xml`.
  - Use a tool like [ReportGenerator](https://github.com/danielpalme/ReportGenerator) to generate a human-readable report:
    ```bash
    reportgenerator -reports:CSharpGenerator.Tests/TestResults/coverage.cobertura.xml -targetdir:CSharpGenerator.Tests/TestResults/CoverageReport
    ```