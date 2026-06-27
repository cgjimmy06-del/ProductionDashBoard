using FProductionDashBoard.Models;
using FProductionDashBoard.Models.Extra;
using System.Collections.Generic;

namespace FProductionDashBoard.Services
{
    public static class MesEquipmentMapper
    {
        private static readonly Dictionary<int, string> _typeIdToGroup = new()
        {
            [1] = "Robot",
            [2] = "PLC",
            [3] = "AGV",
            [4] = "Meter",
        };

        private static readonly Dictionary<string, int> _groupToTypeId = new()
        {
            ["Robot"] = 1,
            ["PLC"]   = 2,
            ["AGV"]   = 3,
            ["Meter"] = 4,
        };

        public static Equipment ToEquipment(MesDevice mes) => new()
        {
            Code     = mes.DeviceId,
            Name     = mes.Name,
            Ip       = mes.Ip,
            Port     = int.TryParse(mes.SerialNum, out var port) ? port : 0,
            TypeId   = _groupToTypeId.TryGetValue(mes.Group, out var tid) ? tid : null,
            Factory  = string.IsNullOrEmpty(mes.Factory)  ? null : mes.Factory,
            Building = string.IsNullOrEmpty(mes.Building) ? null : mes.Building,
            Floor    = string.IsNullOrEmpty(mes.Floor)    ? null : mes.Floor,
        };

        public static MesDevice ToMesDevice(Equipment eq) => new()
        {
            DeviceId  = eq.Code,
            Name      = eq.Name,
            Ip        = eq.Ip,
            SerialNum = eq.Port.ToString(),
            Group     = eq.TypeId.HasValue && _typeIdToGroup.TryGetValue(eq.TypeId.Value, out var grp) ? grp : string.Empty,
            Factory   = eq.Factory  ?? string.Empty,
            Building  = eq.Building ?? string.Empty,
            Floor     = eq.Floor    ?? string.Empty,
        };

        public static bool IsSynced(Equipment eq, MesDevice mes)
        {
            var expectedGroup     = eq.TypeId.HasValue && _typeIdToGroup.TryGetValue(eq.TypeId.Value, out var g) ? g : string.Empty;
            var expectedSerialNum = eq.Port.ToString();
            return eq.Name == mes.Name
                && eq.Ip   == mes.Ip
                && expectedSerialNum == mes.SerialNum
                && expectedGroup     == mes.Group
                && (eq.Factory  ?? string.Empty) == mes.Factory
                && (eq.Building ?? string.Empty) == mes.Building
                && (eq.Floor    ?? string.Empty) == mes.Floor;
        }
    }
}
