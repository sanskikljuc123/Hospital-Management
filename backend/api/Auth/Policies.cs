using Domain.Constants;
using Microsoft.AspNetCore.Authorization;

namespace api.Auth
{
    public static class Policies
    {
        /// <summary>Administration: master data, user management, audit trail.</summary>
        public const string AdminOnly = nameof(AdminOnly);
        /// <summary>Anyone working in the hospital: Admin, Doctor, Nurse.</summary>
        public const string Staff = nameof(Staff);
        /// <summary>People allowed to read clinical content: Doctor, Nurse.</summary>
        public const string ClinicalStaff = nameof(ClinicalStaff);
        /// <summary>People allowed to write clinical content: Doctor.</summary>
        public const string DoctorOnly = nameof(DoctorOnly);

        public static void Configure(AuthorizationOptions options)
        {
            // Every endpoint requires a valid token unless it explicitly opts out with [AllowAnonymous].
            options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();

            options.AddPolicy(AdminOnly, p => p.RequireRole(Roles.Admin));
            options.AddPolicy(Staff, p => p.RequireRole(Roles.Admin, Roles.Doctor, Roles.Nurse));
            options.AddPolicy(ClinicalStaff, p => p.RequireRole(Roles.Doctor, Roles.Nurse));
            options.AddPolicy(DoctorOnly, p => p.RequireRole(Roles.Doctor));
        }
    }
}
