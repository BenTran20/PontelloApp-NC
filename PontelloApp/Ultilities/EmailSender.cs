using MailKit.Net.Smtp;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using MimeKit;
using PontelloApp.ViewModels;

namespace PontelloApp.Ultilities
{
    public class EmailSender : IEmailSender
    {
        private readonly IEmailConfiguration _emailConfiguration;
        private readonly ILogger<EmailSender> _logger;

        public EmailSender(IEmailConfiguration emailConfiguration, ILogger<EmailSender> logger)
        {
            _emailConfiguration = emailConfiguration;
            _logger = logger;
        }

        public async Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            var message = new MimeMessage();

            message.From.Add(new MailboxAddress(
                _emailConfiguration.SmtpFromName,
                _emailConfiguration.SmtpUsername));

            message.To.Add(new MailboxAddress(email, email));

            message.Subject = subject;

            var bodyBuilder = new BodyBuilder
            {
                HtmlBody = htmlMessage
            };

            message.Body = bodyBuilder.ToMessageBody();

            await SendEmailMessageAsync(message);
        }

        public async Task SendEmailWithAttachmentAsync(
            string email,
            string subject,
            string htmlMessage,
            string pdfPath)
        {
            var message = new MimeMessage();

            message.From.Add(new MailboxAddress(
                _emailConfiguration.SmtpFromName,
                _emailConfiguration.SmtpUsername));

            message.To.Add(new MailboxAddress(email, email));

            message.Subject = subject;

            var builder = new BodyBuilder
            {
                HtmlBody = htmlMessage
            };

            // attach PDF
            builder.Attachments.Add(pdfPath);

            message.Body = builder.ToMessageBody();

            await SendEmailMessageAsync(message);
        }

        private async Task SendEmailMessageAsync(MimeMessage message)
        {
            try
            {
                using var emailClient = new SmtpClient();

                emailClient.Connect(
                    _emailConfiguration.SmtpServer,
                    _emailConfiguration.SmtpPort,
                    false);

                emailClient.AuthenticationMechanisms.Remove("XOAUTH2");

                emailClient.Authenticate(
                    _emailConfiguration.SmtpUsername,
                    _emailConfiguration.SmtpPassword);

                await emailClient.SendAsync(message);

                emailClient.Disconnect(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.GetBaseException().Message);
            }
        }
    }
}
