using FProductionDashBoard.Dtos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace FProductionDashBoard.Services
{
    public class FileLicenseService : ILicenseService
    {
        private readonly Func<LicenseFileDto> _loadLicense;
        private readonly Func<TrialInfoDto> _loadTrialInfo;
        private readonly Action<TrialInfoDto> _saveTrialInfo;

        private List<string> _enabledFeatures = new();

        public LicenseStatus Status { get; private set; }
        public DateTime? ExpiryDate { get; private set; }
        public int DaysRemaining { get; private set; }
        public string CustomerName { get; private set; } = "";

        public event EventHandler? LicenseUpdated;

        public FileLicenseService()
            : this(
                () => JsonDataService.Load<LicenseFileDto>(LicenseConstants.LicenseFileName),
                () => JsonDataService.Load<TrialInfoDto>(LicenseConstants.TrialInfoFileName),
                dto => JsonDataService.Save(dto, LicenseConstants.TrialInfoFileName))
        { }

        public FileLicenseService(
            Func<LicenseFileDto> loadLicense,
            Func<TrialInfoDto> loadTrialInfo,
            Action<TrialInfoDto> saveTrialInfo)
        {
            _loadLicense = loadLicense;
            _loadTrialInfo = loadTrialInfo;
            _saveTrialInfo = saveTrialInfo;
            Initialize();
        }

        public bool IsFeatureEnabled(LicensedFeature feature)
        {
            if (Status is LicenseStatus.Expired or LicenseStatus.InvalidSignature or LicenseStatus.TrialExpired)
                return false;

            if (Status is LicenseStatus.Trial or LicenseStatus.TrialExpiringSoon)
                return true;

            return _enabledFeatures.Contains(feature.ToString());
        }

        public void Reload()
        {
            Initialize();
            LicenseUpdated?.Invoke(this, EventArgs.Empty);
        }

        private void Initialize()
        {
            var dto = _loadLicense();

            if (string.IsNullOrEmpty(dto.Signature))
            {
                InitializeTrial();
                return;
            }

            if (!ValidateSignature(dto))
            {
                Status = LicenseStatus.InvalidSignature;
                ExpiryDate = null;
                DaysRemaining = 0;
                CustomerName = "";
                _enabledFeatures = new List<string>();
                return;
            }

            CustomerName = dto.CustomerName;
            _enabledFeatures = dto.EnabledFeatures ?? new List<string>();

            if (dto.ExpiryDate == null)
            {
                Status = LicenseStatus.Valid;
                ExpiryDate = null;
                DaysRemaining = 0;
                return;
            }

            var today = DateTime.Today;
            ExpiryDate = dto.ExpiryDate;
            var remaining = (dto.ExpiryDate.Value.Date - today).Days;
            DaysRemaining = Math.Max(0, remaining);

            Status = remaining < 0 ? LicenseStatus.Expired
                : remaining <= LicenseConstants.ExpiringSoonDays ? LicenseStatus.ValidExpiringSoon
                : LicenseStatus.Valid;
        }

        private void InitializeTrial()
        {
            var trialInfo = _loadTrialInfo();

            if (trialInfo.TrialStartDate == default)
            {
                trialInfo = new TrialInfoDto { TrialStartDate = DateTime.Today };
                _saveTrialInfo(trialInfo);
            }

            var remaining = LicenseConstants.TrialDays - (DateTime.Today - trialInfo.TrialStartDate.Date).Days;
            DaysRemaining = Math.Max(0, remaining);
            ExpiryDate = trialInfo.TrialStartDate.Date.AddDays(LicenseConstants.TrialDays);
            CustomerName = "";
            _enabledFeatures = new List<string>();

            Status = remaining < 0 ? LicenseStatus.TrialExpired
                : remaining <= LicenseConstants.ExpiringSoonDays ? LicenseStatus.TrialExpiringSoon
                : LicenseStatus.Trial;
        }

        private static bool ValidateSignature(LicenseFileDto dto)
        {
            try
            {
                var key = Convert.FromBase64String(LicenseConstants.HmacKey);
                var payload = BuildPayloadString(dto);
                var payloadBytes = Encoding.UTF8.GetBytes(payload);
                using var hmac = new HMACSHA256(key);
                var expected = Convert.ToBase64String(hmac.ComputeHash(payloadBytes));
                return expected == dto.Signature;
            }
            catch
            {
                return false;
            }
        }

        public static string BuildPayloadString(LicenseFileDto dto)
        {
            var features = string.Join(",", (dto.EnabledFeatures ?? new List<string>()).OrderBy(f => f));
            var expiry = dto.ExpiryDate?.ToString("yyyy-MM-dd") ?? "null";
            return $"{dto.CustomerName}|{expiry}|{dto.IssuedAt:yyyy-MM-dd}|{features}";
        }

        public static string ComputeSignature(LicenseFileDto dto)
        {
            var key = Convert.FromBase64String(LicenseConstants.HmacKey);
            var payload = BuildPayloadString(dto);
            var payloadBytes = Encoding.UTF8.GetBytes(payload);
            using var hmac = new HMACSHA256(key);
            return Convert.ToBase64String(hmac.ComputeHash(payloadBytes));
        }
    }
}
