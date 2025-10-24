using Content.Shared._Floof.LoadoutsAndTraits.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared.Clothing.Loadouts.Prototypes;


/// <summary>
///     A prototype defining a valid category for <see cref="LoadoutPrototype"/>s to go into.
/// </summary>
[Prototype]
public sealed partial class LoadoutCategoryPrototype : IPrototype, IRecursivePrototypeCategory<LoadoutCategoryPrototype, LoadoutPrototype> // Floof - implement IRecursivePrototypeCategory
{
    [IdDataField]
    public string ID { get; } = default!;

    [DataField]
    public bool Root { get; }

    [DataField]
    public List<ProtoId<LoadoutCategoryPrototype>> SubCategories { get; } = new();
}
