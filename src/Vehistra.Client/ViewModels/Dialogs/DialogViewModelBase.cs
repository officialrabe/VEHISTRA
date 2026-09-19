using CommunityToolkit.Mvvm.Input;

namespace Vehistra.Client.ViewModels.Dialogs;

/// <summary>Basisklasse aller modalen Bearbeitungsdialoge.</summary>
public abstract partial class DialogViewModelBase : ViewModelBase
{
    /// <summary>Wird ausgeloest, wenn der Dialog geschlossen werden soll.</summary>
    public event EventHandler<bool>? CloseRequested;

    /// <summary>Beschriftung der Bestaetigungsschaltflaeche.</summary>
    public virtual string ConfirmButtonText => "Speichern";

    [RelayCommand]
    private async Task ConfirmAsync()
    {
        if (await SaveAsync().ConfigureAwait(true))
        {
            CloseRequested?.Invoke(this, true);
        }
    }

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke(this, false);

    /// <summary>Speichert die Eingaben. Rueckgabe <c>false</c> laesst den Dialog geoeffnet.</summary>
    protected abstract Task<bool> SaveAsync();

    protected void Close(bool result) => CloseRequested?.Invoke(this, result);
}
