namespace K4os.NatsTransit.Abstractions;

public interface IExceptionSerializer
{
    public string Serialize(Exception exception);
    public Exception Deserialize(string payload);
}

public class DumbExceptionSerializer: IExceptionSerializer
{
    public static DumbExceptionSerializer Instance { get; } = new();
    public string Serialize(Exception exception) => exception.Message;
    public Exception Deserialize(string payload) => new(payload);
}