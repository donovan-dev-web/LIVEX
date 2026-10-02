using System.Windows.Input;

namespace Launcher.Presentation.ViewModel;

/// <summary>Commande relais typée, suffisante pour la navigation et les actions de carte.</summary>
public sealed class RelayCommand<T> : ICommand
{
    private readonly Action<T> _execute;
    private readonly Func<T, bool>? _canExecute;

    /// <summary>Initialise la commande avec son action.</summary>
    public RelayCommand(Action<T> execute, Func<T, bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    /// <inheritdoc />
    public event EventHandler? CanExecuteChanged
    {
        add { CommandManager.RequerySuggested += value; }
        remove { CommandManager.RequerySuggested -= value; }
    }

    /// <inheritdoc />
    public bool CanExecute(object? parameter) => parameter is T value && (_canExecute?.Invoke(value) ?? true);

    /// <inheritdoc />
    public void Execute(object? parameter)
    {
        if (parameter is T value)
        {
            _execute(value);
        }
    }
}

/// <summary>Réinterrogation de CanExecute, sans dépendance au médiateur Avalonia.</summary>
public static class CommandManager
{
    /// <summary>Événement levé quand l'interface doit réévaluer CanExecute.</summary>
    public static event EventHandler? RequerySuggested
    {
        add { _requerySuggested += value; }
        remove { _requerySuggested -= value; }
    }

    private static EventHandler? _requerySuggested;

    /// <summary>Force la réévaluation des commandes.</summary>
    public static void InvalidateRequerySuggested() => _requerySuggested?.Invoke(null, EventArgs.Empty);
}
