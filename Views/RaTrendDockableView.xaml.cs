using System.ComponentModel.Composition;
using System.Windows;

namespace RATrendCompensatorNINA.Views {

    [Export(typeof(ResourceDictionary))]
    public partial class RaTrendDockableView : ResourceDictionary {
        public RaTrendDockableView() {
            InitializeComponent();
        }
    }
}
