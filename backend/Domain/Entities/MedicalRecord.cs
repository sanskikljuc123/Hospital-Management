namespace Domain.Entities
{
    public class MedicalRecord
    {
        public int Id { get; set; }
        public int PatientId { get; set; }
        public Patient? Patient { get; set; }
        public int DoctorId { get; set; }
        public Doctor? Doctor { get; set; }
        public int? AppointmentId { get; set; }
        public Appointment? Appointment { get; set; }
        public string Diagnosis { get; set; } = string.Empty;
        public string? Treatment { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        /// <summary>The doctor who made the latest change; null until the record is first edited.</summary>
        public int? LastEditedByDoctorId { get; set; }
        public Doctor? LastEditedByDoctor { get; set; }
        public ICollection<MedicalRecordRevision> Revisions { get; set; } = new List<MedicalRecordRevision>();
    }

    /// <summary>
    /// One version of a medical record's clinical content. Revision 1 is the record as created;
    /// every edit that changes something adds the next revision. Revisions are never changed or deleted.
    /// </summary>
    public class MedicalRecordRevision
    {
        public long Id { get; set; }
        public int MedicalRecordId { get; set; }
        public MedicalRecord? MedicalRecord { get; set; }
        public int Revision { get; set; }
        public string Diagnosis { get; set; } = string.Empty;
        public string? Treatment { get; set; }
        public string? Notes { get; set; }
        /// <summary>Comma-separated field names changed from the previous revision; empty for revision 1.</summary>
        public string ChangedFields { get; set; } = string.Empty;
        public int EditedByDoctorId { get; set; }
        public Doctor? EditedByDoctor { get; set; }
        /// <summary>Keycloak user name of the caller at the time of the change.</summary>
        public string? EditedByUserName { get; set; }
        public DateTime EditedAt { get; set; }
    }
}
