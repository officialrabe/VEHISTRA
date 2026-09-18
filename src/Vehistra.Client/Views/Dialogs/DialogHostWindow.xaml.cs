using System.Windows;
using Vehistra.Client.ViewModels.Dialogs;

namespace Vehistra.Client.Views.Dialogs;

/// <summary>
/// Allgemeines Fenster fuer modale Bearbeitungsdialoge. Die konkrete Ansicht wird
/// ueber die DataTemplates aus dem Ansichtsmodell abgeleitet.
/// </summary>
public partial class DialogHostWindow : Window
{
    public DialogHostWindow()
    {
        InitializeComponent();
    }

    public void Initialize(object viewModel)
    {
        DataContext = viewModel;

        if (viewModel is DialogViewModelBase dialog)
        {
            Title = dialog.Title;
            dialog.CloseRequested += OnCloseRequested;
        }
    }

    private void OnCloseRequested(object? sender, bool result)
    {
        if (sender is DialogViewModelBase dialog)
        {
            dialog.CloseRequested -= OnCloseRequested;
        }

        DialogResult = result;
        Close();
    }
}
