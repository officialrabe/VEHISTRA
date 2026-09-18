using System.Windows;
using System.Windows.Controls;
using Fuhrpark.Client.ViewModels.Dialogs;

namespace Fuhrpark.Client.Views.Dialogs;

/// <summary>Dialog fuer Benutzerkonten.</summary>
public partial class UserEditDialog : UserControl
{
    public UserEditDialog()
    {
        InitializeComponent();
    }

    private void OnPasswordChanged(object sender, RoutedEventArgs e) =>
        (DataContext as UserEditViewModel)?.SetPassword(PasswordBox.Password);
}
