using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TextTemplating;
using Raml.Parser.Expressions;
using Raml.Tools;
using Raml.Tools.WebApiGenerator;
using System.Reflection;


namespace MuleSoft.RAMLGen
{
    public class RamlServerGenerator
    {
        private readonly string modelT4Template = "ApiModel.t4";
        private readonly string enumT4Template = "ApiEnum.t4";
        private readonly string baseControllerT4Template = "ApiControllerBase.t4";
        private readonly string implementationControllerT4Template = "ApiControllerImplementation.t4";
        private readonly string interfaceControllerT4Template = "ApiControllerInterface.t4";

        private readonly RamlDocument ramlDoc;
        private readonly string targetNamespace;
        private readonly string templatesFolder;
        private readonly string targetFileName;
        private readonly string destinationFolder;
        private readonly CustomCmdLineHost host;
        private readonly Engine engine;
        private readonly bool useAsyncMethods;
        private bool hasModels;

        public RamlServerGenerator(RamlDocument ramlDoc, string targetNamespace, string templatesFolder, 
            string targetFileName, string destinationFolder, bool useAsyncMethods, bool targetWebApi)
        {
            this.ramlDoc = ramlDoc;
            this.targetNamespace = targetNamespace;
            this.templatesFolder = templatesFolder;
            this.targetFileName = targetFileName;
            this.destinationFolder = destinationFolder;
            this.useAsyncMethods = useAsyncMethods;

            this.templatesFolder = string.IsNullOrWhiteSpace(templatesFolder)
                ? GetDefaultTemplateFolder(targetWebApi)
                : templatesFolder;

            host = new CustomCmdLineHost();
            engine = new Engine();
        }

        private static string GetDefaultTemplateFolder(bool targetWebApi)
        {
            return Path.GetDirectoryName(Assembly.GetAssembly(typeof (Program)).Location) + Path.DirectorySeparatorChar +
                   "Templates" + Path.DirectorySeparatorChar + (targetWebApi ? "WebApi2" : "AspNet5");
        }

        public void Generate()
        {
            var model = new WebApiGeneratorService(ramlDoc, targetNamespace).BuildModel();

            var models = model.Objects;
            // when is an XML model, skip empty objects
            if (models.Any(o => !string.IsNullOrWhiteSpace(o.GeneratedCode)))
                models = models.Where(o => o.Properties.Any() || !string.IsNullOrWhiteSpace(o.GeneratedCode));

            models = models.Where(o => !o.IsArray || o.Type == null); // skip array of primitives
            models = models.Where(o => !o.IsScalar); // skip scalars
            hasModels = models.Any();

            GenerateModels(model.Objects);
            GenerateEnums(model.Enums);
            GenerateInterfaceControllers(model.Controllers);
            GenerateBaseControllers(model.Controllers);
            GenerateImplementationControllers(model.Controllers);
        }

        private void GenerateImplementationControllers(IEnumerable<ControllerObject> controllers)
        {
            var extraParams = new Dictionary<string, bool>
            {
                {"useAsyncMethods", useAsyncMethods},
                {"hasModels", hasModels}
            };
            var destFolder = Path.Combine(destinationFolder, "Controllers").Replace("\\\\","\\") + Path.DirectorySeparatorChar;
            GenerateModels(controllers, "controllerObject", implementationControllerT4Template,
                o => o.Name + "Controller.cs", extraParams, destFolder);
        }

        private void GenerateBaseControllers(IEnumerable<ControllerObject> controllers)
        {
            var extraParams = new Dictionary<string, bool>
            {
                {"useAsyncMethods", useAsyncMethods},
                {"hasModels", hasModels}
            };
            GenerateModels(controllers, "controllerObject", baseControllerT4Template, o => o.Name + "Controller.cs", extraParams);
        }

        private void GenerateInterfaceControllers(IEnumerable<ControllerObject> controllers)
        {
            var extraParams = new Dictionary<string, bool>
            {
                {"useAsyncMethods", useAsyncMethods},
                {"hasModels", hasModels}
            };
            GenerateModels(controllers, "controllerObject", interfaceControllerT4Template, o => "I" + o.Name + "Controller.cs", extraParams);
        }

        private void GenerateEnums(IEnumerable<ApiEnum> enums)
        {
            GenerateModels(enums, "apiEnum", enumT4Template, o => o.Name + ".cs");
        }

        private void GenerateModels(IEnumerable<ApiObject> models)
        {
            GenerateModels(models,"apiObject", modelT4Template, o => o.Name + ".cs");
        }

        private void GenerateModels<T>(IEnumerable<T> models, string parameterKey, string t4TemplateName, 
            Func<T, string> getName, IDictionary<string, bool> extraParams = null, string destFolder = null) where T : IHasName
        {
            foreach (var parameter in models)
            {
                var result = GenerateModel(parameter, parameterKey, t4TemplateName, extraParams);

                foreach (CompilerError error in result.Errors)
                {
                    Console.WriteLine(error.ToString());
                }

                if (destFolder == null)
                    destFolder = destinationFolder;

                if (!Directory.Exists(destFolder))
                    Directory.CreateDirectory(destFolder);

                if (result.Errors.Count == 0)
                    File.WriteAllText(Path.Combine(destFolder, getName(parameter)), result.Content, result.Encoding);
            }
        }

        private GenerationResult GenerateModel<T>(T parameter, string parameterKey, string t4TemplateName, IDictionary<string, bool> extraParams = null) where T : IHasName

        {
            host.TemplateFileValue = Path.Combine(templatesFolder, t4TemplateName);
            var extensionPath = Path.GetDirectoryName(GetType().Assembly.Location) + Path.DirectorySeparatorChar;

            // Read the T4 from disk into memory
            var templateFileContent = File.ReadAllText(Path.Combine(templatesFolder, t4TemplateName));
            templateFileContent = templateFileContent.Replace("$(binDir)", extensionPath);
            templateFileContent = templateFileContent.Replace("$(ramlFile)", targetFileName.Replace("\\", "\\\\"));
            templateFileContent = templateFileContent.Replace("$(namespace)", targetNamespace);

            host.Session = host.CreateSession();
            host.Session[parameterKey] = parameter;

            if (extraParams != null)
            {
                foreach (var extraParam in extraParams)
                {
                    host.Session[extraParam.Key] = extraParam.Value;
                }
            }

            var output = engine.ProcessTemplate(templateFileContent, host);

            return new GenerationResult
            {
                Content = output,
                Encoding = host.FileEncoding,
                Errors = host.Errors
            };
        }
    }
}