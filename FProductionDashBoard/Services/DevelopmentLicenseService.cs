using System;

namespace FProductionDashBoard.Services
{
    public class DevelopmentLicenseService : ILicenseService
    {
        public LicenseStatus Status       => LicenseStatus.Development;
        public DateTime?     ExpiryDate   => null;
        public int           DaysRemaining => 0;
        public string        CustomerName  => "Development";
        public event EventHandler? LicenseUpdated;

        public bool IsFeatureEnabled(LicensedFeature feature)
            => feature != LicensedFeature.Charts;
    }
}
