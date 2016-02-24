using System;
using System.IO;
using System.Linq;

namespace Raml.Common
{
    public static class RamlReferenceReader
    {
        public static string GetRamlNamespace(string referenceFilePath)
        {
            var contents = File.ReadAllText(referenceFilePath);
            var lines = contents.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
            return lines[2].Replace("namespace:", string.Empty).Trim();
        }

        public static string GetRamlSource(string referenceFilePath)
        {
            var contents = File.ReadAllText(referenceFilePath);
            var lines = contents.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
            var source = lines[1].Replace("source:", string.Empty).Trim();
            return source;
        }

        public static bool GetRamlUseAsyncMethods(string referenceFilePath)
        {
            var contents = File.ReadAllText(referenceFilePath);
            var lines = contents.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
            
            if (lines.Count() <= 3)
                return false;

            var useAsync = lines[3].Replace("async:", string.Empty).Trim();
            
            if(string.IsNullOrWhiteSpace(useAsync))
                return false;

            bool result;
            Boolean.TryParse(useAsync, out result);
            return result;
        }

        public static bool GetRamlIncludeApiVersionInRoutePrefix(string referenceFilePath)
        {
            var contents = File.ReadAllText(referenceFilePath);
            var lines = contents.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

            if (lines.Count() <= 4)
                return false;

            var useAsync = lines[4].Replace("includeApiVersionInRoutePrefix:", string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(useAsync))
                return false;

            bool result;
            Boolean.TryParse(useAsync, out result);
            return result;
        }

        public static string GetClientRootClassName(string referenceFilePath)
        {
            var contents = File.ReadAllText(referenceFilePath);
            var lines = contents.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Count() <= 3)
                return "ApiClient";

            var clientRootClassName = lines[3].Replace("client:", string.Empty).Trim();
            return clientRootClassName;
        }
    }
}