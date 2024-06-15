using K4os.NatsTransit.Abstractions;
using MediatR;

namespace K4os.NatsTransit.Api.Handlers;

public class EchoCommandHandler: IRequestHandler<EchoCommand, EchoResponse>
{
    private readonly IMessageBus _messageBus;

    public EchoCommandHandler(IMessageBus messageBus)
    {
        _messageBus = messageBus;
    }
    
    public async Task<EchoResponse> Handle(EchoCommand request, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        var message = request.Message;

        // await _messageBus.Publish(new EchoReceived(message));

        // throw new ArgumentException("I don't like your message!");
        
        return new EchoResponse($"{message} {message}");
    }
}