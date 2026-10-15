using Content.Shared.Body;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Necronomicon;
using Content.Shared.Preferences;
using Content.Shared.RoundEnd;
using Content.Shared.Store;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.Necronomicon.Components;

/// <summary>
/// Marks any entity that belongs to the Kandarian side: the demon, its physical form, Deadites and Demonspawn.
/// These entities never count as "holding" the book and never generate Fear.
/// </summary>
[RegisterComponent]
public sealed partial class KandarianEntityComponent : Component
{
    /// <summary>The demon this entity serves.</summary>
    [ViewVariables]
    public EntityUid? Demon;

    /// <summary>Next time this entity's speech may generate Fear.</summary>
    [ViewVariables]
    public TimeSpan NextSpeechFear;
}

/// <summary>
/// The incorporeal Kandarian Demon. Owns the Necronomicon, Soul Power (store currency), possession charges and upgrades.
/// </summary>
[RegisterComponent]
public sealed partial class NecroDemonComponent : Component
{
    public const string BookContainerId = "necronomicon-book";

    [DataField]
    public ProtoId<CurrencyPrototype> Currency = "SoulPower";

    [DataField]
    public EntProtoId BookPrototype = "NecronomiconBook";

    [ViewVariables]
    public EntityUid? Book;

    /// <summary>False until the old recording is played (or the auto-awaken timer fires).</summary>
    [ViewVariables]
    public bool Awakened;

    /// <summary>Set once the book burns. Disables every ability and purchase.</summary>
    [ViewVariables]
    public bool Banished;

    #region Possession
    [DataField]
    public int PossessionCharges = 2;

    /// <summary>JOIN US reach, measured from the demon (the book only has to be manifested somewhere).</summary>
    [DataField]
    public float JoinUsRange = 12f;

    [DataField]
    public float FarWhisperJoinUsRange = 18f;

    /// <summary>How close a victim must be to the manifested book when possession starts.</summary>
    [DataField]
    public float PossessRange = 20f;

    /// <summary>Extra distance allowed when the possession completes, because victims run.</summary>
    [DataField]
    public float PossessCompletionGrace = 10f;

    /// <summary>How close the demon must be to its victim to start a possession.</summary>
    [DataField]
    public float PossessCasterRange = 12f;

    [DataField]
    public TimeSpan PossessDuration = TimeSpan.FromSeconds(6);

    [DataField]
    public TimeSpan SwiftPossessDuration = TimeSpan.FromSeconds(3);

    /// <summary>How long a victim stays prepared after JOIN US / a bite.</summary>
    [DataField]
    public TimeSpan PreparedDuration = TimeSpan.FromMinutes(5);

    [DataField]
    public FixedPoint2 ConversionReward = 3;
    #endregion

    #region Book transitions
    [DataField]
    public FixedPoint2 ManifestBookCost = 0;

    [DataField]
    public FixedPoint2 DemanifestBookCost = 0;

    [DataField]
    public TimeSpan BookTransitionTime = TimeSpan.FromSeconds(4);

    [ViewVariables]
    public NecroBookTransition PendingTransition = NecroBookTransition.None;

    [ViewVariables]
    public TimeSpan TransitionEnd;

    [ViewVariables]
    public EntityCoordinates TransitionCoords;
    #endregion

    #region Powers
    /// <summary>Played only to the victim of JOIN US and Whisper.</summary>
    [DataField]
    public SoundSpecifier WhisperSound = new SoundCollectionSpecifier("NecroWhispers");

    [DataField]
    public SoundSpecifier DemonspawnSummonSound = new SoundCollectionSpecifier("DemonspawnCries");

    [DataField]
    public FixedPoint2 WhisperCost = 2;

    [DataField]
    public float WhisperRange = 12f;

    [DataField]
    public FixedPoint2 DarkPresenceCost = 4;

    [DataField]
    public float DarkPresenceRange = 5f;

    [DataField]
    public FixedPoint2 ManifestFormCost = 5;

    [DataField]
    public TimeSpan ManifestFormCooldownOnDeath = TimeSpan.FromSeconds(120);

    [ViewVariables]
    public TimeSpan NextManifestForm;

