using Domain.Entities;

namespace Services.Integrations
{
    /// <summary>Outbound e-mail gateway (SMTP, SendGrid, SES, ...).</summary>
    public interface IEmailService
    {
        Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default);
    }

    /// <summary>Outbound SMS gateway (Twilio, Vonage, ...).</summary>
    public interface ISmsService
    {
        Task SendAsync(string phoneNumber, string message, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Business-level notifications. Decides who is told what and over which channel;
    /// delivery is delegated to <see cref="IEmailService"/> and <see cref="ISmsService"/>.
    /// </summary>
    public interface INotificationService
    {
        Task NotifyAppointmentScheduledAsync(Appointment appointment, CancellationToken cancellationToken = default);
        Task NotifyAppointmentUpdatedAsync(Appointment appointment, CancellationToken cancellationToken = default);
    }
}
