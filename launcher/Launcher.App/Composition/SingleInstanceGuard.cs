// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (LAUNCHER)
// Auteur : CHARTRAIN Donovan
// Rôle : Instance unique : un seul Launcher opère un espace de travail à la fois ; le second le dit explicitement
//        au lieu d'échouer en silence.

namespace Launcher.App.Composition;

/// <summary>
/// Instance unique : un seul Launcher opère un espace de
/// travail à la fois ; le second le dit explicitement au lieu d'échouer en silence.
/// </summary>
public static class SingleInstanceGuard
{
    private static Mutex? _mutex;

    /// <summary>Tente d'acquérir le mutex d'instance unique.</summary>
    public static bool TryAcquire(out IDisposable guard)
    {
        _mutex = new Mutex(initiallyOwned: true, @"Local\livex-launcher-instance", out var createdNew);
        if (createdNew)
        {
            guard = new Guard(_mutex);
            return true;
        }

        _mutex.Dispose();
        _mutex = null;
        guard = new NoOpGuard();
        return false;
    }

    private sealed class Guard : IDisposable
    {
        private readonly Mutex _mutex;

        public Guard(Mutex mutex)
        {
            _mutex = mutex;
        }

        public void Dispose() => _mutex.Dispose();
    }

    private sealed class NoOpGuard : IDisposable
    {
        public void Dispose()
        {
        }
    }
}
