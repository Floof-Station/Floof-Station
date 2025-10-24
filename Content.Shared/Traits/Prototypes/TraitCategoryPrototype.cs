using Content.Shared._Floof.LoadoutsAndTraits.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared.Traits;


/// <summary>
///     A prototype defining a valid category for <see cref="TraitPrototype"/>s to go into.
/// </summary>
[Prototype("traitCategory")]
public sealed partial class TraitCategoryPrototype : IPrototype, IRecursivePrototypeCategory<TraitCategoryPrototype, TraitPrototype> // Floof - implement IRecursivePrototypeCategory
{
    [IdDataField]
    public string ID { get; } = default!;

    [DataField]
    public bool Root { get; }

    [DataField]
    public List<ProtoId<TraitCategoryPrototype>> SubCategories { get; } = new();
}
