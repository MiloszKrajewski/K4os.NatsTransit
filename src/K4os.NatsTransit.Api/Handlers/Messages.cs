// ReSharper disable ClassNeverInstantiated.Global

using MediatR;

namespace K4os.NatsTransit.Api.Handlers;

public record EchoCommand(string Message): IRequest<EchoResponse>;

public record EchoResponse(string Message);

public record EchoReceived(string Message): INotification;
