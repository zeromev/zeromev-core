using System;

namespace ZeroMev.Shared
{
    public static class Time
    {
        static TimeZoneInfo tzUS;
        static TimeZoneInfo tzEU;
        static TimeZoneInfo tzAS;

        // these are needed due to the inconsistent behaviour of timezones on Blazor/Azure (see https://github.com/dotnet/runtime/issues/60175)
        const string US = @"Central Standard Time;-360;(UTC-06:00) Central Time (US & Canada);Central Standard Time;Central Summer Time;[01:01:0001;12:31:2006;60;[0;02:00:00;4;1;0;];[0;02:00:00;10;5;0;];][01:01:2007;12:31:9999;60;[0;02:00:00;3;2;0;];[0;02:00:00;11;1;0;];];";
        const string EU = @"Central European Standard Time;60;(UTC+01:00) Sarajevo, Skopje, Warsaw, Zagreb;Central European Standard Time;Central European Summer Time;[01:01:0001;12:31:9999;60;[0;02:00:00;3;5;0;];[0;03:00:00;10;5;0;];];";
        const string AS = @"Asia/Singapore;480;(UTC+08:00) Kuala Lumpur, Singapore;Malay Peninsula Standard Time;Malay Peninsula Summer Time;;";

        static Time()
        {
            tzUS = TimeZoneInfo.FromSerializedString(US);
            tzEU = TimeZoneInfo.FromSerializedString(EU);
            tzAS = TimeZoneInfo.FromSerializedString(AS);
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