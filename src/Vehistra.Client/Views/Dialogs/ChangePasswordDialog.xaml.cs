using System.Windows;
using System.Windows.Controls;
using Vehistra.Client.ViewModels.Dialogs;

namespace Vehistra.Client.Views.Dialogs;

/// <summary>
/// Dialog zur Passwortaenderung. Passwoerter werden nicht an das Ansichtsmodell gebunden,
/// sondern nur bei Eingabe uebergeben.
/// </summary>
public partial class ChangePasswordDialog : UserControl
{
    public ChangePasswordDialog()
    {
        InitializeComponent();
    }

    private ChangePasswordViewModel? ViewModel => DataContext as ChangePasswordViewModel;

    private void OnCurrentPasswordChanged(object sender, RoutedEventArgs e) =>
        ViewModel?.SetCurrentPassword(CurrentPasswordBox.Password);

    private void OnNewPasswordChanged(object sender, RoutedEventArgs e) =>
        ViewModel?.SetNewPassword(NewPasswordBox.Password);

    private void OnRepeatPasswordChanged(object sender, RoutedEventArgs e) =>
        ViewModel?.SetRepeatPassword(RepeatPasswordBox.Password);
}