    [ViewVariables]
    public EntityUid? Manifestation;

    /// <summary>Physical form prototype, chosen in the store. Cosmetic until physical manifestation is bought.</summary>
    [DataField]
    public EntProtoId SelectedForm = "MobKandarianFormAbomination";

    /// <summary>Colour of the demon's ghostly tint and of its physical form's glow (Aura, chosen in the loadout).</summary>
    [DataField]
    public Color AuraColor = Color.FromHex("#a01818");

    [DataField]
    public FixedPoint2 HardenedFleshBonus = 75;

    [DataField]
    public DamageSpecifier RendingClawsBonus = new();

    [ViewVariables]
    public int HardenedFleshStacks;

    [ViewVariables]
    public int RendingClawsStacks;

    [DataField]
    public EntProtoId DemonspawnPrototype = "MobDemonspawn";

    /// <summary>Radius of the poltergeist burst (flickering lights, flying objects) around supernatural events.</summary>
    [DataField]
    public float SupernaturalRange = 5f;

    /// <summary>Physical form: anyone this close to an Unholy Roar (and not mindshielded) is thrown down.</summary>
    [DataField]
    public float RoarKnockdownRange = 3f;

    [DataField]
    public TimeSpan RoarKnockdownTime = TimeSpan.FromSeconds(2.5);

    [DataField]
    public float RoarRange = 7f;

    /// <summary>Physical form: Poltergeist hurls up to this many loose objects.</summary>
    [DataField]
    public int PoltergeistObjects = 8;

    [DataField]
    public float PoltergeistRange = 6f;
    #endregion

    #region Demonic possession (harmless puppeting)
    [DataField]
    public FixedPoint2 PuppetCost = 3;

    [DataField]
    public FixedPoint2 PuppetAnnounceCost = 3;

    [DataField]
    public float PuppetRange = 12f;

    [DataField]
    public TimeSpan PuppetDuration = TimeSpan.FromSeconds(45);

    [DataField]
    public TimeSpan PuppetAnnounceCooldown = TimeSpan.FromMinutes(2);

    /// <summary>Cooldown between possessions, counted from when a possession starts.</summary>
    [DataField]
    public TimeSpan PuppetCooldown = TimeSpan.FromSeconds(60);

    [ViewVariables]
    public TimeSpan NextPuppet;

    [ViewVariables]
    public EntityUid? PuppetHost;

    [ViewVariables]
    public TimeSpan PuppetEndAt;

    /// <summary>The next sentence the demon speaks becomes a station announcement.</summary>
    [ViewVariables]
    public bool PuppetAnnounceNext;

    [ViewVariables]
    public TimeSpan NextPuppetAnnounce;
    #endregion

    #region Fear
    /// <summary>Multiplier applied to every Fear reward.</summary>
    [ViewVariables]
    public float FearMultiplier = 1f;

    [DataField]
    public float FearMultiplierStep = 0.5f;

    /// <summary>Extra multiplier for proximity fear when Dread Aura is bought.</summary>
    [ViewVariables]
    public float ProximityMultiplier = 1f;

    /// <summary>Fear generated by mindshielded crew is scaled by this.</summary>
    [DataField]
    public float MindshieldFearScale = 0.25f;

    /// <summary>How quickly per-victim fatigue decays, per second. Fatigue divides further rewards.</summary>
    [DataField]
    public float FatigueDecayPerSecond = 0.02f;

    [DataField]
    public float FatiguePerReward = 0.6f;

    [DataField]
    public Dictionary<NecroFearSource, NecroFearSourceConfig> FearSources = new()
    {
        { NecroFearSource.Proximity, new() { Reward = 0.25f, Cooldown = TimeSpan.FromSeconds(20) } },
        { NecroFearSource.Speech, new() { Reward = 0.5f, Cooldown = TimeSpan.FromSeconds(30) } },
        { NecroFearSource.Attacked, new() { Reward = 1f, Cooldown = TimeSpan.FromSeconds(15) } },
        { NecroFearSource.Witness, new() { Reward = 0.5f, Cooldown = TimeSpan.FromSeconds(20) } },
        { NecroFearSource.Reveal, new() { Reward = 1.5f, Cooldown = TimeSpan.FromSeconds(60) } },
        { NecroFearSource.Supernatural, new() { Reward = 0.5f, Cooldown = TimeSpan.FromSeconds(30) } },
    };

