using Hypermedia.Json;
using Hypermedia.JsonApi;
using Hypermedia.JsonApi.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Palantir_Core.Patreon.Models;

namespace Palantir_Core.Patreon;

public class PatreonApiClient
{
    private const string BaseUrl = "https://www.patreon.com/api/oauth2/v2";
    
    private readonly ILogger<PatreonApiClient> _logger;
    private readonly HttpClient _client;
    private readonly PatreonApiClientOptions _options;
    private readonly string _patronTierId, _patronizerTierId;
    private readonly JsonApiSerializerOptions _serializerOptions = new ()
    {
        ContractResolver = PatreonApiResolver.CreateResolver(),
        FieldNamingStrategy = DasherizedFieldNamingStrategy.Instance,
        JsonConverters = [new SocialConnectionsConverter()] // add support for nested parsing of discord connection
    };
    
    public PatreonApiClient(IOptions<PatreonApiClientOptions> options, ILogger<PatreonApiClient> logger)
    {
        _patronizerTierId = options.Value.PatronizerTierId;
        _patronTierId = options.Value.PatronTierId;
        _logger = logger;
        _options = options.Value;
        
        _client = new HttpClient();
        _client.DefaultRequestHeaders.Accept.Clear();

        // add default headers
        _client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/vnd.api+json"));
        _client.DefaultRequestHeaders.Add("Authorization", $"Bearer {options.Value.CreatorAccessToken}");
    }
    
    private async Task<List<TResult>> GetManyApiResponse<TResult>(String url, Dictionary<String, String> queryParams)
    {
        _logger.LogTrace("GetManyApiResponse<{type}>({url}, {queryParams})", typeof(TResult).Name, url, queryParams);
        
        var query = await new FormUrlEncodedContent(queryParams).ReadAsStringAsync();
        var response = await _client.GetAsync($"{BaseUrl}{url}?{query}");
        response.EnsureSuccessStatusCode();

        var resource = await response.Content.ReadAsJsonApiManyAsync<TResult>(_serializerOptions);
        return resource;
    } 
    
    public async Task<PatreonSubscriptions> GetCurrentSubscriptions()
    {
        _logger.LogTrace("GetCurrentSubscriptions()");
        
        // build required queryparams since patreon api by default omits all fields
        var queryParams = new Dictionary<string, string>()
        {
            {"page[size]", "500"},
            {"include", "user,currently_entitled_tiers"},
            {"fields[user]", "social_connections"},
            {"fields[member]", "full_name"},
            {"fields[tier]", "title,discord_role_ids"},
        };

        // get data from api
        var response = await GetManyApiResponse<Member>("/campaigns/6634236/members", queryParams);
        var patrons = new List<long>(_options.AdditionalPatronDiscordIds);
        var patronizer = new List<long>(_options.AdditionalPatronizerDiscordIds);
        
        // find currently active patrons
        foreach(var member in response)
        {
            var id = member.user.social_connections?.discordId;
            if (id is not null)
            {
                foreach (var tier in member.currently_entitled_tiers)
                {
                    if(tier.id == _patronizerTierId) patronizer.Add(Convert.ToInt64(id));
                    else if(tier.id == _patronTierId) patrons.Add(Convert.ToInt64(id));
                }
            }
        }
        
        return new PatreonSubscriptions(patronizer, patrons);
    }
}

