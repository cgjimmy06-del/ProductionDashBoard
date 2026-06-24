using FProductionDashBoard.Dtos;
using FProductionDashBoard.Services;
using System;
using System.Collections.Generic;
using Xunit;

namespace FProductionDashBoard.Tests.Services
{
    public class FileLicenseServiceTests
    {
        private static LicenseFileDto CreateValidDto(
            string customer = "Test Co",
            DateTime? expiry = null,
            List<string>? features = null)
        {
            var dto = new LicenseFileDto
            {
                CustomerName = customer,
                ExpiryDate = expiry,
                EnabledFeatures = features ?? new List<string> { "Charts", "Scheduling", "ProgramLibrary", "MaterialManagement" },
                IssuedAt = new DateTime(2026, 1, 1),
                Signature = ""
            };
            return dto with { Signature = FileLicenseService.ComputeSignature(dto) };
        }

        private static FileLicenseService BuildService(LicenseFileDto? license = null, TrialInfoDto? trialInfo = null)
        {
            var savedTrial = trialInfo ?? new TrialInfoDto();
            return new FileLicenseService(
                () => license ?? new LicenseFileDto(),
                () => savedTrial,
                dto => { savedTrial = dto; });
        }

        // --- Valid license ---

        [Fact]
        public void ValidLicense_NoExpiry_ReturnsValid()
        {
            var svc = BuildService(license: CreateValidDto());
            Assert.Equal(LicenseStatus.Valid, svc.Status);
            Assert.Null(svc.ExpiryDate);
            Assert.Equal(0, svc.DaysRemaining);
            Assert.Equal("Test Co", svc.CustomerName);
        }

        [Fact]
        public void ValidLicense_FutureExpiry_ReturnsValid()
        {
            var expiry = DateTime.Today.AddDays(30);
            var svc = BuildService(license: CreateValidDto(expiry: expiry));
            Assert.Equal(LicenseStatus.Valid, svc.Status);
            Assert.Equal(expiry, svc.ExpiryDate);
        }

        [Fact]
        public void ValidLicense_ExpiringSoon_ReturnsValidExpiringSoon()
        {
            var expiry = DateTime.Today.AddDays(5);
            var svc = BuildService(license: CreateValidDto(expiry: expiry));
            Assert.Equal(LicenseStatus.ValidExpiringSoon, svc.Status);
            Assert.Equal(5, svc.DaysRemaining);
        }

        [Fact]
        public void ValidLicense_ExpiryToday_ReturnsValidExpiringSoon()
        {
            var svc = BuildService(license: CreateValidDto(expiry: DateTime.Today));
            Assert.Equal(LicenseStatus.ValidExpiringSoon, svc.Status);
            Assert.Equal(0, svc.DaysRemaining);
        }

        [Fact]
        public void ValidLicense_Expired_ReturnsExpiredAndLocksAllFeatures()
        {
            var expiry = DateTime.Today.AddDays(-1);
            var svc = BuildService(license: CreateValidDto(expiry: expiry));
            Assert.Equal(LicenseStatus.Expired, svc.Status);
            Assert.False(svc.IsFeatureEnabled(LicensedFeature.Charts));
            Assert.False(svc.IsFeatureEnabled(LicensedFeature.Scheduling));
        }

        // --- Feature filtering ---

        [Fact]
        public void ValidLicense_PartialFeatures_OnlyEnabledFeaturesWork()
        {
            var dto = CreateValidDto(features: new List<string> { "Charts", "Scheduling" });
            var svc = BuildService(license: dto);
            Assert.True(svc.IsFeatureEnabled(LicensedFeature.Charts));
            Assert.True(svc.IsFeatureEnabled(LicensedFeature.Scheduling));
            Assert.False(svc.IsFeatureEnabled(LicensedFeature.ProgramLibrary));
            Assert.False(svc.IsFeatureEnabled(LicensedFeature.MaterialManagement));
        }

        // --- Invalid signature ---

        [Fact]
        public void TamperedLicense_ReturnsInvalidSignatureAndLocksAll()
        {
            var dto = CreateValidDto();
            var tampered = dto with { CustomerName = "Hacked" };
            var svc = BuildService(license: tampered);
            Assert.Equal(LicenseStatus.InvalidSignature, svc.Status);
            Assert.False(svc.IsFeatureEnabled(LicensedFeature.Charts));
        }

        // --- Trial mode ---

        [Fact]
        public void NoLicenseFile_FirstRun_CreatesTrial()
        {
            TrialInfoDto? saved = null;
            var svc = new FileLicenseService(
                () => new LicenseFileDto(),
                () => new TrialInfoDto(),
                dto => { saved = dto; });

            Assert.Equal(LicenseStatus.Trial, svc.Status);
            Assert.NotNull(saved);
            Assert.Equal(DateTime.Today, saved!.TrialStartDate);
            Assert.Equal(30, svc.DaysRemaining);
        }

        [Fact]
        public void NoLicenseFile_TrialMidway_ReturnsTrial()
        {
            var startDate = DateTime.Today.AddDays(-10);
            var svc = BuildService(trialInfo: new TrialInfoDto { TrialStartDate = startDate });
            Assert.Equal(LicenseStatus.Trial, svc.Status);
            Assert.Equal(20, svc.DaysRemaining);
        }

        [Fact]
        public void NoLicenseFile_TrialExpiringSoon_ReturnsTrialExpiringSoon()
        {
            var startDate = DateTime.Today.AddDays(-25);
            var svc = BuildService(trialInfo: new TrialInfoDto { TrialStartDate = startDate });
            Assert.Equal(LicenseStatus.TrialExpiringSoon, svc.Status);
            Assert.Equal(5, svc.DaysRemaining);
        }

        [Fact]
        public void NoLicenseFile_TrialExpired_ReturnsTrialExpiredAndLocksAll()
        {
            var startDate = DateTime.Today.AddDays(-31);
            var svc = BuildService(trialInfo: new TrialInfoDto { TrialStartDate = startDate });
            Assert.Equal(LicenseStatus.TrialExpired, svc.Status);
            Assert.Equal(0, svc.DaysRemaining);
            Assert.False(svc.IsFeatureEnabled(LicensedFeature.Charts));
        }

        [Fact]
        public void TrialMode_AllFeaturesEnabled()
        {
            var svc = BuildService(trialInfo: new TrialInfoDto { TrialStartDate = DateTime.Today });
            Assert.Equal(LicenseStatus.Trial, svc.Status);
            Assert.True(svc.IsFeatureEnabled(LicensedFeature.Charts));
            Assert.True(svc.IsFeatureEnabled(LicensedFeature.Scheduling));
            Assert.True(svc.IsFeatureEnabled(LicensedFeature.ProgramLibrary));
            Assert.True(svc.IsFeatureEnabled(LicensedFeature.MaterialManagement));
        }

        // --- Reload ---

        [Fact]
        public void Reload_FiresLicenseUpdatedEvent()
        {
            var svc = BuildService(trialInfo: new TrialInfoDto { TrialStartDate = DateTime.Today });
            bool fired = false;
            svc.LicenseUpdated += (_, _) => fired = true;
            svc.Reload();
            Assert.True(fired);
        }
    }
}
