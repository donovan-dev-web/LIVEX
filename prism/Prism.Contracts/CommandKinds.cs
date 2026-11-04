namespace Prism.Contracts;

/// <summary>
/// Canonical command vocabulary of the PRISM contract. Dispatch, the published JSON schema and the
/// contract tests share this list so a new kind cannot ship without its schema entry and matrix case.
/// </summary>
public static class CommandKinds
{
    public static readonly IReadOnlyList<string> All =
    [
        "Incarner",
        "Retirer",
        "Aller",
        "Miner",
        "Labourer",
        "Recolter",
        "Poser",
        "Fabriquer",
        "FabriquerMacro",
        "Planter",
        "Consommer",
        "Transferer",
        "Prendre",
        "Deposer",
        "DefinirAccesConteneur",
        "DemarrerChantier",
        "Contribuer",
        "DefinirAcces",
        "ObtenirObjet",
        "Cultiver",
        "Annuler",
    ];
}
