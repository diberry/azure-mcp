using System;
using NUnit.Framework;

namespace CSharpGenerator.Tests
{
    public class NaturalLanguageMapperTests
    {
        [Test]
        public void GetNaturalLanguage_KnownParameter_ReturnsNaturalLanguage()
        {
            // Arrange
            string parameterName = "account";

            // Act
            string result = NaturalLanguageMapper.GetNaturalLanguage(parameterName);

            // Assert
            Assert.AreEqual("Account", result);
        }

        [Test]
        public void GetNaturalLanguage_UnknownParameter_ReturnsTBD()
        {
            // Arrange
            string parameterName = "unknown-param";

            // Act
            string result = NaturalLanguageMapper.GetNaturalLanguage(parameterName);

            // Assert
            Assert.AreEqual("TBD", result);
        }
    }
}
