using NINA.Plugin;
using NINA.Plugin.Interfaces;
using NINA.Profile.Interfaces;
using System.ComponentModel.Composition;

namespace RATrendCompensatorNINA {

    /// <summary>
    /// Mandatory plugin entry point. All the metadata NINA's plugin manager
    /// needs is pulled automatically from the assembly attributes in
    /// Properties/AssemblyInfo.cs.
    ///
    /// Also stashes IProfileService statically so the options page (loaded by
    /// NINA via a DataTemplate lookup rather than constructor injection) has
    /// a simple way to reach the same PluginSettings the dockable panel uses.
    /// </summary>
    [Export(typeof(IPluginManifest))]
    public class RATrendCompensatorPlugin : PluginBase {

        public static IProfileService ProfileService { get; private set; }

        [ImportingConstructor]
        public RATrendCompensatorPlugin(IProfileService profileService) {
            ProfileService = profileService;
        }
    }
}
