using System.Numerics;
using Content.Server.Necronomicon.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Eye;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Item;
using Content.Shared.Necronomicon;
using Content.Shared.Preferences;
using Content.Shared.Tag;
using Content.Shared.Throwing;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server.Necronomicon;

/// <summary>
/// "My Character" appearance: the demon's ghost is the player's real character, built by the normal player-spawning
/// code (so every species, custom species, hair style and marking works), dressed in a black suit.
/// The body is a lifeless shell parented to the demon: visible only on the ghost layer, no collision, no damage,
/// no interaction. The demon itself stays the incorporeal entity that holds all of the antagonist logic.
/// </summary>
public sealed partial class NecronomiconSystem
{
    [Dependency] private SharedGodmodeSystem _godmode = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private TagSystem _tag = default!;
    [Dependency] private VisibilitySystem _visibility = default!;

    private static readonly EntProtoId BlackSuit = "ClothingUniformJumpsuitLawyerBlack";
    private static readonly EntProtoId BlackShoes = "ClothingShoesColorBlack";
    private static readonly ProtoId<TagPrototype> FootstepTag = "FootstepSound";
    private static readonly ProtoId<TagPrototype> DoorBumpTag = "DoorBumpOpener";

    /// <summary>Components that would let a lifeless shell make noise, breathe, push or otherwise give itself away.</summary>
    private static readonly string[] ShellStrippedComponents =
    {
        "Respirator", "MobCollision", "Puller", "Pullable", "Strippable", "Buckle", "Climbing", "Deathgasp",
        "EmoteOnDamage", "AutoEmote", "Vocal",
    };

    private void InitializeCharacter()
    {
        // The shell can't interact with anything.
        SubscribeLocalEvent<NecroGhostShellComponent, InteractionAttemptEvent>(OnShellInteract);
        SubscribeLocalEvent<NecroGhostShellComponent, UseAttemptEvent>(OnShellBlock);
        SubscribeLocalEvent<NecroGhostShellComponent, AttackAttemptEvent>(OnShellBlock);
        SubscribeLocalEvent<NecroGhostShellComponent, PickupAttemptEvent>(OnShellBlock);
        SubscribeLocalEvent<NecroGhostShellComponent, ThrowAttemptEvent>(OnShellBlock);
    }

    /// <summary>
    /// Spawns the player's character exactly as the game would for a crew member, then dresses it in a black suit.
    /// </summary>
    public EntityUid SpawnCharacterBody(HumanoidCharacterProfile profile, EntityCoordinates coords)
    {
        var body = _stationSpawning.SpawnPlayerMob(coords, null, profile, null);

        // Species without these slots simply keep what they have.
        EquipOrDelete(body, BlackSuit, "jumpsuit");
        EquipOrDelete(body, BlackShoes, "shoes");
        return body;
    }

    private void EquipOrDelete(EntityUid body, EntProtoId item, string slot)
    {
        if (_inventory.TryGetSlotEntity(body, slot, out var existing))
            QueueDel(existing.Value);

        var spawned = Spawn(item, Transform(body).Coordinates);
        if (!_inventory.TryEquip(body, spawned, slot, silent: true, force: true))
            QueueDel(spawned);
    }

    /// <summary>
    /// Gives the demon a ghost made from the player's character: a black-suited shell that follows the demon and that
    /// only the dead, observers and Kandarian entities can see.
    /// </summary>
    public void SetCharacterGhost(Entity<NecroDemonComponent> demon, HumanoidCharacterProfile profile)
    {
        if (demon.Comp.GhostShell is { } old && !TerminatingOrDeleted(old))
            QueueDel(old);

        var shell = SpawnCharacterBody(profile, Transform(demon).Coordinates);
        demon.Comp.GhostShell = shell;

        EnsureComp<NecroGhostShellComponent>(shell).Demon = demon;
        EnsureComp<KandarianEntityComponent>(shell).Demon = demon;

        // Untouchable and silent.
        _godmode.EnableGodmode(shell);
        foreach (var name in ShellStrippedComponents)
        {
            if (EntityManager.ComponentFactory.TryGetRegistration(name, out var registration))
                RemComp(shell, registration.Type);
        }
        _tag.RemoveTag(shell, FootstepTag);
        _tag.RemoveTag(shell, DoorBumpTag);

        // Ghost layer only: the living never receive it.
        var visibility = EnsureComp<VisibilityComponent>(shell);
        _visibility.AddLayer((shell, visibility), (ushort) VisibilityFlags.Ghost, false);
        _visibility.RemoveLayer((shell, visibility), (ushort) VisibilityFlags.Normal, false);
        _visibility.RefreshVisibility(shell, visibility);

        // Faded on the client, and the demon's own placeholder sprite is hidden.
        EnsureComp<NecroDemonVisualsComponent>(shell);
        _appearance.SetData(shell, NecroDemonVisuals.Form, NecroDemonVisualsConstants.GhostShell);
        _appearance.SetData(demon, NecroDemonVisuals.Form, NecroDemonVisualsConstants.HiddenDemon);

        AttachShell(demon, shell);
    }

    /// <summary>Parents the shell to the demon so it moves and turns with it.</summary>
    private void AttachShell(Entity<NecroDemonComponent> demon, EntityUid shell)
    {
        var xform = Transform(shell);
        if (xform.ParentUid == demon.Owner)
            return;

        _physics.SetCanCollide(shell, false, force: true);
        _transform.SetCoordinates(shell, xform, new EntityCoordinates(demon, Vector2.Zero), rotation: Angle.Zero);
        _physics.SetLinearVelocity(shell, Vector2.Zero);
    }

    /// <summary>Keeps the shell attached (something could knock it loose) and cleans up if the demon is gone.</summary>
    private void UpdateGhostShell(Entity<NecroDemonComponent> demon)
    {
        if (demon.Comp.GhostShell is not { } shell)
            return;

        if (TerminatingOrDeleted(shell))
        {
            demon.Comp.GhostShell = null;
            return;
        }

        AttachShell(demon, shell);
    }

    private void OnShellBlock<T>(Entity<NecroGhostShellComponent> ent, ref T args) where T : CancellableEntityEventArgs
    {
        args.Cancel();
    }

    private void OnShellInteract(Entity<NecroGhostShellComponent> ent, ref InteractionAttemptEvent args)
    {
        args.Cancelled = true;
    }
}
