using Hypermedia.Json;
using JsonLite.Ast;
using tobeh.Palantir.Core.Patreon.JsonApi.Models;

namespace tobeh.Palantir.Core.Patreon.JsonApi;

public class SocialConnectionsConverter : IJsonConverter
{
    public JsonValue SerializeValue(IJsonSerializer serializer, Type type, object value)
    {
        // not needed
        throw new NotImplementedException();
    }

    public object DeserializeValue(IJsonDeserializer deserializer, Type type, JsonValue jsonValue)
    {
        // try to find a field with discord id
        try
        {
            var idValue =
                ((jsonValue as JsonObject).Members.FirstOrDefault(m => m.Name == "discord").Value as JsonObject)
                .Members
                .FirstOrDefault(m => m.Name == "user_id").Value;

            var id = (idValue as JsonString).Value;
            return new SocialConnections() { discordId = id };
        }
        catch
        {
            return new SocialConnections();
        }
    }

    public bool CanConvert(Type type)
    {
        return type == typeof(SocialConnections);
    }
}