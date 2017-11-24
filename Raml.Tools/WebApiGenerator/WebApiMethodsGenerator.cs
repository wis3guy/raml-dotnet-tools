using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using Raml.Common;
using Raml.Parser.Expressions;
using Raml.Tools.WebApiGenerator;

namespace Raml.Tools
{
	public class WebApiMethodsGenerator : MethodsGeneratorBase
	{
		private readonly QueryParametersParser queryParametersParser;

		public WebApiMethodsGenerator(RamlDocument raml, IDictionary<string, ApiObject> schemaResponseObjects,
			IDictionary<string, ApiObject> schemaRequestObjects, IDictionary<string, string> linkKeysWithObjectNames,
			IDictionary<string, ApiObject> schemaObjects)
			: base(raml, schemaResponseObjects, schemaRequestObjects, linkKeysWithObjectNames, schemaObjects)
		{
			queryParametersParser = new QueryParametersParser(schemaObjects);
		}

		public IEnumerable<ControllerMethod> GetMethods(Resource resource, string url, ControllerObject parent, string objectName, IDictionary<string, Parameter> parentUriParameters)
		{
			var methodsNames = new List<string>
			{
				HttpMethod.Delete.ToString(),
				HttpMethod.Get.ToString(),
				HttpMethod.Head.ToString(),
				HttpMethod.Options.ToString(),
				HttpMethod.Post.ToString(),
				HttpMethod.Put.ToString(),
				HttpMethod.Trace.ToString(),
				"PATCH" // for some reason this is not defined in the HttpMethod class ...
			};

			if (parent != null && parent.Methods != null)
				methodsNames.AddRange(parent.Methods.Select(m => m.Name));

			var generatorMethods = new Collection<ControllerMethod>();
			if (resource.Methods == null)
				return generatorMethods;

			foreach (var method in resource.Methods)
			{
				var generatedMethod = BuildControllerMethod(url, method, resource, parent, parentUriParameters);

				var name = generatedMethod.Name;
				if (IsVerbForMethod(method))
				{
					if (methodsNames.Contains(generatedMethod.Name, StringComparer.OrdinalIgnoreCase))
						generatedMethod.Name = GetUniqueName(methodsNames, generatedMethod.Name, resource.RelativeUri);

					if (method.QueryParameters != null && method.QueryParameters.Any())
					{
						var queryParameters = queryParametersParser.ParseParameters(method);
						generatedMethod.QueryParameters = queryParameters;
					}

					generatorMethods.Add(generatedMethod);
					methodsNames.Add(generatedMethod.Name);
				}
			}

			return generatorMethods;
		}

		private ControllerMethod BuildControllerMethod(string url, Method method, Resource resource, ControllerObject parent, IDictionary<string, Parameter> parentUriParameters)
		{
			var relativeUri = UrlGeneratorHelper.GetRelativeUri(url, parent.PrefixUri);
			var parentUrl = UrlGeneratorHelper.GetParentUri(url, resource.RelativeUri);

			return new ControllerMethod
			{
				Name = GetMethodName(method, url),
				Parameter = GetParameter(GeneratorServiceHelper.GetKeyForResource(method, resource, parentUrl), method, resource, url),
				UriParameters = uriParametersGenerator.GetUriParameters(resource, url, parentUriParameters),
				ReturnType = GetReturnType(GeneratorServiceHelper.GetKeyForResource(method, resource, parentUrl), method, resource, url),
				Comment = GetComment(resource, method),
				Url = relativeUri,
				Verb = NetNamingMapper.Capitalize(method.Verb),
				Parent = null,
				UseSecurity =
					raml.SecuredBy != null && raml.SecuredBy.Any() ||
					resource.Methods.Any(m => m.Verb == method.Verb && m.SecuredBy != null && m.SecuredBy.Any()),
				SecurityParameters = GetSecurityParameters(raml, method),
				FromForm = method.Body.Keys.Any(x => x.Contains("form"))
			};
		}

		private static string GetMethodName(Method method, string url)
		{
			var parts = url.Split('/').Reverse().ToArray();
			var name = method.Verb ?? "Get";
			var resource = string.Empty;
			var bySegments = new List<string>();
			var forSegments = new List<string>();

			foreach (var part in parts)
				if (string.IsNullOrEmpty(resource))
					if (part.StartsWith("{"))
						bySegments.Insert(0, part.Substring(1, part.Length - 2));
					else
						resource = part;
				else if (part.StartsWith("{"))
					forSegments.Insert(0, part.Substring(1, part.Length - 2));

			if (!string.IsNullOrEmpty(resource))
				name += CapitalizeFirstChar(resource);

			if (forSegments.Any())
				name += $"For{string.Join("And", forSegments.Select(CapitalizeFirstChar))}";

			if (bySegments.Any())
				name += $"By{string.Join("And", bySegments.Select(CapitalizeFirstChar))}";

			return NetNamingMapper.GetMethodName(name); // deals with special chars etc ...
		}

		private static string CapitalizeFirstChar(string input)
		{
			var chars = input.ToCharArray();
			chars[0] = char.ToUpperInvariant(chars[0]);
			return new string(chars);
		}

		/// <summary>
		///     Strips the (optional) alias of the include file (uses statement in RAML) from the security scheme name
		///     NOTE: THIS IS A BAND-AID, SEE: https://github.com/mulesoft-labs/raml-dotnet-tools/issues/159
		/// </summary>
		private static string StripIncludeFileAlias(string @is)
		{
			var idx = @is.LastIndexOf('.');

			return idx > 0
				? @is.Substring(idx + 1)
				: @is;
		}

		private IEnumerable<Property> GetSecurityParameters(RamlDocument ramlDocument, Method method)
		{
			var securityParams = new Collection<Property>();
			if (ramlDocument.SecuritySchemes == null || !ramlDocument.SecuritySchemes.Any())
				return securityParams;

			if ((ramlDocument.SecuredBy == null || !ramlDocument.SecuredBy.Any())
			    && (method.SecuredBy == null || !method.SecuredBy.Any()))
				return securityParams;

			var securedBy = method.SecuredBy != null && method.SecuredBy.Any() ? method.SecuredBy : ramlDocument.SecuredBy;

			if (securedBy == null)
				return securityParams;

			var secured = StripIncludeFileAlias(securedBy.First()); // NOTE: THIS IS A BAND-AID, SEE: https://github.com/mulesoft-labs/raml-dotnet-tools/issues/159

			var dic = ramlDocument.SecuritySchemes.FirstOrDefault(s => s.ContainsKey(secured));
			if (dic == null)
				return securityParams;

			var descriptor = ramlDocument.SecuritySchemes.First(s => s.ContainsKey(secured))[secured].DescribedBy;
			if (descriptor == null || descriptor.QueryParameters == null || !descriptor.QueryParameters.Any())
				return securityParams;

			return queryParametersParser.ConvertParametersToProperties(descriptor.QueryParameters);
		}
	}
}