﻿using System;
using System.Collections.Generic;
using System.Linq;
using Raml.Tools.ClientGenerator;
 
namespace Raml.Tools.WebApiGenerator
{
    [Serializable]
    public class ControllerMethod
    {
        public string Name { get; set; }
        public string ReturnType { get; set; }

        public GeneratorParameter Parameter { get; set; }
        public string Comment { get; set; }

        public string Url { get; set; }

        public string Verb { get; set; }
        public IEnumerable<GeneratorParameter> UriParameters { get; set; }

        public ClientGeneratorMethod Parent { get; set; }

        public IEnumerable<Property> SecurityParameters { get; set; }

        public bool UseSecurity { get; set; }

        public string ParametersString
        {
            get
            {
                var parameters = new Dictionary<string, MethodParameter>();

                if (HasInputParameter())
                    parameters.Add(Parameter.Name, new MethodParameter(false, (Parameter.Type == "string" || CollectionTypeHelper.IsCollection(Parameter.Type) ? "[FromBody] " + Parameter.Type : "Models." + Parameter.Type) + " " + Parameter.Name));

                if (UriParameters != null && UriParameters.Any())
                    foreach (var parameter in UriParameters.Where(parameter => !parameters.ContainsKey(parameter.Name)))
                        parameters.Add(parameter.Name, new MethodParameter(false, "[FromUri] " + parameter.Type + " " + parameter.Name));

                
                if (UseSecurity && SecurityParameters != null && SecurityParameters.Any())
                    foreach (var prop in SecurityParameters.Where(parameter => !parameters.ContainsKey(parameter.Name.ToLowerInvariant())))
                        parameters.Add(prop.Name.ToLowerInvariant(),
                            new MethodParameter(!prop.Required,
                                "[FromUri] " + prop.Type + " " + prop.Name.ToLowerInvariant() + (!prop.Required ? " = null" : string.Empty)));

                if (QueryParameters != null && QueryParameters.Any())
                    foreach (var prop in QueryParameters.Where(parameter => !parameters.ContainsKey(parameter.Name.ToLowerInvariant())))
                        parameters.Add(prop.Name.ToLowerInvariant(),
                            new MethodParameter(!prop.Required,
                                "[FromUri] " + prop.Type + " " + prop.Name.ToLowerInvariant() + (!prop.Required ? " = null" : string.Empty)));

                var methodParameters = parameters.Values.OrderBy(p => p.IsOptional).ToList();
                var parametersString = string.Join(",", methodParameters.Select(p => p.ParameterDeclaration));
                return parametersString;
            }
        }

        public string ParametersStringForAspNet5
        {
            get
            {
                var parameters = new Dictionary<string, MethodParameter>();

                if (HasInputParameter())
                {
	                var parameterSourceAttribute = FromForm ? "[FromForm]" : "[FromBody]";

	                var parameterType = Parameter.Type == "string" || CollectionTypeHelper.IsCollection(Parameter.Type) 
		                ? $"{parameterSourceAttribute} " + Parameter.Type 
		                : $"{parameterSourceAttribute} Models." + Parameter.Type;

	                var declaration = $"{parameterSourceAttribute} {parameterType} {Parameter.Name}";

					parameters.Add(Parameter.Name, new MethodParameter(false, declaration));
                }

	            if (UriParameters != null && UriParameters.Any())
                    foreach (var parameter in UriParameters.Where(parameter => !parameters.ContainsKey(parameter.Name)))
                        parameters.Add(parameter.Name, new MethodParameter(false, parameter.Type + " " + parameter.Name));


                if (UseSecurity && SecurityParameters != null && SecurityParameters.Any())
                    foreach (var prop in SecurityParameters.Where(parameter => !parameters.ContainsKey(parameter.Name.ToLowerInvariant())))
                        parameters.Add(prop.Name.ToLowerInvariant(),
                            new MethodParameter(!prop.Required,
                                prop.Type + " " + prop.Name.ToLowerInvariant() + (!prop.Required ? " = null" : string.Empty)));

                if (QueryParameters != null && QueryParameters.Any())
                    foreach (var prop in QueryParameters.Where(parameter => !parameters.ContainsKey(parameter.Name.ToLowerInvariant())))
                        parameters.Add(prop.Name.ToLowerInvariant(),
                            new MethodParameter(!prop.Required,
                                prop.Type + " " + prop.Name.ToLowerInvariant() + (!prop.Required ? " = null" : string.Empty)));

                var methodParameters = parameters.Values.OrderBy(p => p.IsOptional).ToList();
                var parametersString = string.Join(",", methodParameters.Select(p => p.ParameterDeclaration));
                return parametersString;
            }
        }

