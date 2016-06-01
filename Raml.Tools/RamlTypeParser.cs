using System;
using System.Collections.Generic;
using System.Linq;
using Raml.Common;
using Raml.Parser.Expressions;
using Raml.Tools.JSON;
using Raml.Tools.XML;

namespace Raml.Tools
{
    public class RamlTypeParser
    {
        private readonly RamlTypesOrderedDictionary ramlTypes;
        private readonly IDictionary<string, ApiObject> schemaObjects;
        private readonly string targetNamespace;

        private readonly IDictionary<string, string> warnings;
        private readonly IDictionary<string, ApiEnum> enums;

        public RamlTypeParser(RamlTypesOrderedDictionary ramlTypes, IDictionary<string, ApiObject> schemaObjects, 
            string targetNamespace, IDictionary<string, ApiEnum> enums, IDictionary<string, string> warnings)
        {
            this.ramlTypes = ramlTypes;
            this.schemaObjects = schemaObjects;
            this.targetNamespace = targetNamespace;
            this.enums = enums;
            this.warnings = warnings;
        }

        public void Parse()
        {
            foreach (var key in ramlTypes.Keys)
            {
                var apiObject = ParseRamlType(key, ramlTypes.GetByKey(key));
                if(apiObject != null && !schemaObjects.ContainsKey(key))
                    schemaObjects.Add(key, apiObject);
            }
        }

        public ApiObject ParseInline(string key, RamlType inline, IDictionary<string, ApiObject> objects)
        {
            return ParseRamlType(key, inline);
        }

        private ApiObject ParseRamlType(string key, RamlType ramlType)
        {
            if (ramlType.External != null)
            {
                return ParseExternal(key, ramlType);
            }
            if (ramlType.Type.Contains("|")) // Union type
            {
                return ParseUnion(key, ramlType);
            }
            if (ramlType.Scalar != null)
            {
                return ParseScalar(key, ramlType);
            }
            if (ramlType.Object != null)
            {
                return ParseObject(key, ramlType);
            }
            if (ramlType.Array != null)
            {
                return ParseArray(key, ramlType);
            }
            throw new InvalidOperationException("Cannot parse type of " + key);
        }

        private ApiObject ParseUnion(string key, RamlType ramlType)
        {
            var apiObject = new ApiObject
            {
                IsUnionType = true,
                Name = NetNamingMapper.GetObjectName(key),
                Description = ramlType.Description,
                Example = GetExample(ramlType.Example, ramlType.Examples),
                Type = NetNamingMapper.GetObjectName(key)
            };

            var originalType = ramlType.Type;

            var isArray = false;
            if (originalType.StartsWith("(") && originalType.EndsWith(")[]"))
            {
                isArray = true;
                originalType = originalType.Substring(0, originalType.Length - 2);
            }
            originalType = originalType.Replace("(", string.Empty).Replace(")", string.Empty);

            var types = originalType.Split(new []{"|"}, StringSplitOptions.RemoveEmptyEntries);
            foreach (var type in types)
            {
              apiObject.Properties.Add(new Property
              {
                  Name = NetNamingMapper.GetPropertyName(type.Trim()),
                  Type = isArray 
                        ? CollectionTypeHelper.GetCollectionType(RamlTypesHelper.DecodeRaml1Type(type.Trim())) 
                        : RamlTypesHelper.DecodeRaml1Type(type.Trim())
              });
            }
            return apiObject;
        }

        private ApiObject ParseNestedType(RamlType ramlType, string keyOrTypeName)
        {
            return ParseRamlType(keyOrTypeName, ramlType);
        }


        private ApiObject ParseArray(string key, RamlType ramlType)
        {
            var typeOfArray = GetTypeOfArray(key, ramlType);

            var baseType = CollectionTypeHelper.GetBaseType(typeOfArray);
            if (NetTypeMapper.Map(baseType) == null &&
                ramlType.Array.Items != null && ramlType.Array.Items.Type == "object")
            {
                if (baseType == typeOfArray)
                    baseType = typeOfArray + "Item";

                baseType = NetNamingMapper.GetObjectName(baseType);

                var itemType = ParseNestedType(ramlType.Array.Items, baseType);
                schemaObjects.Add(baseType, itemType);
                typeOfArray = CollectionTypeHelper.GetCollectionType(baseType);
            }

            return new ApiObject
            {
                IsArray = true,
                Name = NetNamingMapper.GetObjectName(key),
                Description = ramlType.Description,
                Example = ramlType.Example,
                Type = typeOfArray
            };
        }

