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

        private const string DefaultHost = "127.0.0.1";
        private const int DefaultPort = 4401;

        private readonly IProfileService profileService;

        public PluginSettings(IProfileService profileService) {
            this.profileService = profileService;
        }

        public string Host {
            get {
                return profileService.ActiveProfile.PluginSettings.TryGetValue(PluginGuid, HostKey, out string value)
                    ? value
                    : DefaultHost;
            }
            set => profileService.ActiveProfile.PluginSettings.SetValue(PluginGuid, HostKey, value);
        }

        public int Port {
            get {
                return profileService.ActiveProfile.PluginSettings.TryGetValue(PluginGuid, PortKey, out int value)
                    ? value
                    : DefaultPort;
            }
            set => profileService.ActiveProfile.PluginSettings.SetValue(PluginGuid, PortKey, value);
        }

        public bool Enabled {
            get {
                return profileService.ActiveProfile.PluginSettings.TryGetValue(PluginGuid, EnabledKey, out bool value)
                    ? value
                    : true;
            }
            set => profileService.ActiveProfile.PluginSettings.SetValue(PluginGuid, EnabledKey, value);
        }

        public bool VerboseLogging {
            get {
                return profileService.ActiveProfile.PluginSettings.TryGetValue(PluginGuid, VerboseLoggingKey, out bool value)
                    ? value
                    : false;
            }
            set => profileService.ActiveProfile.PluginSettings.SetValue(PluginGuid, VerboseLoggingKey, value);
        }
    }
}
