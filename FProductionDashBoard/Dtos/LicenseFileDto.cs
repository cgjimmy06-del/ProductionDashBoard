using System;
using System.Collections.Generic;

namespace FProductionDashBoard.Dtos
{
    public record LicenseFileDto
    {
        public string CustomerName { get; init; } = "";
        public DateTime? ExpiryDate { get; init; }
        public List<string> EnabledFeatures { get; init; } = new();
        public DateTime IssuedAt { get; init; }
        public string Signature { get; init; } = "";
    }
}
