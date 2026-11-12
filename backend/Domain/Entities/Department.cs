using System.Text.Json.Serialization;

namespace Domain.Entities
{
    /// <summary>Organisational unit (e.g. Cardiology) grouping doctors and the services they offer.</summary>
    public class Department
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        [JsonIgnore]
        public virtual ICollection<Doctor> Doctors { get; set; } = new List<Doctor>();
        [JsonIgnore]
        public virtual ICollection<Service> Services { get; set; } = new List<Service>();
    }
}
