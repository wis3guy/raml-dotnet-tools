
namespace Raml.Common
{
    public class RamlProperties
    {
        public string Namespace { get; set; }

        public string Source { get; set; }

        public bool UseAsyncMethods { get; set; }

        public string ClientName { get; set; }

        public bool IncludeApiVersionInRoutePrefix { get; set; }
    }
}