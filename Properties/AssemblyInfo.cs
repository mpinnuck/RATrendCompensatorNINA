using System.Reflection;
using System.Runtime.InteropServices;
using NINA.Plugin;

// -- Required --

[assembly: AssemblyTitle("RA Trend Compensator Monitor")]
[assembly: Guid("6f2a9d3e-6b6c-4b9a-8e6f-1a1c2c9c9a11")] // generate your own GUID and never change it after first release
[assembly: AssemblyVersion("3.1.0.0")]
[assembly: AssemblyFileVersion("3.1.0.0")]
[assembly: AssemblyMetadata("ShortDescription", "Dockable RA_TrendCompensator monitor for NINA with optional app auto-launch and live RA error charting.")]

// -- Recommended --

[assembly: AssemblyCompany("Mark")]
[assembly: AssemblyMetadata("License", "MIT")]
[assembly: AssemblyMetadata("LicenseURL", "https://opensource.org/licenses/MIT")]
[assembly: AssemblyMetadata("Repository", "")]
[assembly: AssemblyMetadata("MinimumApplicationVersion", "3.1.0.0")]

// -- Optional --

[assembly: AssemblyMetadata("ChangelogURL", "")]
[assembly: AssemblyMetadata("Tags", "guiding,PHD2,mount,drift,RA")]
[assembly: AssemblyMetadata("Homepage", "")]
[assembly: AssemblyMetadata("LongDescription", @"Connects to the RA_TrendCompensator status socket (127.0.0.1:4401 by
default) and shows its live state in a dockable panel in NINA's imaging tab:
running/dry-run, mount/PHD2 state, current RightAscensionRate offset, RA
deviation/slope, guide RMS and trend, PHD2 average distance, declination, side
of pier, and a live RA error history chart.

When enabled, the plugin can also auto-launch RA_TrendCompensator on startup.
The status protocol remains read-only: the plugin does not send control commands
to RA_TrendCompensator over the status socket.")]
[assembly: AssemblyMetadata("FeaturedImageURL", "")]
[assembly: AssemblyMetadata("ScreenshotURL", "")]
[assembly: AssemblyMetadata("AltScreenshotURL", "")]

[assembly: AssemblyProduct("RATrendCompensatorNINA")]
[assembly: AssemblyCopyright("Copyright © 2026")]
[assembly: ComVisible(false)]
