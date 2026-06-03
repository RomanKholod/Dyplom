using MediatR;
using MimeKit;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RentalManagement.Application.Features.Notifications;

namespace RentalManagement.Infrastructure.Services.Email;

public class EmailService : IEmailService
{
    private readonly IConfiguration _config;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration config, ILogger<EmailService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        try
        {
            var email = new MimeMessage();
            email.From.Add(MailboxAddress.Parse(_config["Email:From"] ?? "noreply@rental.ua"));
            email.To.Add(MailboxAddress.Parse(message.To));
            email.Subject = message.Subject;

            var builder = new BodyBuilder { HtmlBody = message.HtmlBody };
            email.Body = builder.ToMessageBody();

            using var smtp = new SmtpClient();
            await smtp.ConnectAsync(
                _config["Email:Host"] ?? "localhost",
                int.Parse(_config["Email:Port"] ?? "587"),
                MailKit.Security.SecureSocketOptions.StartTls, ct);

            var user = _config["Email:User"];
            var pass = _config["Email:Password"];
            if (!string.IsNullOrEmpty(user) && !string.IsNullOrEmpty(pass))
                await smtp.AuthenticateAsync(user, pass, ct);

            await smtp.SendAsync(email, ct);
            await smtp.DisconnectAsync(true, ct);

            _logger.LogInformation("Email sent to {To}: {Subject}", message.To, message.Subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {To}", message.To);
        }
    }

    public Task SendOverdueNotificationAsync(string tenantEmail, string tenantName,
        string invoiceNumber, decimal amount, DateTime dueDate, CancellationToken ct = default)
    {
        var html = $"""
            <div style="font-family:Arial,sans-serif;max-width:600px;margin:auto">
              <div style="background:#2563eb;color:#fff;padding:24px;border-radius:8px 8px 0 0">
                <h2 style="margin:0">Rental Management System</h2>
                <p style="margin:4px 0 0;opacity:.8">Нагадування про оплату</p>
              </div>
              <div style="background:#fff;border:1px solid #e5e7eb;padding:24px;border-radius:0 0 8px 8px">
                <p>Шановний(а) <strong>{tenantName}</strong>,</p>
                <p>Нагадуємо, що рахунок <strong>{invoiceNumber}</strong> на суму
                   <strong style="color:#dc2626">{amount:#,##0.00} грн</strong>
                   мав бути оплачений до <strong>{dueDate:dd.MM.yyyy}</strong>.</p>
                <div style="background:#fef2f2;border-left:4px solid #dc2626;padding:12px 16px;margin:16px 0;border-radius:4px">
                  <strong>Будь ласка, здійсніть оплату якнайшвидше, щоб уникнути нарахування штрафних санкцій.</strong>
                </div>
                <p>У разі виникнення питань зверніться до вашого менеджера.</p>
                <p style="color:#6b7280;font-size:13px;margin-top:24px">
                  З повагою,<br>Rental Management System
                </p>
              </div>
            </div>
            """;

        return SendAsync(new EmailMessage(
            tenantEmail,
            $"Нагадування: рахунок {invoiceNumber} прострочено",
            html), ct);
    }

    public Task SendContractExpiringNotificationAsync(string tenantEmail, string tenantName,
        string contractNumber, DateTime endDate, int daysLeft, CancellationToken ct = default)
    {
        var urgency = daysLeft <= 7 ? "#dc2626" : "#d97706";
        var html = $"""
            <div style="font-family:Arial,sans-serif;max-width:600px;margin:auto">
              <div style="background:#2563eb;color:#fff;padding:24px;border-radius:8px 8px 0 0">
                <h2 style="margin:0">Rental Management System</h2>
                <p style="margin:4px 0 0;opacity:.8">Договір закінчується</p>
              </div>
              <div style="background:#fff;border:1px solid #e5e7eb;padding:24px;border-radius:0 0 8px 8px">
                <p>Шановний(а) <strong>{tenantName}</strong>,</p>
                <p>Ваш договір оренди <strong>{contractNumber}</strong> закінчується
                   <strong>{endDate:dd.MM.yyyy}</strong>.</p>
                <div style="background:#fffbeb;border-left:4px solid {urgency};padding:12px 16px;margin:16px 0;border-radius:4px">
                  <strong style="color:{urgency}">Залишилось {daysLeft} {(daysLeft == 1 ? "день" : daysLeft < 5 ? "дні" : "днів")}.</strong><br>
                  Зверніться до менеджера для продовження договору.
                </div>
                <p style="color:#6b7280;font-size:13px;margin-top:24px">
                  З повагою,<br>Rental Management System
                </p>
              </div>
            </div>
            """;

        return SendAsync(new EmailMessage(
            tenantEmail,
            $"Договір {contractNumber} закінчується через {daysLeft} дн.",
            html), ct);
    }

    public Task SendWelcomeEmailAsync(string email, string fullName, string password, CancellationToken ct = default)
    {
        var html = $"""
            <div style="font-family:Arial,sans-serif;max-width:600px;margin:auto">
              <div style="background:#2563eb;color:#fff;padding:24px;border-radius:8px 8px 0 0">
                <h2 style="margin:0">Rental Management System</h2>
                <p style="margin:4px 0 0;opacity:.8">Ласкаво просимо!</p>
              </div>
              <div style="background:#fff;border:1px solid #e5e7eb;padding:24px;border-radius:0 0 8px 8px">
                <p>Вітаємо, <strong>{fullName}</strong>!</p>
                <p>Ваш акаунт у системі Rental Management було створено.</p>
                <div style="background:#f0f9ff;border:1px solid #bae6fd;padding:16px;border-radius:8px;margin:16px 0">
                  <p style="margin:0"><strong>Email:</strong> {email}</p>
                  <p style="margin:8px 0 0"><strong>Пароль:</strong> {password}</p>
                </div>
                <p>Рекомендуємо змінити пароль після першого входу.</p>
              </div>
            </div>
            """;

        return SendAsync(new EmailMessage(email, "Ласкаво просимо до Rental Management System", html), ct);
    }
}
