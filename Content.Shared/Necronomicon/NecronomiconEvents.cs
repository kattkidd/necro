using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Necronomicon;

// ---------------------------------------------------------------------------------------------
// Kandarian Demon actions
// ---------------------------------------------------------------------------------------------

/// <summary>Bring the Necronomicon into the physical world at the demon's position.</summary>
public sealed partial class NecroManifestBookActionEvent : InstantActionEvent;

/// <summary>Pull the Necronomicon back into the ether. Blocked while held by a non-Deadite.</summary>
public sealed partial class NecroDemanifestBookActionEvent : InstantActionEvent;

/// <summary>The manifested book whispers "join us" to a crew member near it, preparing them for possession.</summary>
public sealed partial class NecroJoinUsActionEvent : EntityTargetActionEvent;

/// <summary>Possess a prepared victim near the manifested book, consuming one possession charge.</summary>
public sealed partial class NecroPossessActionEvent : EntityTargetActionEvent;

/// <summary>Whisper terrors into a single crew member's mind.</summary>
public sealed partial class NecroWhisperActionEvent : EntityTargetActionEvent;

/// <summary>Disturb everything around the demon (lights, lockers, machines).</summary>
public sealed partial class NecroDarkPresenceActionEvent : InstantActionEvent;

/// <summary>Take on the purchased physical form.</summary>
public sealed partial class NecroManifestFormActionEvent : InstantActionEvent;

/// <summary>Leave the physical form and return to the incorporeal demon.</summary>
public sealed partial class NecroReturnToEtherActionEvent : InstantActionEvent;

/// <summary>The Force (The Evil Dead): the demon tears across the station in a straight line, bursting through doors and bowling over anyone in the way.</summary>
public sealed partial class NecroRushActionEvent : WorldTargetActionEvent;

/// <summary>The trees (The Evil Dead): roots and branches burst up through the floor and hold a victim fast.</summary>
public sealed partial class NecroGraspingRootsActionEvent : EntityTargetActionEvent;

/// <summary>The possessed hand (Evil Dead II): a victim's hand turns on them.</summary>
public sealed partial class NecroPossessedHandActionEvent : EntityTargetActionEvent;

/// <summary>False Eyes: a victim sees a friend's eyes flash white.</summary>
public sealed partial class NecroDeceitActionEvent : EntityTargetActionEvent;

/// <summary>Raise a corpse as a Deadite: the dead will not stay buried.</summary>
public sealed partial class NecroRiseAgainActionEvent : EntityTargetActionEvent;

/// <summary>"Dead by dawn": the whole station hears it, and every Deadite surges.</summary>
public sealed partial class NecroDeadByDawnActionEvent : InstantActionEvent;

/// <summary>Physical form: a roar that throws nearby crew to the floor and sets the room shaking.</summary>
public sealed partial class NecroUnholyRoarActionEvent : InstantActionEvent;

/// <summary>Physical form: every loose object nearby is hurled through the air.</summary>
public sealed partial class NecroPoltergeistActionEvent : InstantActionEvent;

/// <summary>Harmless demonic possession: the victim collapses and the demon speaks through them.</summary>
public sealed partial class NecroPuppetActionEvent : EntityTargetActionEvent;

/// <summary>While puppeting, the demon's next sentence becomes a station announcement signed by the host.</summary>
public sealed partial class NecroPuppetAnnounceActionEvent : InstantActionEvent;

// ---------------------------------------------------------------------------------------------
// Deadite actions
// ---------------------------------------------------------------------------------------------

/// <summary>Drop the disguise and reveal the Deadite's true form.</summary>
public sealed partial class DeaditeRevealActionEvent : InstantActionEvent;

/// <summary>Bite a crew member, leaving them open to possession.</summary>
public sealed partial class DeaditeBiteActionEvent : EntityTargetActionEvent;

/// <summary>Read the passage from the manifested book that melts away a victim's mindshield.</summary>
public sealed partial class DeaditeReadPassageActionEvent : EntityTargetActionEvent;

/// <summary>Screamer path: an unholy shriek that terrifies everyone nearby.</summary>
public sealed partial class DeaditeScreamActionEvent : InstantActionEvent;

/// <summary>Infiltrator path: pull the human mask back on after revealing.</summary>
public sealed partial class DeaditeConcealActionEvent : InstantActionEvent;

/// <summary>A revealed Deadite feeds on a downed victim: "I'll swallow your soul!"</summary>
public sealed partial class DeaditeSwallowSoulActionEvent : EntityTargetActionEvent;

