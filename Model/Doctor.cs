using System.ComponentModel.DataAnnotations;

namespace DoctorAPIs.Model
{
    public class Doctor
    {
        [Key]
        public Guid DoctorId { get; set; } = Guid.NewGuid();

        [Required]
        [StringLength(100)]
        public string Name { get; set; }

        [Required]
        [StringLength(100)]
        public string Specialization { get; set; }

        [Range(0, 50)]
        public int Experience { get; set; }

        public List<string> Availability { get; set; }

        public DateTime CreatedAt { get; set; }
    }

    public class DoctorDto
    {
        public Guid DoctorId { get; set; }
        public string Name { get; set; } = null!;
        public string Specialization { get; set; } = null!;
        public int Experience { get; set; }
        public List<string> Availability { get; set; } = new();
        public DateTime CreatedAt { get; set; }
    }

    public class DoctorCreateDto
    {
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = null!;

        [Required]
        [StringLength(100)]
        public string Specialization { get; set; } = null!;

        [Range(0, 50)]
        public int Experience { get; set; }

        public List<string>? Availability { get; set; }
    }


    public class DoctorUpdateDto
    {
        [StringLength(100)]
        public string? Name { get; set; }

        [StringLength(100)]
        public string? Specialization { get; set; }

        [Range(0, 50)]
        public int? Experience { get; set; }

        public List<string>? Availability { get; set; }
    }

    public class PaginatedResponse<T>
    {
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
        public int TotalCount { get; set; }
        public List<T> Items { get; set; } = new();
    }
}
