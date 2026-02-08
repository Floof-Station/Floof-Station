using Content.Server.DeltaV.EnvyClone.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server.DeltaV.EnvyClone.Components;

/// <summary>
/// Creates a random paradox anomaly and tranfers mind to it when taken by a player.
/// </summary>
[RegisterComponent, Access(typeof(EnvyCloneSystem))]
public sealed partial class EnvyCloneSpawnerComponent : Component
{
    [DataField]
    public HashSet<string> CopiedComponents = new()
    {
        "Damage",
        "DetailExaminable",
        "Dna",
        "Fingerprint",
        "LanguageSpeaker",
        "Psionic",
        "Scent"
    };
}
