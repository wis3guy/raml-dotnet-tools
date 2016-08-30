using System.IO;
using NUnit.Framework;
using Raml.Parser;
using Raml.Tools.ClientGenerator;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;

namespace Raml.Tools.Tests
{
    [TestFixture]
    public class ClientGeneratorRaml1Tests
    {

        [Test, Ignore]
        public async Task ShouldBuild_WhenAnnotationTargets()
        {
            var model = await GetAnnotationTargetsModel();
            Assert.IsNotNull(model);
        }

        [Test, Ignore]
        public async Task ShouldBuild_WhenAnnotations()
        {
            var model = await GetAnnotationsModel();
            Assert.IsNotNull(model);
        }

        [Test]
        public async Task ShouldBuild_WhenCustomScalar()
        {
            var model = await GetCustomScalarModel();
            Assert.IsNotNull(model);
        }

        [Test]
        public async Task ShouldBuildUriParameter_WhenCustomScalar()
        {
            var model = await GetCustomScalarModel();
            Assert.IsNotNull(model.Objects.First(o => o.Name == "Id"));
            Assert.IsTrue(model.Objects.First(o => o.Name == "Id").IsScalar);
            Assert.AreEqual("long", model.Root.Methods.First().UriParameters.First().Type);
        }

        [Test]
        public async Task ShouldBuild_WhenMovieType()
        {
            var model = await GetMovieTypeModel();
            Assert.IsNotNull(model);
        }

        [Test]
        public async Task ShouldBuildTypes_WhenMovies()
        {
            var model = await GetMoviesModel();
            Assert.IsTrue(model.Objects.Any(o => o.Name == "Movie"));
            Assert.AreEqual(9, model.Objects.First(o => o.Name == "Movie").Properties.Count);
            Assert.IsNotNull(model);
        }

        [Test]
        public async Task ShouldBuildArrayTypes()
        {
            var model = await GetArraysModel();
            
            Assert.IsTrue(model.Objects.Any(o => o.Name == "ArrayOfObjectItem"));
            Assert.IsTrue(model.Objects.Any(o => o.Name == "ArrayOfPerson"));
            Assert.IsTrue(model.Objects.Any(o => o.Name == "ArrayOfInt"));
            Assert.AreEqual(CollectionTypeHelper.GetCollectionType("long"), model.Objects.First(o => o.Name == "ArrayOfInt").Type);
            Assert.IsTrue(model.Objects.Any(o => o.Name == "ArrayOfObject"));
            Assert.AreEqual(CollectionTypeHelper.GetCollectionType("ArrayOfObjectItem"), model.Objects.First(o => o.Name == "ArrayOfObject").Type);
            Assert.AreEqual(CollectionTypeHelper.GetCollectionType("Person"), model.Objects.First(o => o.Name == "ArrayOfPerson").Type);

            Assert.AreEqual(CollectionTypeHelper.GetCollectionType("Person"), model.Objects
                .First(o => o.Name == "TypeThatHasArrayProperty")
                .Properties
                .First(p => p.Name == "Persons").Type);

            Assert.AreEqual(6, model.Objects.Count());
        }

        [Test]
        public async Task ShouldBuildMapTypes()
        {
            var model = await GetMapsModel();
            Assert.AreEqual(6, model.Objects.Count());
            Assert.IsTrue(model.Objects.Any(o => o.Name == "MapOfObjectItem"));
            Assert.IsTrue(model.Objects.Any(o => o.Name == "MapOfPerson"));
            Assert.AreEqual("MapOfPerson", model.Objects.First(o => o.Name == "MapOfPerson").Type);
            Assert.IsTrue(model.Objects.Any(o => o.Name == "MapOfInt"));
            Assert.AreEqual("MapOfInt", model.Objects.First(o => o.Name == "MapOfInt").Type);
            Assert.IsTrue(model.Objects.Any(o => o.Name == "MapOfObject"));
            Assert.AreEqual("MapOfObject", model.Objects.First(o => o.Name == "MapOfObject").Type);

            Assert.AreEqual("MapOfPersonArray", model.Objects.First(c => c.Name == "MapOfPersonArray").Type);
            Assert.AreEqual("MapOfPersonArray", model.Classes.First(c => c.Name == "Map").Methods.First().ReturnType);
        }


        [Test]
        public async Task ShouldBuild_WhenParameters()
        {
            var model = await GetParametersModel();
            Assert.IsNotNull(model);
        }

        [Test]
        public async Task ShouldHandleTypeExpressions()
        {
            var model = await GetTypeExpressionsModel();
            Assert.AreEqual(CollectionTypeHelper.GetCollectionType("Movie"), model.Classes.First().Methods.First(m => m.Verb == "Get").OkReturnType);
            Assert.AreEqual("string", model.Classes.First().Methods.First(m => m.Verb == "Post").Parameter.Type);
            Assert.AreEqual("string", model.Classes.First().Methods.First(m => m.Verb == "Put").Parameter.Type);
        }

        [Test]
        public async Task ShouldHandleInlinedTypes()
        {
            var model = await BuildModel("files/raml1/inlinetype.raml");
            Assert.AreEqual(2, model.Objects.Count());
            Assert.AreEqual("UsersGetOKResponseContent", model.Classes.First().Methods.First(m => m.Verb == "Get").OkReturnType);
            Assert.AreEqual("UsersPostRequestContent", model.Classes.First().Methods.First(m => m.Verb == "Post").Parameter.Type);
        }

        [Test]
        public async Task ShouldHandleEnums()
        {
            var model = await BuildModel("files/raml1/enums.raml");
            Assert.AreEqual(2, model.Enums.Count());
        }

        [Test]
        public async Task ShouldHandleShortcutsSyntacticSugar()
        {
            var model = await BuildModel("files/raml1/shortcuts.raml");
            Assert.AreEqual(2, model.Objects.Count());
            Assert.AreEqual(3, model.Objects.First(o => o.Name == "Person").Properties.Count);
        }

        private static async Task<ClientGeneratorModel> GetAnnotationTargetsModel()
        {
            return await BuildModel("files/raml1/annotations-targets.raml");
        }

        private static async Task<ClientGeneratorModel> GetAnnotationsModel()
        {
            return await BuildModel("files/raml1/annotations.raml");
        }


        private async Task<ClientGeneratorModel> GetCustomScalarModel()
        {
            return await BuildModel("files/raml1/customscalar.raml");
        }

        private async Task<ClientGeneratorModel> GetMoviesModel()
        {
            return await BuildModel("files/raml1/movies-v1.raml");
        }

        private async Task<ClientGeneratorModel> GetMovieTypeModel()
        {
            return await BuildModel("files/raml1/movietype.raml");
        }

        private async Task<ClientGeneratorModel> GetParametersModel()
        {
            return await BuildModel("files/raml1/parameters.raml");
        }

        private async Task<ClientGeneratorModel> GetTypeExpressionsModel()
        {
            return await BuildModel("files/raml1/typeexpressions.raml");
        }

        private async Task<ClientGeneratorModel> GetMapsModel()
        {
            return await BuildModel("files/raml1/maps.raml");
        }

        private async Task<ClientGeneratorModel> GetArraysModel()
        {
            return await BuildModel("files/raml1/arrays.raml");
        }

        private static async Task<ClientGeneratorModel> BuildModel(string ramlFile)
        {
            var fi = new FileInfo(ramlFile);
            var raml = await new RamlParser().LoadAsync(fi.FullName);
            var model = new ClientGeneratorService(raml, "test", "TargetNamespace").BuildModel();

            return model;
        }
    }
}