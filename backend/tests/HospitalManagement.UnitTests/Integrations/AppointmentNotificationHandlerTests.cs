using System.Text.Json;
using Data;
using HospitalManagement.UnitTests.Support;
using Services.Integrations;

namespace HospitalManagement.UnitTests.Integrations;

/// <summary>
/// Delivery of an appointment notification from the outbox (R-042): the patient gets the message by e-mail and, when
/// a phone number is known, by SMS. That the number is formatted without the server's culture is kept by the code
/// analysis gate (CA1305, QUALITY_GATES G1), not by this test.
/// </summary>
public sealed class AppointmentNotificationHandlerTests : IDisposable
{
    private static readonly DateTime ScheduledAt = new(2027, 3, 1, 9, 30, 0, DateTimeKind.Utc);

    private readonly ApplicationDbContext _db = TestDb.Create();
    private readonly RecordingGateways _gateways = new();

    public void Dispose() => _db.Dispose();

    private AppointmentNotificationHandler Handler => new(_db, _gateways, _gateways);

    private async Task<int> PatientAsync(int phone, string? email)
    {
        var patient = _db.AddPatient(_db.AddService().Id);
        patient.Phone = phone;
        patient.Email = email;
        await _db.SaveChangesAsync();
        return patient.Id;
    }

    private static string Payload(int patientId) =>
        JsonSerializer.Serialize(new AppointmentNotification(7, patientId, "Scheduled", ScheduledAt, "Scheduled"));

    [Fact]
    [Trait("Risk", "R-042")]
    public async Task HandleAsync_PatientWithPhone_SendsTheSmsToTheDigitsOfTheNumber()
    {
        var patientId = await PatientAsync(38761234, "patient.one@hospital.test");

        await Handler.HandleAsync(Payload(patientId));

        var sms = Assert.Single(_gateways.Sms);
        Assert.Equal("38761234", sms.Number);
        Assert.Equal("Your appointment is scheduled for 2027-03-01 09:30:00Z.", sms.Message);
    }

    [Fact]
    [Trait("Risk", "R-042")]
    public async Task HandleAsync_PatientWithoutPhone_SendsTheEmailButNoSms()
    {
        var patientId = await PatientAsync(0, "patient.two@hospital.test");

        await Handler.HandleAsync(Payload(patientId));

        Assert.Empty(_gateways.Sms);
        Assert.Equal("patient.two@hospital.test", Assert.Single(_gateways.Email));
    }

    private sealed class RecordingGateways : IEmailService, ISmsService
    {
        public List<string> Email { get; } = [];
        public List<(string Number, string Message)> Sms { get; } = [];

        public Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
        {
            Email.Add(to);
            return Task.CompletedTask;
        }

        public Task SendAsync(string phoneNumber, string message, CancellationToken cancellationToken = default)
        {
            Sms.Add((phoneNumber, message));
            return Task.CompletedTask;
        }
    }
}
