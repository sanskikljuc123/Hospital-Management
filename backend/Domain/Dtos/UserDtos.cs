using System.ComponentModel.DataAnnotations;

namespace Domain.Dtos
{
    public class UserDto
    {
        public int Id { get; set; }
        public string KeycloakId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Role { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public int? DoctorId { get; set; }
        public int? PatientId { get; set; }
    }

    /// <summary>Links a login account to a doctor and/or patient record. Null clears the link.</summary>
    public class UserLinkDto
    {
        public int? DoctorId { get; set; }
        public int? PatientId { get; set; }
    }

    public class UserStatusDto
    {
        public bool IsActive { get; set; }
    }

    public class DepartmentDto
    {
        public int Id { get; set; }
        [Required, MaxLength(200)]
        public string Name { get; set; } = string.Empty;
        [MaxLength(1000)]
        public string? Description { get; set; }
    }

    public class AuditLogDto
    {
        public long Id { get; set; }
        public DateTime Timestamp { get; set; }
        public string Action { get; set; } = string.Empty;
        public string? EntityType { get; set; }
        public string? EntityId { get; set; }
        public string? UserId { get; set; }
        public string? UserName { get; set; }
        public string? IpAddress { get; set; }
        public string? Details { get; set; }
        public bool Success { get; set; }
    }
}
