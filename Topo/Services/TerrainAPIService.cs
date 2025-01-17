﻿using Newtonsoft.Json;
using System.Text;
using Topo.Model.Logbook;
using Topo.Model.Login;
using Topo.Model.Members;
using Topo.Model.Milestone;
using Topo.Model.OAS;
using Topo.Model.Program;
using Topo.Model.SIA;

namespace Topo.Services
{

    public interface ITerrainAPIService
    {
        public Task<AuthenticationResultModel?> LoginAsync(string? branch, string? username, string? password);
        public Task<GetUserResultModel?> GetUserAsync();
        public Task<GetProfilesResultModel> GetProfilesAsync();
        public Task<GetMembersResultModel?> GetMembersAsync(string selectedUnitId);
        public Task<GetCalendarsResultModel?> GetCalendarsAsync(string userId);
        public Task PutCalendarsAsync(string userId, GetCalendarsResultModel putCalendarsResultModel);
        public Task<GetEventsResultModel?> GetEventsAsync(string userId, DateTime fromDate, DateTime toDate);
        public Task<GetEventResultModel?> GetEventAsync(string eventId);
        public Task<GetOASTreeResultsModel?> GetOASTreeAsync(string stream);
        public Task<GetOASTemplateResultModel?> GetOASTemplateAsync(string stream);
        public Task<GetUnitAchievementsResultsModel> GetUnitOASAchievements(string unit, string stream, string branch, int stage);
        public Task<GetSIAResultsModel> GetSIAResultsForMember(string memberId);
        public Task<GetGroupLifeResultModel> GetGroupLifeForUnit(string unitId);
        public Task RevokeAssumedProfiles();
        public Task AssumeProfile(string memberId);
        public Task<GetMemberLogbookMetricsResultModel> GetMemberLogbookMetrics(string memberId);
        public Task<GetMemberLogbookSummaryResultModel> GetMemberLogbookSummary(string memberId);
        public Task<GetMemberLogbookDetailResultModel> GetMemberLogbookDetail(string memberId, string logbookId);
    }

    public class TerrainAPIService : ITerrainAPIService
    {
        private readonly HttpClient _httpClient;
        private readonly StorageService _storageService;
        private readonly string cognitoAddress = "https://cognito-idp.ap-southeast-2.amazonaws.com/";
        private readonly string membersAddress = "https://members.terrain.scouts.com.au/";
        private readonly string eventsAddress = "https://events.terrain.scouts.com.au/";
        private readonly string templatesAddress = "https://templates.terrain.scouts.com.au/";
        private readonly string achievementsAddress = "https://achievements.terrain.scouts.com.au/";
        private readonly string metricsAddress = "https://metrics.terrain.scouts.com.au/";
        private readonly List<string> clientIds = new List<string>
        {
            "6v98tbc09aqfvh52fml3usas3c",
            "5g9rg6ppc5g1pcs5odb7nf7hf9",
            "1u4uajve0lin0ki5n6b61ovva7",
            "21m9o832lp5krto1e8ioo6ldg2"
        };

        public TerrainAPIService(HttpClient httpClient, StorageService storageService)
        {
            _httpClient = httpClient;
            _storageService = storageService;
        }

