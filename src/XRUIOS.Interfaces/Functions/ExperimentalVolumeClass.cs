

namespace XRUIOS.Barebones.Interfaces
{
    public class ExperimentalAudioClass
    {




        public record ExperimentalAudio
        {
            public bool EnvironmentalReduction;
            public bool DecibelLimit;
            public int EnvironmentalReductionPercentage;
            public int DecibelLimitLevel;

            public ExperimentalAudio() { }

            public ExperimentalAudio(bool EnvironmentalReduction, bool DecibelLimit,
                int EnvironmentalReductionPercentage, int DecibelLimitLevel)
            {
                this.EnvironmentalReduction = EnvironmentalReduction;
                this.DecibelLimit = DecibelLimit;
                this.EnvironmentalReductionPercentage = EnvironmentalReductionPercentage;
                this.DecibelLimitLevel = DecibelLimitLevel;
            }
        }

    }


}
