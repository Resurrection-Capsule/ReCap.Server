namespace ReCap.Server.Util.Logging;

public sealed class LogThrottle
{
    readonly long _every;
    long _count;

    public LogThrottle(long every) => _every = every < 1 ? 1 : every;

    public long Count => Interlocked.Read(ref _count);

    public bool ShouldLog() => Interlocked.Increment(ref _count) % _every == 1;
}
