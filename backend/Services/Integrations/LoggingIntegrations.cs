using System.Globalization;
using System.Text.Json;
using Data;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Services.Outbox;

namespace Services.Integrations
{
    /// <summary>Placeholder e-mail gateway: logs instead of sending. Replace with a real provider.</summary>
    public class LoggingEmailService : IEmailService
    {
        private readonly ILogger<LoggingEmailService> _logger;

        public LoggingEmailService(ILogger<LoggingEmailService> logger) => _logger = logger;

        public Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
        {
            // The body is deliberately not logged: it can contain personal data.
            _logger.LogInformation("EMAIL (not sent, logging provider) to {To}: {Subject}", to, subject);
            return Task.CompletedTask;
        }
    }

    /// <summary>Placeholder SMS gateway: logs instead of sending. Replace with a real provider.</summary>
    public class LoggingSmsService : ISmsService
    {
        private readonly ILogger<LoggingSmsService> _logger;

        public LoggingSmsService(ILogger<LoggingSmsService> logger) => _logger = logger;

        public Task SendAsync(string phoneNumber, string message, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("SMS (not sent, logging provider) to {PhoneNumber}, {Length} chars", phoneNumber, message.Length);
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Records appointment notifications in the outbox, in the same transaction as the appointment change. They are
    /// delivered later by <see cref="AppointmentNotificationHandler"/>, so a slow or failing provider never slows
    /// down or loses a booking (decision D-8).
    /// </summary>
    public class NotificationService : INotificationService
    {
        public const string MessageType = "AppointmentNotification";

        private readonly IOutbox _outbox;

        public NotificationService(IOutbox outbox) => _outbox = outbox;

        public Task NotifyAppointmentScheduledAsync(Appointment appointment, CancellationToken cancellationToken = default) =>
            Enqueue(appointment, "Scheduled", cancellationToken);

        public Task NotifyAppointmentUpdatedAsync(Appointment appointment, CancellationToken cancellationToken = default) =>
            Enqueue(appointment, "Updated", cancellationToken);

        // Ids and the state at the time of the change; no contact details in the outbox.
        private Task Enqueue(Appointment appointment, string kind, CancellationToken cancellationToken) =>
            _outbox.EnqueueAsync(MessageType, new AppointmentNotification(
                appointment.Id, appointment.PatientId, kind, appointment.ScheduledAt, appointment.Status.ToString()), cancellationToken);
    }

    public sealed record AppointmentNotification(int AppointmentId, int PatientId, string Kind, DateTime ScheduledAt, string Status);

    /// <summary>Delivers an appointment notification to the patient by e-mail and SMS.</summary>
    public class AppointmentNotificationHandler : IOutboxHandler
    {
        private readonly ApplicationDbContext _context;
        private readonly IEmailService _email;
        private readonly ISmsService _sms;

        public AppointmentNotificationHandler(ApplicationDbContext context, IEmailService email, ISmsService sms)
        {
            _context = context;
            _email = email;
            _sms = sms;
        }

        public string Type => NotificationService.MessageType;

        public async Task HandleAsync(string payload, CancellationToken cancellationToken = default)
        {
            var notification = JsonSerializer.Deserialize<AppointmentNotification>(payload)
                ?? throw new InvalidOperationException("Empty appointment notification");

            var patient = await _context.Patients.AsNoTracking()
                .Where(p => p.Id == notification.PatientId)
                .Select(p => new { p.Email, p.Phone })
                .FirstOrDefaultAsync(cancellationToken);
            if (patient == null) return; // the patient no longer exists: nothing to deliver

            var (subject, message) = notification.Kind == "Scheduled"
                ? ("Appointment scheduled", $"Your appointment is scheduled for {notification.ScheduledAt:u}.")
                : ("Appointment updated", $"Your appointment on {notification.ScheduledAt:u} is now {notification.Status}.");

            if (!string.IsNullOrWhiteSpace(patient.Email))
                await _email.SendAsync(patient.Email, subject, message, cancellationToken);
            if (patient.Phone > 0)
                await _sms.SendAsync(patient.Phone.ToString(CultureInfo.InvariantCulture), message, cancellationToken);
        }
    }
}
