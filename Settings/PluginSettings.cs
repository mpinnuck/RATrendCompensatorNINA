using NINA.Profile.Interfaces;

namespace RATrendCompensatorNINA.Settings {

    /// <summary>
    /// Thin wrapper around NINA's per-profile plugin settings store, scoped by
    /// this plugin's assembly GUID (see Properties/AssemblyInfo.cs) so values
    /// don't collide with other plugins' settings.
    ///
    /// </summary>
    public class PluginSettings {
        private static readonly System.Guid PluginGuid = System.Guid.Parse("6f2a9d3e-6b6c-4b9a-8e6f-1a1c2c9c9a11");

        private const string HostKey = "StatusHost";
        private const string PortKey = "StatusPort";
        private const string EnabledKey = "Enabled";
        private const string VerboseLoggingKey = "VerboseLogging";
        private const string LaunchAppKey = "LaunchApp";
        private const string ExecutablePathKey = "ExecutablePath";
        private const string ArgumentsKey = "Arguments";
        private const string PlotHistoryHoursKey = "PlotHistoryHours";

        private const string DefaultHost = "127.0.0.1";
        private const int DefaultPort = 4401;
        private const double DefaultPlotHistoryHours = 3.0;

        private readonly IProfileService profileService;

        private IPluginSettings Store => profileService?.ActiveProfile?.PluginSettings;

        public PluginSettings(IProfileService profileService) {
            this.profileService = profileService;
        }

        public string Host {
            get {
                var store = Store;
                return store != null && store.TryGetValue(PluginGuid, HostKey, out string value)
                    ? value
                    : DefaultHost;
            }
            set {
                var store = Store;
                if (store == null) return;
                store.SetValue(PluginGuid, HostKey, value);
            }
        }

        public int Port {
            get {
                var store = Store;
                return store != null && store.TryGetValue(PluginGuid, PortKey, out int value)
                    ? value
                    : DefaultPort;
            }
            set {
                var store = Store;
                if (store == null) return;
                store.SetValue(PluginGuid, PortKey, value);
            }
        }

        public bool Enabled {
            get {
                var store = Store;
                return store != null && store.TryGetValue(PluginGuid, EnabledKey, out bool value)
                    ? value
                    : true;
            }
            set {
                var store = Store;
                if (store == null) return;
                store.SetValue(PluginGuid, EnabledKey, value);
            }
        }

        public bool VerboseLogging {
            get {
                var store = Store;
                return store != null && store.TryGetValue(PluginGuid, VerboseLoggingKey, out bool value)
                    ? value
                    : false;
            }
            set {
                var store = Store;
                if (store == null) return;
                store.SetValue(PluginGuid, VerboseLoggingKey, value);
            }
        }

        /// <summary>Whether the plugin should launch RA_TrendCompensator itself on startup and close it on shutdown.</summary>
        public bool LaunchApp {
            get {
                var store = Store;
                return store != null && store.TryGetValue(PluginGuid, LaunchAppKey, out bool value)
                    ? value
                    : false;
            }
            set {
                var store = Store;
                if (store == null) return;
                store.SetValue(PluginGuid, LaunchAppKey, value);
            }
        }

        /// <summary>Path to RA_TrendCompensator's executable (a PyInstaller-built .exe, or python.exe if using Arguments to point at the script).</summary>
        public string ExecutablePath {
            get {
                var store = Store;
                return store != null && store.TryGetValue(PluginGuid, ExecutablePathKey, out string value)
                    ? value
                    : "";
            }
            set {
                var store = Store;
                if (store == null) return;
                store.SetValue(PluginGuid, ExecutablePathKey, value);
            }
        }

        /// <summary>Optional command-line arguments, e.g. a script path if ExecutablePath points at python.exe.</summary>
        public string Arguments {
            get {
                var store = Store;
                return store != null && store.TryGetValue(PluginGuid, ArgumentsKey, out string value)
                    ? value
                    : "";
            }
            set {
                var store = Store;
                if (store == null) return;
                store.SetValue(PluginGuid, ArgumentsKey, value);
            }
        }

        public double PlotHistoryHours {
            get {
                var store = Store;
                return store != null && store.TryGetValue(PluginGuid, PlotHistoryHoursKey, out double value)
                    ? value
                    : DefaultPlotHistoryHours;
            }
            set {
                var store = Store;
                if (store == null) return;
                store.SetValue(PluginGuid, PlotHistoryHoursKey, value);
            }
        }
    }
}
