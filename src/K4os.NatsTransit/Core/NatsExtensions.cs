using K4os.NatsTransit.Extensions;
using NATS.Client.Core;

namespace K4os.NatsTransit.Core;

public static class NatsExtensions
{
    public static string? GetErrorHeader(this NatsHeaders? headers) =>
        headers?.TryGetValueOrDefault(NatsConstants.ErrorHeaderName).ToString();
    
    public static string? GetKnownTypeHeader(this NatsHeaders? headers) =>
        headers?.TryGetValueOrDefault(NatsConstants.KnownTypeHeaderName).ToString();
    
    public static string? GetReplyToHeader(this NatsHeaders? headers) =>
        headers?.TryGetValueOrDefault(NatsConstants.ReplyToHeaderName).ToString();
}
