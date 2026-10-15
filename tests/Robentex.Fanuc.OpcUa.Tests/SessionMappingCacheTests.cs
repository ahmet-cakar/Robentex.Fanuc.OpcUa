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

        var close = (Task)type.GetMethod("CloseSessionAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(client, [CancellationToken.None])!;
        await close;

        Assert.Empty(assignments.Cast<object>());
        Assert.Empty(automatic.Cast<DictionaryEntry>());
        Assert.False((bool)type.GetField("_dynamicAssignmentsInitialized", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(client)!);
    }
}
