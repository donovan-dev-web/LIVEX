// SPDX-License-Identifier: GPL-3.0-or-later
using System.Windows.Input;

namespace Setup.App;

public sealed class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

    public void Execute(object? parameter) => _execute();

    public event EventHandler? CanExecuteChanged;

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

public sealed class RelayCommand<T> : ICommand
{
    private readonly Action<T?> _execute;
    private readonly Func<T?, bool>? _canExecute;

    public RelayCommand(Action<T?> execute, Func<T?, bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke(Convert(parameter)) ?? true;

    public void Execute(object? parameter) => _execute(Convert(parameter));

    public event EventHandler? CanExecuteChanged;

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);

    private static T? Convert(object? parameter)
    {
        if (parameter is T typed)
        {
            return typed;
        }

        if (parameter is string text && typeof(T).IsEnum && Enum.TryParse(typeof(T), text, true, out var parsed))
        {
            return (T?)parsed;
        }

        return default;
    }
}
