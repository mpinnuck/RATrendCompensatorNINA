using System.Reflection;
using System.Runtime.InteropServices;
using NINA.Plugin;

// -- Required --

[assembly: AssemblyTitle("RA Trend Compensator Monitor")]
[assembly: Guid("6f2a9d3e-6b6c-4b9a-8e6f-1a1c2c9c9a11")] // generate your own GUID and never change it after first release
[assembly: AssemblyVersion("2.0.0.0")]
[assembly: AssemblyFileVersion("2.0.0.0")]
[assembly: AssemblyMetadata("ShortDescription", "Live dockable panel showing RA_TrendCompensator's PHD2 drift-compensation state inside NINA's imaging tab.")]

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
default) and shows its live state -- running/dry-run, current RightAscensionRate
offset, RA deviation and slope, guide RMS and its trend, PHD2 average distance,
declination and side of pier -- in a dockable panel in NINA's imaging tab.

Read-only: this plugin never sends anything to RA_TrendCompensator, it only
displays the status snapshots that tool already broadcasts.")]
[assembly: AssemblyMetadata("FeaturedImageURL", "")]
[assembly: AssemblyMetadata("ScreenshotURL", "")]
[assembly: AssemblyMetadata("AltScreenshotURL", "")]

[assembly: AssemblyProduct("RATrendCompensatorNINA")]
[assembly: AssemblyCopyright("Copyright © 2026")]
[assembly: ComVisible(false)]
