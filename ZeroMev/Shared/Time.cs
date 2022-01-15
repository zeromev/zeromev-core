using System;

namespace ZeroMev.Shared
{
    public static class Time
    {
        static TimeZoneInfo tzUS;
        static TimeZoneInfo tzEU;
        static TimeZoneInfo tzAS;

        static Time()
        {
            tzUS = TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");
            tzEU = TimeZoneInfo.FindSystemTimeZoneById("Central European Standard Time");
            tzAS = TimeZoneInfo.FindSystemTimeZoneById("Asia/Singapore");
        }

        public static DateTime ToUTC(ExtractorPoP extractor, DateTime localTime)
        {
            switch (extractor)
            {
                case ExtractorPoP.US:
                    return TimeZoneInfo.ConvertTimeToUtc(localTime, tzUS);
                case ExtractorPoP.EU:
                    return TimeZoneInfo.ConvertTimeToUtc(localTime, tzEU);
                case ExtractorPoP.AS:
                    return TimeZoneInfo.ConvertTimeToUtc(localTime, tzAS);
                default:
                    return localTime;
            }
        }

        public static string DurationStr(DateTime from, DateTime to)
        {
            TimeSpan ts = to - from;
            return DurationStr(ts);
        }

        public static string DurationStr(long ticks)
        {
            TimeSpan ts = new TimeSpan(ticks);
            return DurationStr(ts);
        }

        public static string DurationStr(TimeSpan ts)
        {
            double ms = ts.TotalMilliseconds;

            if (ms < 1)
                return "0 ms";
            else if (ms < 1000)
                return ((int)ts.TotalMilliseconds) + " ms";
            else if (ms < 1000D * 60)
                return ts.Seconds + " secs";
            else if (ms < 1000D * 60 * 60)
                return ts.Minutes + " mins " + ts.Seconds + " secs";
            else if (ms < 1000D * 60 * 60 * 24)
                return ts.Hours + " hrs " + ts.Minutes + " mins";
            else
                return ts.Days + " days " + ts.Hours + " hrs";
        }
    }
}