using Content.Shared.Traits;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;
using Content.Server.Body.Systems;
using Content.Server.Body.Components;
using Content.Shared.Body.Components;
using Content.Shared.Body.Prototypes;
using Content.Shared.HeightAdjust;
using System.Linq;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Silicon.Components;
using Content.Shared.Speech;


namespace Content.Server._Floof.Traits;

[UsedImplicitly]
public sealed partial class TraitModifyMetabolism : TraitFunction
{

    /// <summary>
    ///     List of entries to add (or remove) from the metabolizer types of the organ.
    /// </summary>
    [DataField, AlwaysPushInheritance]
    public HashSet<ProtoId<MetabolizerTypePrototype>> Types { get; private set; } = new();

    /// <summary>
    ///     List of metabolizer groups this should affect.
    ///     If empty, this will affect all metabolizer groups.
    /// </summary>
    [DataField, AlwaysPushInheritance]
    public HashSet<ProtoId<MetabolismGroupPrototype>>? Groups { get; private set; } = new();

    /// <summary>
    ///     If true, add these metabolizer types to the organ's metabolizer types.
    ///     Otherwise, remove them.
    /// </summary>
    [DataField, AlwaysPushInheritance]
    public bool Add = false;

    public override void OnPlayerSpawn(EntityUid uid,
        IComponentFactory factory,
        IEntityManager entityManager,
        ISerializationManager serializationManager)
    {
        if (!entityManager.TryGetComponent<BodyComponent>(uid, out var body))
            return;

        var bodySystem = entityManager.System<BodySystem>();

        if (bodySystem is null)
            return;

        if (!bodySystem.TryGetBodyOrganComponents<MetabolizerComponent>(uid, out var metabolizers, body))
            return;

        foreach (var (metabolizer, _) in metabolizers)
        {
            if (metabolizer.MetabolizerTypes is null
                || metabolizer.MetabolismGroups is null)
                continue;
            if (Groups == null || Groups.Count == 0)
                ApplyCulinaryAdaptation(metabolizer);
            // otherwise, if the metabolizer has any of the groups that the culinary adaptation metabolizer has, apply it
            else if (metabolizer.MetabolismGroups.Any(metabolismGroup => Groups.Contains(metabolismGroup.Id)))
                ApplyCulinaryAdaptation(metabolizer);
        }
    }

    /// <summary>
    ///     Apply the CulinaryAdaptation metabolizer type(s) this affected metabolizer!
    /// </summary>
    private void ApplyCulinaryAdaptation(MetabolizerComponent metabolizer)
    {
        foreach (var metabType in Types)
        {
            if (Add)
                metabolizer.MetabolizerTypes?.Add(metabType);
            else
                metabolizer.MetabolizerTypes?.Remove(metabType);
        }
    }
}

// Scales/modifies the size of the character using the Floofstation modified heightAdjustSystem function SetScale
public sealed partial class TraitSetScale : TraitFunction
{
    [DataField]
    public float scale;

    public override void OnPlayerSpawn(EntityUid uid,
        IComponentFactory factory,
        IEntityManager entityManager,
        ISerializationManager serializationManager)
    {
        entityManager.System<HeightAdjustSystem>().SetScale(uid, scale, restricted: false);
    }
}
/// <summary>
///     Used for traits that modify SiliconComponent.
/// </summary>
[UsedImplicitly]
public sealed partial class TraitModifySilicon : TraitFunction
{
    /// <summary>
    ///     Flat modifier to a silicon's battery drain per second.
    /// </summary>
    [DataField, AlwaysPushInheritance]
    public float BatteryDrainModifier;
    
    /// <summary>
    ///     Flat modifier to a silicon's flammability multiplier. This only affects the overheat mechanic, and
    ///     does not affect overall flammability.
    /// </summary>
    [DataField, AlwaysPushInheritance]
    public float FireStackModifier;
    
