using DeviceDrivers.Modbus.Models;
using Xunit;

namespace DeviceDrivers.Modbus.Tests;

public class ModbusValueConverterTests
{
    [Fact]
    public void WordCount_UInt16_Returns1()
        => Assert.Equal(1, ModbusValueConverter.WordCount(ModbusValueFormat.UInt16));

    [Fact]
    public void WordCount_Int16_Returns1()
        => Assert.Equal(1, ModbusValueConverter.WordCount(ModbusValueFormat.Int16));

    [Theory]
    [InlineData(ModbusValueFormat.UInt32_BE)]
    [InlineData(ModbusValueFormat.UInt32_LE)]
    [InlineData(ModbusValueFormat.Int32_BE)]
    [InlineData(ModbusValueFormat.Int32_LE)]
    [InlineData(ModbusValueFormat.Float32_BE)]
    [InlineData(ModbusValueFormat.Float32_LE)]
    public void WordCount_32BitFormats_Returns2(ModbusValueFormat format)
        => Assert.Equal(2, ModbusValueConverter.WordCount(format));

    [Fact]
    public void Format_UInt16_MaxValue()
        => Assert.Equal("65535", ModbusValueConverter.Format([0xFFFF], 0, ModbusValueFormat.UInt16));

    [Fact]
    public void Format_UInt16_Zero()
        => Assert.Equal("0", ModbusValueConverter.Format([0x0000], 0, ModbusValueFormat.UInt16));

    [Fact]
    public void Format_Int16_NegativeOne()
        => Assert.Equal("-1", ModbusValueConverter.Format([0xFFFF], 0, ModbusValueFormat.Int16));

    [Fact]
    public void Format_Int16_MinValue()
        => Assert.Equal("-32768", ModbusValueConverter.Format([0x8000], 0, ModbusValueFormat.Int16));

    [Fact]
    public void Format_UInt32_BE_CombinesHighLow()
    {
        // high=0x0001, low=0x0002 → 0x00010002 = 65538
        Assert.Equal("65538", ModbusValueConverter.Format([0x0001, 0x0002], 0, ModbusValueFormat.UInt32_BE));
    }

    [Fact]
    public void Format_UInt32_LE_CombinesLowHigh()
    {
        // low=[0]=0x0002, high=[1]=0x0001 → 0x00010002 = 65538
        Assert.Equal("65538", ModbusValueConverter.Format([0x0002, 0x0001], 0, ModbusValueFormat.UInt32_LE));
    }

    [Fact]
    public void Format_Int32_BE_NegativeOne()
        => Assert.Equal("-1", ModbusValueConverter.Format([0xFFFF, 0xFFFF], 0, ModbusValueFormat.Int32_BE));

    [Fact]
    public void Format_Int32_LE_NegativeOne()
        => Assert.Equal("-1", ModbusValueConverter.Format([0xFFFF, 0xFFFF], 0, ModbusValueFormat.Int32_LE));

    [Fact]
    public void Format_Float32_BE_Pi()
    {
        // IEEE 754 π ≈ 3.14159: 0x4049_0FDB
        var result = ModbusValueConverter.Format([0x4049, 0x0FDB], 0, ModbusValueFormat.Float32_BE);
        Assert.Contains("3.14", result);
    }

    [Fact]
    public void Format_Float32_LE_Pi()
    {
        // LE: low=[0]=0x0FDB, high=[1]=0x4049
        var result = ModbusValueConverter.Format([0x0FDB, 0x4049], 0, ModbusValueFormat.Float32_LE);
        Assert.Contains("3.14", result);
    }

    [Fact]
    public void Format_UsesOffset()
    {
        // offset=1: skip first word, read second
        var words = new ushort[] { 0x0000, 0x0005 };
        Assert.Equal("5", ModbusValueConverter.Format(words, 1, ModbusValueFormat.UInt16));
    }

    [Fact]
    public void Format_UInt32_BE_UsesOffset()
    {
        // offset=1: [1]=0x0001, [2]=0x0002 → 65538
        var words = new ushort[] { 0xFFFF, 0x0001, 0x0002 };
        Assert.Equal("65538", ModbusValueConverter.Format(words, 1, ModbusValueFormat.UInt32_BE));
    }
}
