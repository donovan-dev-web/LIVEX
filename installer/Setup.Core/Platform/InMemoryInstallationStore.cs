// SPDX-License-Identifier: GPL-3.0-or-later
using Setup.Core.Lifecycle;

namespace Setup.Core.Platform;

/// <summary>
/// Enregistrement d'installation en mémoire : aucun registre, aucun fichier.
/// Utilisé par le mode démonstration et par les tests.
/// </summary>
public sealed class InMemoryInstallationStore : IInstallationStore
{
    private InstallationRecord? _record;

    public InMemoryInstallationStore(InstallationRecord? seed = null) => _record = seed;

    public string StorePath => "(mémoire)";

    public InstallationRecord? Read() => _record;

    public void Write(InstallationRecord record) => _record = record;

    public void Delete() => _record = null;
}
