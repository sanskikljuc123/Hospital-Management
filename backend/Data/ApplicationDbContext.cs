using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Data
{
    public class ApplicationDbContext : DbContext
    {
        public DbSet<Doctor> Doctors { get; set; }
        public DbSet<Service> Services { get; set; }
        public DbSet<Patient> Patients { get; set; }
        public DbSet<DoctorService> DoctorServices { get; set; }
        public DbSet<Department> Departments { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Appointment> Appointments { get; set; }
        public DbSet<MedicalRecord> MedicalRecords { get; set; }
        public DbSet<MedicalRecordRevision> MedicalRecordRevisions { get; set; }
        public DbSet<OutboxMessage> OutboxMessages { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            // PostgreSQL "timestamp with time zone" only accepts UTC values. Normalise every
            // DateTime on the way in and mark it as UTC on the way out.
            configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<DoctorService>()
                .HasKey(ds => new { ds.DoctorId, ds.ServiceId });
            modelBuilder.Entity<DoctorService>()
                .HasOne(d => d.Doctor)
                .WithMany(ds => ds.DoctorServices)
                .HasForeignKey(d => d.DoctorId);
            modelBuilder.Entity<DoctorService>()
                .HasOne(s => s.Service)
                .WithMany(ds => ds.DoctorServices)
                .HasForeignKey(s => s.ServiceId);

            modelBuilder.Entity<Department>(e =>
            {
                e.Property(d => d.Name).HasMaxLength(200);
                e.Property(d => d.Description).HasMaxLength(1000);
                e.HasIndex(d => d.Name).IsUnique();
            });

            modelBuilder.Entity<Doctor>(e =>
            {
                e.Property(d => d.Birthday).HasColumnType("date");
                e.HasOne(d => d.Department).WithMany(d => d.Doctors)
                    .HasForeignKey(d => d.DepartmentId).OnDelete(DeleteBehavior.SetNull);
                e.HasOne(d => d.User).WithMany()
                    .HasForeignKey(d => d.UserId).OnDelete(DeleteBehavior.SetNull);
                e.HasIndex(d => d.UserId).IsUnique();
            });

            modelBuilder.Entity<Service>()
                .HasOne(s => s.Department).WithMany(d => d.Services)
                .HasForeignKey(s => s.DepartmentId).OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Patient>(e =>
            {
                e.Property(p => p.Email).HasMaxLength(320);
                // Deleting a service must never delete its patients (it used to cascade).
                e.HasOne(p => p.Service).WithMany(s => s.Patients)
                    .HasForeignKey(p => p.ServiceId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(p => p.User).WithMany()
                    .HasForeignKey(p => p.UserId).OnDelete(DeleteBehavior.SetNull);
                e.HasIndex(p => p.UserId).IsUnique();
            });

            modelBuilder.Entity<User>(e =>
            {
                e.Property(u => u.KeycloakId).HasMaxLength(64);
                e.Property(u => u.UserName).HasMaxLength(255);
                e.Property(u => u.Email).HasMaxLength(320);
                e.Property(u => u.Role).HasMaxLength(50);
                e.Property(u => u.LastSessionId).HasMaxLength(64);
                e.HasIndex(u => u.KeycloakId).IsUnique();
            });

            modelBuilder.Entity<Appointment>(e =>
            {
                e.Property(a => a.Status).HasConversion<string>().HasMaxLength(20);
                e.Property(a => a.Reason).HasMaxLength(500);
                e.Property(a => a.Notes).HasMaxLength(2000);
                e.Property(a => a.CreatedBy).HasMaxLength(64);
                // Clinical history must not disappear when a doctor or patient is removed.
                e.HasOne(a => a.Patient).WithMany()
                    .HasForeignKey(a => a.PatientId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(a => a.Doctor).WithMany()
                    .HasForeignKey(a => a.DoctorId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(a => a.Service).WithMany()
                    .HasForeignKey(a => a.ServiceId).OnDelete(DeleteBehavior.SetNull);
                e.HasIndex(a => new { a.DoctorId, a.ScheduledAt });
                e.HasIndex(a => a.PatientId);
                // EndsAt is derived; the database keeps it honest. The exclusion constraint that uses it
                // (no overlapping appointments of one doctor, R-048) is created in migration
                // AppointmentNoOverlap, because EF Core has no model API for exclusion constraints.
                e.ToTable(t => t.HasCheckConstraint("CK_Appointments_EndsAt",
                    "\"EndsAt\" = \"ScheduledAt\" + make_interval(mins => \"DurationMinutes\")"));
            });

            modelBuilder.Entity<MedicalRecord>(e =>
            {
                e.Property(m => m.Diagnosis).HasMaxLength(2000);
                e.Property(m => m.Treatment).HasMaxLength(4000);
                e.Property(m => m.Notes).HasMaxLength(4000);
                e.HasOne(m => m.Patient).WithMany()
                    .HasForeignKey(m => m.PatientId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(m => m.Doctor).WithMany()
                    .HasForeignKey(m => m.DoctorId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(m => m.Appointment).WithMany()
                    .HasForeignKey(m => m.AppointmentId).OnDelete(DeleteBehavior.SetNull);
                e.HasOne(m => m.LastEditedByDoctor).WithMany()
                    .HasForeignKey(m => m.LastEditedByDoctorId).OnDelete(DeleteBehavior.Restrict);
                e.HasIndex(m => m.PatientId);
            });

            modelBuilder.Entity<MedicalRecordRevision>(e =>
            {
                e.Property(r => r.Diagnosis).HasMaxLength(2000);
                e.Property(r => r.Treatment).HasMaxLength(4000);
                e.Property(r => r.Notes).HasMaxLength(4000);
                e.Property(r => r.ChangedFields).HasMaxLength(200);
                e.Property(r => r.EditedByUserName).HasMaxLength(255);
                e.HasOne(r => r.MedicalRecord).WithMany(m => m.Revisions)
                    .HasForeignKey(r => r.MedicalRecordId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(r => r.EditedByDoctor).WithMany()
                    .HasForeignKey(r => r.EditedByDoctorId).OnDelete(DeleteBehavior.Restrict);
                e.HasIndex(r => new { r.MedicalRecordId, r.Revision }).IsUnique();
            });

            modelBuilder.Entity<OutboxMessage>(e =>
            {
                e.Property(m => m.Type).HasMaxLength(100);
                e.Property(m => m.Payload).HasColumnType("jsonb");
                e.Property(m => m.LastError).HasMaxLength(500);
                // The processor's query: undelivered, not failed, due.
                e.HasIndex(m => new { m.ProcessedAt, m.FailedAt, m.NextAttemptAt });
            });

            modelBuilder.Entity<AuditLog>(e =>
            {
                e.Property(a => a.Action).HasMaxLength(100);
                e.Property(a => a.EntityType).HasMaxLength(100);
                e.Property(a => a.EntityId).HasMaxLength(64);
                e.Property(a => a.UserId).HasMaxLength(64);
                e.Property(a => a.UserName).HasMaxLength(255);
                e.Property(a => a.IpAddress).HasMaxLength(64);
                e.HasIndex(a => a.Timestamp);
                e.HasIndex(a => a.Action);
                e.HasIndex(a => a.UserId);
            });
        }

        private sealed class UtcDateTimeConverter : ValueConverter<DateTime, DateTime>
        {
            public UtcDateTimeConverter() : base(
                v => v.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(v, DateTimeKind.Utc) : v.ToUniversalTime(),
                v => DateTime.SpecifyKind(v, DateTimeKind.Utc))
            {
            }
        }
    }
}
