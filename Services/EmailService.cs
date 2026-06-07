using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace FlowerShop.Web.Services
{
    public class EmailService(IOptions<MailSettings> options) : IEmailService
    {
        private readonly MailSettings _settings = options.Value;

        private async Task SendAsync(string toEmail, string toName, string subject, string htmlBody, CancellationToken ct)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_settings.FromName, _settings.FromEmail));
            message.To.Add(new MailboxAddress(toName, toEmail));
            message.Subject = subject;
            message.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

            using var client = new SmtpClient();
            var secureOption = _settings.UseSsl
                ? SecureSocketOptions.SslOnConnect
                : SecureSocketOptions.StartTlsWhenAvailable;

            await client.ConnectAsync(_settings.SmtpHost, _settings.SmtpPort, secureOption, ct);
            await client.AuthenticateAsync(_settings.SmtpUser, _settings.SmtpPassword, ct);
            await client.SendAsync(message, ct);
            await client.DisconnectAsync(true, ct);
        }

        public Task SendConfirmationEmailAsync(string toEmail, string toName, string confirmationLink, CancellationToken ct = default) =>
            SendAsync(toEmail, toName, "Подтверждение регистрации — Flower & Shop", $"""
                <div style="font-family:Arial,sans-serif;max-width:600px;margin:0 auto;padding:20px">
                    <h2 style="color:#4a7c59">Flower & Shop</h2>
                    <h3>Подтвердите вашу почту</h3>
                    <p>Здравствуйте, <strong>{toName}</strong>!</p>
                    <p>Для завершения регистрации нажмите на кнопку:</p>
                    <a href="{confirmationLink}" style="display:inline-block;padding:12px 24px;background:#4a7c59;color:#fff;text-decoration:none;border-radius:6px;margin:16px 0">Подтвердить почту</a>
                    <p style="color:#888;font-size:13px">Если вы не регистрировались — просто проигнорируйте это письмо.</p>
                </div>
                """, ct);

        public Task SendEmailChangeConfirmationAsync(string toNewEmail, string toName, string confirmLink, CancellationToken ct = default) =>
            SendAsync(toNewEmail, toName, "Подтверждение нового email — Flower & Shop", $"""
                <div style="font-family:Arial,sans-serif;max-width:600px;margin:0 auto;padding:20px">
                    <h2 style="color:#4a7c59">Flower & Shop</h2>
                    <h3>Подтверждение смены email</h3>
                    <p>Здравствуйте, <strong>{toName}</strong>!</p>
                    <p>Вы запросили смену email на <strong>{toNewEmail}</strong>.</p>
                    <p>Нажмите кнопку для подтверждения:</p>
                    <a href="{confirmLink}" style="display:inline-block;padding:12px 24px;background:#4a7c59;color:#fff;text-decoration:none;border-radius:6px;margin:16px 0">Подтвердить новый email</a>
                    <p style="color:#888;font-size:13px">Если вы не запрашивали смену — проигнорируйте это письмо.</p>
                </div>
                """, ct);

        public Task SendPasswordResetEmailAsync(string toEmail, string toName, string resetLink, CancellationToken ct = default) =>
            SendAsync(toEmail, toName, "Сброс пароля — Flower & Shop", $"""
                <div style="font-family:Arial,sans-serif;max-width:600px;margin:0 auto;padding:20px">
                    <h2 style="color:#4a7c59">Flower & Shop</h2>
                    <h3>Сброс пароля</h3>
                    <p>Здравствуйте, <strong>{toName}</strong>!</p>
                    <p>Вы запросили сброс пароля. Ссылка действует <strong>1 час</strong>.</p>
                    <a href="{resetLink}" style="display:inline-block;padding:12px 24px;background:#c0392b;color:#fff;text-decoration:none;border-radius:6px;margin:16px 0">Сбросить пароль</a>
                    <p style="color:#888;font-size:13px">Если вы не запрашивали сброс — проигнорируйте это письмо.</p>
                </div>
                """, ct);
    }
}
