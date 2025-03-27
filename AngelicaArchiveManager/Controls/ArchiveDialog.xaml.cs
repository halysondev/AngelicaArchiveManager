using AngelicaArchiveManager.Controls.CustomFileDialog;
using AngelicaArchiveManager.Core.ArchiveEngine;
using System.Windows.Controls;
using System.Windows;

namespace AngelicaArchiveManager.Controls
{
    public partial class ArchiveDialog : ControlAddOnBase
    {
        private CheckBox UseSpecificKeyCheckBox;

        public ArchiveKey Key
        {
            get => Settings.Keys[ArchiveType.SelectedIndex];
        }

        public bool UseSpecificKey
        {
            get => UseSpecificKeyCheckBox?.IsChecked ?? false;
        }

        public ArchiveDialog()
        {
            InitializeComponent();
            foreach (var key in Settings.Keys)
                ArchiveType.Items.Add(key.Name);
            ArchiveType.SelectedIndex = 0;

            // Criar o CheckBox para escolher entre usar chave específica ou tentar todas
            UseSpecificKeyCheckBox = new CheckBox
            {
                Content = "Usar chave específica",
                Margin = new Thickness(5, 10, 5, 0),
                IsChecked = true // Por padrão, usar chave específica
            };

            // Adicionar o CheckBox ao layout existente
            if (Content is Grid mainGrid && mainGrid.Children.Count > 0)
            {
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                Grid.SetRow(UseSpecificKeyCheckBox, mainGrid.RowDefinitions.Count - 1);
                mainGrid.Children.Add(UseSpecificKeyCheckBox);
            }

            // Atualizar visibilidade do ComboBox com base na seleção
            UseSpecificKeyCheckBox.Checked += (s, e) => 
            {
                if (ArchiveType != null)
                    ArchiveType.IsEnabled = true;
            };

            UseSpecificKeyCheckBox.Unchecked += (s, e) => 
            {
                if (ArchiveType != null)
                    ArchiveType.IsEnabled = false;
            };
        }
    }
}
