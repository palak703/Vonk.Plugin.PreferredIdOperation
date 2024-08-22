using Hl7.Fhir.Model;
using Microsoft.Extensions.Logging;
using Moq;
using Vonk.Core.Common;
using Vonk.Core.Context;
using Vonk.Core.Repository;
using Vonk.Fhir.R4;

namespace Vonk.Plugin.PreferredIdOperation.Test
{
    [TestClass]
    public class PreferredIdServiceTest
    {
        private Mock<IAdministrationSearchRepository> _mockRepository;
        private Mock<ILogger<PreferredIdService>> _mockLogger;
        private Mock<IVonkContext> _mockVonkContext;
        private PreferredIdService _service;

        [TestInitialize]
        public void Setup()
        {
            _mockRepository = new Mock<IAdministrationSearchRepository>();
            _mockLogger = new Mock<ILogger<PreferredIdService>>();
            _mockVonkContext = new Mock<IVonkContext>();
            _service = new PreferredIdService(_mockRepository.Object, _mockLogger.Object);

        }

        [TestMethod]
        public async System.Threading.Tasks.Task PreferredIdGet_ValidInput_ReturnsSuccess()
        {
            // Arrange
            IArgument idArgument = new Argument(ArgumentSource.Query, "id", "http://hl7.org/fhir/administrative-gender");
            IArgument typeArgument = new Argument(ArgumentSource.Query, "type", "url");
            var argumentCollection = new ArgumentCollection(idArgument, typeArgument);

            var searchOptions = new SearchOptions(VonkInteraction.type_custom, new Uri("http://localhost:4080/administration"));

            var mockArgumentHelper = new Mock<ArgumentHelper>();
            mockArgumentHelper.Setup(x => x.TryGetArgument(It.IsAny<IArgumentCollection>(), "id", out idArgument)).Returns(true);
            mockArgumentHelper.Setup(x => x.TryGetArgument(It.IsAny<IArgumentCollection>(), "type", out typeArgument)).Returns(true);


            // setups
            _mockVonkContext.Setup(x => x.Arguments).Returns(argumentCollection);
            _mockVonkContext.Setup(x => x.Request.Interaction).Returns(VonkInteraction.type_custom);
            _mockVonkContext.Setup(x => x.InformationModel).Returns("Fhir4.0");
            _mockVonkContext.Setup(x => x.Response.Payload).Returns(It.IsAny<IResource>());
           
            var namingSystem = new NamingSystem
            {
                UniqueId = new List<NamingSystem.UniqueIdComponent>
        {
            new NamingSystem.UniqueIdComponent
            {
                Type = NamingSystem.NamingSystemIdentifierType.Uri,
                Value = "http://hl7.org/fhir/administrative-gender"
            }
        }
            };
            var searchResult = new SearchResult(new[] { namingSystem.ToIResource() }, 1);

            _mockRepository.Setup(x => x.Search(It.IsAny<ArgumentCollection>(), It.IsAny<SearchOptions>()))
                .ReturnsAsync(searchResult);

            // Act
            await _service.PreferredIdGet(_mockVonkContext.Object);

            // Assert
            _mockRepository.Verify(x => x.Search(It.IsAny<ArgumentCollection>(), It.IsAny<SearchOptions>()), Times.Once);
            _mockVonkContext.VerifySet(x => x.Response.Payload = It.IsAny<IResource>(), Times.Once);
            _mockVonkContext.VerifySet(x => x.Response.HttpResult = 200, Times.Once);
        }

        public class ArgumentHelper
        {
            public virtual bool TryGetArgument(IArgumentCollection arguments, string key, out IArgument argument)
            {
                return arguments.TryGetArgument(key, out argument);
            }
        }
    }
}