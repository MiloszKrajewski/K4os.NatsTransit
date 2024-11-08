﻿using System.Buffers;
using System.Text.Json;
using K4os.NatsTransit.Abstractions.Serialization;
using K4os.NatsTransit.Serialization;
using NATS.Client.Core;

namespace K4os.NatsTransit.Core;

public class SystemJsonNatsSerializerFactory: INatsSerializerFactory
{
    private readonly JsonSerializerOptions? _options;

    public SystemJsonNatsSerializerFactory(JsonSerializerOptions? options) => 
        _options = options;

    public OutboundAdapter<T> GetOutboundAdapter<T>() => 
        new(new Instance<T>(_options));

    public InboundAdapter<T> GetInboundAdapter<T>() => 
        new(new Instance<T>(_options));

    public IExceptionSerializer? GetExceptionSerializer() => null;

    public class Instance<T>: INatsSerialize<T>, INatsDeserialize<T>
    {
        private readonly JsonSerializerOptions? _options;

        public Instance(JsonSerializerOptions? options) => 
            _options = options;

        public void Serialize(IBufferWriter<byte> bufferWriter, T value)
        {
            var writer = new Utf8JsonWriter(bufferWriter);
            JsonSerializer.Serialize(writer, value, _options);
        }

        public T? Deserialize(in ReadOnlySequence<byte> buffer)
        {
            if (buffer.IsEmpty) return default;
            var reader = new Utf8JsonReader(buffer);
            return JsonSerializer.Deserialize<T>(ref reader, _options);
        }
    }
}