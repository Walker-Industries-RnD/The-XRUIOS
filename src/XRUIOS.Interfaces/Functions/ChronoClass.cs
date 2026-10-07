using YuukoProtocol;

namespace XRUIOS.Barebones.Interfaces
{
    public class ChronoClass
    {

        public enum TimeFormat { TwelveHour, TwentyFourHour }

        public enum ShortTime { hhdmm, hhpmm, hhdmmds, hhpmmps } // d = :, p = .

        public enum ShortDate { mmzddzyy, ddzmmzyy, mmxddxyy, ddxmmxyy, mmcddcyy, ddcmmcyy } // z = ., x = -, c = /

        public enum LongTime
        {
            EightThirthy,
            ThirtyMinutesPastEight,
            EightThirtyandTwentySeconds,
            EightMinutesandTwentySecondsPastEight
        }

        public enum LongDate
        {
            xxdaymmddyyyy,
            mmddyyyy,
            mmdd,
            ddmmyyyy
        }

        public record DateData
        {
            // Format selections
            public TimeFormat TimeFormat;
            public ShortTime ShortTimeFormat;
            public ShortDate ShortDateFormat;
            public LongTime LongTimeFormat;
            public LongDate LongDateFormat;

            public string TimeZone;

            public List<string> WorldTimes;


            public DateData() { }

            public DateData(
                TimeFormat timeFormat, ShortTime shortTimeFormat, ShortDate shortDateFormat, LongTime longTimeFormat, LongDate longDateFormat,
                string timeZone, List<string> worldTimes)

            {
                TimeFormat = timeFormat;
                ShortTimeFormat = shortTimeFormat;
                ShortDateFormat = shortDateFormat;
                LongTimeFormat = longTimeFormat;
                LongDateFormat = longDateFormat;
                TimeZone = timeZone;
                WorldTimes = worldTimes;

            }
        }






















    }

}
