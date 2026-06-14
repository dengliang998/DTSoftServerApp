using NodaTime;

namespace DTSoft.Core.Common;

public static class TimeUtil
{
    public static DateTime CstDateTime
    {
        get
        {
            Instant now = SystemClock.Instance.GetCurrentInstant();
            var shanghaiZone = DateTimeZoneProviders.Tzdb["Asia/Shanghai"];
            return now.InZone(shanghaiZone).ToDateTimeUnspecified();
        }
    }
}
public static class DateTimeExtentions
{
    public static DateTime ToCstTime(this DateTime time) => TimeUtil.CstDateTime;
}
