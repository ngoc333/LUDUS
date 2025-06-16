using System.Collections.Generic;
using System.Linq;

namespace LUDUS.Services {
    public class DeviceManager {
        private readonly AdbService _adb;
        public List<string> Devices { get; private set; }
        public string CurrentDevice { get; private set; }

        public DeviceManager(AdbService adb) {
            _adb = adb;
            Devices = new List<string>();
        }

        public void Refresh() {
            var (output, _, _) = _adb.Run("devices");
            var lines = output
                .Split(new[] { "\r\n", "\n" }, System.StringSplitOptions.RemoveEmptyEntries)
                .Skip(1);

            Devices.Clear();
            foreach (var line in lines) {
                var parts = line.Split('\t');
                if (parts.Length >= 2 && parts[1].Trim() == "device")
                    Devices.Add(parts[0].Trim());
            }

            if (Devices.Count > 0)
                CurrentDevice = Devices[0];
        }

        public bool Connect(string deviceId) {
            var (output, _, code) = _adb.Run($"connect {deviceId}");
            if (code == 0 && output.Contains("connected to")) {
                CurrentDevice = deviceId;
                return true;
            }
            return false;
        }
    }
}
