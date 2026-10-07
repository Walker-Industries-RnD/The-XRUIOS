using System.Management;
using System.Text.Json.Nodes;
using static XRUIOS.Barebones.Interfaces.SystemInfoDisplayClass;
using static XRUIOS.Barebones.Interfaces.SystemInfoDisplayClass.SystemInfoDisplayWindows;

namespace XRUIOS.Barebones.Functions
{
    public static class SystemInfoDisplayClass
    {

        public static class SystemInfoDisplayWindows
        {


            public static SystemSpecs GenerateSpecs()
            {
                SystemSpecs specs = new SystemSpecs
                {
                    OSInfo = GetOSInfo(),
                    CPUInfo = GetCPUInfo(),
                    MemoryInfo = GetMemoryInfo(),
                    DiskInfo = GetDiskInfo(),
                    GPUInfo = GetGPUInfo(),
                    NetworkInfo = GetNetworkInfo(),
                    UptimeInfo = GetUptimeInfo(),
                    VRHeadsetStatus = CheckHardware()
                };

                return specs;
            }


            static string GetOSInfo()
            {
                return Environment.OSVersion.ToString();
            }

            static string GetCPUInfo()
            {
                string cpuInfo = "";
                ManagementObjectSearcher searcher = new ManagementObjectSearcher("select * from Win32_Processor");
                foreach (ManagementObject obj in searcher.Get())
                {
                    cpuInfo += $"{obj["Name"]}, Cores: {obj["NumberOfCores"]}\n";
                }
                return cpuInfo;
            }

            static string GetMemoryInfo()
            {
                ManagementObjectSearcher searcher = new ManagementObjectSearcher("select * from Win32_OperatingSystem");
                foreach (ManagementObject obj in searcher.Get())
                {
                    return $"Total: {Math.Round(Convert.ToDouble(obj["TotalVisibleMemorySize"]) / 1024 / 1024, 2)} GB, Free: {Math.Round(Convert.ToDouble(obj["FreePhysicalMemory"]) / 1024 / 1024, 2)} GB";
                }
                return "N/A";
            }

            static string GetDiskInfo()
            {
                string diskInfo = "";
                ManagementObjectSearcher searcher = new ManagementObjectSearcher("select * from Win32_LogicalDisk");
                foreach (ManagementObject obj in searcher.Get())
                {
                    diskInfo += $"{obj["DeviceID"]}: Free: {Math.Round(Convert.ToDouble(obj["FreeSpace"]) / 1024 / 1024 / 1024, 2)} GB, Total: {Math.Round(Convert.ToDouble(obj["Size"]) / 1024 / 1024 / 1024, 2)} GB\n";
                }
                return diskInfo;
            }

            static string GetGPUInfo()
            {
                string gpuInfo = "";
                ManagementObjectSearcher searcher = new ManagementObjectSearcher("select * from Win32_VideoController");
                foreach (ManagementObject obj in searcher.Get())
                {
                    gpuInfo += $"{obj["Name"]}, RAM: {Math.Round(Convert.ToDouble(obj["AdapterRAM"]) / 1024 / 1024 / 1024, 2)} GB\n";
                }
                return gpuInfo;
            }

            static string GetNetworkInfo()
            {
                string networkInfo = "";
                ManagementObjectSearcher searcher = new ManagementObjectSearcher("select * from Win32_NetworkAdapterConfiguration where IPEnabled = 'TRUE'");
                foreach (ManagementObject obj in searcher.Get())
                {
                    networkInfo += $"{obj["Description"]}: IP: {string.Join(", ", (string[])obj["IPAddress"])}\n";
                }

                var networkItems = new List<JsonObject>();

                foreach (ManagementObject obj in searcher.Get())
                {
                    var newNetworkItem = new JsonObject();

                    newNetworkItem.Add("Description", obj["Description"].ToString());
                    newNetworkItem.Add("IPAddress", string.Join(", ", (string[])obj["IPAddress"]));
                }


                return networkInfo;
            }

            static string GetUptimeInfo()
            {
                ManagementObjectSearcher searcher = new ManagementObjectSearcher("select * from Win32_OperatingSystem");
                foreach (ManagementObject obj in searcher.Get())
                {
                    DateTime lastBootUpTime = ManagementDateTimeConverter.ToDateTime(obj["LastBootUpTime"].ToString());
                    TimeSpan uptime = DateTime.Now - lastBootUpTime;

                    var returnPack = new JsonObject();
                    returnPack.Add("LastBootUpTime", lastBootUpTime.ToString());
                    returnPack.Add("UptimeDays", uptime.Days);
                    returnPack.Add("UptimeHours", uptime.Hours);
                    returnPack.Add("UptimeMinutes", uptime.Minutes);

                    //Guys please be smart; you don't need to run this on an update function to know the uptime; take a recording of the time as is and use this to get a reference point
                    //THEN you should add to the uptime based on the difference of time between when the call was done and now; this CAN be an update but you'd best make it async too

                    //Yeah no this was past me speaking and he was highkey speaking in bars, be smart


                    return $"{uptime.Days} days, {uptime.Hours} hours, {uptime.Minutes} minutes";
                }
                return "N/A";
            }

            static string CheckHardware()
            {
                //Does this have a display?
                //Does this have 2D capability?
                //Does this have 3D capability?
                //Does this have VR capability?
                //Is the VR session active?

                //Nah i'll be lazy LOL, back to this at the end

                return "WOW!";
            }

            //Okay what the HELL was younger me doing, we can still use this and it's good but let's format it as a JsonObject Instead

            //No, this is past me (Well older than younger me); this is fine
        }


    }
}
