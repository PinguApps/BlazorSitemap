namespace PinguApps.BlazorSitemap.Tests.Support;

public sealed class ManualClock : TimeProvider
{
    private long _ticks;
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;
    public override long GetTimestamp() => _ticks;
    public void Advance(TimeSpan amount) => _ticks += amount.Ticks;
}
