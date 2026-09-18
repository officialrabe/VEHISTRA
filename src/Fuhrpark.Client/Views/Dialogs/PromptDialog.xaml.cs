using System.Windows;

namespace Fuhrpark.Client.Views.Dialogs;

/// <summary>Einfache Freitexteingabe, z. B. fuer die Angabe eines Grundes.</summary>
public partial class PromptDialog : Window
{
    public PromptDialog()
    {
        InitializeComponent();
    }

    public string? Value { get; private set; }

    public void Initialize(string title, string message, string? initialValue)
    {
        Title = title;
        MessageText.Text = message;
        InputBox.Text = initialValue ?? string.Empty;
        InputBox.Focus();
        InputBox.SelectAll();
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        Value = InputBox.Text;
        DialogResult = true;
    }
}
