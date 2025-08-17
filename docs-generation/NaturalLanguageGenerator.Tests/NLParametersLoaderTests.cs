using NUnit.Framework;
using NaturalLanguageGenerator;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;

namespace NaturalLanguageNameGenerator.Tests
{
    public class NLParametersLoaderTests
    {
        private const string TestFilePath = "./docs-generation/nl-parameters.json";

        [SetUp]
        public void SetUp()
        {
            // Create a test nl-parameters.json file
            var testData = new Dictionary<string, string>
            {
                { "azure-ai-services", "Azure AI Services" },
                { "azure-url-service", "Azure URL Service" }
            };

            File.WriteAllText(TestFilePath, JsonSerializer.Serialize(testData));
        }

        [TearDown]
        public void TearDown()
        {
            // Clean up the test file
            if (File.Exists(TestFilePath))
            {
                File.Delete(TestFilePath);
            }
        }

        [Test]
        public void LoadParameters_FileExists_ReturnsDictionary()
        {
            var result = NLParametersLoader.LoadParameters();

            Assert.IsNotNull(result, "Expected a non-null dictionary.");
            Assert.IsTrue(result.ContainsKey("azure-ai-services"), "Expected key 'azure-ai-services' to exist.");
            Assert.AreEqual("Azure AI Services", result["azure-ai-services"], "Expected value for 'azure-ai-services' to match.");
        }

        [Test]
        public void LoadParameters_FileDoesNotExist_ReturnsNull()
        {
            // Delete the test file to simulate missing file
            if (File.Exists(TestFilePath))
            {
                File.Delete(TestFilePath);
            }

            var result = NLParametersLoader.LoadParameters();

            Assert.IsNull(result, "Expected null when the file does not exist.");
        }
    }
}
