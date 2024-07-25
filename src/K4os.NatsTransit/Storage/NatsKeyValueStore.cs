// using K4os.NatsTransit.Abstractions;
// using Microsoft.Extensions.Logging;
// using NATS.Client.KeyValueStore;
//
// namespace K4os.NatsTransit.Storage;
//
// public class NatsKeyValueStore: IKeyValueStore
// {
//     protected readonly ILogger Log;
//     private readonly INatsKVStore _store;
//     private readonly INatsSerializerFactory _serializerFactory;
//
//     public NatsKeyValueStore(
//         ILoggerFactory loggerFactory,
//         INatsKVContext context,
//         INatsSerializerFactory serializerFactory,
//         NatsKeyValueStoreConfig config)
//     {
//         Log = loggerFactory.CreateLogger(GetType());
//         var storeConfig = new NatsKVConfig(config.StoreName) {
//             History = 3,
//             Storage = NatsKVStorageType.File
//         };
//         _serializerFactory = serializerFactory;
//         
//         _store = CreateStore(context, storeConfig);
//     }
//
//     private INatsKVStore CreateStore(INatsKVContext context, NatsKVConfig storeConfig) =>
//         context
//             .CreateStoreAsync(storeConfig, CancellationToken.None)
//             .AsTask().GetAwaiter().GetResult();
//
//     public async Task<(T? Value, long Revision)?> Get<T>(string key, CancellationToken token = default)
//     {
//         var serializer = _serializerFactory.PayloadDeserializer<T>();
//         try
//         {
//             var entry = await _store.GetEntryAsync(key, 0uL, serializer, token);
//             entry.EnsureSuccess();
//             return (entry.Value, (long)entry.Revision);
//         }
//         catch (NatsKVKeyNotFoundException)
//         {
//             return null;
//         }
//     }
//
//     public async Task<bool> Add<T>(string key, T value, CancellationToken token = default)
//     {
//         var serializer = _serializerFactory.PayloadSerializer<T>();
//         try
//         {
//             var revision = await _store.CreateAsync(key, value, serializer, token);
//         }
//         catch (NatsKVCreateException)
//         {
//             return false;
//         }
//     }
//
//     public Task<bool> Update<T>(string key, T value, long revision, CancellationToken token = default)
//     {
//         throw new NotImplementedException();
//     }
//
//     public Task AddOrUpdate<T>(string key, T value, CancellationToken token = default)
//     {
//         throw new NotImplementedException();
//     }
// }
//
// public class NatsKeyValueStoreConfig
// {
//     public string StoreName { get; set; }
// }
