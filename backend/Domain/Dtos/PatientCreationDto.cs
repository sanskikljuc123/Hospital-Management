using System.ComponentModel.DataAnnotations;

namespace Domain.Dtos
{
    public class PatientCreationDto
    {
        public string? UserName { get; set; }
        public int Phone { get; set; }
        [EmailAddress]
        public string? Email { get; set; }
        public int ServiceId { get; set; }
    }
}
