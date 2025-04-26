using DoctorAPIs.Data;
using DoctorAPIs.Model;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace DoctorAPIs.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    [Consumes("application/json")]
    public class DoctorsController : ControllerBase
    {
        private readonly DoctorDbContext _context;
        private readonly ILogger<DoctorsController> _logger;
        private readonly IMemoryCache _cache;
        private static readonly HashSet<string> _validDays = new()
        {
            "Monday", "Tuesday", "Wednesday", "Thursday",
            "Friday", "Saturday", "Sunday"
        };
        private const int DefaultPageSize = 10;
        private const int MaxPageSize = 50;
        private const string DoctorsCacheKey = "Doctors_";

        public DoctorsController(
            DoctorDbContext context,
            ILogger<DoctorsController> logger,
            IMemoryCache cache )
        {
            _context = context;
            _logger = logger;
            _cache = cache;
        }

        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult<PaginatedResponse<DoctorDto>>> GetAllDoctors(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = DefaultPageSize )
        {
            pageSize = Math.Min(pageSize, MaxPageSize);

            var cacheKey = $"{DoctorsCacheKey}{pageNumber}_{pageSize}";
            if(!_cache.TryGetValue(cacheKey, out List<DoctorDto>? cachedDoctors))
            {
                var doctors = await _context.Doctors
                    .OrderBy(d => d.Name)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .Select(d => new DoctorDto
                    {
                        DoctorId = d.DoctorId,
                        Name = d.Name,
                        Specialization = d.Specialization,
                        Experience = d.Experience,
                        Availability = d.Availability,
                        CreatedAt = d.CreatedAt
                    })
                    .ToListAsync();

                var cacheEntryOptions = new MemoryCacheEntryOptions()
                    .SetSlidingExpiration(TimeSpan.FromMinutes(5))
                    .SetAbsoluteExpiration(TimeSpan.FromMinutes(10));

                _cache.Set(cacheKey, doctors, cacheEntryOptions);
                cachedDoctors = doctors;
            }

            var totalCount = await _context.Doctors.CountAsync();
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            return Ok(new PaginatedResponse<DoctorDto>
            {
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalPages = totalPages,
                TotalCount = totalCount,
                Items = cachedDoctors ?? new List<DoctorDto>()
            });
        }

        [HttpGet("{doctorId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ResponseCache(Duration = 60)]
        public async Task<ActionResult<DoctorDto>> GetDoctor( Guid doctorId )
        {
            var cacheKey = $"{DoctorsCacheKey}{doctorId}";
            if(!_cache.TryGetValue(cacheKey, out DoctorDto? doctorDto))
            {
                var doctor = await _context.Doctors.FindAsync(doctorId);
                if(doctor == null)
                {
                    _logger.LogWarning("Doctor with ID {DoctorId} not found", doctorId);
                    return NotFound();
                }

                doctorDto = new DoctorDto
                {
                    DoctorId = doctor.DoctorId,
                    Name = doctor.Name,
                    Specialization = doctor.Specialization,
                    Experience = doctor.Experience,
                    Availability = doctor.Availability,
                    CreatedAt = doctor.CreatedAt
                };
                var cacheEntryOptions = new MemoryCacheEntryOptions()
                   .SetSlidingExpiration(TimeSpan.FromMinutes(5))
                   .SetAbsoluteExpiration(TimeSpan.FromMinutes(10));

                _cache.Set(cacheKey, doctorDto, cacheEntryOptions);
            }

            return Ok(doctorDto);
        }

        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<DoctorDto>> CreateDoctor( [FromBody] DoctorCreateDto doctorCreateDto )
        {
            if(!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if(doctorCreateDto.Availability?.Any(day => !_validDays.Contains(day)) ?? false)
            {
                return BadRequest($"Invalid day(s) in availability. Valid days are: {string.Join(", ", _validDays)}");
            }

            var doctor = new Doctor
            {
                Name = doctorCreateDto.Name,
                Specialization = doctorCreateDto.Specialization,
                Experience = doctorCreateDto.Experience,
                Availability = doctorCreateDto.Availability ?? new List<string>()
            };

            try
            {
                _context.Doctors.Add(doctor);
                await _context.SaveChangesAsync();

                _cache.Remove($"{DoctorsCacheKey}1_{DefaultPageSize}");

                var doctorDto = new DoctorDto
                {
                    DoctorId = doctor.DoctorId,
                    Name = doctor.Name,
                    Specialization = doctor.Specialization,
                    Experience = doctor.Experience,
                    Availability = doctor.Availability,
                    CreatedAt = doctor.CreatedAt
                };

                return CreatedAtAction(nameof(GetDoctor), new { doctorId = doctor.DoctorId }, doctorDto);
            }
            catch(DbUpdateException ex)
            {
                _logger.LogError(ex, "Error creating doctor");
                return StatusCode(500, "An error occurred while creating the doctor.");
            }
        }

        [HttpPut("{doctorId}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateDoctor(
            Guid doctorId,
            [FromBody] DoctorUpdateDto doctorUpdateDto )
        {
            if(!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if(doctorUpdateDto.Availability?.Any(day => !_validDays.Contains(day)) ?? false)
            {
                return BadRequest("Invalid day(s) in availability");
            }

            var doctor = await _context.Doctors.FindAsync(doctorId);
            if(doctor == null) return NotFound();

            if(doctorUpdateDto.Name != null) doctor.Name = doctorUpdateDto.Name;
            if(doctorUpdateDto.Specialization != null) doctor.Specialization = doctorUpdateDto.Specialization;
            if(doctorUpdateDto.Experience.HasValue) doctor.Experience = doctorUpdateDto.Experience.Value;
            if(doctorUpdateDto.Availability != null) doctor.Availability = doctorUpdateDto.Availability;

            try
            {
                await _context.SaveChangesAsync();
                _cache.Remove($"{DoctorsCacheKey}{doctorId}");
                _cache.Remove($"{DoctorsCacheKey}1_{DefaultPageSize}");
                return NoContent();
            }
            catch(DbUpdateConcurrencyException)
            {
                if(!await DoctorExistsAsync(doctorId)) return NotFound();
                throw;
            }
        }

        [HttpDelete("{doctorId}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteDoctor( Guid doctorId )
        {
            var doctor = await _context.Doctors.FindAsync(doctorId);
            if(doctor == null) return NotFound();

            _context.Doctors.Remove(doctor);
            await _context.SaveChangesAsync();

            _cache.Remove($"{DoctorsCacheKey}{doctorId}");
            _cache.Remove($"{DoctorsCacheKey}1_{DefaultPageSize}");

            return NoContent();
        }

        [HttpPut("{doctorId}/availability")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateAvailability(
            Guid doctorId,
            [FromBody] List<string> availability )
        {
            if(availability.Any(day => !_validDays.Contains(day)))
            {
                return BadRequest("Invalid day(s) in availability");
            }

            var doctor = await _context.Doctors.FindAsync(doctorId);
            if(doctor == null) return NotFound();

            doctor.Availability = availability;

            try
            {
                await _context.SaveChangesAsync();
                _cache.Remove($"{DoctorsCacheKey}{doctorId}");
                _cache.Remove($"{DoctorsCacheKey}1_{DefaultPageSize}");
                return NoContent();
            }
            catch(DbUpdateConcurrencyException)
            {
                if(!await DoctorExistsAsync(doctorId)) return NotFound();
                throw;
            }
        }

        [HttpGet("{doctorId}/availability")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetAvailability( Guid doctorId )
        {
            var doctor = await _context.Doctors.FindAsync(doctorId);
            if(doctor == null)
                return NotFound();

            if(doctor.Availability == null || doctor.Availability.Any(day => !_validDays.Contains(day)))
                return BadRequest("Doctor has invalid availability data.");

            return Ok(doctor.Availability);
        }

        private async Task<bool> DoctorExistsAsync( Guid id ) =>
            await _context.Doctors.AnyAsync(e => e.DoctorId == id);
    }
}