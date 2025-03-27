using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace AngelicaArchiveManager.Controls.CustomFileDialog
{
    public class InputDialog : Window
    {
        private readonly StackPanel _mainPanel;
        private readonly Dictionary<string, object> _controls = new Dictionary<string, object>();

        public InputDialog()
        {
            Width = 400;
            Height = 250;
            SizeToContent = SizeToContent.Height;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;
            
            var grid = new Grid();
            Content = grid;
            
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            
            _mainPanel = new StackPanel { Margin = new Thickness(10) };
            grid.Children.Add(_mainPanel);
            Grid.SetRow(_mainPanel, 0);
            
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(10),
                Height = 30
            };
            
            var okButton = new Button
            {
                Content = "OK",
                Width = 75,
                Margin = new Thickness(0, 0, 10, 0),
                IsDefault = true
            };
            okButton.Click += (s, e) => { DialogResult = true; };
            
            var cancelButton = new Button
            {
                Content = "Cancel",
                Width = 75,
                IsCancel = true
            };
            
            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);
            
            grid.Children.Add(buttonPanel);
            Grid.SetRow(buttonPanel, 1);
        }
        
        public TextBox AddTextBox(string key, string label, string defaultValue = "")
        {
            var panel = new DockPanel { Margin = new Thickness(0, 5, 0, 5) };
            var textLabel = new Label { Content = label, Width = 120 };
            var textBox = new TextBox 
            { 
                Text = defaultValue,
                VerticalContentAlignment = VerticalAlignment.Center,
                Height = 24
            };
            
            DockPanel.SetDock(textLabel, Dock.Left);
            panel.Children.Add(textLabel);
            panel.Children.Add(textBox);
            
            _mainPanel.Children.Add(panel);
            _controls[key] = textBox;
            
            return textBox;
        }
        
        public ComboBox AddComboBox(string key, string label, List<string> items)
        {
            var panel = new DockPanel { Margin = new Thickness(0, 5, 0, 5) };
            var comboLabel = new Label { Content = label, Width = 120 };
            var comboBox = new ComboBox 
            { 
                ItemsSource = items,
                VerticalContentAlignment = VerticalAlignment.Center,
                Height = 24
            };
            
            DockPanel.SetDock(comboLabel, Dock.Left);
            panel.Children.Add(comboLabel);
            panel.Children.Add(comboBox);
            
            _mainPanel.Children.Add(panel);
            _controls[key] = comboBox;
            
            return comboBox;
        }
        
        public string GetTextBoxValue(string key)
        {
            if (_controls.ContainsKey(key) && _controls[key] is TextBox textBox)
            {
                return textBox.Text;
            }
            return string.Empty;
        }
        
        public int GetComboBoxSelectedIndex(string key)
        {
            if (_controls.ContainsKey(key) && _controls[key] is ComboBox comboBox)
            {
                return comboBox.SelectedIndex;
            }
            return -1;
        }
        
        public string GetComboBoxSelectedValue(string key)
        {
            if (_controls.ContainsKey(key) && _controls[key] is ComboBox comboBox)
            {
                return comboBox.SelectedItem?.ToString();
            }
            return string.Empty;
        }
    }
} 