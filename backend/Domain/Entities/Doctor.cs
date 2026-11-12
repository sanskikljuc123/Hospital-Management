using System.Text.Json.Serialization;

namespace Domain.Entities
{
    public class Doctor
    {
        public int Id { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public DateTime? Birthday { get; set; }
        public string? Address { get; set; }
        public int? DepartmentId { get; set; }
        [JsonIgnore]
        public virtual Department? Department { get; set; }
        /// <summary>Linked login account, if the doctor has one.</summary>
        public int? UserId { get; set; }
        [JsonIgnore]
        public virtual User? User { get; set; }
        public virtual ICollection<DoctorService> DoctorServices { get; set; }

    }
}