    [ViewVariables]
    public Dictionary<EntityUid, NecroVictimFear> Victims = new();

    [DataField]
    public TimeSpan ProximityInterval = TimeSpan.FromSeconds(5);

    [ViewVariables]
    public TimeSpan NextProximityCheck;

    [DataField]
    public float ProximityRange = 5f;

    [DataField]
    public float WitnessRange = 7f;

    [ViewVariables]
    public FixedPoint2 TotalFearEarned;
    #endregion

    #region Stats (round-end summary)
    /// <summary>Souls consumed: crew turned into Deadites by possession.</summary>
    [ViewVariables]
    public int Conversions;

    /// <summary>Harmless demonic possessions (puppeting) that took hold.</summary>
    [ViewVariables]
    public int PuppetPossessions;

    [ViewVariables]
    public int DemonspawnSummoned;

    [ViewVariables]
    public int DemonspawnMatured;

    /// <summary>Mindshield-melting rituals completed by Deadites.</summary>
    [ViewVariables]
    public int RitualsPerformed;

    /// <summary>Deadites successfully exorcised by the chaplain.</summary>
    [ViewVariables]
    public int Exorcisms;

    /// <summary>Exorcisms started against this demon's Deadites (successful or not).</summary>
    [ViewVariables]
    public int ExorcismAttempts;

    /// <summary>All Soul Power earned in play (fear, conversions, reveals). Excludes admin grants.</summary>
    [ViewVariables]
    public FixedPoint2 TotalSoulPowerEarned;

    [ViewVariables]
    public TimeSpan? AwakenedAt;

    [ViewVariables]
    public TimeSpan? BanishedAt;
    #endregion

    [ViewVariables]
    public EntityUid? ManifestBookActionEntity;

    #region HUD
    [ViewVariables]
    public NecroBookState LastBookState = NecroBookState.Ether;

    [ViewVariables]
    public TimeSpan NextStatusUpdate;

    /// <summary>Names of everyone the book has claimed, for the round-end summary.</summary>
    [ViewVariables]
    public List<string> ConsumedNames = new();

    /// <summary>Names of Deadites the chaplain has freed, for the round-end summary.</summary>
    [ViewVariables]
    public List<string> ExorcisedNames = new();
    #endregion
}

public enum NecroBookTransition : byte
{
    None,
    Manifesting,
    Demanifesting,
}

public enum NecroFearSource : byte
{
    Proximity,
    Speech,
    Attacked,
    Witness,
    Reveal,
    Supernatural,
}

[DataDefinition]
public sealed partial class NecroFearSourceConfig
{
    [DataField]
    public float Reward;

    [DataField]
    public TimeSpan Cooldown;
}

public sealed class NecroVictimFear
{
    public readonly Dictionary<NecroFearSource, TimeSpan> NextAllowed = new();
    public float Fatigue;
    public TimeSpan LastUpdate;
}

/// <summary>
/// Put on the preview entity of an appearance loadout (Character Setup → Antags → Kandarian Demon → Loadout).
/// Says which form that option means.
/// </summary>
[RegisterComponent]
public sealed partial class NecroAppearanceOptionComponent : Component
{
    /// <summary>Physical form prototype.</summary>
    [DataField(required: true)]
    public EntProtoId Form;

    /// <summary>RSI and state shown on the incorporeal ghost.</summary>
    [DataField(required: true)]
    public string Rsi = string.Empty;

    [DataField(required: true)]
    public string State = string.Empty;
}

/// <summary>
/// Put on the preview entity of an aura loadout (Character Setup → Antags → Kandarian Demon → Loadout).
/// Tints the demon's ghost and colours its physical form's glow.
/// </summary>
[RegisterComponent]
public sealed partial class NecroAuraOptionComponent : Component
{
    [DataField(required: true)]
    public Color Color;
}

