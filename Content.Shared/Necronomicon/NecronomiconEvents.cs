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

// ---------------------------------------------------------------------------------------------
// Do-afters
// ---------------------------------------------------------------------------------------------

[Serializable, NetSerializable]
public sealed partial class NecroPossessDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class DeaditeReadPassageDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class NecroRecordingDoAfterEvent : SimpleDoAfterEvent;

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
}
