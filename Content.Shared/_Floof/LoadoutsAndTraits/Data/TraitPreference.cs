using Content.Shared.Traits;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;


namespace Content.Shared._Floof.LoadoutsAndTraits.Data;


/// <summary>
///     Because EE didn't bother.
/// </summary>
[Serializable, NetSerializable, DataDefinition]
public sealed partial class TraitPreference
{
    [DataField] public ProtoId<TraitPrototype> Prototype;
    [DataField] public bool Selected;

    public TraitPreference(ProtoId<TraitPrototype> prototype, bool selected = true)
    {
        Prototype = prototype;
        Selected = selected;
    }

    public TraitPreference(TraitPreference other) : this(other.Prototype, other.Selected) { }

    /// <summary>
    ///     For compatibility with EE code.
    /// </summary>
    public static implicit operator string(TraitPreference pref) => pref.Prototype;

    public override bool Equals(object? obj)
    {
        if (obj is not TraitPreference other)
            return false;

        return Prototype == other.Prototype && Selected == other.Selected;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Prototype, Selected);
    }
}
