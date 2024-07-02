using System.Net.Mail;
using FlowDemo.Messages;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FlowDemo.Handlers;

public class SendNotificationHandler: IRequestHandler<SendNotificationCommand>
{
    protected readonly ILogger Log;
    private readonly Uri _smtp;

    public SendNotificationHandler(ILoggerFactory loggerFactory, IConfiguration configuration)
    {
        Log = loggerFactory.CreateLogger(GetType());
        _smtp = new Uri(configuration.GetConnectionString("Smtp") ?? "localhost:1025");
    }

    public async Task Handle(SendNotificationCommand request, CancellationToken token)
    {
        var recipient = request.Recipient;
        if (recipient is null) return;

        await SendEmail(recipient, request, token);
    }

    private async Task SendEmail(
        string recipient, SendNotificationCommand request, CancellationToken cancellationToken)
    {
        var client = new SmtpClient(_smtp.Host, _smtp.Port);
        var message = new MailMessage(
            "system@company.com",
            recipient, request.Subject, request.Body);
        await client.SendMailAsync(message, cancellationToken);
    }
}