    /// <summary>
    ///     If true, should go along with a battery component. One will not be added automatically.
    /// </summary>
    [DataField, AlwaysPushInheritance]
    public bool? IsBatteryPowered;
    
    /// <summary>
    ///     Whether or not a Silicon will cancel all sleep events.
    ///     Maybe you want an android that can sleep as well as drink APCs? I'm not going to judge..
    /// </summary>
    [DataField, AlwaysPushInheritance]
    public bool? IsAbleToDreamOfElectricSheep;
    
    /// <summary>
    ///     The new percentages at which the silicon will enter each state.
    /// </summary>
    /// <example>
    ///     The Silicon will always be Full at 100%.
    ///     Setting Critical to 0 will cause the Silicon to never enter the dead state.
    ///     Unlike the ChargeThreshold variables in SiliconComponent.cs, it does not support null.
    /// </example>
    [DataField, AlwaysPushInheritance]
    public float? NewThresholdMid;
    
    /// <inheritdoc cref="NewThresholdMid"/>
    [DataField, AlwaysPushInheritance]
    public float? NewThresholdLow;
    
    /// <inheritdoc cref="NewThresholdMid"/>
    [DataField, AlwaysPushInheritance]
    public float? NewThresholdCritical;
    

    public override void OnPlayerSpawn(EntityUid uid,
        IComponentFactory factory,
        IEntityManager entityManager,
        ISerializationManager serializationManager)
    {
        if (!entityManager.TryGetComponent<SiliconComponent>(uid, out var siliconComponent))
            return;

        siliconComponent.DrainPerSecond += BatteryDrainModifier;
        siliconComponent.FireStackMultiplier += FireStackModifier;
        
        // M3739 - #1209 - Ideally, I would have wanted the possibility for null to be supplied by the YAML to the
        // battery threshold datafields, but as it turns out, it is easier said than done to do it properly.
        if (NewThresholdMid.HasValue)
        siliconComponent.ChargeThresholdMid = NewThresholdMid;
        
        if (NewThresholdLow.HasValue)
        siliconComponent.ChargeThresholdLow = NewThresholdLow;
        
        if (NewThresholdCritical.HasValue)
        siliconComponent.ChargeThresholdCritical = NewThresholdCritical;
        
        if (IsBatteryPowered.HasValue)
        siliconComponent.BatteryPowered = IsBatteryPowered.Value;
        
        if (IsAbleToDreamOfElectricSheep.HasValue)
        siliconComponent.DoSiliconsDreamOfElectricSheep = IsAbleToDreamOfElectricSheep.Value;
    }
}

/// <summary>
///     Used for traits that modify SpeechComponent.
/// </summary>
[UsedImplicitly]
public sealed partial class TraitModifySpeech : TraitFunction
{
    /// <summary>
    ///     Replaces SpeechComponent's 'SpeechSounds'.
    /// </summary>
    [DataField, AlwaysPushInheritance]
    public ProtoId<SpeechSoundsPrototype>? NewSounds;
    
    /// <summary>
    ///     Replaces SpeechComponent's 'SpeechVerb'.
    /// </summary>
    [DataField, AlwaysPushInheritance]
    public ProtoId<SpeechVerbPrototype>? NewVerb;
    
    /// <summary>
    ///     Replaces SpeechComponent's list of allowed emotes.
    /// </summary>
    [DataField, AlwaysPushInheritance]
    public List<ProtoId<EmotePrototype>>? NewAllowedEmotes;
    
    public override void OnPlayerSpawn(EntityUid uid,
        IComponentFactory factory,
        IEntityManager entityManager,
        ISerializationManager serializationManager)
    {
        if (!entityManager.TryGetComponent<SpeechComponent>(uid, out var speechComponent))
            return;

        if (NewSounds != null)
            speechComponent.SpeechSounds = NewSounds.Value;

        if (NewVerb != null)
            speechComponent.SpeechVerb = NewVerb.Value;
        
        if (NewAllowedEmotes != null)
            speechComponent.AllowedEmotes = NewAllowedEmotes;
            
    }
}