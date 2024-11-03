// namespace K4os.NatsTransit.Abstractions.KeyValueStore;
//
// public interface IKeyValueStore
// {
//     public Task<(T? Value, long Revision)?> Get<T>(
//         string key, CancellationToken token = default);
//
//     public Task<bool> Add<T>(
//         string key, T value, CancellationToken token = default);
//
//     public Task<bool> Update<T>(
//         string key, T value, long revision, CancellationToken token = default);
//
//     public Task AddOrUpdate<T>(
//         string key, T value, CancellationToken token = default);
// }