/// <summary>A revealed Deadite's taunt ("Join us!", "Dead by dawn!"...).</summary>
public sealed partial class DeaditeTauntActionEvent : InstantActionEvent;

// ---------------------------------------------------------------------------------------------
// Do-afters
// ---------------------------------------------------------------------------------------------

[Serializable, NetSerializable]
public sealed partial class NecroPossessDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class DeaditeReadPassageDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class NecroRecordingDoAfterEvent : SimpleDoAfterEvent;

/// <summary>A chaplain's exorcism rite, performed with a Bible in hand.</summary>
[Serializable, NetSerializable]
public sealed partial class DeaditeExorcismDoAfterEvent : SimpleDoAfterEvent;

/// <summary>Reading the Passage of Rebuking from the Necronomicon at a possessed body.</summary>
[Serializable, NetSerializable]
public sealed partial class NecroBookRebukeDoAfterEvent : SimpleDoAfterEvent;

/// <summary>Reading the Passage of Unmaking from the Necronomicon at a Demonspawn.</summary>
[Serializable, NetSerializable]
public sealed partial class NecroBookUnmakeDoAfterEvent : SimpleDoAfterEvent;

/// <summary>Reading aloud from a random page of the Necronomicon. Anything can happen.</summary>
[Serializable, NetSerializable]
public sealed partial class NecroBookReadAloudDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class DeaditeSwallowSoulDoAfterEvent : SimpleDoAfterEvent;

/// <summary>The lost pages, first passage: conjure the evil into the flesh.</summary>
[Serializable, NetSerializable]
public sealed partial class NecroPagesConjureDoAfterEvent : SimpleDoAfterEvent;

/// <summary>The lost pages, second passage: open the rift that casts the evil out.</summary>
[Serializable, NetSerializable]
public sealed partial class NecroPagesRiftDoAfterEvent : SimpleDoAfterEvent;

// ---------------------------------------------------------------------------------------------
// Skill Store product events (raised on the buyer, validated server-side)
// ---------------------------------------------------------------------------------------------

/// <summary>Cosmetic form selection. Stores which physical form the demon will use.</summary>
[Serializable, NetSerializable, DataDefinition]
public sealed partial class NecroSelectFormEvent : EntityEventArgs
{
    /// <summary>Mob prototype spawned when the demon physically manifests.</summary>
    [DataField(required: true)]
    public EntProtoId Form;

    /// <summary>RSI used to tint the demon's incorporeal sprite (cosmetic only).</summary>
    [DataField(required: true)]
    public string Rsi = string.Empty;

    [DataField(required: true)]
    public string State = string.Empty;
}

/// <summary>Drags one of the wicked dead up out of Hell into a body of flesh.</summary>
[Serializable, NetSerializable, DataDefinition]
public sealed partial class NecroSummonWickedDeadEvent : EntityEventArgs;

/// <summary>Raises a squad of the Army of Darkness (Army of Darkness).</summary>
[Serializable, NetSerializable, DataDefinition]
public sealed partial class NecroRaiseArmyEvent : EntityEventArgs;

/// <summary>Adds a possession charge.</summary>
[Serializable, NetSerializable, DataDefinition]
public sealed partial class NecroBuyPossessionChargeEvent : EntityEventArgs;

/// <summary>Summons one Demonspawn ghost role.</summary>
[Serializable, NetSerializable, DataDefinition]
public sealed partial class NecroSummonDemonspawnEvent : EntityEventArgs;

[Serializable, NetSerializable]
public enum NecroUpgrade : byte
{
    FearMultiplier,
    DreadAura,
    SwiftPossession,
    FarWhisper,
    HardenedFlesh,
    RendingClaws,

    // Deadite progression tree. Each path has two tiers; see necronomicon_catalog.yml for costs and limits.
    InfiltratorI,
    InfiltratorII,
    BruteI,
    BruteII,
    StalkerI,
    StalkerII,
    ScreamerI,
    ScreamerII,
}

/// <summary>Applies a passive upgrade to the demon.</summary>
[Serializable, NetSerializable, DataDefinition]
public sealed partial class NecroUpgradeEvent : EntityEventArgs
{
    [DataField(required: true)]
    public NecroUpgrade Upgrade;
}

// ---------------------------------------------------------------------------------------------
// Visuals
// ---------------------------------------------------------------------------------------------

[Serializable, NetSerializable]
public enum NecroDemonVisuals : byte
{
    /// <summary>String "rsiPath|state" of the selected cosmetic form.</summary>
    Form,

    /// <summary>Aura colour tinting the incorporeal demon.</summary>
    Tint,
}
