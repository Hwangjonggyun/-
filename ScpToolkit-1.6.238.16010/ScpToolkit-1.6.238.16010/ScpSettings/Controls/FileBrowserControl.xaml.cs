using System.Windows;
using System.Windows.Controls;
using AutoDependencyPropertyMarker;
using Ookii.Dialogs.Wpf;

namespace ScpSettings.Controls
{
    /// <summary>
    ///     Interaction logic for FileBrowserControl.xaml
    /// </summary>
    public partial class FileBrowserControl : UserControl
    {
        public FileBrowserControl()
        {
            InitializeComponent();

            MainGrid.DataContext = this;
        }

        [AutoDependencyProperty(Options = FrameworkPropertyMetadataOptions.BindsTwoWayByDefault)]
        public string FilePath { get; set; }

        [AutoDependencyProperty(Options = FrameworkPropertyMetadataOptions.BindsTwoWayByDefault)]
        public bool IsSoundEnabled { get; set; }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var fileBrowser = new VistaOpenFileDialog
            {
                CheckFileExists = true,
                CheckPathExists = true,
                Filter = Properties.Resources.SupportedAudioFilesFilter
            };

            if (fileBrowser.ShowDialog() != true) return;

            FilePath = fileBrowser.FileName;
        }
    }
}