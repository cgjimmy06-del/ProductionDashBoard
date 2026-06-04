using DeviceDrivers.Modbus.Models;
using System;
using System.Globalization;

namespace DeviceDrivers.Modbus;

public static class ModbusValueConverter
{
    public static int WordCount(ModbusValueFormat format) => format switch
    {
        ModbusValueFormat.UInt16 or ModbusValueFormat.Int16 => 1,
        _ => 2
    };

    public static string Format(ushort[] words, int offset, ModbusValueFormat format) => format switch
    {
        ModbusValueFormat.UInt16    => words[offset].ToString(),
        ModbusValueFormat.Int16     => ((short)words[offset]).ToString(),
        ModbusValueFormat.UInt32_BE => ToU32BE(words, offset).ToString(),
        ModbusValueFormat.UInt32_LE => ToU32LE(words, offset).ToString(),
        ModbusValueFormat.Int32_BE  => ((int)ToU32BE(words, offset)).ToString(),
        ModbusValueFormat.Int32_LE  => ((int)ToU32LE(words, offset)).ToString(),
        ModbusValueFormat.Float32_BE => BitConverter.Int32BitsToSingle((int)ToU32BE(words, offset)).ToString("G6", CultureInfo.InvariantCulture),
        ModbusValueFormat.Float32_LE => BitConverter.Int32BitsToSingle((int)ToU32LE(words, offset)).ToString("G6", CultureInfo.InvariantCulture),
        _ => throw new ArgumentOutOfRangeException(nameof(format))
    };

    // Big-Endian: [offset]=high word, [offset+1]=low word
    private static uint ToU32BE(ushort[] w, int i) => ((uint)w[i] << 16) | w[i + 1];

    // Little-Endian: [offset]=low word, [offset+1]=high word
    private static uint ToU32LE(ushort[] w, int i) => ((uint)w[i + 1] << 16) | w[i];
}
