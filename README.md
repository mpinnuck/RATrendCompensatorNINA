# RATrendCompensatorNINA

A NINA plugin that adds a dockable panel to the imaging tab showing
RA_TrendCompensator's live PHD2 drift-compensation state.

## How it works

- RA_TrendCompensator's `StatusServer` already broadcasts a JSON snapshot
  once a second over a local, read-only TCP socket (`127.0.0.1:4401` by
  default) -- see `status_server.py` / `get_status_snapshot()` in the
  Python app.
- This plugin's `StatusClient` connects to that socket, reconnecting
  automatically if RA_TrendCompensator isn't running yet or drops the
  connection, and pushes each snapshot into `RaTrendDockableVM`.
- `RaTrendDockableVM` is exported as an `IDockableVM`, so NINA shows it as
  a panel you can dock anywhere in the imaging tab.
- Nothing is ever sent back over the socket -- this mirrors the read-only
  design of `status_server.py` deliberately (per the comment in that
  file, any future two-way control belongs in this plugin's own control
  logic, not mixed into the status feed).

## Project layout

```
RATrendCompensatorNINA/
  RATrendCompensatorNINA.csproj
  Plugin.cs                        -- IPluginManifest export (metadata only)
  Properties/AssemblyInfo.cs       -- required plugin metadata attributes
  Model/RaTrendStatusSnapshot.cs   -- JSON shape, mirrors get_status_snapshot()
  Model/StatusClient.cs            -- TCP client, auto-reconnect
  Settings/PluginSettings.cs       -- host/port/enabled, persisted per-profile
  ViewModels/RaTrendDockableVM.cs  -- IDockableVM, owns the StatusClient
  Views/RaTrendDockableView.xaml   -- the panel's UI (DataTemplate)
  Views/OptionsView.xaml           -- host/port settings on the plugin's page
```

## Setting up the Visual Studio project

1. Install the NINA plugin project template (VSIX) from
   https://github.com/isbeorn/nina.plugin.template -- or just open this
   folder as-is; the `.csproj` here already targets `net8.0-windows` with
   `UseWPF`.
2. Open `RATrendCompensatorNINA.csproj` in Visual Studio 2022+.
3. In NuGet Package Manager, update the `NINA.Plugin` package reference to
   match the NINA version you're running (Help → About in NINA). The
   version pinned in the `.csproj` is a placeholder.
4. Build. The post-build step copies the DLL to
   `%LocalAppData%\NINA\Plugins\3rdParty\RATrendCompensatorNINA\` so NINA
   picks it up on next launch -- no manual copying needed for local dev.

## Things worth double-checking once you have the real SDK in front of you

I wrote this against the plugin template's public documentation rather
than a compiled copy of `NINA.Plugin`/`NINA.WPF.Base`, so a few call sites
are my best reconstruction of the current API and are worth a quick sanity
check in Visual Studio's IntelliSense before you trust them:

- **`PluginSettings` accessor names** (`GetValueString`/`SetValueString`/
  `GetValueInt32`/`GetValueBoolean` on `profileService.ActiveProfile.PluginSettings`)
  -- these are the NINA.Plugin 3.x names as best I can tell; if IntelliSense
  shows something different, swap them in `Settings/PluginSettings.cs` only
  -- nothing else depends on the exact accessor names.
- **`DockableVM` base class members** -- I've used `Title`, `ContentId`,
  `IsTool`, and a `RaisePropertyChanged()` helper, following the pattern
  used elsewhere in NINA's own WPF codebase. Confirm the base class
  actually exposes these under those names.
- **The `_Options` DataTemplate key** -- must exactly equal
  `<IPluginManifest.Name>_Options`. `Name` is populated from
  `[AssemblyTitle]` in `AssemblyInfo.cs` ("RA Trend Compensator Monitor").
  If NINA derives `Name` differently (e.g. trims/normalizes it), adjust
  the key in `OptionsView.xaml` to match what you see in the plugin list.
- **GUID in `AssemblyInfo.cs` and `PluginSettings.cs`** -- I generated a
  placeholder GUID (`6f2a9d3e-...`); regenerate your own once and then
  never change it, per the template's warning about breaking existing
  installs/sequences.

None of these affect the actual logic (the TCP client, reconnect handling,
and JSON parsing in `Model/` don't depend on NINA's SDK at all and can be
tested standalone), just the plumbing that wires the panel into NINA's UI.

## Keeping the JSON shape in sync

If `get_status_snapshot()` in `view_model.py` ever changes field names or
adds/removes fields, mirror that in `Model/RaTrendStatusSnapshot.cs`
(the `[JsonPropertyName]` attributes) and, if you want it displayed,
`ViewModels/RaTrendDockableVM.cs` + `Views/RaTrendDockableView.xaml`.