/// <summary>The physical Necronomicon. Can only be destroyed by burning.</summary>
[RegisterComponent]
public sealed partial class NecronomiconComponent : Component
{
    [ViewVariables]
    public EntityUid? Demon;

    /// <summary>Seconds of continuous fire needed to destroy the book.</summary>
    [DataField]
    public float BurnTime = 20f;

    /// <summary>How fast progress is lost once the fire goes out, per second.</summary>
    [DataField]
    public float BurnRecovery = 0.5f;

    [ViewVariables]
    public float BurnProgress;

    [ViewVariables]
    public int BurnStage;

    [ViewVariables]
    public bool Destroyed;

    [DataField]
    public SoundSpecifier BurnSound = new SoundPathSpecifier("/Audio/Effects/burning.ogg");

    [DataField]
    public SoundSpecifier DestroyedSound = new SoundPathSpecifier("/Audio/Effects/demon_dies.ogg");

    [DataField]
    public SoundSpecifier ManifestSound = new SoundPathSpecifier("/Audio/Effects/desecration-01.ogg");

    [DataField]
    public EntProtoId DestroyedSpawn = "Ash";

    #region Passages (read by a living, unpossessed person holding the book)
    /// <summary>The book needs to rest between readings.</summary>
    [DataField]
    public TimeSpan ReadCooldown = TimeSpan.FromSeconds(60);

    [ViewVariables]
    public TimeSpan NextReading;

    /// <summary>How close the target of a passage must be.</summary>
    [DataField]
    public float ReadRange = 2f;

    [DataField]
    public TimeSpan RebukeTime = TimeSpan.FromSeconds(8);

    [DataField]
    public TimeSpan UnmakeTime = TimeSpan.FromSeconds(5);

    [DataField]
    public TimeSpan ReadAloudTime = TimeSpan.FromSeconds(4);

    /// <summary>Someone rebuked out of a body can't be claimed again for this long.</summary>
    [DataField]
    public TimeSpan RebukeBlessing = TimeSpan.FromMinutes(5);

    /// <summary>What reading from the book costs the reader.</summary>
    [DataField]
    public DamageSpecifier ReaderToll = new()
    {
        DamageDict = new() { { "Cellular", 6 } },
    };

    /// <summary>A rebuked Deadite crawls while the passage is read at it.</summary>
    [DataField]
    public float RebukeSlow = 0.4f;
    #endregion
}

/// <summary>A victim prepared for possession by JOIN US or a Deadite bite.</summary>
[RegisterComponent]
public sealed partial class NecroPreparedComponent : Component
{
    [ViewVariables]
    public TimeSpan ExpiresAt;
}

/// <summary>
/// Marks a victim currently being possessed, blocking duplicate conversions. While it's on them they are held down
/// and the Kandarian chant rises around them.
/// </summary>
[RegisterComponent]
public sealed partial class NecroBeingPossessedComponent : Component
{
    [ViewVariables]
    public TimeSpan NextChant;

    [ViewVariables]
    public int ChantIndex;

    /// <summary>Safety net: removed after this even if the do-after never reports back.</summary>
    [ViewVariables]
    public TimeSpan ExpiresAt;
}

/// <summary>
/// Someone temporarily puppeted by the demon: collapsed, unable to act or speak for themselves,
/// while the demon talks through their mouth. Harmless and temporary; not a Deadite.
/// </summary>
[RegisterComponent]
public sealed partial class NecroPuppetHostComponent : Component
{
    [ViewVariables]
    public EntityUid? Demon;
}

/// <summary>A possessed crew member.</summary>
[RegisterComponent]
public sealed partial class DeaditeComponent : Component
{
    [ViewVariables]
    public EntityUid? Demon;

    [ViewVariables]
    public bool Revealed;

    [ViewVariables]
    public TimeSpan NextLaugh;

    [ViewVariables]
    public TimeSpan NextTalkSound;

    /// <summary>Head severed: cannot act, cannot die, can still talk.</summary>
    [ViewVariables]
    public bool Severed;

    [DataField]
    public FixedPoint2 RevealReward = 5;

    [DataField]
    public Color SkinColor = Color.FromHex("#d8d8cf");

