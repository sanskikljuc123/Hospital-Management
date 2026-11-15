using System.ComponentModel.DataAnnotations;

namespace Domain.Dtos
{
    /// <summary>
    /// A medical service, as input and output. On input <see cref="Id"/> is ignored (the route id wins), and
    /// only these fields can be set: links to doctors and patients are managed through their own endpoints.
    /// </summary>
    public class ServiceDto
    {
        public int Id { get; set; }
        [Required, MaxLength(200)]
        public string Name { get; set; } = string.Empty;
        [MaxLength(1000)]
        public string? Description { get; set; }
        public int? DepartmentId { get; set; }
    }
}
