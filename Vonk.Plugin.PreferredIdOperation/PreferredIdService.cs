using Hl7.Fhir.ElementModel;
using Hl7.Fhir.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Vonk.Core.Common;
using Vonk.Core.Context;
using Vonk.Core.Repository;
using Vonk.Core.Support;
using Vonk.Fhir.R4;
using static Hl7.Fhir.Model.NamingSystem;
using static Vonk.Core.Context.VonkOutcome;

namespace Vonk.Plugin.PreferredIdOperation
{
    public class PreferredIdService
    {
        private readonly IAdministrationSearchRepository _administrationSearchRepository;
        private readonly ILogger<PreferredIdService> _logger;
        private const string ResourceName = "NamingSystem";
        private const string SearchParamId = "id";
        private const string SearchParamType = "type";
        private const string SearchParamValue = "value";


        /// <summary>
        /// This service is a custom operation for NamingResource
        /// </summary>
        /// <param name="searchRepository"></param>
        /// <param name="logger"></param>
        public PreferredIdService(IAdministrationSearchRepository searchRepository,
            ILogger<PreferredIdService> logger)
        {
            Check.NotNull(searchRepository, nameof(searchRepository));
            Check.NotNull(logger, nameof(logger));
            _administrationSearchRepository = searchRepository;
            _logger = logger;
        }

        /// <summary>
        /// Retrieves the requested unique ID from Naming resource
        /// </summary>
        /// <param name="vonkContext"></param>
        /// <returns></returns>
        public async System.Threading.Tasks.Task PreferredIdGet(IVonkContext vonkContext)
        {
            vonkContext.Arguments.TryGetArgument(SearchParamId, out var id);
            vonkContext.Arguments.TryGetArgument(SearchParamType, out var type);
            var serverBaseUrl = new Uri(@"http://localhost:4080/administration"); // ToDo: Server base URL shall be retrieved from vonkContext request

            await PreferredId(new ArgumentCollection(id, type), serverBaseUrl, vonkContext);
        }

        private async System.Threading.Tasks.Task PreferredId(ArgumentCollection arguments, Uri serverBaseUrl,
            IVonkContext vonkContext)
        {
            var searchOptions = new SearchOptions(vonkContext.Request.Interaction, serverBaseUrl);

            var (parametersResolved, resolvedResource, error) =
                await SearchResourceWithArguments(searchOptions, arguments);

            if (parametersResolved)
            {
                if (resolvedResource?.InformationModel != vonkContext.InformationModel)
                {
                    CancelPreferredIdOperation(vonkContext, StatusCodes.Status415UnsupportedMediaType,
                        WrongInformationModel(vonkContext.InformationModel, resolvedResource));
                    return;
                }
            }

            if (!(error is null))
            {
                switch (error.IssueType)
                {
                    case IssueType.NotFound:
                        _logger.LogDebug(error.Details);
                        CancelPreferredIdOperation(vonkContext, StatusCodes.Status404NotFound, error);
                        break;
                    case IssueType.NotSupported:
                        _logger.LogDebug(error.Details);
                        CancelPreferredIdOperation(vonkContext, StatusCodes.Status501NotImplemented, error);
                        break;
                    default:
                        _logger.LogDebug(error.Details);
                        CancelPreferredIdOperation(vonkContext, StatusCodes.Status500InternalServerError, error);
                        break;
                }

                return;
            }

            SendPreferredId(vonkContext, resolvedResource);
        }

        private async Task<(bool success, IResource? parameters, VonkIssue? error)> SearchResourceWithArguments(
            SearchOptions searchOptions, ArgumentCollection argumentCollection)
        {
            try
            {
                var type = GetSearchParameterType(argumentCollection);

                if (type == NamingSystemIdentifierType.Other) // If type in the Query for this custom operation is other then Url and OID, then returns error.
                    return (false, null, ReferenceNotResolvedForTypeIssue(type.ToString()));

                var searchArgs = new ArgumentCollection(
                new Argument(ArgumentSource.Internal, ArgumentNames.resourceType, ResourceName),
                new Argument(ArgumentSource.Internal, SearchParamValue, GetSearchParameterId(argumentCollection))); // prepares search argument collection (Resource name and search parameter)

                var result = await _administrationSearchRepository.Search(searchArgs, searchOptions);
                if (result == null || !result.Any())
                    return (false, null, ReferenceNotResolvedForValueIssue(GetSearchParameterId(argumentCollection)))!; // No results found for requested search parameter

                IResource res = BuildParameterResource(result, type); //Converts the search result into required Parameter resource.

                return (true, res, null);
            }
            catch (Exception e)
            {
                _logger.LogDebug($"Internal server error occurred while executing $document. Details: {e.Message}");

                return (false, null, InternalServerError());
            }
        }

