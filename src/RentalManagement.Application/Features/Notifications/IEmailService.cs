namespace RentalManagement.Application.Features.Notifications;

public record EmailMessage(string To, string Subject, string HtmlBody);

public interface IEmailService
{
    Task SendAsync(EmailMessage message, CancellationToken ct = default);

    Task SendOverdueNotificationAsync(
        string tenantEmail, string tenantName,
        string invoiceNumber, decimal amount, DateTime dueDate,
        CancellationToken ct = default);

    Task SendContractExpiringNotificationAsync(
        string tenantEmail, string tenantName,
        string contractNumber, DateTime endDate, int daysLeft,
        CancellationToken ct = default);

    Task SendWelcomeEmailAsync(
        string email, string fullName, string password,
        CancellationToken ct = default);
}
