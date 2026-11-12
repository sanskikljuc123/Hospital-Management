using System.ComponentModel.DataAnnotations;

namespace Domain.Dtos
{
    public class MedicalRecordCreateDto
    {
        [Range(1, int.MaxValue)]
        public int PatientId { get; set; }
        [Range(1, int.MaxValue)]
        public int DoctorId { get; set; }
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
    }
}