        public async Task<AuthenticationResultModel?> LoginAsync(string? branch, string? username, string? password)
        {
            AuthenticationResultModel authenticationResultModel = new AuthenticationResultModel();
            var savedClientId = "";
            //TODO Persist savedClientId to browser storage
            var result = "";
            var initiateAuth = new InitiateAuthModel();
            initiateAuth.ClientMetadata = new ClientMetadata();
            initiateAuth.AuthFlow = "USER_PASSWORD_AUTH";
            initiateAuth.AuthParameters = new AuthParameters();
            initiateAuth.AuthParameters.USERNAME = $"{branch}-{username}";
            initiateAuth.AuthParameters.PASSWORD = password;
            if (!string.IsNullOrEmpty(savedClientId))
            {
                initiateAuth.ClientId = savedClientId;
                var content = JsonConvert.SerializeObject(initiateAuth);
                result = await SendRequest(HttpMethod.Post, cognitoAddress, content, "AWSCognitoIdentityProviderService.InitiateAuth");
                var authenticationSuccessResult = JsonConvert.DeserializeObject<AuthenticationSuccessResultModel>(result);
                if (authenticationSuccessResult?.AuthenticationResult != null)
                {
                    authenticationResultModel.AuthenticationSuccessResultModel = authenticationSuccessResult;
                    _storageService.ClientId = savedClientId;
                }
            }
            else
            {
                foreach (var clientId in clientIds)
                {
                    initiateAuth.ClientId = clientId;
                    var content = JsonConvert.SerializeObject(initiateAuth);
                    result = await SendRequest(HttpMethod.Post, cognitoAddress, content, "AWSCognitoIdentityProviderService.InitiateAuth");
                    var authenticationSuccessResult = JsonConvert.DeserializeObject<AuthenticationSuccessResultModel>(result);
                    if (authenticationSuccessResult?.AuthenticationResult != null)
                    {
                        authenticationResultModel.AuthenticationSuccessResultModel = authenticationSuccessResult;
                        _storageService.ClientId = clientId;
                        //TODO Persist savedClientId to browser storage
                        break;
                    }
                }
            }
            if (authenticationResultModel.AuthenticationSuccessResultModel.AuthenticationResult == null)
            {
                var authenticationErrorResultModel = JsonConvert.DeserializeObject<AuthenticationErrorResultModel>(result);
                authenticationResultModel.AuthenticationErrorResultModel = authenticationErrorResultModel;
            }

            return authenticationResultModel;
        }

        public async Task<GetUserResultModel?> GetUserAsync()
        {
            AccessTokenModel accessToken = new AccessTokenModel() { AccessToken = _storageService.AuthenticationResult?.AccessToken };
            var content = JsonConvert.SerializeObject(accessToken);
            var result = await SendRequest(HttpMethod.Post, cognitoAddress, content, "AWSCognitoIdentityProviderService.GetUser");
            var getUserResultModel = JsonConvert.DeserializeObject<GetUserResultModel>(result);

            return getUserResultModel;
        }

        public async Task RefreshTokenAsync()
        {
            if (_storageService.TokenExpiry < DateTime.Now)
            {
                var initiateAuth = new InitiateAuthModel();
                initiateAuth.ClientMetadata = new ClientMetadata();
                initiateAuth.AuthFlow = "REFRESH_TOKEN_AUTH";
                initiateAuth.ClientId = _storageService.ClientId;
                initiateAuth.AuthParameters = new AuthParameters();
                initiateAuth.AuthParameters.REFRESH_TOKEN = _storageService?.AuthenticationResult?.RefreshToken;
                initiateAuth.AuthParameters.DEVICE_KEY = null;
                var content = JsonConvert.SerializeObject(initiateAuth);
                var result = await SendRequest(HttpMethod.Post, cognitoAddress, content, "AWSCognitoIdentityProviderService.InitiateAuth");
                var authenticationResult = JsonConvert.DeserializeObject<AuthenticationSuccessResultModel>(result);
                if (_storageService != null && _storageService.AuthenticationResult != null)
                {
                    _storageService.AuthenticationResult.AccessToken = authenticationResult?.AuthenticationResult?.AccessToken;
                    _storageService.AuthenticationResult.IdToken = authenticationResult?.AuthenticationResult?.IdToken;
                    _storageService.AuthenticationResult.ExpiresIn = authenticationResult?.AuthenticationResult?.ExpiresIn;
                    _storageService.AuthenticationResult.TokenType = authenticationResult?.AuthenticationResult?.TokenType;
                    _storageService.TokenExpiry = DateTime.Now.AddSeconds((authenticationResult?.AuthenticationResult?.ExpiresIn ?? 0) - 60);
                }
            }
        }

        public async Task<GetProfilesResultModel> GetProfilesAsync()
        {
            await RefreshTokenAsync();
            string requestUri = $"{membersAddress}profiles";
            var result = await SendRequest(HttpMethod.Get, requestUri);
            var getProfilesResultModel = DeserializeObject<GetProfilesResultModel>(result);

            return getProfilesResultModel;
        }

        public async Task<GetMembersResultModel?> GetMembersAsync(string selectedUnitId)
        {
            await RefreshTokenAsync();

            string requestUri = $"{membersAddress}units/{selectedUnitId}/members";
            var result = await SendRequest(HttpMethod.Get, requestUri);
            var getMembersResultModel = DeserializeObject<GetMembersResultModel>(result);

            return getMembersResultModel;
        }

