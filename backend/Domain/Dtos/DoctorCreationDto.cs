using System.ComponentModel.DataAnnotations;

namespace Domain.Dtos
{
    /// <summary>Input for creating or updating a doctor. <see cref="Id"/> is ignored: the route id wins.</summary>
    public class DoctorCreationDto
    {
        public int? Id { get; set; }
        [Required, MaxLength(100)]
        public string? FirstName { get; set; }
        [Required, MaxLength(100)]
        public string? LastName { get; set; }
        public DateTime? Birthday { get; set; }
        [MaxLength(300)]
        public string? Address { get; set; }
        public int? DepartmentId { get; set; }
        public int[] ServiceIds { get; set; } = Array.Empty<int>();
    }

    /// <summary>A doctor as returned by the API. Account links and navigation data are never exposed.</summary>
    public class DoctorDto
    {
        public int Id { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public DateTime? Birthday { get; set; }
        public string? Address { get; set; }
        public int? DepartmentId { get; set; }
        public int[] ServiceIds { get; set; } = Array.Empty<int>();
    }
}
