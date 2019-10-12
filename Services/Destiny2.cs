using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Destiny2.Config;
using Destiny2.Responses;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Destiny2.Services
{
    class Destiny2 : IDestiny2
    {
        private readonly HttpClient _client;
        private readonly ILogger _logger;
        private readonly ITraceWriter _jsonLogWriter;
        private JsonSerializerSettings _settings = new JsonSerializerSettings();

        public Destiny2(HttpClient client, ILogger<Destiny2> logger, ITraceWriter jsonLogWriter)
        {
            _client = client;
            _logger = logger;
            _jsonLogWriter = jsonLogWriter;
        }

        public bool DeserializationDebugging
        {
            get { return _settings.TraceWriter != null; }
            set
            {
                if (value)
                {
                    _settings.TraceWriter = _jsonLogWriter;
                }
                else
                {
                    _settings.TraceWriter = null;
                }
            }
        }

        public string BaseUrl => _client.BaseAddress.AbsolutePath;

        public Task<Manifest> GetManifest()
        {
            return Get<Manifest>(string.Empty, "Destiny2/Manifest");
        }

        public Task<DestinyLinkedProfilesResponse> GetLinkedProfiles(string accessToken, long membershipId, BungieMembershipType type = BungieMembershipType.BungieNext)
        {
            return Get<DestinyLinkedProfilesResponse>(accessToken, $"Destiny2/{(int)type}/Profile/{membershipId}/LinkedProfiles");
        }

        public Task<DestinyProfileResponse> GetProfile(string accessToken, BungieMembershipType type, long id)
        {
            return GetProfile(accessToken, type, id, DestinyComponentType.Profiles);
        }

        public Task<DestinyProfileResponse> GetProfile(string accessToken, BungieMembershipType type, long id,
            params DestinyComponentType[] components)
        {
            var query = ConvertComponents(components);
            return Get<DestinyProfileResponse>(accessToken, $"Destiny2/{(int)type}/Profile/{id}", new[] { query });
        }

        public Task<DestinyCharacterResponse> GetCharacterInfo(string accessToken, BungieMembershipType type, long id,
            long characterId, params DestinyComponentType[] infos)
        {
            var query = ConvertComponents(infos);
            return Get<DestinyCharacterResponse>(accessToken, $"Destiny2/{(int)type}/Profile/{id}/Character/{characterId}/", query);
        }

        public Task<DestinyItemResponse> GetItem(string accessToken, BungieMembershipType type, long id, long itemInstanceId,
            params DestinyComponentType[] infos)
        {
            var query = ConvertComponents(infos);
            return Get<DestinyItemResponse>(accessToken, $"Destiny2/{(int)type}/Profile/{id}/Item/{itemInstanceId}/", query);
        }

        public Task<int> EquipItem(string accessToken, BungieMembershipType type, long characterId, long itemInstanceId)
        {
            dynamic body = new
            {
                itemId = itemInstanceId,
                characterId,
                membershipType = type,
            };
            return Post<int>(accessToken, $"/Destiny2/Actions/Items/EquipItem/", body);
        }

        public Task<DestinyEquipItemResponse> EquipItems(string accessToken, BungieMembershipType type, long characterId, long[] itemInstanceIds)
        {
            dynamic body = new
            {
                itemIds = itemInstanceIds,
                characterId,
                membershipType = type,
            };
            return Post<DestinyEquipItemResponse>(accessToken, $"/Destiny2/Actions/Items/EquipItems/", body);
        }

        public async Task<bool> DownloadFile(string relativePath, string destination)
        {
            try
            {
                using (var inputStream = await _client.GetStreamAsync(relativePath))
                {
                    using (var outputStream = File.Create(destination))
                    {
                        await inputStream.CopyToAsync(outputStream);
                        return true;
                    }
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError($"Error downloading {relativePath}: {ex.Message}");
                return false;
            }
        }

        private Uri BuildUrl(string method, IEnumerable<(string name, string value)> queryItems = null)
        {
            var builder = new UriBuilder(_client.BaseAddress + $"Platform/{method}/");

            if (queryItems != null)
            {
                var translated = from query in queryItems
                                 select $"{query.name}={query.value}";
                builder.Query = string.Join("&", translated);
            }

            return builder.Uri;
        }

        private async Task<T> Get<T>(string accessToken, string method, params (string name, string value)[] queryItems)
        {
            return await Request<T>("GET", accessToken, method, null, queryItems);
        }

        private async Task<T> Post<T>(string accessToken, string method, object body, params (string name, string value)[] queryItems)
        {
            return await Request<T>("POST", accessToken, method, body, queryItems);
        }

        private async Task<T> Request<T>(string method, string accessToken, string apiMethod, object body, params (string name, string value)[] queryItems)
        {
            if (!string.IsNullOrEmpty(accessToken))
            {
                _client.DefaultRequestHeaders.Remove("Authorization");
                _client.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");
            }

            Func<HttpClient, Uri, HttpContent, Task<string>> requestMethod = RequestGet;

            switch (method)
            {
                case "POST":
                    requestMethod = RequestPost;
                    break;
                default:
                    break;
            }

            try
            {
                var url = BuildUrl(apiMethod, queryItems);
                _logger.LogInformation($"Calling {url}");

                var stringContent = body != null ? new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json") : null;
                var json = await requestMethod(_client, url, stringContent);

                var response = JsonConvert.DeserializeObject<Response<T>>(json, _settings);

                if (response.ErrorCode != 1)
                {
                    _logger.LogWarning($"Error Code: {response.ErrorCode}; Error Status: {response.ErrorStatus}");
                    return default(T);
                }

                return response.Data;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError($"Error calling {apiMethod}: {ex.Message}");
                return default(T);
            }
        }

        private async Task<string> RequestGet(HttpClient client, Uri url, HttpContent _) => await client.GetStringAsync(url);
        private async Task<string> RequestPost(HttpClient client, Uri url, HttpContent content)
        {
            var result = await client.PostAsync(url, content);

            if (!result.IsSuccessStatusCode)
            {
                throw new HttpRequestException("Response failure");
            }

            return await result.Content.ReadAsStringAsync();
        }

        private static (string name, string value) ConvertComponents(IEnumerable<DestinyComponentType> components)
        {
            var rawValues = from component in components
                            select (int)component;
            return ("components",  string.Join(",", rawValues));
        }
    }
}
