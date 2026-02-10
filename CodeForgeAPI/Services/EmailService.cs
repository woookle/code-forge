using CodeForgeAPI.Models;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace CodeForgeAPI.Services;

public class EmailService : IEmailService
{
    private readonly EmailSettings _emailSettings;

    public EmailService(IOptions<EmailSettings> emailSettings)
    {
        _emailSettings = emailSettings.Value;
    }

    public async Task SendEmailAsync(string to, string subject, string body)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_emailSettings.SenderName, _emailSettings.SenderEmail));
        message.To.Add(new MailboxAddress("", to));
        message.Subject = subject;

        var bodyBuilder = new BodyBuilder
        {
            HtmlBody = body
        };
        message.Body = bodyBuilder.ToMessageBody();

        using (var client = new SmtpClient())
        {
            try
            {
                // Accept all SSL certificates (in case of self-signed) - strictly for dev/test if needed, 
                // but for Gmail usually not required if using standard ports. 
                // client.ServerCertificateValidationCallback = (s, c, h, e) => true;

                // Connect to the server
                // 587 (StartTls) or 465 (SslOnConnect)
                await client.ConnectAsync(_emailSettings.MailServer, _emailSettings.MailPort, _emailSettings.UseSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None);

                // Authenticate
                await client.AuthenticateAsync(_emailSettings.SenderEmail, _emailSettings.Password);

                // Send
                await client.SendAsync(message);
                
                await client.DisconnectAsync(true);
            }
            catch (Exception ex)
            {
                // Adding basic logging or rethrow
                Console.WriteLine($"[EmailService] Error sending email: {ex.Message}");
                throw;
            }
        }
    }
}
