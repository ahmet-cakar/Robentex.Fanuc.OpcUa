using System.Collections;
using System.Reflection;
using Xunit;

namespace Robentex.Fanuc.OpcUa.Tests;

public sealed class SessionMappingCacheTests
{
    [Fact]
    public async Task CloseSession_Clears_SessionScoped_Mapping_Caches()
    {
        await using var client = new FanucOpcUaClient();
        var type = typeof(FanucOpcUaClient);
        var assignments = Assert.IsAssignableFrom<IList>(type.GetField("_dynamicAssignments", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(client));
        var automatic = Assert.IsAssignableFrom<IDictionary>(type.GetField("_automaticAssignments", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(client));
        assignments.Add(new FanucDynamicAssignment(10001, 2, "R[1]", 0m));
        automatic.Add("register\0100", new FanucDynamicAssignment(10001, 2, "R[1]", 0m));
        type.GetField("_dynamicAssignmentsInitialized", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(client, true);
        type.GetField("_dynamicAssignmentsNeedRefresh", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(client, true);

        var close = (Task)type.GetMethod("CloseSessionAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(client, [CancellationToken.None])!;
        await close;

        Assert.Empty(assignments.Cast<object>());
        Assert.Empty(automatic.Cast<DictionaryEntry>());
        Assert.False((bool)type.GetField("_dynamicAssignmentsInitialized", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(client)!);
        Assert.False((bool)type.GetField("_dynamicAssignmentsNeedRefresh", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(client)!);
    }

    [Theory]
    [InlineData("ProgramSpeed", true)]
    [InlineData("Alarm", true)]
    [InlineData("OperationState", false)]
    [InlineData("ServoState", false)]
    public void StatusValueRead_OnlyMarksKnownMappingInvalidators(string nodeName, bool expected)
    {
        var invalidates = FanucOpcUaClient.StatusValueReadInvalidatesDynamicAssignments([nodeName]);

        Assert.Equal(expected, invalidates);
    }

    [Fact]
    public void StatusValueRead_MarksWhenBatchContainsProgramSpeedOrAlarm()
    {
        Assert.True(FanucOpcUaClient.StatusValueReadInvalidatesDynamicAssignments(["OperationState", "ProgramSpeed", "Alarm"]));
    }

    [Fact]
    public async Task FailedMappingRefresh_KeepsRefreshRequiredFlagSet()
    {
        await using var client = new FanucOpcUaClient();
        var type = typeof(FanucOpcUaClient);
        var assignments = Assert.IsAssignableFrom<IList>(type.GetField("_dynamicAssignments", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(client));
        assignments.Add(new FanucDynamicAssignment(10001, 2, "$MCR.$GENOVERRIDE"));
        type.GetField("_dynamicAssignmentsNeedRefresh", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(client, true);

        var refresh = (Task)type.GetMethod("RefreshDynamicAssignmentsIfNeededLockedAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(client, [CancellationToken.None])!;

        await Assert.ThrowsAsync<InvalidOperationException>(() => refresh);
        Assert.True((bool)type.GetField("_dynamicAssignmentsNeedRefresh", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(client)!);
    }
}
