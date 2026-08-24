using CsvHelper;
using System.Diagnostics;
using System.Globalization;
using static XRUIOS.Barebones.Interfaces.StopwatchClass;
using static XRUIOS.Barebones.XRUIOS;

namespace XRUIOS.Barebones
{
    public static class StopwatchClass
    {

     


        public static Dictionary<string, (long, List<StopwatchRecord>)> StopWatches = new Dictionary<string, (long, List<StopwatchRecord>)>();

        public static string CreateStopwatch()
        {
            string id = Guid.NewGuid().ToString("N");

            StopWatches.Add(id, (Stopwatch.GetTimestamp(), new List<StopwatchRecord>()));

            return id;
        }

        public static TimeSpan GetTimeElapsed(string id)
        {
            try
            {
                var val = StopWatches[id].Item1;
                return Stopwatch.GetElapsedTime(val);
            }
            catch
            {
                throw new InvalidOperationException("A Stopwatch with this ID does not exist.");
            }
        }

        public static StopwatchRecord CreateLap(string id)
        {
            var elapsedLaps = StopWatches[id].Item2.Count;

            var currentTime = GetTimeElapsed(id).Seconds;

            var newStopwatchRecord = new StopwatchRecord(elapsedLaps, currentTime);

            StopWatches[id].Item2.Add(newStopwatchRecord);

            return newStopwatchRecord;

        }

        public static List<StopwatchRecord> DestroyStopwatch(string id)
        {
            var records = StopWatches[id].Item2;
            StopWatches.Remove(id);
            return records;

        }

        //Create Later
        public static void SaveStopwatchValuesAsSheet(List<StopwatchRecord> Values, DateTime RecordedOn, string FileName)
        {
            var directoryPath = Path.Combine(DataPath, $"{FileName}____RecordedOn_{RecordedOn:yyyy-MM-dd_HH-mm-ss}.csv");

            using (var writer = new StreamWriter(directoryPath))
            using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
            {
                csv.WriteRecords(Values);
            }
        }



    }


}