        /// <summary>
        /// Returns the value of Id parameter in cutom operation Query
        /// </summary>
        /// <param name="arguments"></param>
        /// <returns></returns>
        private static string GetSearchParameterId(ArgumentCollection arguments)
        {
            arguments.TryGetArgument(SearchParamId, out var id);
            return id.ArgumentValue.ToString();
        }

        /// <summary>
        /// Returns the value of type parameter in cutom operation Query
        /// </summary>
        /// <param name="arguments"></param>
        /// <returns></returns>
        private static NamingSystemIdentifierType GetSearchParameterType(ArgumentCollection arguments)
        {
            arguments.TryGetArgument(SearchParamType, out var argId);
            return argId.ArgumentValue.ToString()?.ToLower() switch
            {
                "url" => NamingSystemIdentifierType.Uri,
                "oid" => NamingSystemIdentifierType.Oid,
                _ => NamingSystemIdentifierType.Other
            };
        }
        /// <summary>
        /// Converts search result to Parameter Resource
        /// </summary>
        /// <param name="result"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        private static IResource BuildParameterResource(SearchResult result, NamingSystemIdentifierType type)
        {
            var namingSystem = result.FirstOrDefault()?.ToPoco<NamingSystem>();

            var parameters = new Parameters
            {
                { "name", new FhirString(namingSystem?.UniqueId?.FirstOrDefault(x => x.Type == type)?.Value) }
            };
            var res = parameters.ToIResource();
            return res;
        }

        #region Methods for sending response to server
        /// <summary>
        /// Send successful response to server i.e. requested Parameter Resource
        /// </summary>
        /// <param name="vonkContext"></param>
        /// <param name="res"></param>
        private static void SendPreferredId(IVonkContext vonkContext, IResource? res)
        {
            vonkContext.Response.Payload = res;
            vonkContext.Response.HttpResult = 200;
        }
        
        /// <summary>
        ///  Send unsuccessful response in case of error or unsupported agrument types
        /// </summary>
        /// <param name="vonkContext"></param>
        /// <param name="statusCode"></param>
        /// <param name="failedReference"></param>
        private static void CancelPreferredIdOperation(IVonkContext vonkContext, int statusCode,
            VonkIssue? failedReference = null)
        {
            vonkContext.Response.HttpResult = statusCode;
            if (failedReference != null)
                vonkContext.Response.Outcome.AddIssue(failedReference);
        }

        #endregion

        #region Methods for creating Vonk Issues
        private static VonkIssue ReferenceNotResolvedForValueIssue(string? failedId)
        {
            var issue = new VonkIssue(IssueSeverity.Error,
                IssueType.NotFound, "MSG_LOCAL_FAIL",
                $"Unable to search resource {ResourceName} which has uniQueID value - {failedId}")
            {
                DetailCodeSystem = "http://vonk.fire.ly/fhir/ValueSet/OperationOutcomeIssueDetails"
            };

            return issue;
        }

        private static VonkIssue? ReferenceNotResolvedForTypeIssue(string failedType)
        {
            var issue = new VonkIssue(IssueSeverity.Error,
                IssueType.NotSupported, "MSG_LOCAL_FAIL",
                $" {ResourceName} only supports uniQueID type as Url or oid")
            {
                DetailCodeSystem = "http://vonk.fire.ly/fhir/ValueSet/OperationOutcomeIssueDetails"
            };

            return issue;
        }

        private static VonkIssue? WrongInformationModel(string expectedInformationModel, IResource? resolvedResource)
        {
            return new VonkIssue(VonkIssue.PROCESSING_ERROR.Severity, VonkIssue.PROCESSING_ERROR.IssueType,
                details:
                $"Found {resolvedResource?.Type}/{resolvedResource?.Id} in information model {resolvedResource?.InformationModel}. Expected information model {expectedInformationModel} instead.");
        }

        private static VonkIssue? InternalServerError()
        {
            return VonkIssue.INTERNAL_ERROR.CloneWithDetails(
                "Internal server error occurred while executing $document. Please see server logs for more details");
        }
        #endregion
    }
}