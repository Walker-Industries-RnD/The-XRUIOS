
namespace XRUIOS.Barebones.Interfaces
{
    public static class VolumeClass
    {

        //AudioGroupClass deleted, this is pretty much that but better tbh
        //Remember we use te master volume in SoundEQ as a main volume switch!

        public record VolumeSetting
        {
            public string VolumeSettingName;
            public Dictionary<string, int> Volumes;

            public VolumeSetting() { }

            public VolumeSetting(string volumeSettingName, Dictionary<string, int> volumes)
            {
                VolumeSettingName = volumeSettingName;
                Volumes = volumes;
            }
        }



    }

}
