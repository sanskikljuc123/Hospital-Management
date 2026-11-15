using Domain.Constants;
using Services.Common;
using Services.UserService;

namespace api.Auth
{
    /// <summary>Which staff roles may see every patient's data for a given resource.</summary>
    public enum StaffScope
    {
        /// <summary>Admin, Doctor, Nurse (patient registry, appointments).</summary>
        Staff,
        /// <summary>Doctor, Nurse (clinical content).</summary>
        ClinicalStaff
    }

    public enum AccessDecision
    {
        Allow,
        /// <summary>Staff asked for something that does not exist.</summary>
        NotFound,
        /// <summary>Not the caller's patient — or it does not exist and the caller is not staff.</summary>
        Forbidden
    }

    /// <summary>The patients a caller may see in a list.</summary>
    public readonly record struct PatientScope(bool AllPatients, int? OnlyPatientId)
    {
        public static PatientScope All => new(true, null);
        public static PatientScope Nobody => new(false, null);
        public static PatientScope Only(int patientId) => new(false, patientId);
        public bool IsEmpty => !AllPatients && OnlyPatientId == null;
    }

    /// <summary>
    /// The single place that decides whether the caller may reach data that belongs to a patient
    /// (ARCHITECTURE.md §3, decision D-9). Role attributes decide which endpoints a role may call; this policy
    /// decides which patient's data; services enforce business rules. Controllers never resolve ownership
    /// themselves (guarded by an architecture test).
    /// <para>
    /// Rules: staff in the given scope reach every patient. Anyone else reaches only the patient record linked to
    /// their account, and gets the same answer (403) for a missing id as for someone else's, so ids cannot be probed.
    /// </para>
    /// </summary>
    public interface IPatientAccessPolicy
    {
        /// <summary>The patients the caller may list.</summary>
        Task<PatientScope> ListScopeAsync(StaffScope scope, CancellationToken cancellationToken = default);

        /// <summary>
        /// Whether the caller may reach a resource of <paramref name="resourcePatientId"/>;
        /// pass <c>null</c> when the resource does not exist.
        /// </summary>
        Task<AccessDecision> CheckAsync(int? resourcePatientId, StaffScope scope, CancellationToken cancellationToken = default);
    }

    public sealed class PatientAccessPolicy : IPatientAccessPolicy
    {
        private readonly ICurrentUserContext _caller;
        private readonly IUserService _users;

        public PatientAccessPolicy(ICurrentUserContext caller, IUserService users)
        {
            _caller = caller;
            _users = users;
        }

        public async Task<PatientScope> ListScopeAsync(StaffScope scope, CancellationToken cancellationToken = default)
        {
            if (IsStaff(scope)) return PatientScope.All;
            var own = await _users.GetCurrentPatientIdAsync(cancellationToken);
            return own.HasValue ? PatientScope.Only(own.Value) : PatientScope.Nobody;
        }

        public async Task<AccessDecision> CheckAsync(int? resourcePatientId, StaffScope scope, CancellationToken cancellationToken = default)
        {
            if (IsStaff(scope)) return resourcePatientId.HasValue ? AccessDecision.Allow : AccessDecision.NotFound;
            if (!resourcePatientId.HasValue) return AccessDecision.Forbidden;

            var own = await _users.GetCurrentPatientIdAsync(cancellationToken);
            return own == resourcePatientId ? AccessDecision.Allow : AccessDecision.Forbidden;
        }

        private bool IsStaff(StaffScope scope) => scope switch
        {
            StaffScope.ClinicalStaff => _caller.IsInRole(Roles.Doctor) || _caller.IsInRole(Roles.Nurse),
            _ => _caller.IsInRole(Roles.Admin) || _caller.IsInRole(Roles.Doctor) || _caller.IsInRole(Roles.Nurse)
        };
    }
}
