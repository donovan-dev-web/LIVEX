// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Avalonia.Logging;

namespace Setup.App.Diagnostics;

/// <summary>Journal de diagnostic Avalonia vers la console (liaisons manquantes, erreurs XAML).</summary>
public sealed class ConsoleLogSink : ILogSink
{
    public bool IsEnabled(LogEventLevel level, string area) => level >= LogEventLevel.Warning;

    public void Log(LogEventLevel level, string area, object? source, string messageTemplate) =>
        Console.Error.WriteLine($"[{level}] {area}: {messageTemplate}");

    public void Log(LogEventLevel level, string area, object? source, string messageTemplate, params object?[] propertyValues) =>
        Console.Error.WriteLine($"[{level}] {area}: {string.Format(messageTemplate, propertyValues)}");
}
