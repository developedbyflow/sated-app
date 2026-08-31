namespace Sated.Api.Tests;

public sealed class ClockFinerThanPostgres : TimeProvider
{
    public override DateTimeOffset GetUtcNow()
    {
        var now = TimeProvider.System.GetUtcNow();

        return now.AddTicks(7 - now.Ticks % TimeSpan.TicksPerMicrosecond);
    }
}