    [DataField]
    public Color EyeColor = Color.White;

    /// <summary>Extra unarmed damage once revealed. Deadites are added at runtime, so defaults live here.</summary>
    [DataField]
    public DamageSpecifier RevealedDamage = new()
    {
        DamageDict = new() { { "Slash", 6 }, { "Blunt", 4 } },
    };

    [DataField]
    public float RevealedSpeed = 1.15f;

    [DataField]
    public string Accent = "deadite";

    [DataField]
    public float BiteRange = 1.5f;

    [DataField]
    public DamageSpecifier BiteDamage = new()
    {
        DamageDict = new() { { "Piercing", 8 } },
    };

    /// <summary>A single Slash hit of at least this much while critical severs the head.</summary>
    [DataField]
    public FixedPoint2 SeverThreshold = 15;

    [DataField]
    public TimeSpan ReadPassageTime = TimeSpan.FromSeconds(10);

    [DataField]
    public float ReadPassageRange = 2f;

    [DataField]
    public EntProtoId RevealAction = "ActionDeaditeReveal";

    [DataField]
    public EntProtoId BiteAction = "ActionDeaditeBite";

    [DataField]
    public EntProtoId ReadPassageAction = "ActionDeaditeReadPassage";

    [ViewVariables]
    public EntityUid? RevealActionEntity;

    [ViewVariables]
    public EntityUid? BiteActionEntity;

    [ViewVariables]
    public EntityUid? ReadPassageActionEntity;

    /// <summary>Original dead threshold, restored on cleanup.</summary>
    [ViewVariables]
    public FixedPoint2? OriginalDeadThreshold;

    /// <summary>The victim's own skin and eye colours, captured before the first reveal so an exorcism can restore them.</summary>
    [ViewVariables]
    public Dictionary<ProtoId<OrganCategoryPrototype>, OrganProfileData>? OriginalProfiles;

    /// <summary>The reveal Soul Power bonus is paid once per Deadite, so Conceal can't be used to farm it.</summary>
    [ViewVariables]
    public bool RevealRewarded;

    #region Progression tree (tuning; which tiers are active is read from the demon's store purchases)
    /// <summary>Infiltrator II: exorcisms of this Deadite take this much longer.</summary>
    [DataField]
    public float InfiltratorExorcismMultiplier = 1.5f;

    /// <summary>Brute I: multiplier on the revealed melee bonus.</summary>
    [DataField]
    public float BruteDamageMultiplier = 1.6f;

    /// <summary>Brute I: multiplier on the time spent critical before rising again.</summary>
    [DataField]
    public float BruteReviveMultiplier = 0.6f;

    /// <summary>Brute II: incoming damage is multiplied by this while revealed.</summary>
    [DataField]
    public float BruteDamageTaken = 0.75f;

    /// <summary>Brute II: extra Slash needed in a single blow to sever the head.</summary>
    [DataField]
    public FixedPoint2 BruteSeverBonus = 10;

    /// <summary>Stalker I: revealed speed multiplier (replaces <see cref="RevealedSpeed"/>).</summary>
    [DataField]
    public float StalkerSpeed = 1.3f;

    /// <summary>Stalker I: bite reach.</summary>
    [DataField]
    public float StalkerBiteRange = 2.5f;

    /// <summary>Stalker II: bitten victims are slowed for this long.</summary>
    [DataField]
    public TimeSpan StalkerSlowDuration = TimeSpan.FromSeconds(4);

    [DataField]
    public float StalkerSlowMultiplier = 0.5f;

    [DataField]
    public EntProtoId StalkerSlowEffect = "DeaditeBiteSlowdownStatusEffect";

    /// <summary>Screamer I: radius of the shriek.</summary>
    [DataField]
    public float ScreamRange = 6f;

    /// <summary>Screamer II: anyone this close (and not mindshielded) is knocked off their feet.</summary>
    [DataField]
    public float ScreamKnockdownRange = 3f;

    [DataField]
    public TimeSpan ScreamKnockdownTime = TimeSpan.FromSeconds(2);

    [DataField]
    public EntProtoId ScreamAction = "ActionDeaditeScream";

