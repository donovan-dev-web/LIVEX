namespace Setup.Core.Distribution;

/// <summary>
/// Signature optionnelle du manifeste (réservée à un usage futur ; non vérifiée en V1).
/// </summary>
public sealed record SignatureDescriptor(
    string Algorithm,
    string KeyId,
    string Value);