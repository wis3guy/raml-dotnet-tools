using Newtonsoft.Json.Schema;
using NUnit.Framework;

namespace Raml.Tools.Tests
{
    [TestFixture]
    public class NetTypeMapperTests
    {
        [Test]
        public void ShouldConvertIntegerToInt()
        {
            Assert.AreEqual("int", NetTypeMapper.GetNetType(JsonSchemaType.Integer, null));
        }

        [Test]
        public void ShouldConvertToString()
        {
            Assert.AreEqual("string", NetTypeMapper.GetNetType(JsonSchemaType.String, null));
        }

        [Test]
        public void ShouldConvertToBool()
        {
            Assert.AreEqual("bool", NetTypeMapper.GetNetType(JsonSchemaType.Boolean, null));
        }

        [Test]
        public void ShouldConvertToDecimal()
        {
            Assert.AreEqual("decimal", NetTypeMapper.GetNetType(JsonSchemaType.Float, null));
        }

        [Test]
        public void ShouldConvertToByteArrayWhenFile()
        {
            Assert.AreEqual("byte[]", NetTypeMapper.GetNetType("file", null));
        }

        [Test]
        public void ShouldConvertToDateTimeWhenDate()
        {
            Assert.AreEqual("DateTime", NetTypeMapper.GetNetType("datetime", null));
        }
    }
}