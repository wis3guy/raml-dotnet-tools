using System;
using System.Collections.Generic;
using System.Linq;

namespace Raml.Tools.WebApiGenerator
{
    [Serializable]
    public class WebApiGeneratorModel
    {
        public WebApiGeneratorModel()
        {
            Warnings = new Dictionary<string, string>();
        }

        private string baseUri;
        public string Namespace { get; set; }
        public IDictionary<string, ApiObject> SchemaObjects { get; set; }
        public IDictionary<string, ApiObject> ResponseObjects { get; set; }
        public IDictionary<string, ApiObject> RequestObjects { get; set; }

        public IEnumerable<ControllerObject> Controllers { get; set; }

        public IDictionary<string, string> Warnings { get; set; }
        public IEnumerable<ApiObject> Objects
        {
            get
            {
                var objects = SchemaObjects.Values.ToList();
                objects.AddRange(RequestObjects.Values);
                objects.AddRange(ResponseObjects.Values);
                return objects;
            }
        }

        public IEnumerable<GeneratorParameter> BaseUriParameters { get; set; }

        public string BaseUri
        {
            get { return !string.IsNullOrWhiteSpace(baseUri) && !baseUri.EndsWith("/") ? baseUri + "/" : baseUri; }
            set { baseUri = value; }
        }

        public Security Security { get; set; }

        public string BaseUriParametersString
        {
            get
            {
                if (BaseUriParameters == null || !BaseUriParameters.Any())
                    return string.Empty;

                return string.Join(",", BaseUriParameters
                    .Select(p => p.Type + " " + p.Name)
                    .ToArray());
            }
        }

        public IEnumerable<ApiEnum> Enums { get; set; }

        public string ApiVersion { get; set; }
    }
}