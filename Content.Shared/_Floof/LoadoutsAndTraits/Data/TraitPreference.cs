using System.Text;
using Content.Shared.Traits;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;


namespace Content.Shared._Floof.LoadoutsAndTraits.Data;


/// <summary>
///     Because EE didn't bother.
/// </summary>
[Serializable, NetSerializable, DataDefinition]
public sealed partial class TraitPreference : ISelfSerialize
{
    // IMPORTANT NOTE: if you are going to add new fields to this, you are going to have to modify the db model.
    // If you are going to modify the DB model, you will have to create a migration as the server still stores traits as a List<string>
    [DataField] public ProtoId<TraitPrototype> Prototype;
    [DataField] public bool Selected = true;

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

    // This is to preserve backwards compatibility with profiles created in older versions.
    // We serialize the contents of this trait as if it was a JSON object (which makes for a valid YAML dictionary)
    // This will MAYBE allow us to later transition away from ISelfSerialize
    public void Deserialize(string value)
    {
        value = value.Trim();
        if (!value.StartsWith('{') && !value.Contains(','))
        {
            // This may be the old format, where the value is just the prototype ID.
            Prototype = value;
            Selected = true;
            return;
        }

        if (value.StartsWith('{'))
            value = value.Substring(1);
        if (value.EndsWith('}'))
            value = value.Substring(0, value.Length - 1);

        var parts = value.Split(',');
        foreach (var part in parts)
        {
            var delimiter = part.IndexOf(':');
            if (delimiter == -1)
            {
                Logger.GetSawmill("traits").Error($"Failed to deserialize trait preference: {part}. Assuming it's the prototype ID.");
                Prototype = part;
                continue;
            }

            var partKey = part.Substring(0, delimiter).Trim();
            var partValue = part.Substring(delimiter + 1).Trim();
            switch (partKey)
            {
                case "Prototype":
                    Prototype = partValue;
                    break;
                case "Selected":
                    Selected = bool.Parse(partValue);
                    break;
                default:
                    Logger.GetSawmill("traits").Error($"Failed to deserialize trait preference: {part}.");
                    break;
            }
        }
    }

    public string Serialize()
    {
        var builder = new StringBuilder();
        builder.Append('{');
        builder.Append($"Prototype: {Prototype}");
        if (Selected != true) // Only serialize if different from default
        {
            builder.Append(',');
            builder.Append($"Selected: {Selected}");
        }
        builder.Append('}');
        return builder.ToString();
    }
}
