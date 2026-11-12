using Domain.Entities;

namespace Data
{
    public class Seed
    {
        private readonly ApplicationDbContext _context;

        public Seed(ApplicationDbContext context)
        {
            _context = context;
        }

        public void SeedDataContext()
        {
            if (!_context.Departments.Any())
            {
                _context.Departments.AddRange(
                    new Department { Name = "Cardiology", Description = "Heart and vascular care" },
                    new Department { Name = "General Medicine", Description = "Primary and internal medicine" });
                _context.SaveChanges();
            }

            var departments = _context.Departments.OrderBy(d => d.Id).ToList();

            if (!_context.Services.Any())
            {
                _context.Services.AddRange(
                    new Service { Name = "Service1", Description = "Description1", DepartmentId = departments[0].Id },
                    new Service { Name = "Service2", Description = "Description2", DepartmentId = departments[1].Id });
                _context.SaveChanges();
            }

            if (!_context.Doctors.Any())
            {
                _context.Doctors.AddRange(
                    new Doctor { FirstName = "John", LastName = "Doe", Birthday = new DateTime(1980, 1, 1), Address = "123 Main St", DepartmentId = departments[0].Id },
                    new Doctor { FirstName = "Jane", LastName = "Smith", Birthday = new DateTime(1975, 5, 5), Address = "456 Elm St", DepartmentId = departments[1].Id });
                _context.SaveChanges();
            }

            var services = _context.Services.OrderBy(s => s.Id).ToList();
            var doctors = _context.Doctors.OrderBy(d => d.Id).ToList();

            if (!_context.Patients.Any())
            {
                _context.Patients.AddRange(
                    new Patient { UserName = "patient1", Phone = 12345678, Email = "patient1@hospital.local", ServiceId = services[0].Id },
                    new Patient { UserName = "patient2", Phone = 98765432, Email = "patient2@hospital.local", ServiceId = services[1].Id });
                _context.SaveChanges();
            }

            if (!_context.DoctorServices.Any())
            {
                _context.DoctorServices.AddRange(
                    new DoctorService { DoctorId = doctors[0].Id, ServiceId = services[0].Id },
                    new DoctorService { DoctorId = doctors[1].Id, ServiceId = services[1].Id });
                _context.SaveChanges();
            }
        }
    }
}
