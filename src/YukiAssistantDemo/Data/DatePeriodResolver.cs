namespace YukiAssistantDemo.Data;

public interface ISystemClock { DateOnly Today { get; } }

public sealed class SystemClock : ISystemClock
{
    public DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);
}

public sealed record PeriodWindow(DateOnly Start, DateOnly End);

public sealed class DatePeriodResolver(ISystemClock clock)
{
    public PeriodWindow LastMonth()
    {
        var first = new DateOnly(clock.Today.Year, clock.Today.Month, 1);
        var end = first.AddDays(-1);
        return new(new DateOnly(end.Year, end.Month, 1), end);
    }

    public PeriodWindow CurrentQuarter()
    {
        var month = ((clock.Today.Month - 1) / 3) * 3 + 1;
        var start = new DateOnly(clock.Today.Year, month, 1);
        return new(start, start.AddMonths(3).AddDays(-1));
    }
}
