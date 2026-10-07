using YuukoProtocol;


namespace XRUIOS.Barebones.Interfaces
{
    public static class SystemInfoDisplayClass
    {

        public static class SystemInfoDisplayWindows
        {


            public record SystemSpecs
            {
                public string OSInfo;
                public string CPUInfo;
                public string MemoryInfo;
                public string DiskInfo;
                public string GPUInfo;
                public string NetworkInfo;
                public string UptimeInfo;
                public string VRHeadsetStatus;

                public SystemSpecs() { }

                public SystemSpecs(string osInfo, string cpuInfo, string memoryInfo, string deskInfo, string gpuInfo, string networkInfo, string uptimeInfo, string vrHeadsetStatus)
                {
                    OSInfo = osInfo;
                    CPUInfo = cpuInfo;
                    MemoryInfo = memoryInfo;
                    DiskInfo = deskInfo;
                    GPUInfo = gpuInfo;
                    NetworkInfo = networkInfo;
                    UptimeInfo = uptimeInfo;
                    VRHeadsetStatus = vrHeadsetStatus;
                }


            }


     
        }


    }
}
