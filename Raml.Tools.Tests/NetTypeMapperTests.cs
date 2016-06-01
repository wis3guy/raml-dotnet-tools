using Newtonsoft.Json.Schema;
using NUnit.Framework;

namespace Raml.Tools.Tests
{
    [TestFixture]
    public class NetTypeMapperTests
    {
        [Test]
        public void ShouldConvertToInt()
        {
            Assert.AreEqual("int", NetTypeMapper.Map(JsonSchemaType.Integer));
        }

        [Test]
        public void ShouldConvertToString()
        {
            Assert.AreEqual("string", NetTypeMapper.Map(JsonSchemaType.String));
        }

        [Test]
        public void ShouldConvertToBool()
        {
            Assert.AreEqual("bool", NetTypeMapper.Map(JsonSchemaType.Boolean));
        }

        [Test]
        public void ShouldConvertToDecimal()
        {
            Assert.AreEqual("decimal", NetTypeMapper.Map(JsonSchemaType.Float));
        }

        [Test]
        public void ShouldConvertToByteArrayWhenFile()
        {
            Assert.AreEqual("byte[]", NetTypeMapper.Map("file"));
        }

    }
}