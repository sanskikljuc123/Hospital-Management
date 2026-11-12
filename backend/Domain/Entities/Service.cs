using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Domain.Entities
{
    public class Service
    {
        [Key]
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public int? DepartmentId { get; set; }
        [JsonIgnore]
        public virtual Department? Department { get; set; }
        public virtual ICollection<DoctorService>? DoctorServices { get; set; }
        public virtual ICollection<Patient>? Patients { get; set; }

    }
}
