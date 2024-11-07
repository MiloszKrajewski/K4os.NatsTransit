using System.Diagnostics;
using NATS.Client.Core;
using NATS.Client.JetStream;

namespace K4os.NatsTransit.Extensions;

internal static class ActivityExtensions
{
    public static Activity OnSending<T>(this Activity activity, string subject, T message) =>
        activity
            .AddTag("sent.subject", subject)
            .AddTag("sent.message_type", message?.GetType().GetFriendlyName() ?? "<null>");

    public static Activity OnReceived(this Activity activity, string stream, string consumer, string subject) =>
        activity.AddTag("rcvd.stream", stream).AddTag("rcvd.consumer", consumer).AddTag("rcvd.subject", subject);

    public static Activity OnReceived(this Activity activity, string subject) =>
        activity.AddTag("rcvd.subject", subject);

    public static Activity OnReceived<T>(
        this Activity activity, string stream, string consumer, NatsJSMsg<T> message) =>
        activity.OnReceived(stream, consumer, message.Subject);

    public static Activity OnReceived<T>(this Activity activity, NatsMsg<T> message) =>
        activity.OnReceived(message.Subject);

    public static Activity OnUnpacked<T>(this Activity activity, T message) =>
        activity.AddTag("rcvd.message_type", message?.GetType().GetFriendlyName() ?? "<null>");

    public static Activity OnException(this Activity activity, Exception error) =>
        activity.AddTag("error_type", error.GetType().GetFriendlyName()).AddTag("error", error.Explain());
}
