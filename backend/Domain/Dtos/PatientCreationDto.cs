using System.ComponentModel.DataAnnotations;

namespace Domain.Dtos
{
    /// <summary>Input for registering or updating a patient.</summary>
    public class PatientCreationDto
    {
        [Required, MaxLength(200)]
        public string? UserName { get; set; }
        public int Phone { get; set; }
        [EmailAddress, MaxLength(254)]
        public string? Email { get; set; }
        [Range(1, int.MaxValue)]
        public int ServiceId { get; set; }
    }

    /// <summary>A patient as returned by the API. Account links and navigation data are never exposed.</summary>
    public class PatientDto
    {
        public int Id { get; set; }
        public string? UserName { get; set; }
        public int Phone { get; set; }
        public string? Email { get; set; }
        public int ServiceId { get; set; }
        public string? ServiceName { get; set; }
    }
}
