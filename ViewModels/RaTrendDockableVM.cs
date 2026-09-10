using NINA.Profile.Interfaces;
using NINA.WPF.Base.ViewModel;
using RATrendCompensatorNINA.Model;
using RATrendCompensatorNINA.Settings;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace RATrendCompensatorNINA.ViewModels {

    /// <summary>
    /// Dockable panel for NINA's imaging tab showing RA_TrendCompensator's live
    /// state, and (if configured) launching/closing the app itself alongside
    /// NINA. Display is purely a reader of the status socket -- it never
    /// writes to it (RA_TrendCompensator's own status_server.py is
    /// deliberately read-only, and this plugin honours that on this side too).
    /// The only thing this plugin ever "controls" is the app's process
    /// lifecycle (via AppLifecycleManager), not its compensation logic.
    /// </summary>
    [Export(typeof(NINA.Equipment.Interfaces.ViewModel.IDockableVM))]
    public class RaTrendDockableVM : DockableVM, IDisposable {

        // Snapshots arrive roughly once a second (status_server_interval_seconds
        // in the Python app's config); if nothing has arrived for a few
        // intervals, treat the display as stale rather than showing a frozen
        // last-known value with no indication it might be old.
        private static readonly TimeSpan StaleAfter = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan PlotGridInterval = TimeSpan.FromMinutes(15);
        private const int PlotWidth = 260;
        private const int PlotHeight = 100;
        private const int MaxPlotSamples = 12000;
        private const double DefaultPlotYMinArcsec = -1.0;
        private const double DefaultPlotYMaxArcsec = 1.0;

        private readonly PluginSettings settings;
        private readonly StatusClient statusClient;
        private readonly AppLifecycleManager appLifecycle;
        private readonly DispatcherTimer ageTimer;
        private readonly List<RaErrorSample> raErrorHistory = new List<RaErrorSample>();
        private readonly TimeSpan plotHistoryWindow;
        private DateTime? lastSnapshotReceivedAt;

        private readonly struct RaErrorSample {
            public RaErrorSample(DateTime timestampUtc, double? valueArcsec) {
                TimestampUtc = timestampUtc;
                ValueArcsec = valueArcsec;
            }

            public DateTime TimestampUtc { get; }
            public double? ValueArcsec { get; }
        }

        public readonly struct PlotTimeTick {
            public PlotTimeTick(double x, string label) {
                X = x;
                Label = label;
                LabelX = Math.Max(0, Math.Min(PlotWidth - 30, x - 14));
            }

            public double X { get; }
            public string Label { get; }
            public double LabelX { get; }
        }

        [ImportingConstructor]
        public RaTrendDockableVM(IProfileService profileService) : base(profileService) {
            PluginLog.WriteStartupHeader();
            PluginLog.Write("Startup", "RaTrendDockableVM constructor entered.");

            Title = "RA Trend Compensator";
            settings = new PluginSettings(profileService);
            var configuredPlotHistoryHours = settings.PlotHistoryHours;
            var boundedPlotHistoryHours = Math.Max(0.25, Math.Min(24.0, configuredPlotHistoryHours));
            plotHistoryWindow = TimeSpan.FromHours(boundedPlotHistoryHours);
            PluginLog.Write("Startup", $"Settings loaded: enabled={settings.Enabled}, launchApp={settings.LaunchApp}, verboseLogging={settings.VerboseLogging}, host={settings.Host}, port={settings.Port}, bufferHours={boundedPlotHistoryHours:0.##}, executablePathSet={!string.IsNullOrWhiteSpace(settings.ExecutablePath)}");
            ConnectionStatusText = "not connected";

            var hasExecutablePath = !string.IsNullOrWhiteSpace(settings.ExecutablePath);
            var hasValidExecutablePath = hasExecutablePath && File.Exists(settings.ExecutablePath);

            if (settings.LaunchApp && hasValidExecutablePath) {
                try {
                    PluginLog.Write("Startup", "LaunchApp is enabled and executable path is valid; creating AppLifecycleManager.");
                    appLifecycle = new AppLifecycleManager(settings.ExecutablePath, settings.Arguments,
                        msg => {
                            PluginLog.Write("Lifecycle", msg);
                            DispatchToUi(() => LifecycleStatusText = msg);
                        });
                    appLifecycle.Launch();
                    // Standard WPF application-exit event -- fires when NINA's
                    // Application.Shutdown() runs or its last window closes.
                    // This is the primary shutdown hook; Dispose() below (in
                    // case NINA disposes MEF-composed IDockableVM instances
                    // directly) is a second, redundant safety net -- whichever
                    // fires first does the actual work, CloseMainWindow() /
                    // Kill() are both safe to call more than once.
                    DispatchToUi(() => {
                        if (Application.Current != null) {
                            Application.Current.Exit += (s, e) => appLifecycle?.Shutdown(TimeSpan.FromSeconds(5));
                        }
                    });
                } catch (Exception ex) {
                    try {
                        PluginLog.Write("Lifecycle", $"WARNING: autolaunch setup failed: {ex.Message}");
                    } catch {
                    }
                }
            } else if (settings.LaunchApp && !hasExecutablePath) {
                PluginLog.Write("Startup", "LaunchApp is enabled but executable path is empty; skipping launch and waiting for status socket.");
            } else if (settings.LaunchApp && !hasValidExecutablePath) {
                PluginLog.Write("Startup", $"LaunchApp is enabled but executable path does not exist: '{settings.ExecutablePath}'. Skipping launch and waiting for status socket.");
            } else {
                PluginLog.Write("Startup", "LaunchApp is disabled; plugin will not launch RA_TrendCompensator.");
            }

            statusClient = new StatusClient(settings.Host, settings.Port, settings.VerboseLogging);
            PluginLog.Write("Startup", "StatusClient created and event handlers wiring started.");
            statusClient.SnapshotReceived += (s, snapshot) =>
                DispatchToUi(() => ApplySnapshot(snapshot));
            statusClient.ConnectionStateChanged += (s, connected) =>
                DispatchToUi(() => ApplyConnectionState(connected));

            // Ticks once a second purely to refresh "last update Ns ago" /
            // staleness even when no new snapshot has arrived -- it never
            // touches the socket itself.
            ageTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            ageTimer.Tick += (s, e) => RefreshAgeDisplay();
            ageTimer.Start();
            PluginLog.Write("Startup", "Age timer started.");

            if (settings.Enabled) {
                statusClient.Start();
                PluginLog.Write("Startup", "StatusClient started (Enabled=true).");
            } else {
                PluginLog.Write("Startup", "StatusClient not started (Enabled=false).");
            }

            PluginLog.Write("Startup", "RaTrendDockableVM constructor completed.");
        }

        private static void DispatchToUi(Action action) {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null) {
                try {
                    PluginLog.Write("Lifecycle", "UI dispatcher not available; skipping UI update.");
                } catch {
                }
                return;
            }

            if (dispatcher.CheckAccess()) {
                try {
                    action();
                } catch (Exception ex) {
                    try {
                        PluginLog.Write("Lifecycle", $"WARNING: UI update failed: {ex.GetType().Name}: {ex.Message}");
                    } catch {
                    }
                }
                return;
            }

            try {
                dispatcher.BeginInvoke(action);
            } catch (Exception ex) {
                try {
                    PluginLog.Write("Lifecycle", $"WARNING: BeginInvoke failed: {ex.GetType().Name}: {ex.Message}");
                } catch {
                }
            }
        }

        public void Dispose() {
            // Redundant safety net alongside the Application.Current.Exit
            // hook in the constructor -- see that comment for why both exist.
            appLifecycle?.Dispose();
        }

        private void ApplyConnectionState(bool connected) {
            IsConnected = connected;
            ConnectionStatusText = connected
                ? "connected"
                : "not connected";
            if (!connected) {
                IsStale = false;
            }
        }

        private void ApplySnapshot(RaTrendStatusSnapshot snapshot) {
            lastSnapshotReceivedAt = DateTime.UtcNow;
            IsStale = false;

            Running = snapshot.Running;
            Phd2Connected = snapshot.Phd2Connected;
            Phd2Guiding = snapshot.Phd2Guiding;
            IsPaused = snapshot.Paused;
            ActivelyCorrecting = snapshot.ActivelyCorrecting;
            MountTrackingText = snapshot.MountTracking switch {
                true => "Tracking",
                false => "Not tracking",
                null => "Unknown",
            };
            StateText = DetermineStateText(snapshot);
            DryRun = snapshot.DryRun;
            CurrentOffsetText = FormatRate(snapshot.CurrentOffset);
            CurrentDeviationText = FormatArcsec(snapshot.CurrentRaDeviationArcsec);
            if (snapshot.Running && snapshot.ActivelyCorrecting) {
                AddRaErrorSample(snapshot.CurrentRaDeviationArcsec, UnixSecondsToUtc(snapshot.Timestamp));
            }
            SlopeText = FormatRate(snapshot.LastSlopeArcsecPerSec, "arcsec/s", snapshot.LastTrendNSamples);
            GuideRmsText = FormatArcsec(snapshot.GuideRmsArcsec);
            GuideRmsTrendText = FormatRate(snapshot.GuideRmsTrendArcsecPerSec, "arcsec/s", snapshot.GuideRmsTrendNSamples);
            Phd2AvgDistText = FormatArcsec(snapshot.Phd2AvgDistArcsec);
            RightAscensionText = FormatHours(snapshot.RightAscensionHours);
            DeclinationText = snapshot.DeclinationDeg.HasValue ? $"{snapshot.DeclinationDeg.Value:F2}°" : "--";
            SideOfPierText = string.IsNullOrEmpty(snapshot.SideOfPier) ? "--" : snapshot.SideOfPier;

            RefreshAgeDisplay();
        }

        private void AddRaErrorSample(double? valueArcsec, DateTime timestampUtc) {
            raErrorHistory.Add(new RaErrorSample(timestampUtc, valueArcsec));

            if (raErrorHistory.Count > MaxPlotSamples) {
                raErrorHistory.RemoveRange(0, raErrorHistory.Count - MaxPlotSamples);
            }

            RefreshRaErrorPlot();
        }

        public void ClearRaErrorPlot() {
            raErrorHistory.Clear();
            RefreshRaErrorPlot();
        }

        private static DateTime FloorToIntervalLocal(DateTime timestampLocal, TimeSpan interval) {
            var ticks = interval.Ticks;
            if (ticks <= 0) return timestampLocal;
            return new DateTime(timestampLocal.Ticks - (timestampLocal.Ticks % ticks), timestampLocal.Kind);
        }

        private static List<PlotTimeTick> BuildVerticalGridLines(DateTime timeStart, DateTime timeEnd, double totalSeconds) {
            var ticks = new List<PlotTimeTick>();
            var startLocal = timeStart.ToLocalTime();
            var endLocal = timeEnd.ToLocalTime();

            var tickLocal = FloorToIntervalLocal(startLocal, PlotGridInterval);
            if (tickLocal < startLocal) {
                tickLocal = tickLocal.Add(PlotGridInterval);
            }

            while (tickLocal <= endLocal) {
                var x = ((tickLocal - startLocal).TotalSeconds / totalSeconds) * PlotWidth;
                if (x >= 0 && x <= PlotWidth) {
                    var xClamped = Math.Max(0, Math.Min(PlotWidth - 1, x));
                    ticks.Add(new PlotTimeTick(xClamped, tickLocal.ToString("HH:mm")));
                }
                tickLocal = tickLocal.Add(PlotGridInterval);
            }

            return ticks;
        }

        private static PointCollection CreateFrozenPointCollection() {
            var points = new PointCollection();
            points.Freeze();
            return points;
        }

        private static DateTime UnixSecondsToUtc(double unixSeconds) {
            if (double.IsNaN(unixSeconds) || double.IsInfinity(unixSeconds) || unixSeconds <= 0) {
                return DateTime.UtcNow;
            }

            var wholeSeconds = (long)Math.Floor(unixSeconds);
            var fractionalSeconds = unixSeconds - wholeSeconds;
            return DateTimeOffset.FromUnixTimeSeconds(wholeSeconds)
                .AddSeconds(fractionalSeconds)
                .UtcDateTime;
        }

        private static string FormatTimeLabel(DateTime timestampUtc) =>
            timestampUtc.ToLocalTime().ToString("HH:mm");

        private void RefreshRaErrorPlot() {
            if (raErrorHistory.Count == 0) {
                RaErrorPlotPoints = CreateFrozenPointCollection();
                RaErrorPlotVerticalGridLines = Array.Empty<PlotTimeTick>();
                RaErrorPlotRangeText = "RA error history (no samples yet)";
                RaErrorPlotXMinText = "--:--";
                RaErrorPlotXMaxText = "--:--";
                RaErrorPlotYMinText = $"{DefaultPlotYMinArcsec:+0.00;-0.00;0.00}\"";
                RaErrorPlotYMidText = "0.00\"";
                RaErrorPlotYMaxText = $"{DefaultPlotYMaxArcsec:+0.00;-0.00;0.00}\"";
                return;
            }

            var points = new PointCollection();
            var timeStart = raErrorHistory[0].TimestampUtc;
            var timeEnd = raErrorHistory[raErrorHistory.Count - 1].TimestampUtc;
            var totalSeconds = Math.Max(1.0, (timeEnd - timeStart).TotalSeconds);
            RaErrorPlotVerticalGridLines = BuildVerticalGridLines(timeStart, timeEnd, totalSeconds);

            double? minY = null;
            double? maxY = null;
            foreach (var sample in raErrorHistory) {
                if (!sample.ValueArcsec.HasValue) continue;
                minY = !minY.HasValue ? sample.ValueArcsec.Value : Math.Min(minY.Value, sample.ValueArcsec.Value);
                maxY = !maxY.HasValue ? sample.ValueArcsec.Value : Math.Max(maxY.Value, sample.ValueArcsec.Value);
            }

            if (!minY.HasValue || !maxY.HasValue) {
                RaErrorPlotPoints = CreateFrozenPointCollection();
                RaErrorPlotRangeText = "RA error history (no numeric values)";
                RaErrorPlotXMinText = FormatTimeLabel(timeStart);
                RaErrorPlotXMaxText = FormatTimeLabel(timeEnd);
                RaErrorPlotYMinText = $"{DefaultPlotYMinArcsec:+0.00;-0.00;0.00}\"";
                RaErrorPlotYMidText = "0.00\"";
                RaErrorPlotYMaxText = $"{DefaultPlotYMaxArcsec:+0.00;-0.00;0.00}\"";
                return;
            }

            var minValue = minY.Value;
            var maxValue = maxY.Value;
            if (Math.Abs(maxValue - minValue) < 0.001) {
                minValue -= 0.5;
                maxValue += 0.5;
            }

            foreach (var sample in raErrorHistory) {
                if (!sample.ValueArcsec.HasValue) continue;

                var x = ((sample.TimestampUtc - timeStart).TotalSeconds / totalSeconds) * PlotWidth;
                var yNorm = (sample.ValueArcsec.Value - minValue) / (maxValue - minValue);
                var y = PlotHeight - (yNorm * PlotHeight);
                points.Add(new Point(x, y));
            }

            points.Freeze();
            RaErrorPlotPoints = points;
            var ageSec = (timeEnd - timeStart).TotalSeconds;
            RaErrorPlotRangeText = $"RA error history ({ageSec:F0}s, {minValue:+0.00;-0.00;0.00}..{maxValue:+0.00;-0.00;0.00}\")";
            RaErrorPlotXMinText = FormatTimeLabel(timeStart);
            RaErrorPlotXMaxText = FormatTimeLabel(timeEnd);
            RaErrorPlotYMinText = $"{minValue:+0.00;-0.00;0.00}\"";
            RaErrorPlotYMidText = $"{((minValue + maxValue) / 2.0):+0.00;-0.00;0.00}\"";
            RaErrorPlotYMaxText = $"{maxValue:+0.00;-0.00;0.00}\"";
        }

        /// <summary>
        /// One-line human summary of the three-state model (mount tracking /
        /// PHD2 guiding / paused) the app now runs -- most useful single
        /// piece of information on the panel, since the individual booleans
        /// alone don't tell you WHY corrections aren't being applied right now.
        /// </summary>
        private static string DetermineStateText(RaTrendStatusSnapshot s) {
            if (!s.Running) return "Stopped";
            if (s.MountTracking == false) return "Mount not tracking";
            if (s.ActivelyCorrecting) return "Actively correcting";
            if (s.Paused) return "Paused (dither/focus/flip)";
            if (!s.Phd2Guiding) return "PHD2 not guiding (maintaining last offset)";
            return "Idle";
        }

        private void RefreshAgeDisplay() {
            if (lastSnapshotReceivedAt == null) {
                LastUpdateText = "--";
                return;
            }

            var age = DateTime.UtcNow - lastSnapshotReceivedAt.Value;
            LastUpdateText = age.TotalSeconds < 1.5 ? "just now" : $"{age.TotalSeconds:F0}s ago";
            IsStale = IsConnected && age > StaleAfter;
        }

        private static string FormatRate(double? value, string unit = "×sidereal", int? n = null) {
            if (!value.HasValue) return "--";
            var text = $"{value.Value:F4} {unit}";
            if (n.HasValue) text += $" (n={n.Value})";
            return text;
        }

        private static string FormatArcsec(double? value) =>
            value.HasValue ? $"{value.Value:F2}\"" : "--";

        private static string FormatHours(double? value) {
            if (!value.HasValue) return "--";

            var normalizedHours = value.Value % 24.0;
            if (normalizedHours < 0) normalizedHours += 24.0;

            var totalSeconds = normalizedHours * 3600.0;
            var hours = (int)(totalSeconds / 3600.0);
            totalSeconds -= hours * 3600.0;
            var minutes = (int)(totalSeconds / 60.0);
            totalSeconds -= minutes * 60.0;
            var seconds = totalSeconds;

            return $"{hours:00}:{minutes:00}:{seconds:00.0}";
        }

        // -- Bound display properties -------------------------------------------------

        private bool isConnected;
        public bool IsConnected {
            get => isConnected;
            set { isConnected = value; RaisePropertyChanged(); }
        }

        private bool isStale;
        public bool IsStale {
            get => isStale;
            set { isStale = value; RaisePropertyChanged(); }
        }

        private string connectionStatusText;
        public string ConnectionStatusText {
            get => connectionStatusText;
            set { connectionStatusText = value; RaisePropertyChanged(); }
        }

        private bool running;
        public bool Running {
            get => running;
            set { running = value; RaisePropertyChanged(); }
        }

        private bool phd2Connected;
        public bool Phd2Connected {
            get => phd2Connected;
            set { phd2Connected = value; RaisePropertyChanged(); }
        }

        private bool phd2Guiding;
        public bool Phd2Guiding {
            get => phd2Guiding;
            set { phd2Guiding = value; RaisePropertyChanged(); }
        }

        private bool isPaused;
        public bool IsPaused {
            get => isPaused;
            set { isPaused = value; RaisePropertyChanged(); }
        }

        private bool activelyCorrecting;
        public bool ActivelyCorrecting {
            get => activelyCorrecting;
            set { activelyCorrecting = value; RaisePropertyChanged(); }
        }

        private string mountTrackingText = "--";
        public string MountTrackingText {
            get => mountTrackingText;
            set { mountTrackingText = value; RaisePropertyChanged(); }
        }

        private string stateText = "--";
        public string StateText {
            get => stateText;
            set { stateText = value; RaisePropertyChanged(); }
        }

        private string lifecycleStatusText = "";
        public string LifecycleStatusText {
            get => lifecycleStatusText;
            set { lifecycleStatusText = value; RaisePropertyChanged(); }
        }

        private PointCollection raErrorPlotPoints = CreateFrozenPointCollection();
        public PointCollection RaErrorPlotPoints {
            get => raErrorPlotPoints;
            set { raErrorPlotPoints = value; RaisePropertyChanged(); }
        }

        private string raErrorPlotRangeText = "RA error history (no samples yet)";
        public string RaErrorPlotRangeText {
            get => raErrorPlotRangeText;
            set { raErrorPlotRangeText = value; RaisePropertyChanged(); }
        }

        private string raErrorPlotXMinText = "--:--";
        public string RaErrorPlotXMinText {
            get => raErrorPlotXMinText;
            set { raErrorPlotXMinText = value; RaisePropertyChanged(); }
        }

        private string raErrorPlotXMaxText = "--:--";
        public string RaErrorPlotXMaxText {
            get => raErrorPlotXMaxText;
            set { raErrorPlotXMaxText = value; RaisePropertyChanged(); }
        }

        private string raErrorPlotYMinText = $"{DefaultPlotYMinArcsec:+0.00;-0.00;0.00}\"";
        public string RaErrorPlotYMinText {
            get => raErrorPlotYMinText;
            set { raErrorPlotYMinText = value; RaisePropertyChanged(); }
        }

        private string raErrorPlotYMidText = "0.00\"";
        public string RaErrorPlotYMidText {
            get => raErrorPlotYMidText;
            set { raErrorPlotYMidText = value; RaisePropertyChanged(); }
        }

        private string raErrorPlotYMaxText = $"{DefaultPlotYMaxArcsec:+0.00;-0.00;0.00}\"";
        public string RaErrorPlotYMaxText {
            get => raErrorPlotYMaxText;
            set { raErrorPlotYMaxText = value; RaisePropertyChanged(); }
        }

        private IEnumerable<PlotTimeTick> raErrorPlotVerticalGridLines = Array.Empty<PlotTimeTick>();
        public IEnumerable<PlotTimeTick> RaErrorPlotVerticalGridLines {
            get => raErrorPlotVerticalGridLines;
            set { raErrorPlotVerticalGridLines = value; RaisePropertyChanged(); }
        }

        private bool dryRun;
        public bool DryRun {
            get => dryRun;
            set { dryRun = value; RaisePropertyChanged(); }
        }

        private string currentOffsetText = "--";
        public string CurrentOffsetText {
            get => currentOffsetText;
            set { currentOffsetText = value; RaisePropertyChanged(); }
        }

        private string currentDeviationText = "--";
        public string CurrentDeviationText {
            get => currentDeviationText;
            set { currentDeviationText = value; RaisePropertyChanged(); }
        }

        private string slopeText = "--";
        public string SlopeText {
            get => slopeText;
            set { slopeText = value; RaisePropertyChanged(); }
        }

        private string guideRmsText = "--";
        public string GuideRmsText {
            get => guideRmsText;
            set { guideRmsText = value; RaisePropertyChanged(); }
        }

        private string guideRmsTrendText = "--";
        public string GuideRmsTrendText {
            get => guideRmsTrendText;
            set { guideRmsTrendText = value; RaisePropertyChanged(); }
        }

        private string phd2AvgDistText = "--";
        public string Phd2AvgDistText {
            get => phd2AvgDistText;
            set { phd2AvgDistText = value; RaisePropertyChanged(); }
        }

        private string declinationText = "--";
        public string DeclinationText {
            get => declinationText;
            set { declinationText = value; RaisePropertyChanged(); }
        }

        private string rightAscensionText = "--";
        public string RightAscensionText {
            get => rightAscensionText;
            set { rightAscensionText = value; RaisePropertyChanged(); }
        }

        private string sideOfPierText = "--";
        public string SideOfPierText {
            get => sideOfPierText;
            set { sideOfPierText = value; RaisePropertyChanged(); }
        }

        private string lastUpdateText = "--";
        public string LastUpdateText {
            get => lastUpdateText;
            set { lastUpdateText = value; RaisePropertyChanged(); }
        }
    }
}
