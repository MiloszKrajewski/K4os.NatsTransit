namespace K4os.NatsTransit.Abstractions;

public interface IKnownTypeResolver
{
    string? Resolve(Type? type);
    Type? Resolve(string? knownType, string subject);
}
