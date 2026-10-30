namespace Setup.Core.Distribution;

/// <summary>
/// Signature du manifeste (Ed25519).
/// </summary>
public sealed record SignatureDescriptor(
    string Algorithm,
    string KeyId,
    string Value);