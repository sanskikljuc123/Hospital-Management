namespace Domain.Entities
{
    public enum AppointmentStatus
    {
        Scheduled,
        Confirmed,
        Completed,
        Cancelled,
        NoShow
    }

    public class Appointment
    {
        public int Id { get; set; }
        public int PatientId { get; set; }
        public Patient? Patient { get; set; }
        public int DoctorId { get; set; }
        public Doctor? Doctor { get; set; }
        public int? ServiceId { get; set; }
        public Service? Service { get; set; }
        public DateTime ScheduledAt { get; set; }
        public int DurationMinutes { get; set; } = 30;
        /// <summary>
        /// <see cref="ScheduledAt"/> plus <see cref="DurationMinutes"/>, stored so the database can refuse
        /// overlapping appointments of one doctor (exclusion constraint, R-048). A check constraint keeps it in step.
        /// </summary>
        public DateTime EndsAt { get; set; }
        public AppointmentStatus Status { get; set; } = AppointmentStatus.Scheduled;
        public string? Reason { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        /// <summary>Keycloak subject of the user who booked the appointment.</summary>
        public string? CreatedBy { get; set; }
    }
}
