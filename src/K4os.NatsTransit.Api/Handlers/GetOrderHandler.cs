using MediatR;

namespace K4os.NatsTransit.Api.Handlers;

public class GetOrderHandler: IRequestHandler<GetOrderQuery, OrderResponse>
{
    protected readonly ILogger Log;

    public GetOrderHandler(ILoggerFactory loggerFactory)
    {
        Log = loggerFactory.CreateLogger<GetOrderHandler>();
    }

    public async Task<OrderResponse> Handle(GetOrderQuery request, CancellationToken token)
    {
        var orderId = request.OrderId;
        Log.LogInformation("Getting order {OrderId} details", orderId);
        await Task.CompletedTask;
        return new OrderResponse { OrderId = orderId };
    }
}
