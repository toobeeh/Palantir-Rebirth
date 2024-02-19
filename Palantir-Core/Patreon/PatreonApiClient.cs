using Hypermedia.Json;
using Hypermedia.JsonApi;
using Hypermedia.JsonApi.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Palantir_Core.Patreon.Models;

namespace Palantir_Core.Patreon;

public class PatreonApiClient
{ 
    private readonly HttpClient client;
    private readonly String baseUrl = "https://www.patreon.com/api/oauth2/v2";
    private readonly String patronTierId, patronizerTierId;
    private readonly JsonApiSerializerOptions serializerOptions = new ()
    {
        ContractResolver = PatreonApiResolver.CreateResolver(),
        FieldNamingStrategy = DasherizedFieldNamingStrategy.Instance,
        JsonConverters = [new SocialConnectionsConverter()]
    };
    
    public PatreonApiClient(IOptions<PatreonApiClientOptions> options)
    {
        patronizerTierId = options.Value.PatronizerTierId;
        patronTierId = options.Value.PatronTierId;
        
        client = new HttpClient();
        client.DefaultRequestHeaders.Accept.Clear();

        // add default headers
        client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/vnd.api+json"));
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {options.Value.CreatorAccessToken}");
    }
    
    private async Task<List<TResult>> GetManyApiResponse<TResult>(String url, Dictionary<String, String> queryParams)
    {
        var query = await new FormUrlEncodedContent(queryParams).ReadAsStringAsync();
        var response = await client.GetAsync($"{baseUrl}{url}?{query}");
        response.EnsureSuccessStatusCode();

        var resource = await response.Content.ReadAsJsonApiManyAsync<TResult>(serializerOptions);
        return resource;
    } 
    
    public async Task<PatreonSubscriptions> GetCurrentSubscriptions()
    {
        
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

        var patrons = new List<ulong>();
        var patronizer = new List<ulong>();
        
        // find currently active patrons
        foreach(var member in response)
        {
            var id = member.user.social_connections?.discordId;
            if (id is not null)
            {
                foreach (var tier in member.currently_entitled_tiers)
                {
                    if(tier.id == patronizerTierId) patronizer.Add(Convert.ToUInt64(id));
                    else if(tier.id == patronTierId) patrons.Add(Convert.ToUInt64(id));
                }
            }
        }

        return new PatreonSubscriptions(patronizer, patrons);
    }
}

