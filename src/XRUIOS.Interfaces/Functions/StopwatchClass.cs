
namespace XRUIOS.Barebones.Interfaces
{
    public static class StopwatchClass
    {

        public record StopwatchRecord
        {
            public int LapCount { get; set; }
            public int SecondsElapsed { get; set; }

            public StopwatchRecord() { }

            public StopwatchRecord(int lapCount, int secondsElapsed)
            {
                LapCount = lapCount;
                SecondsElapsed = secondsElapsed;
            }
        }




    }


}
