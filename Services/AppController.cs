namespace LUDUS.Services {
    public class AppController {
        private readonly AdbService _adb;
        public AppController(AdbService adb) => _adb = adb;

        public bool Open(string deviceId, string packageName) {
            var (output, error, code) = _adb.Run($"-s {deviceId} shell monkey -p {packageName} -c android.intent.category.LAUNCHER 1");
            return code == 0 && output.IndexOf("Events injected", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public bool Close(string deviceId, string packageName) {
            var (_, error, code) = _adb.Run($"-s {deviceId} shell am force-stop {packageName}");
            return code == 0 && string.IsNullOrEmpty(error);
        }
    }
}
