using System.Text.Json.Nodes;
using static Pariah_Cybersecurity.DataHandler;
using static XRUIOS.Barebones.Interfaces.DeviceManagerClass;
using static XRUIOS.Barebones.XRUIOS;

namespace XRUIOS.Barebones
{
    public static class DeviceManagerClass
    {
        private static readonly string DeviceDirectory =
            Path.Combine(DataPath, "Devices");

        private const string DeviceFileName = "Devices";


        
        // CREATE
        

        public static async Task AddDevice(Device device)
        {

            var deviceFile =
                await JSONDataHandler.LoadJsonFile(
                    DeviceFileName,
                    DeviceDirectory);

            var devices =
                (List<Device>)await JSONDataHandler.GetVariable<List<Device>>(
                    deviceFile,
                    "Data",
                    encryptionKey);

            if (string.IsNullOrWhiteSpace(device.UUID.ToString()))
            {
                device.UUID = Guid.NewGuid();
            }

            if (devices.Any(d => d.UUID == device.UUID))
            {
                throw new InvalidOperationException(
                    $"A device with UUID '{device.UUID}' already exists.");
            }

            devices.Add(device);

            var editedFile =
                await JSONDataHandler.UpdateJson<List<Device>>(
                    deviceFile,
                    "Data",
                    devices,
                    encryptionKey);

            await JSONDataHandler.SaveJson(editedFile);
        }


        
        // READ - ALL
        

        public static async Task<List<Device>> GetDevices()
        {

            var deviceFile =
                await JSONDataHandler.LoadJsonFile(
                    DeviceFileName,
                    DeviceDirectory);

            return (List<Device>)await JSONDataHandler.GetVariable<List<Device>>(
                deviceFile,
                "Data",
                encryptionKey);
        }


        
        // READ - SINGLE
        

        public static async Task<Device?> GetDevice(string uuid)
        {
            var devices = await GetDevices();

            return devices.FirstOrDefault(
                d => d.UUID.ToString() == uuid);
        }


        
        // UPDATE
        

        public static async Task UpdateDevice(Device device)
        {

            var deviceFile =
                await JSONDataHandler.LoadJsonFile(
                    DeviceFileName,
                    DeviceDirectory);

            var devices =
                (List<Device>)await JSONDataHandler.GetVariable<List<Device>>(
                    deviceFile,
                    "Data",
                    encryptionKey);

            var index = devices.FindIndex(
                d => d.UUID == device.UUID);

            if (index == -1)
            {
                throw new InvalidOperationException(
                    $"Device '{device.UUID}' does not exist.");
            }

            devices[index] = device;

            var editedFile =
                await JSONDataHandler.UpdateJson<List<Device>>(
                    deviceFile,
                    "Data",
                    devices,
                    encryptionKey);

            await JSONDataHandler.SaveJson(editedFile);
        }


        
        // DELETE
        

        public static async Task DeleteDevice(string uuid)
        {

            var deviceFile =
                await JSONDataHandler.LoadJsonFile(
                    DeviceFileName,
                    DeviceDirectory);

            var devices =
                (List<Device>)await JSONDataHandler.GetVariable<List<Device>>(
                    deviceFile,
                    "Data",
                    encryptionKey);

            var device =
                devices.FirstOrDefault(d => d.UUID.ToString() == uuid);

            if (device == null)
            {
                throw new InvalidOperationException(
                    $"Device '{uuid}' does not exist.");
            }

            devices.Remove(device);

            var editedFile =
                await JSONDataHandler.UpdateJson<List<Device>>(
                    deviceFile,
                    "Data",
                    devices,
                    encryptionKey);

            await JSONDataHandler.SaveJson(editedFile);
        }

        // PURGE THE FILTH

        public static async Task ClearDevices()
        {
            var directoryPath = Path.Combine(DataPath, "Devices");

            if (!Directory.Exists(directoryPath))
                return;

            var deviceFile = await JSONDataHandler.LoadJsonFile("Devices", directoryPath);

            deviceFile = await JSONDataHandler.UpdateJson<List<Device>>(
                deviceFile,
                "Data",
                new List<Device>(),
                encryptionKey
            );

            await JSONDataHandler.SaveJson(deviceFile);
        }

        // EXISTS


        public static async Task<bool> DeviceExists(string uuid)
        {
            var device = await GetDevice(uuid);

            return device != null;
        }
    }
}