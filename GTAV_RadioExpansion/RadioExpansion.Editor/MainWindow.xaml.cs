using RadioExpansion.Core;
using RadioExpansion.Core.Logging;
using RadioExpansion.Core.RadioPlayers;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace RadioExpansion.Editor
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            Logger.SetLogger(new TextBoxLogger(_logTextBox));

            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // copy the border of the log panel to the scrollview
            var scrollViewBorder = (Border)((ScrollViewer)_logTextBox.Parent).Parent;
            scrollViewBorder.BorderBrush = _logTextBox.BorderBrush;
            scrollViewBorder.BorderThickness = _logTextBox.BorderThickness;
            _logTextBox.BorderThickness = new Thickness(0); // textbox doesn't need it anymore

            Task.Run(() =>
            {
                var radios = RadioConfigManager.LoadRadios();

                Dispatcher.Invoke(() => DataContext = radios);
            });
        }

        private void ButtonGoToDirectory_Click(object sender, RoutedEventArgs e)
        {
            var selectedRadio = (Radio)_listRadios.SelectedItem;

            if (selectedRadio != null)
            {
                Directory.CreateDirectory(selectedRadio.AbsoluteDirectoryPath);
                Process.Start(selectedRadio.AbsoluteDirectoryPath);
            }
        }
        
        private void TextBoxVolume_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !PercentValidationRule.IsValid(e.Text); // accept only non-negative numbers
        }
    }
}
