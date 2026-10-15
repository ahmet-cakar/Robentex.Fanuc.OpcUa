using Xunit;

namespace Robentex.Fanuc.OpcUa.Tests;

public sealed class TargetWriteLockTests
{
    [Fact]
    public async Task Same_Target_Is_Serialized_And_Cancelled_Waiter_Is_Cleaned_Up()
    {
        await using var client = new FanucOpcUaClient();
        var first = await client.AcquireTargetWriteLockAsync("position\0100", CancellationToken.None);
        using var cancellation = new CancellationTokenSource();
        var waiting = client.AcquireTargetWriteLockAsync("position\0100", cancellation.Token);

        Assert.False(waiting.IsCompleted);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await waiting);

        first.Dispose();
        Assert.Equal(0, client.ActiveTargetWriteLockCount);
    }

    [Fact]
    public async Task Different_Targets_Do_Not_Block_Each_Other()
    {
        await using var client = new FanucOpcUaClient();
        using var registerLock = await client.AcquireTargetWriteLockAsync("register\0100", CancellationToken.None);
        using var positionLock = await client.AcquireTargetWriteLockAsync("position\0100", CancellationToken.None);

        Assert.Equal(2, client.ActiveTargetWriteLockCount);
    }
}
