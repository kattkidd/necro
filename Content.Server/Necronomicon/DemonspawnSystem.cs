using Content.Server.Ghost.Roles.Components;
using Content.Server.Mind;
using Content.Server.Necronomicon.Components;
using Content.Shared.Bible.Components;
using Content.Shared.Chemistry;
using Content.Shared.Cuffs;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Jittering;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Console;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Necronomicon;

/// <summary>
/// Demonspawn growth and holy weakness.
/// <para>
/// Maturation: a juvenile grows into an adult (a grey-skinned humanoid with pitch-black eyes) once
/// <see cref="DemonspawnComponent.MaturationTime"/> has passed. The adult is a new entity; the player's mind, the
/// demon it serves, its position and everything it was wearing or holding carry over, and the juvenile is deleted.
/// It happens once (guarded by <see cref="DemonspawnComponent.Matured"/>) and waits while the juvenile is down,
/// restrained or inside something.
/// </para>
/// <para>
/// Holy weakness: holy water (splashed, sprayed, injected...) or a Bible (used on it, swung at it or thrown at it)
/// melts any Demonspawn, juvenile or adult, into dust. Deadites are not affected.
/// </para>
/// </summary>
public sealed class DemonspawnSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IConsoleHost _consoleHost = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedCuffableSystem _cuffable = default!;
    [Dependency] private DeaditeSystem _deadite = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedJitteringSystem _jitter = default!;
    [Dependency] private MindSystem _mind = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private static readonly SoundSpecifier MeltSound = new SoundPathSpecifier("/Audio/Effects/sizzle.ogg");
    private static readonly SoundSpecifier DustSound = new SoundPathSpecifier("/Audio/Effects/extinguish.ogg");
    private static readonly SoundSpecifier MatureSound = new SoundCollectionSpecifier("DemonspawnCries");
    private static readonly EntProtoId HolyLight = "HolyLightEffect";
    private const string HolyWater = "Holywater";

    /// <summary>Warn the juvenile this long before it grows.</summary>
    private static readonly TimeSpan MaturationWarning = TimeSpan.FromSeconds(30);

    private readonly List<Entity<DemonspawnComponent>> _toMature = new();

    /// <summary>The adult doesn't need to eat or drink.</summary>
    private static readonly string[] AdultStrippedComponents = { "Hunger", "Thirst" };

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DemonspawnComponent, MapInitEvent>(OnMapInit);

        SubscribeLocalEvent<DemonspawnComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<DemonspawnComponent, AttackedEvent>(OnAttacked);
        SubscribeLocalEvent<DemonspawnComponent, ThrowHitByEvent>(OnThrowHitBy);
        SubscribeLocalEvent<DemonspawnComponent, ReactionEntityEvent>(OnReaction);

        // Testing helper, like necrosp: growing up normally takes ten minutes.
        _consoleHost.RegisterCommand("necromature",
            "Instantly grow every juvenile Demonspawn into an adult.",
            "necromature",
            MatureCommand);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _consoleHost.UnregisterCommand("necromature");
    }

    [Content.Server.Administration.AdminCommand(Content.Shared.Administration.AdminFlags.Fun)]
    private void MatureCommand(IConsoleShell shell, string argstr, string[] args)
    {
        var juveniles = new List<Entity<DemonspawnComponent>>();
        var query = EntityQueryEnumerator<DemonspawnComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!comp.Adult && !comp.Matured && comp.MeltAt == null)
                juveniles.Add((uid, comp));
        }

        var grown = 0;
        foreach (var juvenile in juveniles)
        {
            if (Mature(juvenile) != null)
                grown++;
        }

        shell.WriteLine($"Matured {grown} Demonspawn.");
    }

    private void OnMapInit(Entity<DemonspawnComponent> ent, ref MapInitEvent args)
    {
        if (!ent.Comp.Adult)
            ent.Comp.MatureAt = _timing.CurTime + ent.Comp.MaturationTime;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<DemonspawnComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.MeltAt is { } meltAt)
            {
                if (now >= meltAt)
                    CrumbleToDust((uid, comp));
                continue;
            }

            if (comp.Adult || comp.Matured || comp.MatureAt is not { } matureAt)
                continue;

            if (!comp.MaturationWarned && now >= matureAt - MaturationWarning)
            {
                comp.MaturationWarned = true;
                _popup.PopupEntity(Loc.GetString("demonspawn-maturing-soon"), uid, uid, PopupType.MediumCaution);
                _jitter.DoJitter(uid, TimeSpan.FromSeconds(3), true, 4f, 4f);
            }

            if (now < matureAt || !CanMatureNow(uid))
                continue;

            _toMature.Add((uid, comp));
        }

        // Maturing spawns a new Demonspawn, so it can't happen while the query above is running.
        foreach (var juvenile in _toMature)
        {
            Mature(juvenile);
        }

        _toMature.Clear();
    }

    #region Maturation

    /// <summary>Growth waits until the juvenile is up, free and out in the open.</summary>
    private bool CanMatureNow(EntityUid uid)
    {
        if (!_mobState.IsAlive(uid))
            return false;

        if (_container.IsEntityOrParentInContainer(uid))
            return false;

        return !_cuffable.IsCuffed(uid, requireFullyCuffed: false);
    }

    /// <summary>
    /// Replaces a juvenile with an adult. Returns the adult, or null if it already matured or can't.
    /// </summary>
    public EntityUid? Mature(Entity<DemonspawnComponent> juvenile)
    {
        if (juvenile.Comp.Adult || juvenile.Comp.Matured || TerminatingOrDeleted(juvenile))
            return null;

        // Once only, whatever happens next.
        juvenile.Comp.Matured = true;

        var xform = Transform(juvenile);
        var coords = xform.Coordinates;
        var adult = Spawn(juvenile.Comp.AdultPrototype, coords);
        _transform.AttachToGridOrMap(adult);
        _transform.SetLocalRotation(adult, xform.LocalRotation);

        // Allegiance.
        EnsureComp<KandarianEntityComponent>(adult).Demon = CompOrNull<KandarianEntityComponent>(juvenile)?.Demon;
        if (TryComp<DemonspawnComponent>(adult, out var adultComp))
        {
            adultComp.Adult = true;
            adultComp.Matured = true;
            adultComp.MatureAt = null;
        }

        foreach (var name in AdultStrippedComponents)
        {
            if (EntityManager.ComponentFactory.TryGetRegistration(name, out var registration))
                RemComp(adult, registration.Type);
        }

        // Grey skin, eyes black as pitch.
        _deadite.ApplyDeaditeLook(adult, juvenile.Comp.AdultSkinColor, juvenile.Comp.AdultEyeColor);

        TransferBelongings(juvenile.Owner, adult);

        // The player goes with it. Take the juvenile's ghost role down first so it can't be re-offered in between.
        RemComp<GhostTakeoverAvailableComponent>(juvenile.Owner);
        RemComp<GhostRoleComponent>(juvenile.Owner);
        if (_mind.TryGetMind(juvenile.Owner, out var mindId, out var mind))
            _mind.TransferTo(mindId, adult, mind: mind);

        if (CompOrNull<KandarianEntityComponent>(adult)?.Demon is { } demonUid
            && TryComp<NecroDemonComponent>(demonUid, out var demon))
        {
            demon.DemonspawnMatured++;
            _popup.PopupEntity(Loc.GetString("demonspawn-matured-demon"), demonUid, demonUid, PopupType.Medium);
        }

        _audio.PlayPvs(MatureSound, adult);
        _jitter.DoJitter(adult, TimeSpan.FromSeconds(3), true, 10f, 8f);
        _popup.PopupEntity(Loc.GetString("demonspawn-matured", ("target", Identity.Entity(adult, EntityManager))),
            adult, PopupType.LargeCaution);
        _popup.PopupEntity(Loc.GetString("demonspawn-matured-self"), adult, adult, PopupType.Large);

        QueueDel(juvenile);
        return adult;
    }

    /// <summary>Moves worn and held items onto the adult; whatever doesn't fit drops to the floor.</summary>
    private void TransferBelongings(EntityUid from, EntityUid to)
    {
        var worn = new List<(string Slot, EntityUid Item)>();
        if (_inventory.TryGetContainerSlotEnumerator(from, out var slots))
        {
            while (slots.MoveNext(out var container, out var slot))
            {
                if (container.ContainedEntity is { } item)
                    worn.Add((slot.Name, item));
            }
        }

        foreach (var (slot, _) in worn)
        {
            _inventory.TryUnequip(from, slot, silent: true, force: true);
        }

        var held = new List<EntityUid>(_hands.EnumerateHeld(from));
        foreach (var item in held)
        {
            _hands.TryDrop(from, item, checkActionBlocker: false, doDropInteraction: false);
        }

        // Two passes: some slots (ID, pockets) need the jumpsuit on first.
        for (var pass = 0; pass < 2; pass++)
        {
            for (var i = worn.Count - 1; i >= 0; i--)
            {
                var (slot, item) = worn[i];
                if (TerminatingOrDeleted(item) || _inventory.TryEquip(to, item, slot, silent: true))
                    worn.RemoveAt(i);
            }
        }

        foreach (var item in held)
        {
            if (!TerminatingOrDeleted(item))
                _hands.TryPickupAnyHand(to, item, checkActionBlocker: false, animate: false);
        }
    }

    #endregion

    #region Holy weakness

    private void OnInteractUsing(EntityUid uid, DemonspawnComponent comp, InteractUsingEvent args)
    {
        if (args.Handled || !HasComp<BibleComponent>(args.Used))
            return;

        args.Handled = true;
        StartMelting((uid, comp), args.User);
    }

    private void OnAttacked(EntityUid uid, DemonspawnComponent comp, AttackedEvent args)
    {
        if (HasComp<BibleComponent>(args.Used))
            StartMelting((uid, comp), args.User);
    }

    private void OnThrowHitBy(Entity<DemonspawnComponent> ent, ref ThrowHitByEvent args)
    {
        if (HasComp<BibleComponent>(args.Thrown))
            StartMelting(ent, args.Component.Thrower);
    }

    private void OnReaction(Entity<DemonspawnComponent> ent, ref ReactionEntityEvent args)
    {
        if (args.Reagent.ID != HolyWater)
            return;

        ent.Comp.HolyWaterReceived += args.ReagentQuantity.Quantity;
        if (ent.Comp.HolyWaterReceived >= ent.Comp.HolyWaterThreshold)
            StartMelting(ent, null);
    }

    /// <summary>Holy contact: the Demonspawn shrieks, seizes up and, a moment later, falls apart into dust.</summary>
    public void StartMelting(Entity<DemonspawnComponent> ent, EntityUid? cause)
    {
        if (ent.Comp.MeltAt != null || TerminatingOrDeleted(ent))
            return;

        ent.Comp.MeltAt = _timing.CurTime + ent.Comp.MeltDuration;

        _audio.PlayPvs(MeltSound, ent);
        _audio.PlayPvs(MatureSound, ent);
        Spawn(HolyLight, Transform(ent).Coordinates);
        _jitter.DoJitter(ent, ent.Comp.MeltDuration + TimeSpan.FromSeconds(1), true, 20f, 12f);
        _stun.TryUpdateParalyzeDuration(ent, ent.Comp.MeltDuration + TimeSpan.FromSeconds(1));

        _popup.PopupEntity(Loc.GetString("demonspawn-melting", ("target", Identity.Entity(ent, EntityManager))),
            ent, PopupType.LargeCaution);
        _popup.PopupEntity(Loc.GetString("demonspawn-melting-self"), ent, ent, PopupType.LargeCaution);

        if (cause is { } user && user != ent.Owner)
            _popup.PopupEntity(Loc.GetString("demonspawn-melting-user"), user, user, PopupType.Medium);
    }

    private void CrumbleToDust(Entity<DemonspawnComponent> ent)
    {
        if (TerminatingOrDeleted(ent))
            return;

        var coords = Transform(ent).Coordinates;

        // Anything it carried survives the holy fire.
        TransferBelongingsToFloor(ent.Owner);

        Spawn(ent.Comp.DustPrototype, coords);
        Spawn(HolyLight, coords);
        _audio.PlayPvs(DustSound, coords);
        _popup.PopupCoordinates(Loc.GetString("demonspawn-dust", ("target", Identity.Entity(ent, EntityManager))),
            coords, PopupType.LargeCaution);

        if (CompOrNull<KandarianEntityComponent>(ent)?.Demon is { } demonUid && Exists(demonUid))
            _popup.PopupEntity(Loc.GetString("demonspawn-dust-demon"), demonUid, demonUid, PopupType.MediumCaution);

        // The player's mind is ghosted by the mind system when the body is deleted.
        QueueDel(ent);
    }

    private void TransferBelongingsToFloor(EntityUid uid)
    {
        if (_inventory.TryGetContainerSlotEnumerator(uid, out var slots))
        {
            var names = new List<string>();
            while (slots.MoveNext(out var container, out var slot))
            {
                if (container.ContainedEntity != null)
                    names.Add(slot.Name);
            }

            foreach (var name in names)
            {
                _inventory.TryUnequip(uid, name, silent: true, force: true);
            }
        }

        foreach (var item in new List<EntityUid>(_hands.EnumerateHeld(uid)))
        {
            _hands.TryDrop(uid, item, checkActionBlocker: false, doDropInteraction: false);
        }
    }

    #endregion
}
