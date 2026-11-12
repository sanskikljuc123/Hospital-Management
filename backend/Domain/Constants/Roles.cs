namespace Domain.Constants
{
    /// <summary>Realm roles defined in Keycloak (HospitalRealm).</summary>
    public static class Roles
    {
        public const string Admin = "Admin";
        public const string Doctor = "Doctor";
        public const string Nurse = "Nurse";
        public const string Patient = "Patient";

        /// <summary>Ordered from most to least privileged; used to pick a primary role.</summary>
        public static readonly string[] All = { Admin, Doctor, Nurse, Patient };
    }
}
