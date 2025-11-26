using System.Windows;
using System.Windows.Media.Imaging;

namespace SEE_INSADE.UI.Dialogs
{
    public partial class HistogramWindow : Window
    {
        public HistogramWindow()
        {
            InitializeComponent();
        }

        public HistogramWindow(WriteableBitmap image) : this()
        {
            // Constructor that takes image parameter for compatibility
        }
    }
}