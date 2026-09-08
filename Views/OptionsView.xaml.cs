using RATrendCompensatorNINA.Settings;
using System.ComponentModel.Composition;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace RATrendCompensatorNINA.Views {

    [Export(typeof(ResourceDictionary))]
    public partial class OptionsView : ResourceDictionary {
        private PluginSettings settings;

        public OptionsView() {
            InitializeComponent();
        }

        // The DataTemplate's DataContext is whatever NINA's plugin page binds
        // it to, which isn't guaranteed to be anything this plugin controls --
        // so settings are read/written directly against PluginSettings
        // (via the IProfileService the main Plugin export stashed statically)
        // rather than through data binding.
        private void RootPanel_Loaded(object sender, RoutedEventArgs e) {
            if (RATrendCompensatorPlugin.ProfileService == null) return;
            settings = new PluginSettings(RATrendCompensatorPlugin.ProfileService);

            var panel = (StackPanel)sender;
            var hostBox = (TextBox)panel.FindName("HostBox");
            var portBox = (TextBox)panel.FindName("PortBox");
            var enabledBox = (CheckBox)panel.FindName("EnabledBox");
            var verboseLoggingBox = (CheckBox)panel.FindName("VerboseLoggingBox");
            var launchAppBox = (CheckBox)panel.FindName("LaunchAppBox");
            var executablePathBox = (TextBox)panel.FindName("ExecutablePathBox");
            var argumentsBox = (TextBox)panel.FindName("ArgumentsBox");
            var plotHistoryHoursBox = (TextBox)panel.FindName("PlotHistoryHoursBox");

            hostBox.Text = settings.Host;
            portBox.Text = settings.Port.ToString();
            enabledBox.IsChecked = settings.Enabled;
            verboseLoggingBox.IsChecked = settings.VerboseLogging;
            launchAppBox.IsChecked = settings.LaunchApp;
            executablePathBox.Text = settings.ExecutablePath;
            argumentsBox.Text = settings.Arguments;
            plotHistoryHoursBox.Text = settings.PlotHistoryHours.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private void HostBox_LostFocus(object sender, RoutedEventArgs e) {
            if (settings == null) return;
            var text = ((TextBox)sender).Text.Trim();
            if (!string.IsNullOrEmpty(text)) {
                settings.Host = text;
            }
        }

        private void PortBox_LostFocus(object sender, RoutedEventArgs e) {
            if (settings == null) return;
            if (int.TryParse(((TextBox)sender).Text.Trim(), out var port) && port > 0 && port <= 65535) {
                settings.Port = port;
            }
        }

        private void EnabledBox_Changed(object sender, RoutedEventArgs e) {
            if (settings == null) return;
            settings.Enabled = ((CheckBox)sender).IsChecked == true;
        }

        private void VerboseLoggingBox_Changed(object sender, RoutedEventArgs e) {
            if (settings == null) return;
            settings.VerboseLogging = ((CheckBox)sender).IsChecked == true;
        }

        private void LaunchAppBox_Changed(object sender, RoutedEventArgs e) {
            if (settings == null) return;
            settings.LaunchApp = ((CheckBox)sender).IsChecked == true;
        }

        private void ExecutablePathBox_LostFocus(object sender, RoutedEventArgs e) {
            if (settings == null) return;
            settings.ExecutablePath = ((TextBox)sender).Text.Trim();
        }

        private void ArgumentsBox_LostFocus(object sender, RoutedEventArgs e) {
            if (settings == null) return;
            settings.Arguments = ((TextBox)sender).Text.Trim();
        }

        private void PlotHistoryHoursBox_LostFocus(object sender, RoutedEventArgs e) {
            if (settings == null) return;

            var box = (TextBox)sender;
            var text = box.Text.Trim();
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var hours)
                && hours >= 0.25
                && hours <= 24) {
                settings.PlotHistoryHours = hours;
                box.Text = hours.ToString("0.##", CultureInfo.InvariantCulture);
                return;
            }

            box.Text = settings.PlotHistoryHours.ToString("0.##", CultureInfo.InvariantCulture);
        }
    }
}
