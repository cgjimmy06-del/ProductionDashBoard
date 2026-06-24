using System;

namespace FProductionDashBoard.Services
{
    public enum LicensedFeature { Charts, Scheduling, ProgramLibrary, MaterialManagement }

    public enum LicenseStatus
    {
        Valid, ValidExpiringSoon,
        Expired, InvalidSignature,
        Trial, TrialExpiringSoon, TrialExpired,
        Development
    }

    public interface ILicenseService
    {
        LicenseStatus Status          { get; }
        bool IsFeatureEnabled(LicensedFeature feature);
        DateTime? ExpiryDate          { get; }
        int DaysRemaining             { get; }
        string CustomerName           { get; }
        event EventHandler? LicenseUpdated;
        void Reload();
    }
}