        public async Task<GetCalendarsResultModel?> GetCalendarsAsync(string userId)
        {
            await RefreshTokenAsync();

            string requestUri = $"{eventsAddress}members/{userId}/calendars";
            var result = await SendRequest(HttpMethod.Get, requestUri);
            var getCalendarsResultModel = DeserializeObject<GetCalendarsResultModel>(result);

            return getCalendarsResultModel;
        }

        public async Task PutCalendarsAsync(string userId, GetCalendarsResultModel putCalendarsResultModel)
        {
            await RefreshTokenAsync();

            string requestUri = $"{eventsAddress}members/{userId}/calendars";
            var content = JsonConvert.SerializeObject(putCalendarsResultModel);
            await SendRequest(HttpMethod.Put, requestUri, content);
        }

        public async Task<GetEventsResultModel?> GetEventsAsync(string userId, DateTime fromDate, DateTime toDate)
        {
            await RefreshTokenAsync();

            var fromDateString = fromDate.ToString("s");
            var toDateString = toDate.ToString("s");
            string requestUri = $"{eventsAddress}members/{userId}/events?start_datetime={fromDateString}&end_datetime={toDateString}";
            var result = await SendRequest(HttpMethod.Get, requestUri);
            var getEventsResultModel = DeserializeObject<GetEventsResultModel>(result);

            return getEventsResultModel;
        }

        public async Task<GetEventResultModel?> GetEventAsync(string eventId)
        {
            await RefreshTokenAsync();

            string requestUri = $"{eventsAddress}events/{eventId}";
            var result = await SendRequest(HttpMethod.Get, requestUri);
            var getEventResultModel = DeserializeObject<GetEventResultModel>(result);

            return getEventResultModel;
        }

        public async Task<GetOASTreeResultsModel?> GetOASTreeAsync(string stream)
        {
            await RefreshTokenAsync();

            string requestUri = $"{templatesAddress}oas/{stream}/tree.json";
            var result = await SendRequest(HttpMethod.Get, requestUri);
            var getOASTreeResultsModel = DeserializeObject<GetOASTreeResultsModel>(result);

            return getOASTreeResultsModel;
        }

        public async Task<GetOASTemplateResultModel?> GetOASTemplateAsync(string stream)
        {
            await RefreshTokenAsync();

            string requestUri = $"{templatesAddress}{stream}/latest.json";
            var result = await SendRequest(HttpMethod.Get, requestUri);
            var getOASTemplateResultModel = DeserializeObject<GetOASTemplateResultModel>(result);

            return getOASTemplateResultModel;
        }

        public async Task<GetUnitAchievementsResultsModel> GetUnitOASAchievements(string unit, string stream, string branch, int stage)
        {
            await RefreshTokenAsync();
            string requestUri = $"{achievementsAddress}units/{unit}/achievements?type=outdoor_adventure_skill&stream={stream}&branch={branch}&stage={stage}";
            var responseContentResult = await SendRequest(HttpMethod.Get, requestUri);
            // Remove uploaded files from response before deserialising
            responseContentResult = responseContentResult.Replace("\"file_uploader\": [],", "");
            responseContentResult = responseContentResult.Replace("\"file_uploader\": []", "");
            var fileUploaderStart = responseContentResult.IndexOf("\"file_uploader\":");
            while (fileUploaderStart > 0)
            {
                var padding = 1;
                var fileUploaderEnd = responseContentResult.IndexOf("]", fileUploaderStart);
                var fileUploaderEndNextChar = responseContentResult.Substring(fileUploaderEnd, 2);
                if (fileUploaderEndNextChar == "],")
                    padding = 2;
                var fileUploader = responseContentResult.Substring(fileUploaderStart, fileUploaderEnd - fileUploaderStart + padding);
                responseContentResult = responseContentResult.Replace(fileUploader, "");
                fileUploaderStart = responseContentResult.IndexOf("\"file_uploader\":", fileUploaderStart);
            }
            var getUnitAchievementsResultsModel = DeserializeObject<GetUnitAchievementsResultsModel>(responseContentResult);

            return getUnitAchievementsResultsModel ?? new GetUnitAchievementsResultsModel();
        }

        public async Task<GetSIAResultsModel> GetSIAResultsForMember(string memberId)
        {
            await RefreshTokenAsync();

            string requestUri = $"{achievementsAddress}members/{memberId}/achievements?type=special_interest_area";
            var result = await SendRequest(HttpMethod.Get, requestUri);
            var getSIAResultsModel = DeserializeObject<GetSIAResultsModel>(result);

            return getSIAResultsModel ?? new GetSIAResultsModel();
        }

