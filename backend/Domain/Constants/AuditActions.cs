namespace Domain.Constants
{
    public static class AuditActions
    {
        public const string UserLogin = "User.Login";
        public const string UserProvisioned = "User.Provisioned";
        public const string UserActivated = "User.Activated";
        public const string UserDeactivated = "User.Deactivated";
        public const string UserLinked = "User.Linked";
        public const string AppointmentCreated = "Appointment.Created";
        public const string AppointmentUpdated = "Appointment.Updated";
        public const string AppointmentDeleted = "Appointment.Deleted";
        public const string MedicalRecordCreated = "MedicalRecord.Created";
        public const string MedicalRecordUpdated = "MedicalRecord.Updated";
    }
}
