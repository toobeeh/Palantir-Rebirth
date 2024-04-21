using Grpc.Core;

namespace Valmar_Client.Grpc;

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
    
    public static async Task<Dictionary<TKey, TItem>> ToDictionaryAsync<TItem, TKey>(this AsyncServerStreamingCall<TItem> asyncEnumerable, Func<TItem, TKey> keySelector) where TKey : notnull
    {
        var enumerator = asyncEnumerable.ResponseStream.ReadAllAsync();
        var dict = new Dictionary<TKey, TItem>();
        await foreach (var item in enumerator)
        {
            var key = keySelector.Invoke(item);
            var success = dict.TryAdd(key, item);
            if (!success)
            {
                throw new InvalidOperationException($"Key {key} already exists in dictionary.");
            }
        }
        return dict;
    }
}