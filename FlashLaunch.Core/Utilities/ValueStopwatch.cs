using System;
using System.Diagnostics;

namespace FlashLaunch.Core.Utilities;

public readonly struct ValueStopwatch
{
    private static readonly double TimestampToTicks = TimeSpan.TicksPerSecond / (double)Stopwatch.Frequency;
    private readonly long _startTimestamp;

    private ValueStopwatch(long startTimestamp)
    {
        _startTimestamp = startTimestamp;
    }

    public static ValueStopwatch StartNew() => new(Stopwatch.GetTimestamp());

    public TimeSpan GetElapsedTime()
    {
        var endTimestamp = Stopwatch.GetTimestamp();
        var elapsedTicks = (long)((endTimestamp - _startTimestamp) * TimestampToTicks);
        return new TimeSpan(elapsedTicks);
    }
}
