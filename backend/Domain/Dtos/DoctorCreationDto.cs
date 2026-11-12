namespace Domain.Dtos
{
    public class DoctorCreationDto
    {
        public int? Id { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public DateTime? Birthday { get; set; }
        public string? Address { get; set; }
        public int? DepartmentId { get; set; }
        public int[] ServiceIds { get; set; } = Array.Empty<int>();
    }
}
