using System.ComponentModel.Composition;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using RATrendCompensatorNINA.ViewModels;

namespace RATrendCompensatorNINA.Views {

    [Export(typeof(ResourceDictionary))]
    public partial class RaTrendDockableView : ResourceDictionary {
        public RaTrendDockableView() {
            InitializeComponent();
        }

        private void RaErrorPlotBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
            if (e.ClickCount < 2) {
                return;
            }

            if (sender is FrameworkElement element && element.DataContext is RaTrendDockableVM vm) {
                vm.ClearRaErrorPlot();
                e.Handled = true;
            }
        }
    }
}
