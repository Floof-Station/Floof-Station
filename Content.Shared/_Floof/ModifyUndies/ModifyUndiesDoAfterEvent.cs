using Content.Shared.DoAfter;
using Content.Shared.Humanoid.Markings;
using Robust.Shared.Serialization;

namespace Content.Shared.FloofStation.ModifyUndies;

[Serializable, NetSerializable]
public sealed partial class ModifyUndiesDoAfterEvent : SimpleDoAfterEvent
{
    /// <summary>
    ///     The marking prototype that is being modified.
    /// </summary>
    [DataField(required: true)]
    public Marking Marking;

    /// <summary>
    ///     Localized string for the marking prototype.
    /// </summary>
    [DataField(required: true)]
    public string MarkingPrototypeName;

    /// <summary>
    ///     Whether or not the marking is visible at the moment.
    /// </summary>
    [DataField(required: true)]
    public bool IsVisible;

    public ModifyUndiesDoAfterEvent(Marking marking, string markingPrototypeName, bool isVisible)
    {
        Marking = marking;
        MarkingPrototypeName = markingPrototypeName;
        IsVisible = isVisible;
    }
}
