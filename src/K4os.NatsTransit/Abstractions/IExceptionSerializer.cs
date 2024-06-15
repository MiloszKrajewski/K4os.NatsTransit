namespace K4os.NatsTransit.Abstractions;

public interface IExceptionSerializer
{
    public string Serialize(Exception exception);
    public Exception Deserialize(string payload);
}

public class FakeExceptionSerializer: IExceptionSerializer
{
    public string Serialize(Exception exception) => exception.Message;
    public Exception Deserialize(string payload) => new(payload);
}