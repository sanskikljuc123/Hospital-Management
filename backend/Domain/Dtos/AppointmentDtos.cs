using System.ComponentModel.DataAnnotations;
using Domain.Entities;

namespace Domain.Dtos
{
    public class AppointmentCreateDto
    {
        [Range(1, int.MaxValue)]
        public int PatientId { get; set; }
        [Range(1, int.MaxValue)]
        public int DoctorId { get; set; }
        public int? ServiceId { get; set; }
        public DateTime ScheduledAt { get; set; }
        [Range(5, 480)]
        public int DurationMinutes { get; set; } = 30;
        [MaxLength(500)]
        public string? Reason { get; set; }
    }

    public class AppointmentUpdateDto
    {
        [Range(1, int.MaxValue)]
        public int DoctorId { get; set; }
        public int? ServiceId { get; set; }
        public DateTime ScheduledAt { get; set; }
        [Range(5, 480)]
        public int DurationMinutes { get; set; } = 30;
        public AppointmentStatus Status { get; set; }
        [MaxLength(500)]
        public string? Reason { get; set; }
        [MaxLength(2000)]
        public string? Notes { get; set; }
    }

    /// <summary>Admin correction of a final status set by mistake (POST /api/Appointment/{id}/correct-status).</summary>
    public class AppointmentStatusCorrectionDto
    {
        public AppointmentStatus Status { get; set; }
        /// <summary>Why the status was wrong; stored in the audit log.</summary>
        [Required, MinLength(10), MaxLength(500)]
        public string Reason { get; set; } = string.Empty;
    }

    public class AppointmentDto
    {
        public int Id { get; set; }
        public int PatientId { get; set; }
        public string? PatientName { get; set; }
        public int DoctorId { get; set; }
        public string? DoctorName { get; set; }
        public int? ServiceId { get; set; }
        public string? ServiceName { get; set; }
        public DateTime ScheduledAt { get; set; }
        public int DurationMinutes { get; set; }
        public AppointmentStatus Status { get; set; }
        public string? Reason { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
