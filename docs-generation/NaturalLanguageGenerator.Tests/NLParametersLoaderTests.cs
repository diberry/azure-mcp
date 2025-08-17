using NUnit.Framework;
using NaturalLanguageGenerator;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;

namespace NaturalLanguageNameGenerator.Tests
{
    public class NLParametersLoaderTests
    {
        private static readonly string TestFilePath = NLParametersLoader.ParametersFilePath;

        [Test]
        public void LoadParameters_FileExists_ReturnsDictionary()
        {
            var TestParameters = NLParametersLoader.LoadParameters();

            Assert.IsNotNull(TestParameters, "Expected a non-null dictionary.");
            if (TestParameters != null)
            {
                Assert.IsTrue(TestParameters.ContainsKey("azure-ai-services"), "Expected key 'azure-ai-services' to exist.");
                Assert.AreEqual("Azure AI services", TestParameters["azure-ai-services"], "Expected value for 'azure-ai-services' to match.");
            }
        }
    }
}
