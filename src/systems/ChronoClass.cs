using Pariah_Cybersecurity;
using static XRUIOS.Barebones.XRUIOS;

namespace XRUIOS.Barebones
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


        private static DateData currentDate = new DateData(
    TimeFormat.TwentyFourHour,
    ShortTime.hhdmm,
    ShortDate.mmcddcyy,
    LongTime.EightThirthy,
    LongDate.mmddyyyy,
    TimeZoneInfo.Local.Id,
    new List<string>()
);

        // Simple setter function
        public static void SetCurrentDate(DateData newDate)
        {
            if (newDate == null) throw new ArgumentNullException(nameof(newDate));
            currentDate = newDate;
        }

        // Optional getter if you need it
        public static DateData GetCurrentDate()
        {
            return currentDate;
        }

        public static async Task SaveDateData()
        {

            var directoryPath = Path.Combine(DataPath, "Chrono");

            var loadedJSON = await DataHandler.JSONDataHandler.LoadJsonFile("Chrono", directoryPath);

            var updatedJson = await DataHandler.JSONDataHandler.UpdateJson<DateData>(loadedJSON, "Data", currentDate, encryptionKey);

            await DataHandler.JSONDataHandler.SaveJson(updatedJson);


        }

        public static async Task LoadDateData()
        {

            var directoryPath = Path.Combine(DataPath, "Chrono");

            var loadedJSON = await DataHandler.JSONDataHandler.LoadJsonFile("Chrono", directoryPath);

            var DateData = (DateData)await DataHandler.JSONDataHandler.GetVariable<DateData>(loadedJSON, "Data", encryptionKey);

            currentDate = DateData;


        }

        //Getters and Setters

        //Timezone

        //G
        public static string GetTimezone(string Timezone)
        {
            return currentDate.TimeZone;
        }

        //S
        public static void SetTimezone(string Timezone)
        {
            TimeZoneInfo easternZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");

            {
                try
                {
                    var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "tzutil.exe",
                        Arguments = $"/s \"{Timezone}\"", // Wrap ID in quotes in case it has spaces
                        UseShellExecute = false,
                        CreateNoWindow = true
                    });

                    if (process != null)
                    {
                        process.WaitForExit();
                        // Clear the cached data in the current application domain to reflect the change
                        TimeZoneInfo.ClearCachedData();
                    }

                    currentDate.TimeZone = Timezone;

                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error changing time zone: {ex.Message}");
                    // Handle exceptions as needed
                }
            }
        }


        //Date

        //G

        public static (string, string) GetDate()
        {

            // Get the time zone information
            TimeZoneInfo timeZone = TimeZoneInfo.FindSystemTimeZoneById(currentDate.TimeZone);


            // Get the current time in the specified time zone
            DateTime currentTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone);

            int day = currentTime.Day;
            int month = currentTime.Month;
            int year = currentTime.Year;

            string ld = default;
            string sd = default;


            switch (currentDate.LongDateFormat)
            {
                case LongDate.xxdaymmddyyyy:
                    ld = $"{currentTime.DayOfWeek}, {MonthConverter.ConvertToWordedMonth(month)} {NumberConvert.NumberToWords(day)}, {NumberConvert.NumberToWords(year)}";
                    break;
                case LongDate.mmddyyyy:
                    ld = $"{MonthConverter.ConvertToWordedMonth(month)} {NumberConvert.NumberToWords(day)}, {NumberConvert.NumberToWords(year)}";
                    break;
                case LongDate.mmdd:
                    ld = $"{MonthConverter.ConvertToWordedMonth(month)} {NumberConvert.NumberToWords(day)}";
                    break;
                case LongDate.ddmmyyyy:
                    ld = $"{NumberConvert.NumberToWords(day)} {MonthConverter.ConvertToWordedMonth(month)}, {NumberConvert.NumberToWords(year)}";
                    break;
                default:
                    ld = $"{MonthConverter.ConvertToWordedMonth(month)} {NumberConvert.NumberToWords(day)}, {NumberConvert.NumberToWords(year)}";
                    break;
            }

            switch (currentDate.ShortDateFormat)
            {
                case ShortDate.mmzddzyy:
                    sd = $"{month}. {day}. {year}";
                    break;
                case ShortDate.ddzmmzyy:
                    sd = $"{day}. {month}. {year}";
                    break;
                case ShortDate.mmxddxyy:
                    sd = $"{month}-{day}-{year}";
                    break;
                case ShortDate.ddxmmxyy:
                    sd = $"{day}-{month}-{year}";
                    break;
                case ShortDate.mmcddcyy:
                    sd = $"{month}/{day}/{year}";
                    break;
                case ShortDate.ddcmmcyy:
                    sd = $"{day}/{month}/{year}";
                    break;
                default:
                    sd = $"{month}. {day}. {year}";
                    break;
            }


            return (ld, sd);

        }

        //S 

        public static async Task SetDate(ShortDate shortDateFormat, LongDate longDateFormat)
        {

            currentDate.ShortDateFormat = shortDateFormat;
            currentDate.LongDateFormat = longDateFormat;

            await SaveDateData();

        }


        //Time

        //G

        public static (string, string) GetTime()
        {

            // Get the time zone information
            TimeZoneInfo timeZone = TimeZoneInfo.FindSystemTimeZoneById(currentDate.TimeZone);


            // Get the current time in the specified time zone
            DateTime currentTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone);


            int hour = currentTime.Hour;
            int minute = currentTime.Minute;
            int second = currentTime.Second;


            string lt = default;
            string st = default;

            if (currentDate.TimeFormat == TimeFormat.TwelveHour)
            {
                hour = hour % 12;
                if (hour == 0) hour = 12;
            }



            switch (currentDate.LongTimeFormat)
            {
                case LongTime.EightThirthy:
                    lt = string.Concat(NumberConvert.NumberToWords(hour), " ", (NumberConvert.NumberToWords(minute)));
                    break;
                case LongTime.ThirtyMinutesPastEight:
                    lt = string.Concat(NumberConvert.NumberToWords(hour), " Past ", (NumberConvert.NumberToWords(minute)));
                    break;
                case LongTime.EightThirtyandTwentySeconds:
                    lt = string.Concat(NumberConvert.NumberToWords(hour), " ", (NumberConvert.NumberToWords(minute)), " And ", string.Concat(NumberConvert.NumberToWords(second)), " Seconds");
                    break;
                case LongTime.EightMinutesandTwentySecondsPastEight:
                    lt = string.Concat(NumberConvert.NumberToWords(minute), " Minutes And ", (NumberConvert.NumberToWords(second)), " Seconds Past ", string.Concat(NumberConvert.NumberToWords(hour)));
                    break;
                default:
                    lt = string.Concat(NumberConvert.NumberToWords(hour), " ", (NumberConvert.NumberToWords(minute)), " And ", string.Concat(NumberConvert.NumberToWords(second)), " Seconds");
                    break;
            }

            switch (currentDate.ShortTimeFormat)
            {
                case ShortTime.hhdmm:
                    st = string.Concat(hour, ":", minute);
                    break;
                case ShortTime.hhpmm:
                    st = string.Concat(hour, ".", minute);
                    break;
                case ShortTime.hhdmmds:
                    st = string.Concat(hour, ":", minute, ":", second);
                    break;
                case ShortTime.hhpmmps:
                    st = string.Concat(hour, ".", minute, ".", second);
                    break;
                default:
                    st = string.Concat(hour, ".", minute);
                    break;
            }

            if (currentDate.TimeFormat == TimeFormat.TwelveHour)
            {
                string amorpm = "AM";

                if (hour >= 12)
                {
                    amorpm = "PM";
                }
                st = string.Concat(st, amorpm);
            }

            return (lt, st);

        }

        //S
        public static async Task SetTime(ShortTime shortTimeFormat, LongTime longTimeFormat)
        {

            currentDate.ShortTimeFormat = shortTimeFormat;
            currentDate.LongTimeFormat = longTimeFormat;

            await SaveDateData();

        }



        //World Time

        //C
        public static void AddWorldTime(string worldTime)
        {
            currentDate.WorldTimes.Add(worldTime);
        }

        //R
        public static List<string> GetWorldTimezoneCollection()
        {
            return currentDate.WorldTimes;
        }


        public static List<Dictionary<string, (string, string, string, string)>> GetWorldTimes()
        {
            var resultList = new List<Dictionary<string, (string, string, string, string)>>();

            foreach (var item in currentDate.WorldTimes)
            {
                // Create a new dictionary for each item
                var timeDict = new Dictionary<string, (string, string, string, string)>();

                timeDict.Add(item, GetTimeInTimezone(item));

                resultList.Add(timeDict);
            }

            return resultList;
        }


        public static (string, string, string, string) GetTimeInTimezone(string timeZoneData)
        {
            // Get the time zone information
            TimeZoneInfo timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneData);


            // Get the current time in the specified time zone
            DateTime currentTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone);


            int hour = currentTime.Hour;
            int minute = currentTime.Minute;
            int second = currentTime.Second;


            int day = currentTime.Day;
            int month = currentTime.Month;
            int year = currentTime.Year;


            string lt = default;
            string st = default;
            string ld = default;
            string sd = default;



            if (currentDate.TimeFormat == TimeFormat.TwelveHour && hour > 13)
            {
                hour = hour - 12;
            }


            switch (currentDate.LongTimeFormat)
            {
                case LongTime.EightThirthy:
                    lt = string.Concat(NumberConvert.NumberToWords(hour), " ", (NumberConvert.NumberToWords(minute)));
                    break;
                case LongTime.ThirtyMinutesPastEight:
                    lt = string.Concat(NumberConvert.NumberToWords(hour), " Past ", (NumberConvert.NumberToWords(minute)));
                    break;
                case LongTime.EightThirtyandTwentySeconds:
                    lt = string.Concat(NumberConvert.NumberToWords(hour), " ", (NumberConvert.NumberToWords(minute)), " And ", string.Concat(NumberConvert.NumberToWords(second)), " Seconds");
                    break;
                case LongTime.EightMinutesandTwentySecondsPastEight:
                    lt = string.Concat(NumberConvert.NumberToWords(minute), " Minutes And ", (NumberConvert.NumberToWords(second)), " Seconds Past ", string.Concat(NumberConvert.NumberToWords(hour)));
                    break;
                default:
                    lt = string.Concat(NumberConvert.NumberToWords(hour), " ", (NumberConvert.NumberToWords(minute)), " And ", string.Concat(NumberConvert.NumberToWords(second)), " Seconds");
                    break;
            }

            switch (currentDate.ShortTimeFormat)
            {
                case ShortTime.hhdmm:
                    st = string.Concat(hour, ":", minute);
                    break;
                case ShortTime.hhpmm:
                    st = string.Concat(hour, ".", minute);
                    break;
                case ShortTime.hhdmmds:
                    st = string.Concat(hour, ":", minute, ":", second);
                    break;
                case ShortTime.hhpmmps:
                    st = string.Concat(hour, ".", minute, ".", second);
                    break;
                default:
                    st = string.Concat(hour, ".", minute);
                    break;
            }

            if (currentDate.TimeFormat == TimeFormat.TwelveHour)
            {
                string amorpm = "AM";

                if (hour >= 12)
                {
                    amorpm = "PM";
                }
                st = string.Concat(st, amorpm);
            }



            switch (currentDate.LongDateFormat)
            {
                case LongDate.xxdaymmddyyyy:
                    ld = $"{currentTime.DayOfWeek}, {MonthConverter.ConvertToWordedMonth(month)} {NumberConvert.NumberToWords(day)}, {NumberConvert.NumberToWords(year)}";
                    break;
                case LongDate.mmddyyyy:
                    ld = $"{MonthConverter.ConvertToWordedMonth(month)} {NumberConvert.NumberToWords(day)}, {NumberConvert.NumberToWords(year)}";
                    break;
                case LongDate.mmdd:
                    ld = $"{MonthConverter.ConvertToWordedMonth(month)} {NumberConvert.NumberToWords(day)}";
                    break;
                case LongDate.ddmmyyyy:
                    ld = $"{NumberConvert.NumberToWords(day)} {MonthConverter.ConvertToWordedMonth(month)}, {NumberConvert.NumberToWords(year)}";
                    break;
                default:
                    ld = $"{MonthConverter.ConvertToWordedMonth(month)} {NumberConvert.NumberToWords(day)}, {NumberConvert.NumberToWords(year)}";
                    break;
            }

            switch (currentDate.ShortDateFormat)
            {
                case ShortDate.mmzddzyy:
                    ld = $"{month}. {day}. {year}";
                    break;
                case ShortDate.ddzmmzyy:
                    ld = $"{day}. {month}. {year}";
                    break;
                case ShortDate.mmxddxyy:
                    ld = $"{month}-{day}-{year}";
                    break;
                case ShortDate.ddxmmxyy:
                    ld = $"{day}-{month}-{year}";
                    break;
                case ShortDate.mmcddcyy:
                    ld = $"{month}/{day}/{year}";
                    break;
                case ShortDate.ddcmmcyy:
                    ld = $"{day}/{month}/{year}";
                    break;
                default:
                    ld = $"{month}. {day}. {year}";
                    break;
            }

            return (lt, st, ld, sd);

        }



        //D
        public static void DeleteWorldTime(string worldTime)
        {
            currentDate.WorldTimes.Remove(worldTime);
        }













        public class NumberConvert
        {

            //From https://github.com/ardimh7/unity-convert-number-to-words/blob/master/NumberConvert.cs

            public static string NumberToWords(long number)
            {
                if (number == 0)
                    return "zero";

                if (number < 0)
                    return "minus " + NumberToWords(Math.Abs(number));

                string words = "";

                if ((number / 1000000000) > 0)
                {
                    words += NumberToWords(number / 1000000000) + " billion ";
                    number %= 1000000000;
                }

                if ((number / 1000000) > 0)
                {
                    words += NumberToWords(number / 1000000) + " million ";
                    number %= 1000000;
                }

                if ((number / 1000) > 0)
                {
                    words += NumberToWords(number / 1000) + " thousand ";
                    number %= 1000;
                }

                if ((number / 100) > 0)
                {
                    words += NumberToWords(number / 100) + " hundred ";
                    number %= 100;
                }

                if (number > 0)
                {
                    if (words != "")
                        words += "and ";

                    var unitsMap = new[] { "zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten", "eleven", "twelve", "thirteen", "fourteen", "fifteen", "sixteen", "seventeen", "eighteen", "nineteen" };
                    var tensMap = new[] { "zero", "ten", "twenty", "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety" };

                    if (number < 20)
                        words += unitsMap[number];
                    else
                    {
                        words += tensMap[number / 10];
                        if ((number % 10) > 0)
                            words += "-" + unitsMap[number % 10];
                    }
                }

                return words;
            }
        }

        public class MonthConverter
        {
            // Function to convert a numerical month to a worded month
            public static string ConvertToWordedMonth(int monthNumber)
            {
                string[] monthNames = { "January", "February", "March", "April", "May", "June",
                                "July", "August", "September", "October", "November", "December" };

                if (monthNumber >= 1 && monthNumber <= 12)
                {
                    return monthNames[monthNumber - 1];
                }
                else
                {
                    Console.WriteLine("Invalid month number. Please provide a number between 1 and 12.");
                    return "Invalid Month";
                }
            }
        }











    }

}
