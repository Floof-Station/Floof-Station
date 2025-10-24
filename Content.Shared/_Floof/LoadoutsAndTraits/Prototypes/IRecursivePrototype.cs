using Robust.Shared.Prototypes;


namespace Content.Shared._Floof.LoadoutsAndTraits.Prototypes;

/// <summary>
///     A prototype that can be placed into a category of type <typeparamref name="TCat"/>.
/// </summary>
/// <typeparam name="TSelf">This type</typeparam>
/// <typeparam name="TCat">Category type</typeparam>
public interface IRecursivePrototype<TCat, TSelf> : IPrototype
    where TCat : class, IRecursivePrototypeCategory<TCat, TSelf>, IPrototype
    where TSelf : class, IRecursivePrototype<TCat, TSelf>, IPrototype
{
    ProtoId<TCat> Category { get; }
}

/// <summary>
///     A category containing more of itself and prototypes of type <typeparamref name="TProto"/>.
/// </summary>
/// <typeparam name="TSelf">Type of this category</typeparam>
/// <typeparam name="TProto">Type of prototypes that can be placed into this category</typeparam>
public interface IRecursivePrototypeCategory<TSelf, TProto>
    where TSelf : class, IRecursivePrototypeCategory<TSelf, TProto>, IPrototype
    where TProto : class, IRecursivePrototype<TSelf, TProto>, IPrototype
{
    bool Root { get; }

    List<ProtoId<TSelf>> SubCategories { get; }
}
