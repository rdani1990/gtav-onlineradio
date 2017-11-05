using RadioExpansion.Core.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using RadioExpansion.Core;
using System.Windows.Controls;
using System.Windows;

namespace RadioExpansion.Editor
{
    public class TextBoxLogger : ILogger
    {
        private TextBox _textBox;

        public TextBoxLogger(TextBox textBox)
        {
            _textBox = textBox;
        }

        public void Close() { }

        public void Log(string line, params object[] args)
        {
            DispatchIfNecessary(() =>
            {
                if (!String.IsNullOrEmpty(_textBox.Text))
                {
                    _textBox.AppendText(Environment.NewLine);
                }

                _textBox.AppendText(String.Format(line, args));
            });
        }

        public void DispatchIfNecessary(Action action)
        {
            if (!Application.Current.Dispatcher.CheckAccess())
                Application.Current.Dispatcher.Invoke(action);
            else
                action.Invoke();
        }

        public void LogTrack(string radio, MetaData track)
        {
            throw new NotSupportedException();
        }
    }
}
