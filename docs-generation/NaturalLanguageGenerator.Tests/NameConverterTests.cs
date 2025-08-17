using NUnit.Framework;
using NaturalLanguageGenerator;

namespace NaturalLanguageGenerator.Tests
{
    public class NameConverterTests
    {
        [Test]
        public void ToNaturalLanguage_ValidProgrammaticName_ReturnsExpectedResult()
        {
            var result = NLP_Name.ToNaturalLanguage("azure-ai-services");
            Assert.AreEqual("Azure AI services", result);
        }

        [Test]
        public void ToNaturalLanguage_EmptyProgrammaticName_ReturnsTBD()
        {
            var result = NLP_Name.ToNaturalLanguage("");
            Assert.AreEqual("TBD", result);
        }

        [Test]
        public void ToNaturalLanguage_AcronymInName_ReturnsExpectedResult()
        {
            var result = NLP_Name.ToNaturalLanguage("azure-ai-id");
            Assert.AreEqual("Azure AI ID", result);
        }

        [Test]
        public void ToNaturalLanguage_URLInName_ReturnsExpectedResult()
        {
            var result = NLP_Name.ToNaturalLanguage("azure-url-service");
            Assert.AreEqual("Azure URL service", result);
        }
    }
}
