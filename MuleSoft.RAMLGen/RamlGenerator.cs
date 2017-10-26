﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Raml.Common;
using Raml.Parser;
using Raml.Parser.Expressions;
using Task = System.Threading.Tasks.Task;

namespace MuleSoft.RAMLGen
{
    public class RamlGenerator
    {
        public async Task HandleReference(Options opts)
        {
            string destinationFolder;
            string targetFileName;
            string targetNamespace;
            HandleParameters(opts, out destinationFolder, out targetFileName, out targetNamespace);

            var ramlDoc = await GetRamlDocument(opts, destinationFolder, targetFileName);

            var generator = new RamlClientGenerator();
            generator.Generate(ramlDoc, targetFileName, targetNamespace, opts.TemplatesFolder, destinationFolder);
        }

	    public async Task HandleContract(ServerOptions opts)
	    {
		    string destinationFolder;
		    string targetFileName;
		    string targetNamespace;
		    HandleParameters(opts, out destinationFolder, out targetFileName, out targetNamespace);

		    var ramlDoc = await GetRamlDocument(opts, destinationFolder, targetFileName);
		    var json = JsonConvert.SerializeObject(ramlDoc);

		    FindAnnotations(ramlDoc.Resources);

		    var generator = new RamlServerGenerator(ramlDoc, targetNamespace, opts.TemplatesFolder, targetFileName,
			    destinationFolder, opts.UseAsyncMethods, opts.WebApi);

		    generator.Generate();
	    }

	    private static bool FindAnnotations(ICollection<Resource> resources)
	    {
		    if (resources != null)
		    {
			    foreach (var resource in resources)
			    {
					Console.WriteLine(resource.RelativeUri);

				    if (resource.Annotations != null && resource.Annotations.Any())
				    {
						Console.WriteLine("Found on resource!");
					    return true;
				    }

					if (resource.Methods != null && resource.Methods.Any(m => m.Annotations != null && m.Annotations.Any()))
					{
						Console.WriteLine("Found on method!");
						return true;
					}


					if (FindAnnotations(resource.Resources))
					    return true;
			    }
		    }

			return false;
	    }

	    public async Task HandleModels(ModelsOptions opts)
        {
            string destinationFolder;
            string targetFileName;
            string targetNamespace;
            HandleParameters(opts, out destinationFolder, out targetFileName, out targetNamespace);

            var ramlDoc = await GetRamlDocument(opts, destinationFolder, targetFileName);

            var generator = new RamlModelsGenerator(ramlDoc, targetNamespace, opts.TemplatesFolder, targetFileName,
                destinationFolder);

            generator.Generate();
        }

        private static async Task<RamlDocument> GetRamlDocument(Options opts, string destinationFolder, string targetFileName)
        {
            var result = new RamlIncludesManager().Manage(opts.Source, destinationFolder, destinationFolder, opts.Overwrite);
            if(!result.IsSuccess && result.StatusCode != HttpStatusCode.OK)
                throw new HttpSourceErrorException("Error trying to get " + opts.Source + " - status code: " + result.StatusCode);

            if (!Directory.Exists(destinationFolder))
                Directory.CreateDirectory(destinationFolder);

            var path = Path.Combine(destinationFolder, targetFileName);
            File.WriteAllText(path, result.ModifiedContents);
            var parser = new RamlParser();
            var ramlDoc = await parser.LoadAsync(path);
            return ramlDoc;
        }

        private void HandleParameters(Options opts, out string destinationFolder, 
            out string targetFileName, out string targetNamespace)
        {
            destinationFolder = opts.DestinationFolder ?? "generated";

            targetFileName = Path.GetFileName(opts.Source);
            if (string.IsNullOrWhiteSpace(targetFileName))
                targetFileName = "root.raml";

            if (!targetFileName.EndsWith(".raml"))
                targetFileName += ".raml";

            targetNamespace = string.IsNullOrWhiteSpace(opts.Namespace)
                ? NetNamingMapper.GetNamespace(Path.GetFileNameWithoutExtension(targetFileName))
                : opts.Namespace;
        }
    }
}