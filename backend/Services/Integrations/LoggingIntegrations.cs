using Data;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

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

    public class NotificationService : INotificationService
    {
        private readonly ApplicationDbContext _context;
        private readonly IEmailService _email;
        private readonly ISmsService _sms;
        private readonly ILogger<NotificationService> _logger;

        public NotificationService(ApplicationDbContext context, IEmailService email, ISmsService sms, ILogger<NotificationService> logger)
        {
            _context = context;
            _email = email;
            _sms = sms;
            _logger = logger;
        }

        public Task NotifyAppointmentScheduledAsync(Appointment appointment, CancellationToken cancellationToken = default) =>
            NotifyPatientAsync(appointment, "Appointment scheduled",
                $"Your appointment is scheduled for {appointment.ScheduledAt:u}.", cancellationToken);

        public Task NotifyAppointmentUpdatedAsync(Appointment appointment, CancellationToken cancellationToken = default) =>
            NotifyPatientAsync(appointment, "Appointment updated",
                $"Your appointment on {appointment.ScheduledAt:u} is now {appointment.Status}.", cancellationToken);

        private async Task NotifyPatientAsync(Appointment appointment, string subject, string message, CancellationToken cancellationToken)
        {
            try
            {
                var patient = await _context.Patients.AsNoTracking()
                    .Where(p => p.Id == appointment.PatientId)
                    .Select(p => new { p.Email, p.Phone })
                    .FirstOrDefaultAsync(cancellationToken);
                if (patient == null) return;

                if (!string.IsNullOrWhiteSpace(patient.Email))
                    await _email.SendAsync(patient.Email, subject, message, cancellationToken);
                if (patient.Phone > 0)
                    await _sms.SendAsync(patient.Phone.ToString(), message, cancellationToken);
            }
            catch (Exception ex)
            {
                // Notifications are best effort; the appointment itself is already saved.
                _logger.LogWarning(ex, "Failed to notify patient {PatientId} about appointment {AppointmentId}",
                    appointment.PatientId, appointment.Id);
            }
        }
    }
}
