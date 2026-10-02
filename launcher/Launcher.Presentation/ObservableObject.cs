using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Launcher.Presentation;

/// <summary>Base des vues modèles : notification de changement de propriété, sans dépendance externe.</summary>
public abstract class ObservableObject : INotifyPropertyChanged
{
    /// <summary>Levée quand une propriété change.</summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Levée manuelle pour une propriété calculée.</summary>
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    /// <summary>Affecte le champ si différent et notifie. Renvoie vrai si modifié.</summary>
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
