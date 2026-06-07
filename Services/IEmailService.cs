namespace FlowerShop.Web.Services
{
    public interface IEmailService
    {
        Task SendConfirmationEmailAsync(string toEmail, string toName, string confirmationLink, CancellationToken ct = default);
        Task SendEmailChangeConfirmationAsync(string toNewEmail, string toName, string confirmLink, CancellationToken ct = default);
        Task SendPasswordResetEmailAsync(string toEmail, string toName, string resetLink, CancellationToken ct = default);
    }
}
