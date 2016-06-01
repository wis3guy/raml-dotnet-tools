using System.Collections.Generic;
using System.Linq;
using System.Xml;
using Newtonsoft.Json.Schema;

namespace Raml.Tools
{
    public class NetTypeMapper
    {
        private static readonly IDictionary<JsonSchemaType, string> typeConversion = 
            new Dictionary<JsonSchemaType, string>
            {
                {
                    JsonSchemaType.Integer,
                    "int"
                },
                {
                    JsonSchemaType.String,
                    "string"
                },
                {
                    JsonSchemaType.Boolean,
                    "bool"
                },
                {
                    JsonSchemaType.Float,
                    "decimal"
                },
                {
                    JsonSchemaType.Any,
                    "object"
                }
            };

        private static readonly IDictionary<Newtonsoft.JsonV4.Schema.JsonSchemaType, string> typeV4Conversion =
            new Dictionary<Newtonsoft.JsonV4.Schema.JsonSchemaType, string>
            {
                {
                    Newtonsoft.JsonV4.Schema.JsonSchemaType.Integer,
                    "int"
                },
                {
                    Newtonsoft.JsonV4.Schema.JsonSchemaType.String,
                    "string"
                },
                {
                    Newtonsoft.JsonV4.Schema.JsonSchemaType.Boolean,
                    "bool"
                },
                {
                    Newtonsoft.JsonV4.Schema.JsonSchemaType.Float,
                    "decimal"
                },
                {
                    Newtonsoft.JsonV4.Schema.JsonSchemaType.Any,
                    "object"
                }
            };

        private static readonly IDictionary<string, string> typeStringConversion =
            new Dictionary<string, string>
            {
                {
                    "integer",
                    "int"
                },
                {
                    "string",
                    "string"
                },
                {
                    "boolean",
                    "bool"
                },
                {
                    "float",
                    "decimal"
                },
                {
                    "number",
                    "decimal"
                },
                {
                    "any",
                    "object"
                },
                {
                    "date",
                    "DateTime"
                },
                {
                    "file",
                    "byte[]"
                }
            };

        public static string Map(JsonSchemaType? type)
        {
            return type == null || !typeConversion.ContainsKey(type.Value) ? null : typeConversion[type.Value];
        }

        public static string Map(string type)
        {
            return !typeStringConversion.ContainsKey(type) ? null : typeStringConversion[type];
        }

        public static string Map(Newtonsoft.JsonV4.Schema.JsonSchemaType? type)
        {
            return type == null || !typeV4Conversion.ContainsKey(type.Value) ? null : typeV4Conversion[type.Value];
        }

        public static bool IsPrimitiveType(string type)
        {
            if (type.EndsWith("?"))
                type = type.Substring(0, type.Length - 1);

			return typeStringConversion.Any(t => t.Value == type);
		}

	    public static string Map(XmlQualifiedName schemaTypeName)
	    {
	        return schemaTypeName.Name;
	    }
	}
}