    [DataField]
    public EntProtoId ConcealAction = "ActionDeaditeConceal";

    [ViewVariables]
    public EntityUid? ScreamActionEntity;

    [ViewVariables]
    public EntityUid? ConcealActionEntity;
    #endregion
}

/// <summary>
/// A summoned Demonspawn. Juveniles grow into an adult after <see cref="MaturationTime"/>; both stages melt into dust
/// on contact with holy water or a Bible.
/// </summary>
[RegisterComponent]
public sealed partial class DemonspawnComponent : Component
{
    /// <summary>True on the grown form. Adults never mature again.</summary>
    [DataField]
    public bool Adult;

    /// <summary>How long after being summoned a juvenile grows up.</summary>
    [DataField]
    public TimeSpan MaturationTime = TimeSpan.FromMinutes(10);

    [DataField]
    public EntProtoId AdultPrototype = "MobDemonspawnAdult";

    /// <summary>Grey skin and pitch-black eyes, applied to the adult's humanoid body.</summary>
    [DataField]
    public Color AdultSkinColor = Color.FromHex("#6e6e6e");

    [DataField]
    public Color AdultEyeColor = Color.Black;

    [ViewVariables]
    public TimeSpan? MatureAt;

    /// <summary>Set the moment maturation starts so it can only ever happen once.</summary>
    [ViewVariables]
    public bool Matured;

    /// <summary>Warning shown to the juvenile shortly before it grows.</summary>
    [ViewVariables]
    public bool MaturationWarned;

    /// <summary>Holy water (in units) that has touched this Demonspawn. Reaching the threshold melts it.</summary>
    [ViewVariables]
    public FixedPoint2 HolyWaterReceived;

    [DataField]
    public FixedPoint2 HolyWaterThreshold = 1;

    /// <summary>Set once it starts melting; it can't be saved after that.</summary>
    [ViewVariables]
    public TimeSpan? MeltAt;

    [DataField]
    public TimeSpan MeltDuration = TimeSpan.FromSeconds(1.5);

    [DataField]
    public EntProtoId DustPrototype = "DemonspawnDust";

    /// <summary>Adults get their grey skin and black eyes the tick after they spawn (once the body exists).</summary>
    [ViewVariables]
    public bool LookPending;

    /// <summary>Next automatic cry (a real emote, shown in chat).</summary>
    [ViewVariables]
    public TimeSpan NextCry;
}

/// <summary>Someone a chaplain freed from a Deadite curse. The book cannot claim them again for a while.</summary>
[RegisterComponent]
public sealed partial class NecroBlessedComponent : Component
{
    [ViewVariables]
    public TimeSpan ExpiresAt;
}

/// <summary>Present on a target while an exorcism is underway. Stops a Deadite from regenerating mid-rite.</summary>
[RegisterComponent]
public sealed partial class NecroBeingExorcisedComponent : Component
{
    [ViewVariables]
    public EntityUid Exorcist;

    /// <summary>Safety net in case the do-after vanishes without raising its event.</summary>
    [ViewVariables]
    public TimeSpan ExpiresAt;
}

/// <summary>
/// Exorcism tuning and cooldown, added to anyone who can use a Bible (chaplains) the first time they try.
/// </summary>
[RegisterComponent]
public sealed partial class NecroExorcistComponent : Component
{
    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(20);

    /// <summary>Cooldown after any completed rite (successful or finding nothing), so it can't be spammed.</summary>
    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(45);

    /// <summary>A mindshield implanted in the Deadite weakens the demon's grip and speeds the rite up.</summary>
    [DataField]
    public float MindshieldMultiplier = 0.5f;

    /// <summary>How long a freed victim is immune to JOIN US, bites and possession.</summary>
    [DataField]
    public TimeSpan BlessingDuration = TimeSpan.FromMinutes(10);

    [DataField]
    public float Range = 1.5f;

    [ViewVariables]
    public TimeSpan NextExorcism;
}

/// <summary>The demon's physical body.</summary>
[RegisterComponent]
public sealed partial class NecroManifestationComponent : Component
{
    [ViewVariables]
    public EntityUid? Demon;

