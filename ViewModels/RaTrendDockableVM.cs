using NINA.Profile.Interfaces;
using NINA.WPF.Base.ViewModel;
using RATrendCompensatorNINA.Model;
using RATrendCompensatorNINA.Settings;
using System;
using System.ComponentModel.Composition;
using System.Windows.Threading;

namespace RATrendCompensatorNINA.ViewModels {

    /// <summary>
    /// Dockable panel for NINA's imaging tab showing RA_TrendCompensator's live
    /// state. Purely a display -- it only ever reads from the status socket,
    /// never writes to it (RA_TrendCompensator's own status_server.py is
    /// deliberately read-only, and this plugin honours that on this side too).
    /// </summary>
    [Export(typeof(NINA.Equipment.Interfaces.ViewModel.IDockableVM))]
    public class RaTrendDockableVM : DockableVM {

        // Snapshots arrive roughly once a second (status_server_interval_seconds
        // in the Python app's config); if nothing has arrived for a few
        // intervals, treat the display as stale rather than showing a frozen
        // last-known value with no indication it might be old.
        private static readonly TimeSpan StaleAfter = TimeSpan.FromSeconds(5);

        private readonly PluginSettings settings;
        private readonly StatusClient statusClient;
        private readonly DispatcherTimer ageTimer;
        private DateTime? lastSnapshotReceivedAt;

        [ImportingConstructor]
        public RaTrendDockableVM(IProfileService profileService) : base(profileService) {
            Title = "RA Trend Compensator";
            settings = new PluginSettings(profileService);
            ConnectionStatusText = "Not connected";

            statusClient = new StatusClient(settings.Host, settings.Port);
            statusClient.SnapshotReceived += (s, snapshot) =>
                Dispatcher.CurrentDispatcher.Invoke(() => ApplySnapshot(snapshot));
            statusClient.ConnectionStateChanged += (s, connected) =>
                Dispatcher.CurrentDispatcher.Invoke(() => ApplyConnectionState(connected));

            // Ticks once a second purely to refresh "last update Ns ago" /
            // staleness even when no new snapshot has arrived -- it never
            // touches the socket itself.
            ageTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            ageTimer.Tick += (s, e) => RefreshAgeDisplay();
            ageTimer.Start();

            if (settings.Enabled) {
                statusClient.Start();
            }
        }

        private void ApplyConnectionState(bool connected) {
            IsConnected = connected;
            ConnectionStatusText = connected
                ? "Connected"
                : $"Waiting for RA_TrendCompensator at {settings.Host}:{settings.Port}...";
            if (!connected) {
                IsStale = false;
            }
        }

        private void ApplySnapshot(RaTrendStatusSnapshot snapshot) {
            lastSnapshotReceivedAt = DateTime.UtcNow;
            IsStale = false;

            Running = snapshot.Running;
            DryRun = snapshot.DryRun;
            CurrentOffsetText = FormatRate(snapshot.CurrentOffset);
            CurrentDeviationText = FormatArcsec(snapshot.CurrentRaDeviationArcsec);
            SlopeText = FormatRate(snapshot.LastSlopeArcsecPerSec, "arcsec/s", snapshot.LastTrendNSamples);
            GuideRmsText = FormatArcsec(snapshot.GuideRmsArcsec);
            GuideRmsTrendText = FormatRate(snapshot.GuideRmsTrendArcsecPerSec, "arcsec/s", snapshot.GuideRmsTrendNSamples);
            Phd2AvgDistText = FormatArcsec(snapshot.Phd2AvgDistArcsec);
            DeclinationText = snapshot.DeclinationDeg.HasValue ? $"{snapshot.DeclinationDeg.Value:F2}°" : "--";
            SideOfPierText = string.IsNullOrEmpty(snapshot.SideOfPier) ? "--" : snapshot.SideOfPier;

            RefreshAgeDisplay();
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
