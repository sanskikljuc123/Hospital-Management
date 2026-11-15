using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Services.AppointmentService;
using Services.AuditService;
using Services.DepartmentService;
using Services.DoctorService;
using Services.Integrations;
using Services.MedicalRecordService;
using Services.PatientService;
using Services.ServiceService;
using Services.UserService;

namespace Services
{
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registers the business services and the external integration abstractions.
        /// The host must also register <see cref="Common.ICurrentUserContext"/>.
        /// </summary>
        public static IServiceCollection AddHospitalServices(this IServiceCollection services)
        {
            services.AddMemoryCache();
            // The clock used by business rules ("has the appointment started?"); tests replace it.
            services.TryAddSingleton(TimeProvider.System);

            services.AddScoped<IServiceService, ServiceServiceImp>();
            services.AddScoped<IPatientService, PatientServiceImp>();
            services.AddScoped<IDoctorService, DoctorServiceImp>();
            services.AddScoped<IDepartmentService, DepartmentService.DepartmentService>();
            services.AddScoped<IUserService, UserService.UserService>();
            services.AddScoped<IAppointmentService, AppointmentService.AppointmentService>();
            services.AddScoped<IMedicalRecordService, MedicalRecordService.MedicalRecordService>();

            services.AddScoped<IAuditService, AuditService.AuditService>();

            // External integrations. Swap these registrations for real providers.
            services.AddScoped<IEmailService, LoggingEmailService>();
            services.AddScoped<ISmsService, LoggingSmsService>();
            services.AddScoped<INotificationService, NotificationService>();

            return services;
        }
    }
}
