// SPDX-License-Identifier: GPL-3.0-or-later
namespace Setup.Core.Acquisition;

/// <summary>
/// Un actif facultatif n'a pas pu être obtenu : réseau absent, 404, empreinte
/// non conforme, ou <c>--no-network</c> sans copie en cache ni dans le
/// répertoire d'actifs. Les modes non interactifs la traduisent en code de
/// sortie <c>6</c> (INS-F-95, INS-F-99) ; l'assistant l'affiche comme un
/// avertissement, jamais comme un échec du socle.
/// </summary>
public sealed class OptionalAssetUnavailableException : Exception
{
    public OptionalAssetUnavailableException(string message)
        : base(message)
    {
    }

    public OptionalAssetUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