        private static string GetTypeOfArray(string key, RamlType ramlType)
        {
            if (!string.IsNullOrWhiteSpace(ramlType.Type))
            {
                var pureType = ramlType.Type.EndsWith("[]") ? ramlType.Type.Substring(0, ramlType.Type.Length - 2) : ramlType.Type;

                if (pureType != "array" && pureType != "object")
                {
                    if (NetTypeMapper.Map(pureType) != null)
                        pureType = NetTypeMapper.Map(pureType);
                    else
                        pureType = NetNamingMapper.GetObjectName(pureType);

                    return CollectionTypeHelper.GetCollectionType(pureType);
                }
            }
            if (!string.IsNullOrWhiteSpace(ramlType.Array.Items.Type))
            {
                if (ramlType.Array.Items.Type != "object")
                {
                    var netType = ramlType.Array.Items.Type;
                    if (NetTypeMapper.Map(netType) != null)
                        netType = NetTypeMapper.Map(netType);
                    else
                        netType = NetNamingMapper.GetObjectName(netType);

                    return CollectionTypeHelper.GetCollectionType(netType);
                }
            }

            if(!string.IsNullOrWhiteSpace(ramlType.Array.Items.Name))
                return CollectionTypeHelper.GetCollectionType(NetNamingMapper.GetObjectName(ramlType.Array.Items.Name));

            return NetNamingMapper.GetObjectName(key);
        }

        private ApiObject ParseScalar(string key, RamlType ramlType)
        {
            // TODO: check, should not really be an object, but is needed to map to primitive type...
            if (ramlType.Scalar.Enum != null && ramlType.Scalar.Enum.Any())
            {
                if (enums.ContainsKey(key))
                    return null;

                enums.Add(key, new ApiEnum
                {
                    Name = NetNamingMapper.GetObjectName(key),
                    Description = ramlType.Description,
                    Values = GetEnumValues(ramlType.Scalar)
                });
                return null;
            }

            var type = GetScalarType(ramlType);

            return new ApiObject
            {
                Type = NetNamingMapper.GetObjectName(key),
                Name = NetNamingMapper.GetObjectName(key),
                Example = ramlType.Example,
                Description = ramlType.Description,
                Properties = new List<Property>
                {
                    new Property
                    {
                        Name = "Value",
                        Type = type,
                        Minimum = (double?) ramlType.Scalar.Minimum,
                        Maximum = (double?) ramlType.Scalar.Maximum,
                        MinLength = ramlType.Scalar.MinLength,
                        MaxLength = ramlType.Scalar.MaxLength,
                        OriginalName = key
                    }
                },
                IsScalar = true,
            };
        }

        private string GetScalarType(RamlType ramlType)
        {
            var type = NetTypeMapper.Map(ramlType.Scalar.Type);

            if (type != null)
                return type;

            if (!ramlTypes.ContainsKey(ramlType.Scalar.Type))
                return "object";

            var subRamlType = ramlTypes[ramlType.Scalar.Type];
            if (subRamlType.Scalar == null) 
                return NetNamingMapper.GetObjectName(ramlType.Scalar.Type);

            type = GetScalarType(subRamlType);
            if (type != null)
                return type;

            throw new InvalidOperationException("Cannot determine type of scalar " + ramlType.Name);
        }

        private static List<string> GetEnumValues(Parameter scalar)
        {
            return scalar.Enum.Select(ConvertToString).ToList();
        }

        private static string ConvertToString(string v)
        {
            return StartsWithNumber(v) ? "N" + v : v;
        }

        private static bool StartsWithNumber(string v)
        {
            int num;
            return int.TryParse(v.Substring(0, 1), out num);
        }

        private ApiObject ParseExternal(string key, RamlType ramlType)
        {
            if (!string.IsNullOrWhiteSpace(ramlType.External.Schema))
            {
                return
                    new JsonSchemaParser().Parse(key, ramlType.External.Schema, schemaObjects, warnings,
                        enums, new Dictionary<string, ApiObject>(), new Dictionary<string, ApiObject>());
            }

            if (!string.IsNullOrWhiteSpace(ramlType.External.Xml))
            {
                return new XmlSchemaParser().Parse(key, ramlType.External.Xml, schemaObjects, targetNamespace);
            }

            throw new InvalidOperationException("Cannot parse external type of " + key);
        }

        private ApiObject ParseObject(string key, RamlType ramlType)
        {
            if (ramlType.Object.Properties.Count == 1 &&
                ramlType.Object.Properties.First().Key.StartsWith("[") &&
                ramlType.Object.Properties.First().Key.EndsWith("]"))
            {
                return ParseMap(ramlType, key);
            }
            else
            {

                return GetApiObjectFromObject(ramlType, key);
            }
        }

        private ApiObject ParseMap(RamlType ramlType, string key)
        {
            var name = NetNamingMapper.GetObjectName(key ?? ramlType.Name);
            var type = ramlType.Object.Properties.First().Value.Type;

            if (ramlType.Object.Properties.First().Value.Object != null && ramlType.Object.Properties.First().Value.Type == "object")
            {
                var itemName = name + "Item";
                var nestedObject = ParseObject(itemName, ramlType.Object.Properties.First().Value);
                type = nestedObject.Name;
                schemaObjects.Add(itemName, nestedObject);
            }

            type = RamlTypesHelper.DecodeRaml1Type(type);

            if (NetTypeMapper.Map(type) != null)
                type = NetTypeMapper.Map(type);

            return new ApiObject
            {
                Type = name,
                Name = name,
                BaseClass = "Dictionary<string," + type + ">",
                Description = ramlType.Description,
                Example = GetExample(ramlType.Example, ramlType.Examples),
                Properties = new Property[0],
                IsMap = true
            };
        }

