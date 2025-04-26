using DoctorAPIs.Model;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Text.Json;

namespace DoctorAPIs.Data
{
    public class DoctorDbContext : DbContext
    {
        public DoctorDbContext( DbContextOptions<DoctorDbContext> options ) : base(options) { }

        public DbSet<Doctor> Doctors { get; set; }

        protected override void OnModelCreating( ModelBuilder modelBuilder )
        {
            modelBuilder.Entity<Doctor>()
                .Property(d => d.Availability)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null),
                    v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions)null))
                .HasColumnType("NVARCHAR(MAX)");

            modelBuilder.Entity<Doctor>()
                .Property(d => d.CreatedAt)
                .HasDefaultValueSql("GETUTCDATE()");
        }
    }
}