    [DataField]
    public EntProtoId ReturnAction = "ActionNecroReturnToEther";

    [ViewVariables]
    public EntityUid? ReturnActionEntity;

    [DataField]
    public EntProtoId RoarAction = "ActionNecroUnholyRoar";

    [ViewVariables]
    public EntityUid? RoarActionEntity;

    [DataField]
    public EntProtoId PoltergeistAction = "ActionNecroPoltergeist";

    [ViewVariables]
    public EntityUid? PoltergeistActionEntity;
}

/// <summary>
/// Shared Kandarian regeneration: ordinary damage cannot kill, and the body recovers some time after going critical.
/// </summary>
[RegisterComponent]
public sealed partial class KandarianRegenComponent : Component
{
    /// <summary>Time spent critical before the body stands back up fully healed.</summary>
    [DataField]
    public TimeSpan ReviveDelay = TimeSpan.FromSeconds(15);

    /// <summary>Passive healing per second while alive and damaged.</summary>
    [DataField]
    public FixedPoint2 PassiveHeal = 0.5;

    [ViewVariables]
    public TimeSpan? ReviveAt;

    [ViewVariables]
    public TimeSpan NextTick;
}

/// <summary>The old recording whose playback summons the demon.</summary>
[RegisterComponent]
public sealed partial class NecroRecordingComponent : Component
{
    [ViewVariables]
    public bool Played;

    [DataField]
    public TimeSpan PlayTime = TimeSpan.FromSeconds(8);

    [DataField]
    public SoundSpecifier PlaySound = new SoundPathSpecifier("/Audio/Effects/desecration-02.ogg");

    [DataField]
    public SoundSpecifier SummonSound = new SoundPathSpecifier("/Audio/Effects/desecration-03.ogg");
}

public enum NecroOutcome : byte
{
    Ongoing,
    CrewVictory,
    NecronomiconVictory,
}

/// <summary>Game rule for the round-start Necronomicon antagonist.</summary>
[RegisterComponent]
public sealed partial class NecronomiconRuleComponent : Component
{
    [ViewVariables]
    public EntityUid? Demon;

    [ViewVariables]
    public EntityUid? Recording;

    [ViewVariables]
    public EntityCoordinates? SpawnCoords;

    [DataField]
    public EntProtoId RecordingPrototype = "NecroOldRecording";

    [DataField]
    public TimeSpan CheckInterval = TimeSpan.FromSeconds(10);

    [ViewVariables]
    public TimeSpan NextCheck;

    [ViewVariables]
    public NecroOutcome Outcome = NecroOutcome.Ongoing;

    [DataField]
    public RoundEndBehavior CrewVictoryBehavior = RoundEndBehavior.ShuttleCall;

    [DataField]
    public RoundEndBehavior NecroVictoryBehavior = RoundEndBehavior.InstantEnd;

    [DataField]
    public TimeSpan EvacShuttleTime = TimeSpan.FromMinutes(3);

    [ViewVariables]
    public string DemonName = string.Empty;

    [ViewVariables]
    public int FinalConversions;

    // Snapshot for the round-end summary (the demon entity is deleted when it is cast out).
    [ViewVariables]
    public List<string> FinalConsumedNames = new();

    [ViewVariables]
    public List<string> FinalExorcisedNames = new();

    [ViewVariables]
    public int FinalDemonspawn;

    [ViewVariables]
    public FixedPoint2 FinalFear;

    [ViewVariables]
    public string DemonUsername = string.Empty;

    [ViewVariables]
    public int FinalPuppetPossessions;

    [ViewVariables]
    public int FinalDemonspawnMatured;

    [ViewVariables]
    public int FinalRituals;

    [ViewVariables]
    public int FinalExorcisms;

    [ViewVariables]
    public int FinalExorcismAttempts;

    [ViewVariables]
    public FixedPoint2 FinalSoulPower;

    /// <summary>How long the demon lasted, from awakening to banishment (or round end).</summary>
    [ViewVariables]
    public TimeSpan FinalTimeSurvived;

    [ViewVariables]
    public bool FinalBanished;
}
