using Robust.Shared.Prototypes;


namespace Content.Shared._Vulp.Speech.Accents.Mumble;


[Prototype("mumbleAccent")]
public sealed class MumbleAccentPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; } = default!;

    /// <summary>
    ///     A list where the nth entry contains replacements for char sequences that are exactly n characters long.
    ///     E.g. the 1st entry must contain replacements like "a" -> "foo". The 2nd is like "ab" -> "bar". The 3rd is "abc" -> "baz". And so on.
    ///     This is done to achieve roughly O(1) lookup time instead of O(n), where n is the number of replacements.
    /// </summary>
    [DataField(required: true)]
    public List<Dictionary<string, string>> Replacements = default!;

    /// <summary>
    ///     If there's no replacement for a sequence, this is the chance to duplicate or drop the said character.
    /// </summary>
    [DataField]
    public float DoubleChance = 0f, DropChance = 0f;

    /// <summary>
    ///     How many db to add to the volume of emote sounds.
    /// </summary>
    [DataField]
    public float EmoteVolume = 0f;


    /// <summary>
    ///     Whether this accent has been initialized. Do not modify directly, call MumbleAccentSystem.InitializePrototype!
    /// </summary>
    [NonSerialized]
    public bool Initialized = false;

    /// <summary>
    ///     Initialized on demand by the system, this provides a lookup for each replacement in <see cref="Replacements"/>.
    /// </summary>
    [NonSerialized]
    public List<Dictionary<string, string>.AlternateLookup<ReadOnlyMemory<char>>>? Lookups = null;

    public int MaxCharacterLength => Replacements.Count;
}
