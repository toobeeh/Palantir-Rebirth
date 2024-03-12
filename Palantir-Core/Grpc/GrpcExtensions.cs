using Grpc.Core;

namespace Palantir_Core.Grpc;

public static class GrpcExtensions
{
    public static async Task<List<TItem>> ToListAsync<TItem>(this AsyncServerStreamingCall<TItem> asyncEnumerable)
    {
        var enumerator = asyncEnumerable.ResponseStream.ReadAllAsync();
        var list = new List<TItem>();
        await foreach (var item in enumerator)
        {
            list.Add(item);
        }
        return list;
    }
}