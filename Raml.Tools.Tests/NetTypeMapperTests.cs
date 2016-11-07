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
            Assert.AreEqual("DateTime", NetTypeMapper.GetNetType("date", null));
        }

        [Test]
        public void ShouldConvertToDateTimeWhenDatetime()
        {
            Assert.AreEqual("DateTime", NetTypeMapper.GetNetType("datetime", null));
        }

        [Test]
        public void ShouldConvertToDateTimeWhenDateOnly()
        {
            Assert.AreEqual("DateTime", NetTypeMapper.GetNetType("date-only", null));
        }

        [Test]
        public void ShouldConvertToDateTimeWhenTimeOnly()
        {
            Assert.AreEqual("DateTime", NetTypeMapper.GetNetType("time-only", null));
        }

        [Test]
        public void ShouldConvertToDateTimeWhenDatetimeOnly()
        {
            Assert.AreEqual("DateTime", NetTypeMapper.GetNetType("datetime-only", null));
        }

        [Test]
        public void ShouldConvertToDateTimeOffsetWhenRfc2616()
        {
            Assert.AreEqual("DateTimeOffset", NetTypeMapper.GetNetType("datetime", "rfc2616"));
        }

        [Test]
        public void ShouldConvertToDateTimeWhenRfc3339()
        {
            Assert.AreEqual("DateTime", NetTypeMapper.GetNetType("datetime", "rfc3339"));
        }

        [Test]
        public void ShouldConvertToLongWhenFormatIsLong()
        {
            Assert.AreEqual("long", NetTypeMapper.GetNetType("number", "long"));
        }

        [Test]
        public void ShouldConvertToLongWhenFormatIsInt64()
        {
            Assert.AreEqual("long", NetTypeMapper.GetNetType("number", "int64"));
        }

        [Test]
        public void ShouldConvertToIntWhenFormatIsInt32()
        {
            Assert.AreEqual("int", NetTypeMapper.GetNetType("number", "int32"));
        }

        [Test]
        public void ShouldConvertToIntWhenFormatIsInt()
        {
            Assert.AreEqual("int", NetTypeMapper.GetNetType("number", "int"));
        }

        [Test]
        public void ShouldConvertToShortWhenFormatIsInt16()
        {
            Assert.AreEqual("short", NetTypeMapper.GetNetType("number", "int16"));
        }

        [Test]
        public void ShouldConvertToByteWhenFormatIsInt8()
        {
            Assert.AreEqual("byte", NetTypeMapper.GetNetType("number", "int8"));
        }

        [Test]
        public void ShouldConvertToIntWhenInteger()
        {
            Assert.AreEqual("int", NetTypeMapper.GetNetType("integer", null));
        }

        [Test]
        public void ShouldConvertToDecimalWhenNumber()
        {
            Assert.AreEqual("decimal", NetTypeMapper.GetNetType("number", null));
        }

        [Test]
        public void ShouldConvertToFloatWhenFormatIsFloat()
        {
            Assert.AreEqual("float", NetTypeMapper.GetNetType("number", "float"));
        }

        [Test]
        public void ShouldConvertToDoubleWhenFormatIsDouble()
        {
            Assert.AreEqual("double", NetTypeMapper.GetNetType("number", "double"));
        }
    }
}