        public async Task<GetGroupLifeResultModel> GetGroupLifeForUnit(string unitId)
        {
            await RefreshTokenAsync();

            var requestUri = $"{metricsAddress}units/{unitId}/members?limit=999";
            var result = await SendRequest(HttpMethod.Get, requestUri);
            var getGroupLifeResultModel = DeserializeObject<GetGroupLifeResultModel>(result);

            return getGroupLifeResultModel ?? new GetGroupLifeResultModel();
        }

        public async Task RevokeAssumedProfiles()
        {
            await RefreshTokenAsync();

            var requestUri = $"{membersAddress}revoke-assumed-profiles";
            var result = await SendRequest(HttpMethod.Post, requestUri);
        }

        public async Task AssumeProfile(string memberId)
        {
            await RefreshTokenAsync();

            var requestUri = $"{membersAddress}members/{memberId}/assume-profiles";
            var result = await SendRequest(HttpMethod.Post, requestUri);
        }

        public async Task<GetMemberLogbookMetricsResultModel> GetMemberLogbookMetrics(string memberId)
        {
            await RefreshTokenAsync();

            var requestUri = $"{achievementsAddress}members/{memberId}/logbook-metrics";
            var result = await SendRequest(HttpMethod.Get, requestUri);
            var getMemberLogbookMetrics = DeserializeObject<GetMemberLogbookMetricsResultModel>(result);

            return getMemberLogbookMetrics ?? new GetMemberLogbookMetricsResultModel();
        }
        public async Task<GetMemberLogbookSummaryResultModel> GetMemberLogbookSummary(string memberId)
        {
            await RefreshTokenAsync();

            var requestUri = $"{achievementsAddress}members/{memberId}/logbook";
            var result = await SendRequest(HttpMethod.Get, requestUri);
            var getMemberLogbookSummary = DeserializeObject<GetMemberLogbookSummaryResultModel>(result);

            return getMemberLogbookSummary ?? new GetMemberLogbookSummaryResultModel();
        }

        public async Task<GetMemberLogbookDetailResultModel> GetMemberLogbookDetail(string memberId, string logbookId)
        {
            await RefreshTokenAsync();

            var requestUri = $"{achievementsAddress}members/{memberId}/logbook/{logbookId}";
            var result = await SendRequest(HttpMethod.Get, requestUri);
            var getMemberLogbookDetail = DeserializeObject<GetMemberLogbookDetailResultModel>(result);

            return getMemberLogbookDetail ?? new GetMemberLogbookDetailResultModel();
        }

        private async Task<string> SendRequest(HttpMethod httpMethod, string requestUri, string content = "", string xAmzTargetHeader = "")
        {
            HttpRequestMessage httpRequest = new HttpRequestMessage(httpMethod, requestUri);
            if (!string.IsNullOrEmpty(content))
                httpRequest.Content = new StringContent(content, Encoding.UTF8, "application/x-amz-json-1.1");
            if (string.IsNullOrEmpty(xAmzTargetHeader))
                httpRequest.Headers.Add("authorization", _storageService?.AuthenticationResult?.IdToken);
            else
                httpRequest.Headers.Add("X-Amz-Target", xAmzTargetHeader);
            httpRequest.Headers.Add("accept", "application/json, text/plain, */*");
            //httpRequest.Headers.Add("X-Amz-User-Agent", "aws-amplify/0.1.x js");

            var response = await _httpClient.SendAsync(httpRequest);
            var responseContent = response.Content.ReadAsStringAsync();
            var result = responseContent.Result;
            if (string.IsNullOrEmpty(xAmzTargetHeader)) // Dont log authorisation requests
            {
                // TODO Logging
                //_logger.LogInformation($"Request: {requestUri}");
                //_logger.LogInformation($"Response: {result}");
            }
            return result;
        }

        private T DeserializeObject<T>(string result)
        {
            try
            {
                return JsonConvert.DeserializeObject<T>(result);
            }
            catch (Exception ex)
            {
                // TODO Logging
                //_logger.LogError($"Error deserialising: {typeof(T)}");
                //_logger.LogError($"String being processed: {result}");
                //_logger.LogError($"Exception message: {ex.Message}");
            }
            return JsonConvert.DeserializeObject<T>("");
        }
    }
}