        private ApiObject GetApiObjectFromObject(RamlType ramlType, string defaultName = "")
        {
            var name = defaultName == "" ? ramlType.Name : defaultName;

            if (string.IsNullOrWhiteSpace(name))
                name = "Type" + DateTime.Now.Ticks;

            return new ApiObject
            {
                Type = NetNamingMapper.GetObjectName(name),
                Name = NetNamingMapper.GetObjectName(name),
                BaseClass = ramlType.Type != "object" ? NetNamingMapper.GetObjectName(ramlType.Type) : string.Empty,
                Description = ramlType.Description,
                Example = GetExample(ramlType.Example, ramlType.Examples),
                Properties = GetProperties(ramlType.Object.Properties)
            };
        }

        private IList<Property> GetProperties(IDictionary<string, RamlType> properties)
        {
            var props = new List<Property>();
            foreach (var kv in properties)
            {
                var prop = kv.Value;
                if (prop.Scalar != null)
                {
                    var newProp = GetPropertyFromScalar(prop, kv);
                    props.Add(newProp);
                    continue;
                }
                if (prop.Object != null)
                {
                    var name = NetNamingMapper.GetPropertyName(kv.Key);
                    if (!schemaObjects.ContainsKey(name) && !schemaObjects.ContainsKey(prop.Type))
                    {
                        var newApiObject = GetApiObjectFromObject(prop, name);
                        schemaObjects.Add(name, newApiObject);
                        props.Add(new Property { Name = name, Type = name, Required = prop.Required, OriginalName = kv.Key.TrimEnd('?') });
                    }
                    else
                    {
                        ApiObject apiObject;
                        if (schemaObjects.ContainsKey(prop.Type))
                            apiObject = schemaObjects[prop.Type];
                        else
                            apiObject = schemaObjects[name];

                        props.Add(new Property { Name = name, Type = apiObject.Name, Required = prop.Required, OriginalName = kv.Key.TrimEnd('?') });
                    }
                    
                    continue;
                }
                if (prop.Array != null)
                {
                    var name = NetNamingMapper.GetPropertyName(kv.Key);
                    var type = kv.Value.Type;
                    if (kv.Value.Array.Items != null)
                    {
                        var obj = ParseArray(kv.Key, kv.Value);

                        type = CollectionTypeHelper.GetCollectionType(obj.Type);
                    }
                    if (type.EndsWith("[]"))
                    {
                        type = type.Substring(0, type.Length - 2);
                        if (NetTypeMapper.Map(type) == null)
                            type = NetNamingMapper.GetObjectName(type);

                        type = CollectionTypeHelper.GetCollectionType(type);
                    }

                    props.Add(new Property { Name = name, Type = type, Required = prop.Required, OriginalName = kv.Key.TrimEnd('?') });
                }
            }
            return props;
        }

        private Property GetPropertyFromScalar(RamlType prop, KeyValuePair<string, RamlType> kv)
        {
            if (prop.Scalar.Enum != null && prop.Scalar.Enum.Any())
            {
                if (!enums.ContainsKey(kv.Key))
                {
                    var apiEnum = new ApiEnum
                    {
                        Name = NetNamingMapper.GetPropertyName(kv.Key),
                        Description = kv.Value.Description,
                        Values = GetEnumValues(kv.Value.Scalar)
                    };
                    enums.Add(kv.Key, apiEnum);
                }
            }

            return new Property
            {
                Minimum = ToDouble(prop.Scalar.Minimum),
                Maximum = ToDouble(prop.Scalar.Maximum),
                Type = GetPropertyType(prop, kv),
                MaxLength = prop.Scalar.MaxLength,
                MinLength = prop.Scalar.MinLength,
                Name = NetNamingMapper.GetPropertyName(kv.Key),
                Required = prop.Required,
                Example = prop.Example,
                Description = prop.Description,
                IsEnum = prop.Scalar.Enum != null && prop.Scalar.Enum.Any(),
                OriginalName = kv.Key.TrimEnd('?')
            };
        }

        private string GetPropertyType(RamlType prop, KeyValuePair<string, RamlType> kv)
        {
            if (string.IsNullOrWhiteSpace(prop.Type))
                return "string";

            if (prop.Type == "object" || (prop.Scalar.Enum != null && prop.Scalar.Enum.Any()))
                return NetNamingMapper.GetPropertyName(kv.Key);

            if (NetTypeMapper.Map(prop.Type) != null) 
                return NetTypeMapper.Map(prop.Type);

            if (schemaObjects.ContainsKey(prop.Type))
            {
                var obj = schemaObjects[prop.Type];
                if (obj.IsScalar)
                    return obj.Properties.First().Type;

                return obj.Type;
            }

            return "object";
        }

        private double? ToDouble(decimal? value)
        {
            if (value == null)
                return null;

            return (double)value.Value;
        }

        private string GetExample(string example, IEnumerable<string> examples)
        {
            var result = string.IsNullOrWhiteSpace(example) ? example + "\r\n" : string.Empty;
            if (examples != null)
                result += string.Join("\r\n", examples);

            return result;
        } 
    }
}