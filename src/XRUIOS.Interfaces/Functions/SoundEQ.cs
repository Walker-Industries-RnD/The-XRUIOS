
using static XRUIOS.Barebones.Interfaces.ExperimentalAudioClass;

namespace XRUIOS.Barebones.Interfaces
{
    public static class SoundEQClass
    {
        public record SoundEQ
        {
            public string EQName;
            public float Software;
            public float Effects;
            public float Voice;
            public float Music;
            public float Alerts;
            public float UI;
            public float Etc;
            public ExperimentalAudio OtherVol;

            public SoundEQ() { }

            public SoundEQ(string eqname, float software, float effects, float voice, float music, float alerts, float ui, float etc, ExperimentalAudio otherVol)
            {
                EQName = eqname;
                Software = software;
                Effects = effects;
                Voice = voice;
                Music = music;
                Alerts = alerts;
                UI = ui;
                Etc = etc;
                OtherVol = otherVol;

            }
        }

 

    }
}
        