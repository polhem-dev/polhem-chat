namespace Polhem.Chat.UnitTests;

internal sealed class ManualTimeProvider : TimeProvider
{
    private long _timestamp;

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override long GetTimestamp() => _timestamp;

    public void Advance(TimeSpan time) => _timestamp += time.Ticks;
}
