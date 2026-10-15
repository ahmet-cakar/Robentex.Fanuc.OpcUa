using Opc.Ua;
using Xunit;

namespace Robentex.Fanuc.OpcUa.Tests;

public sealed class HoldingRegisterAndConversionTests
{
    [Fact]
    public void Allocation_Skips_Existing_And_Reserved_Ranges()
    {
        var assignments = new[]
        {
            new FanucDynamicAssignment(10001, 2, "R[1]", 0m),
            new FanucDynamicAssignment(10005, 2, "PR[1]@1.12", 0m)
        };
        var reserved = new[] { new FanucHoldingRegisterRange(10003, 10004) };

        var address = FanucOpcUaClient.FindAvailableDynamicAddress(10001, 10010, 2, assignments, reserved, rangeVerified: true);

        Assert.Equal(10007, address);
    }

    [Fact]
    public void Allocation_Rejects_Unverified_Range()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            FanucOpcUaClient.FindAvailableDynamicAddress(10001, 10002, 2, [], [], rangeVerified: false));

        Assert.Contains("DynamicAssignmentRangeVerified", exception.Message);
    }

    [Theory]
    [InlineData(9999, 1, "9999")]
    [InlineData(9999, 2, "9999:10000")]
    public void IndexRange_Uses_Single_Index_For_One_Register(int firstIndex, int count, string expected) =>
        Assert.Equal(expected, FanucOpcUaClient.CreateHoldingRegisterIndexRange(firstIndex, count));

    [Fact]
    public void Int16_Words_Combine_Low_Word_First()
    {
        Assert.Equal(100, FanucOpcUaClient.CombineInt16ToInt32(100, 0));
        Assert.Equal(-1, FanucOpcUaClient.CombineInt16ToInt32(-1, -1));
    }

    [Theory]
    [InlineData(10.25)]
    [InlineData(-35.75)]
    public void Float32_Register_RoundTrip_Is_Exact(double value)
    {
        var words = FanucOpcUaClient.ConvertDoubleToRegisterWords(value, 100);

        Assert.Equal(value, FanucOpcUaClient.ConvertRegisterToDouble(words, 100));
    }

    [Fact]
    public void Float32_Register_Rejects_Lossy_Double()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => FanucOpcUaClient.ConvertDoubleToRegisterWords(0.1d, 100));
    }

    [Fact]
    public void StatusCode_Rejects_Bad_And_Uncertain()
    {
        foreach (var statusCode in new[] { StatusCodes.BadUnexpectedError, StatusCodes.Uncertain })
        {
            var exception = Assert.Throws<FanucOpcUaConnectionException>(() =>
                FanucOpcUaClient.EnsureGoodResult(new StatusCodeCollection { statusCode }, "Holding Register write failed."));

            Assert.Contains("Good OPC UA status", exception.Message);
            Assert.Contains(statusCode == StatusCodes.Uncertain ? "Uncertain" : "BadUnexpectedError", exception.Message);
        }
    }

    [Fact]
    public void StatusCode_Accepts_Only_Good()
    {
        var result = FanucOpcUaClient.EnsureGoodResult(new StatusCodeCollection { StatusCodes.Good }, "test");

        Assert.Equal(StatusCodes.Good, result);
    }
}
