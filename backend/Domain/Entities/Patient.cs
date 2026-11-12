using System.Text.Json.Serialization;

namespace Domain.Entities
{
    public class Patient
    {
        public int Id { get; set; }
        public string? UserName { get; set; }
        public int Phone { get; set; }
        public string? Email { get; set; }

        public int ServiceId { get; set; }

        public virtual Service? Service { get; set; }

        /// <summary>Linked login account, if the patient has one.</summary>
        public int? UserId { get; set; }
        [JsonIgnore]
        public virtual User? User { get; set; }
    }
}
