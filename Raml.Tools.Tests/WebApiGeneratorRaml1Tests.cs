using System.IO;
using NUnit.Framework;
using Raml.Parser;
using System.Linq;
using System.Threading.Tasks;
using Raml.Tools.WebApiGenerator;

namespace Raml.Tools.Tests
{
    [TestFixture]
    public class WebApiGeneratorRaml1Tests
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
        public async Task ShouldBuildArrays()
        {
            var model = await BuildModel("files/raml1/arrayTypes.raml");
            Assert.AreEqual(5, model.Objects.Count());
        }

        [Test]
        public async Task ShouldBuild_WhenCustomScalar()
        {
            var model = await GetCustomScalarModel();
            Assert.IsNotNull(model);
        }

        [Test]
        public async Task ShouldMapAttributes_WhenCustomScalarInObject()
        {
            var model = await BuildModel("files/raml1/customscalar-in-object.raml");
            Assert.AreEqual(3, model.Objects.Count());
            Assert.AreEqual(1, model.Objects.First(o => o.Name == "CustomInt").Properties.Count);
            Assert.AreEqual(1, model.Objects.First(o => o.Name == "CustomString").Properties.Count);
            Assert.AreEqual(0, model.Objects.First(o => o.Name == "CustomInt").Properties.First().Minimum);
            Assert.AreEqual(100, model.Objects.First(o => o.Name == "CustomInt").Properties.First().Maximum);
            Assert.AreEqual(5, model.Objects.First(o => o.Name == "CustomString").Properties.First().MinLength);
            Assert.AreEqual(255, model.Objects.First(o => o.Name == "CustomString").Properties.First().MaxLength);
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
        public async Task ShouldDetectArrayTypes_WhenMovies()
        {
            var model = await GetMoviesModel();
            Assert.AreEqual(CollectionTypeHelper.GetCollectionType("Movie"), model.Controllers.First(o => o.Name == "Movies").Methods.First(m => m.Name == "Get").ReturnType);
            Assert.AreEqual(CollectionTypeHelper.GetCollectionType("Movie"), model.Controllers.First(o => o.Name == "Movies").Methods.First(m => m.Name == "GetAvailable").ReturnType);
            Assert.AreEqual(CollectionTypeHelper.GetCollectionType("Movie"), model.Controllers.First(o => o.Name == "Search").Methods.First(m => m.Name == "Get").ReturnType);
            Assert.AreEqual(CollectionTypeHelper.GetCollectionType("Movie"), model.Controllers.First(o => o.Name == "Movies").Methods.First(m => m.Name == "Post").Parameter.Type);
        }

        [Test]
        public async Task ShouldBuild_WhenParameters()
        {
            var model = await GetParametersModel();
            Assert.IsNotNull(model);
        }

        [Test]
        public async Task ShouldBuild_WhenTypeExpressions()
        {
            var model = await GetTypeExpressionsModel();
            Assert.IsNotNull(model);
        }

        [Test]
        public async Task ShouldBuild_EvenWithDisorderedTypes()
        {
            var model = await BuildModel("files/raml1/typesordering.raml");
            Assert.AreEqual(CollectionTypeHelper.GetCollectionType("InvoiceLine"), model.Objects.First(c => c.Name == "Invoice").Properties.First(p => p.Name == "Lines").Type);

            Assert.AreEqual("ArtistByTrack", model.Objects.First(c => c.Name == "ArtistByTrack").Type);
            Assert.AreEqual("Dictionary<string,Artist>", model.Objects.First(c => c.Name == "ArtistByTrack").BaseClass);
            Assert.AreEqual("TracksByArtist", model.Objects.First(c => c.Name == "TracksByArtist").Type);
            Assert.AreEqual("Dictionary<string,IList<Track>>", model.Objects.First(c => c.Name == "TracksByArtist").BaseClass);

            Assert.AreEqual("ArtistByTrack", model.Controllers.First(c => c.Name == "Artists").Methods.First(m => m.Url == "bytrack/{id}").ReturnType);
            Assert.AreEqual("TracksByArtist", model.Controllers.First(c => c.Name == "Tracks").Methods.First(m => m.Url == "byartist/{id}").ReturnType);

            Assert.AreEqual("Artist", model.Controllers.First(c => c.Name == "Artists").Methods.First(m => m.Name == "GetById").ReturnType);
            Assert.AreEqual("Album", model.Controllers.First(c => c.Name == "Albums").Methods.First(m => m.Name == "GetById").ReturnType);
            Assert.AreEqual("Track", model.Controllers.First(c => c.Name == "Tracks").Methods.First(m => m.Name == "GetById").ReturnType);
            Assert.AreEqual("Customer", model.Controllers.First(c => c.Name == "Customers").Methods.First(m => m.Name == "GetById").ReturnType);

            Assert.AreEqual(CollectionTypeHelper.GetCollectionType("Artist"), model.Controllers.First(c => c.Name == "Artists").Methods.First(m => m.Name == "Get").ReturnType);
            Assert.AreEqual(CollectionTypeHelper.GetCollectionType("Album"), model.Controllers.First(c => c.Name == "Albums").Methods.First(m => m.Name == "Get").ReturnType);
            Assert.AreEqual(CollectionTypeHelper.GetCollectionType("Customer"), model.Controllers.First(c => c.Name == "Customers").Methods.First(m => m.Name == "Get").ReturnType);
            Assert.AreEqual(CollectionTypeHelper.GetCollectionType("Track"), model.Controllers.First(c => c.Name == "Tracks").Methods.First(m => m.Name == "Get").ReturnType);

            Assert.AreEqual("Artist", model.Controllers.First(c => c.Name == "Artists").Methods.First(m => m.Name == "Post").Parameter.Type);
            Assert.AreEqual("Album", model.Controllers.First(c => c.Name == "Albums").Methods.First(m => m.Name == "Post").Parameter.Type);
            Assert.AreEqual("Customer", model.Controllers.First(c => c.Name == "Customers").Methods.First(m => m.Name == "Post").Parameter.Type);
            Assert.AreEqual("Track", model.Controllers.First(c => c.Name == "Tracks").Methods.First(m => m.Name == "Post").Parameter.Type);

        }


        [Test]
        public async Task ShouldBuild_WhenChinook()
        {
            var model = await BuildModel("files/raml1/chinook-v1.raml");
            Assert.AreEqual(CollectionTypeHelper.GetCollectionType("InvoiceLine"), model.Objects.First(c => c.Name == "Invoice").Properties.First(p => p.Name == "Lines").Type);

            Assert.AreEqual("ArtistByTrack", model.Objects.First(c => c.Name == "ArtistByTrack").Type);
            Assert.AreEqual("Dictionary<string,Artist>", model.Objects.First(c => c.Name == "ArtistByTrack").BaseClass);
            Assert.AreEqual("TracksByArtist", model.Objects.First(c => c.Name == "TracksByArtist").Type);
            Assert.AreEqual("Dictionary<string,IList<Track>>", model.Objects.First(c => c.Name == "TracksByArtist").BaseClass);

            Assert.AreEqual("ArtistByTrack", model.Controllers.First(c => c.Name == "Artists").Methods.First(m => m.Url == "bytrack/{id}").ReturnType);
            Assert.AreEqual("TracksByArtist", model.Controllers.First(c => c.Name == "Tracks").Methods.First(m => m.Url == "byartist/{id}").ReturnType);

            Assert.AreEqual("Artist", model.Controllers.First(c => c.Name == "Artists").Methods.First(m => m.Name == "GetById").ReturnType);
            Assert.AreEqual("Album", model.Controllers.First(c => c.Name == "Albums").Methods.First(m => m.Name == "GetById").ReturnType);
            Assert.AreEqual("Track", model.Controllers.First(c => c.Name == "Tracks").Methods.First(m => m.Name == "GetById").ReturnType);
            Assert.AreEqual("Customer", model.Controllers.First(c => c.Name == "Customers").Methods.First(m => m.Name == "GetById").ReturnType);

            Assert.AreEqual(CollectionTypeHelper.GetCollectionType("Artist"), model.Controllers.First(c => c.Name == "Artists").Methods.First(m => m.Name == "Get").ReturnType);
            Assert.AreEqual(CollectionTypeHelper.GetCollectionType("Album"), model.Controllers.First(c => c.Name == "Albums").Methods.First(m => m.Name == "Get").ReturnType);
            Assert.AreEqual(CollectionTypeHelper.GetCollectionType("Customer"), model.Controllers.First(c => c.Name == "Customers").Methods.First(m => m.Name == "Get").ReturnType);
            Assert.AreEqual(CollectionTypeHelper.GetCollectionType("Track"), model.Controllers.First(c => c.Name == "Tracks").Methods.First(m => m.Name == "Get").ReturnType);

            Assert.AreEqual("Artist", model.Controllers.First(c => c.Name == "Artists").Methods.First(m => m.Name == "Post").Parameter.Type);
            Assert.AreEqual("Album", model.Controllers.First(c => c.Name == "Albums").Methods.First(m => m.Name == "Post").Parameter.Type);
            Assert.AreEqual("Customer", model.Controllers.First(c => c.Name == "Customers").Methods.First(m => m.Name == "Post").Parameter.Type);
            Assert.AreEqual("Track", model.Controllers.First(c => c.Name == "Tracks").Methods.First(m => m.Name == "Post").Parameter.Type);

        }

        [Test]
        public async Task ShouldHandleUnionTypes()
        {
            var model = await BuildModel("files/raml1/uniontypes.raml");

            Assert.AreEqual(5, model.Objects.Count());

            Assert.AreEqual(2, model.Objects.First(c => c.Name == "Customer").Properties.Count);
            Assert.AreEqual("Person", model.Objects.First(c => c.Name == "Customer").Properties.First(c => c.Name == "Person").Type);
            Assert.AreEqual("Company", model.Objects.First(c => c.Name == "Customer").Properties.First(c => c.Name == "Company").Type);

            Assert.AreEqual(CollectionTypeHelper.GetCollectionType("Person"), model.Objects.First(c => c.Name == "Customers").Properties.First(c => c.Name == "Person").Type);
            Assert.AreEqual(CollectionTypeHelper.GetCollectionType("Company"), model.Objects.First(c => c.Name == "Customers").Properties.First(c => c.Name == "Company").Type);

            Assert.AreEqual(false, model.Objects.First(c => c.Name == "Group").IsArray);
            Assert.AreEqual(CollectionTypeHelper.GetCollectionType("Person"), model.Objects.First(c => c.Name == "Group").Properties.First(c => c.Name == "Person").Type);
            Assert.AreEqual("Company", model.Objects.First(c => c.Name == "Group").Properties.First(c => c.Name == "Company").Type);
        }

        [Test]
        public async Task ShouldHandleXml()
        {
            var model = await BuildModel("files/raml1/ordersXml-v1.raml");

            Assert.AreEqual("PurchaseOrderType", model.Controllers.First().Methods.First(m => m.Verb == "Get").ReturnType);
            Assert.AreEqual("PurchaseOrderType", model.Controllers.First().Methods.First(m => m.Verb == "Post").Parameter.Type);

            Assert.AreEqual(11, model.Objects.Count());
        }

        [Test]
        public async Task ShouldHandleCasing()
        {
            var model = await BuildModel("files/raml1/case.raml");

            Assert.IsNotNull(model.Objects.First(c => c.Name == "Person"));
            Assert.IsNotNull(model.Objects.First(c => c.Name == "Customer"));
            Assert.AreEqual("Person", model.Objects.First(c => c.Name == "Customer").BaseClass);

            Assert.AreEqual(CollectionTypeHelper.GetCollectionType("Person"), model.Controllers.First(c => c.Name == "Persons").Methods.First(m => m.Verb == "Post").Parameter.Type);
            Assert.AreEqual(CollectionTypeHelper.GetCollectionType("Person"), model.Controllers.First(c => c.Name == "Persons").Methods.First(m => m.Verb == "Get").ReturnType);
        }

        [Test]
        public async Task ShouldDiffientiateBetweenTypesAndBaseTypes()
        {
            var model = await BuildModel("files/raml1/underscore.raml");
            Assert.AreEqual(3, model.Objects.Count());
            Assert.AreEqual("Links", model.Objects.First(o => o.Name == "Example").Properties.First(c => c.Name == "Links").Type);
            Assert.AreEqual("Link", model.Objects.First(o => o.Name == "Links").Properties.First(c => c.Name == "Self").Type);
        }

        [Test]
        public async Task ShouldBuildDependentTypes()
        {
            var model = await BuildModel("files/raml1/dependentTypes.raml");
            Assert.AreEqual(2, model.Objects.Count());
            Assert.AreEqual("TypeA", model.Controllers.First().Methods.First().Parameter.Type);
        }

        [Test]
        public async Task ShouldHandleComplexQueryParams()
        {
            var model = await BuildModel("files/raml1/queryParams.raml");
            Assert.AreEqual(1, model.Objects.Count());
            Assert.AreEqual(true, model.Objects.First().IsScalar);
            Assert.AreEqual("string", model.Controllers.First().Methods.First().QueryParameters.First().Type);
        }

        [Test]
        public async Task ShouldHandleDates()
        {
            var model = await BuildModel("files/raml1/dates.raml");
            Assert.AreEqual(3, model.Objects.Count());
            Assert.AreEqual("DateTime", model.Objects.First(x => x.Name == "Person").Properties.First(x => x.Name == "Born").Type);
            Assert.AreEqual("DateTime", model.Objects.First(x => x.Name == "User").Properties.First(x => x.Name == "Lastaccess").Type);
            Assert.AreEqual("DateTime", model.Objects.First(x => x.Name == "Sample").Properties.First(x => x.Name == "Prop1").Type);
            Assert.AreEqual("DateTime", model.Objects.First(x => x.Name == "Sample").Properties.First(x => x.Name == "Prop2").Type);
            Assert.AreEqual("DateTime", model.Controllers.First(x => x.Name == "Access").Methods.First(x => x.Name == "Post").Parameter.Type);
            Assert.AreEqual(CollectionTypeHelper.GetCollectionType("DateTime"), model.Controllers.First(x => x.Name == "Access").Methods.First(x => x.Name == "Get").ReturnType);
            Assert.AreEqual(CollectionTypeHelper.GetCollectionType("DateTime"), model.Controllers.First(x => x.Name == "Persons").Methods.First(x => x.Name == "Put").Parameter.Type);
        }

        [Test]
        public async Task ShouldHandle_SalesOrdersCase()
        {
            var model = await BuildModel("files/raml1/salesOrders.raml");
            Assert.AreEqual(18, model.Objects.Count());
        }

        private static async Task<WebApiGeneratorModel> GetAnnotationTargetsModel()
        {
            return await BuildModel("files/raml1/annotations-targets.raml");
        }

        private static async Task<WebApiGeneratorModel> GetAnnotationsModel()
        {
            return await BuildModel("files/raml1/annotations.raml");
        }


        private async Task<WebApiGeneratorModel> GetCustomScalarModel()
        {
            return await BuildModel("files/raml1/customscalar.raml");
        }

        private async Task<WebApiGeneratorModel> GetMoviesModel()
        {
            return await BuildModel("files/raml1/movies-v1.raml");
        }

        private async Task<WebApiGeneratorModel> GetMovieTypeModel()
        {
            return await BuildModel("files/raml1/movietype.raml");
        }

        private async Task<WebApiGeneratorModel> GetParametersModel()
        {
            return await BuildModel("files/raml1/parameters.raml");
        }

        private async Task<WebApiGeneratorModel> GetTypeExpressionsModel()
        {
            return await BuildModel("files/raml1/typeexpressions.raml");
        }


        private static async Task<WebApiGeneratorModel> BuildModel(string ramlFile)
        {
            var fi = new FileInfo(ramlFile);
            var raml = await new RamlParser().LoadAsync(fi.FullName);
            var model = new WebApiGeneratorService(raml, "TargetNamespace").BuildModel();

            return model;
        }
    }
}