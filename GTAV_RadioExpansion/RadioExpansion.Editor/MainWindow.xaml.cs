using RadioExpansion.Core;
using RadioExpansion.Core.Logging;
using RadioExpansion.Core.RadioPlayers;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
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
            var configManager = new RadioConfigManager();
            var radios = configManager.LoadRadios();

            DataContext = radios;
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

    public class PercentValidationRule : ValidationRule
    {
        private const ushort UpperLimit = 1000;

        public override ValidationResult Validate(object value, CultureInfo cultureInfo)
        {
            if (IsValid(value))
            {
                return ValidationResult.ValidResult;
            }
            else
            {
                return new ValidationResult(false, $"Illegal value, please enter a non-negative number, which is less than {UpperLimit}!");
            }
        }

        public static bool IsValid(object value)
        {
            ushort parsedResult;
            return UInt16.TryParse((string)value, out parsedResult) && parsedResult < UpperLimit;
        }
    }

    public class PercentConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return ((float)value * 100);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (Single.Parse((string)value) / 100);
        }
    }
}