        public string ParametersCallString
        {
            get
            {
                var parameters = new Dictionary<string, MethodParameter>();

                if (HasInputParameter())
                    parameters.Add(Parameter.Name, new MethodParameter(false, Parameter.Name));

                if (UriParameters != null && UriParameters.Any())
                    foreach (var parameter in UriParameters.Where(parameter => !parameters.ContainsKey(parameter.Name)))
                        parameters.Add(parameter.Name, new MethodParameter(false, parameter.Name));


                if (UseSecurity && SecurityParameters != null && SecurityParameters.Any())
                    foreach (var prop in SecurityParameters.Where(parameter => !parameters.ContainsKey(parameter.Name.ToLowerInvariant())))
                        parameters.Add(prop.Name.ToLowerInvariant(),
                            new MethodParameter(!prop.Required, prop.Name.ToLowerInvariant()));

                if (QueryParameters != null && QueryParameters.Any())
                    foreach (var prop in QueryParameters.Where(parameter => !parameters.ContainsKey(parameter.Name.ToLowerInvariant())))
                        parameters.Add(prop.Name.ToLowerInvariant(), new MethodParameter(!prop.Required, prop.Name.ToLowerInvariant()));

                var methodParameters = parameters.Values.OrderBy(p => p.IsOptional).ToList();
                var parametersString = string.Join(",", methodParameters.Select(p => p.ParameterDeclaration));
                return parametersString;
            }
        }

        private bool HasInputParameter()
        {
            return (Verb == "Post" || Verb == "Put" || Verb == "Patch") && Parameter != null;
        }

        public IList<Property> QueryParameters { get; set; }

        public string XmlComment
        {
            get
            {
                var xmlComment = string.Empty;
                if (!string.IsNullOrWhiteSpace(Comment))
                {

                    xmlComment += "/// <summary>\r\n" +
                                  "\t\t/// " + XmlCommentHelper.Escape(Comment) + "\r\n" +
                                  "\t\t/// </summary>\r\n";

                }

                if (HasInputParameter())
                    xmlComment += "\t\t/// <param name=\"" + Parameter.Name + "\">" + (Parameter.Description == null ? string.Empty : XmlCommentHelper.Escape(Parameter.Description)) + "</param>\r\n";

                if (UriParameters != null && UriParameters.Any())
                {
                    xmlComment = UriParameters.Aggregate(xmlComment,
                        (current, parameter) =>
                            current +
                            ("\t\t/// <param name=\"" + parameter.Name + "\">" + (parameter.Description == null ? string.Empty : XmlCommentHelper.Escape(parameter.Description)) + "</param>\r\n"));
                }

                if (QueryParameters != null && QueryParameters.Any())
                {
                    xmlComment = QueryParameters.Aggregate(xmlComment,
                        (current, parameter) =>
                            current +
                            ("\t\t/// <param name=\"" + parameter.Name.ToLowerInvariant() + "\">" +
                             (parameter.Description == null
                                 ? string.Empty
                                 : XmlCommentHelper.Escape(parameter.Description)) +
                             "</param>\r\n"));
                }

                if (UseSecurity && SecurityParameters != null && SecurityParameters.Any())
                {
                    xmlComment = SecurityParameters.Aggregate(xmlComment,
                        (current, parameter) =>
                            current +
                            ("\t\t/// <param name=\"" + parameter.Name.ToLowerInvariant() + "\">" +
                             (parameter.Description == null
                                 ? string.Empty
                                 : XmlCommentHelper.Escape(parameter.Description)) +
                             "</param>\r\n"));
                }

                if (ReturnType != null && ReturnType != "string")
                {
                    xmlComment += "\t\t/// <returns>" + XmlCommentHelper.Escape(ReturnType) + "</returns>\r\n";
                }

                if (!string.IsNullOrWhiteSpace(xmlComment))
                    xmlComment = xmlComment.Substring(0, xmlComment.Length - 2); // remove last new line

                return xmlComment;
            }
        }

	    public bool FromForm { get; set; }
    }
}