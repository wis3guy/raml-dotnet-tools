using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Raml.Parser.Expressions;

namespace Raml.Tools.Tests
{
    [TestFixture]
    public class RamlTypeParserTests
    {
        [Test]
        public void ShouldParseRquiredAttribute()
        {
            var ramlTypesOrderedDictionary = new RamlTypesOrderedDictionary();
            var ramlType = new RamlType
            {
                Type = "object",
                Object = new ObjectType
                {
                    Properties = new Dictionary<string, RamlType>()
                }
            
            };
            var property = new Parser.Expressions.Property
            {
                Type = "string",
                Required = true,
                DisplayName = "subject"
            };
            var subject = new RamlType
            {
                Type = "string",
                Scalar = property
            };

            ramlType.Object.Properties.Add("subject", subject);
            ramlTypesOrderedDictionary.Add("mail", ramlType );
            var objects = new Dictionary<string, ApiObject>();
            var typeParser = new RamlTypeParser(ramlTypesOrderedDictionary,
                objects, "SomeNamespace", new Dictionary<string, ApiEnum>(),
                new Dictionary<string, string>());

            typeParser.Parse();
            Assert.AreEqual(true, objects.First().Value.Properties.First().Required);
        }
    }
}