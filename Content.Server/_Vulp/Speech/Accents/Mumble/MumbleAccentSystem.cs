using System.Text;
using Content.Server.Chat.Systems;
using Content.Shared._Vulp.Speech.Accents.Mumble;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;


namespace Content.Server._Vulp.Speech.Accents.Mumble;


public sealed class MumbleAccentSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _protoMan = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        // Can we all just agree to burn the event bus down? I have to subscribe to this broadcasted event instead of AccentGetEvent
        // because making sure this is applied after every other accept system is basically impossible
        SubscribeLocalEvent<TransformSpeechEvent>(OnTransformSpeech);
    }

    private void OnTransformSpeech(TransformSpeechEvent args)
    {
        // Note to self: TransformSpeechEvent is not raised if the language doesn't require speech
        if (!TryComp<MumbleAccentComponent>(args.Sender, out var accent))
            return;

        OnTransformSpeech((args.Sender, accent), ref args);
    }

    private void OnTransformSpeech(Entity<MumbleAccentComponent> ent, ref TransformSpeechEvent args)
    {
        if (ent.Comp.AccentPrototype == null && !LoadAccent(ent) || ent.Comp.AccentPrototype is not { Initialized: true } accent)
            return;

        // The code below doesn't preserve case anyway, so why bother?
        args.Message = args.Message.ToLower();

        var i = 0;
        var result = new StringBuilder(args.Message.Length);
        while (i < args.Message.Length)
        {
            // Try to find a suitable replacement
            start:
            var maxSubstr = Math.Min(args.Message.Length - i, accent.MaxCharacterLength);
            for (var substrLen = maxSubstr; substrLen > 0; substrLen--)
            {
                var span = args.Message.AsMemory(i, substrLen);
                var lookup = accent.Lookups![substrLen - 1];
                if (lookup.TryGetValue(span, out var replacement))
                {
                    result.Append(replacement);
                    i += substrLen;
                    goto start;
                }
            }

            // No replacement, use the original character
            var c = args.Message[i];
            var isLetter = char.IsLetter(c);
            if (isLetter && _random.Prob(accent.DropChance))
                continue;

            result.Append(c);
            if (isLetter && _random.Prob(accent.DoubleChance))
                result.Append(c);

            i++;
        }

        args.Message = _chat.SanitizeMessageCapital(result.ToString());
    }

    /// <summary>
    ///     Sets mumble accent on the entity. If accent is null, removes it instead.
    /// </summary>
    /// <returns>True if accent was set or removed successfully, false otherwise.</returns>
    public bool SetAccent(Entity<MumbleAccentComponent?> ent, MumbleAccentPrototype? accent)
    {
        if (accent == null)
        {
            if (!Resolve(ent, ref ent.Comp, false))
                return false;

            RemCompDeferred(ent, ent.Comp);
            return true;
        }

        if (ent.Comp == null)
            ent.Comp = EnsureComp<MumbleAccentComponent>(ent);

        ent.Comp.Accent = accent;
        return LoadAccent(ent!);
    }

    private bool LoadAccent(Entity<MumbleAccentComponent> ent)
    {
        if (!_protoMan.TryIndex(ent.Comp.Accent, out var accent))
            return false;

        ent.Comp.AccentPrototype = accent;
        if (!accent.Initialized)
            InitializePrototype(accent);

        return true;
    }

    public void InitializePrototype(MumbleAccentPrototype accent)
    {
        accent.Lookups?.Clear();
        accent.Lookups ??= new();

        for (var i = 0; i < accent.MaxCharacterLength; i++)
        {
            var dict = accent.Replacements[i];
            accent.Replacements[i] = new(dict, new StringSpanComparer());
            accent.Lookups.Add(accent.Replacements[i].GetAlternateLookup<ReadOnlyMemory<char>>());
        }

        accent.Initialized = true;
    }

    // Why is this not part of C#, RobustToolbox, or anything else? This is so useful.
    private struct StringSpanComparer : IEqualityComparer<string>, IAlternateEqualityComparer<ReadOnlyMemory<char>, string>
    {
        public bool Equals(string? x, string? y) => x == y;
        public bool Equals(ReadOnlyMemory<char> alternate, string other) => alternate.Span.SequenceEqual(other);
        public int GetHashCode(string obj) => obj.GetHashCode();
        public int GetHashCode(ReadOnlyMemory<char> alternate) => string.GetHashCode(alternate.Span);
        // This is only called in edge cases like when throwing an exception or doing GetOrNew so it's fine
        public string Create(ReadOnlyMemory<char> alternate) => new(alternate.ToArray());
    }
}
