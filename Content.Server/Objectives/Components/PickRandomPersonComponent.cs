using Content.Server.Objectives.Systems;

namespace Content.Server.Objectives.Components;

/// <summary>
/// Sets the target for <see cref="TargetObjectiveComponent"/> to a random person.
/// </summary>
[RegisterComponent, Access(typeof(KillPersonConditionSystem))]
public sealed partial class PickRandomPersonComponent : Component
{
    //Floofstation Target Consent Traits: Start
    [DataField]
    public ObjectiveTypes ObjectiveType;
    //Floofstation Target Consent Traits: End
    [DataField]
    public bool NeedsOrganic; // Goobstation: Only pick non-silicon players.
}

//Floofstation Target Consent Traits: Start
[Flags]
public enum ObjectiveTypes
{
    Unspecified = 0,
    TraitorKill = 1 << 0,
    TraitorTeach = 1 << 1
}
//Floofstation Target Consent Traits: End
