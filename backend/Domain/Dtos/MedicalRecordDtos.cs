using System.ComponentModel.DataAnnotations;

namespace Domain.Dtos
{
    public class MedicalRecordCreateDto
    {
        [Range(1, int.MaxValue)]
        public int PatientId { get; set; }
        // No DoctorId: the author is always the doctor linked to the caller's account.
        public int? AppointmentId { get; set; }
        [Required, MaxLength(2000)]
        public string Diagnosis { get; set; } = string.Empty;
        [MaxLength(4000)]
        public string? Treatment { get; set; }
        [MaxLength(4000)]
        public string? Notes { get; set; }
    }

    public class MedicalRecordUpdateDto
    {
        [Required, MaxLength(2000)]
        public string Diagnosis { get; set; } = string.Empty;
        [MaxLength(4000)]
        public string? Treatment { get; set; }
        [MaxLength(4000)]
        public string? Notes { get; set; }
    }

    public class MedicalRecordDto
    {
        public int Id { get; set; }
        public int PatientId { get; set; }
        public string? PatientName { get; set; }
        public int DoctorId { get; set; }
        public string? DoctorName { get; set; }
        public int? AppointmentId { get; set; }
        public string Diagnosis { get; set; } = string.Empty;
        public string? Treatment { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public int? LastEditedByDoctorId { get; set; }
        public string? LastEditedByDoctorName { get; set; }
        /// <summary>Number of the current version; 1 means never edited.</summary>
        public int Revision { get; set; }
    }

    /// <summary>One version of a medical record, as returned by GET /api/MedicalRecord/{id}/history.</summary>
    public class MedicalRecordRevisionDto
    {
        public int Revision { get; set; }
        public string Diagnosis { get; set; } = string.Empty;
        public string? Treatment { get; set; }
        public string? Notes { get; set; }
        public IReadOnlyList<string> ChangedFields { get; set; } = Array.Empty<string>();
        public int EditedByDoctorId { get; set; }
        public string? EditedByDoctorName { get; set; }
        public string? EditedByUserName { get; set; }
        public DateTime EditedAt { get; set; }
    }
}
