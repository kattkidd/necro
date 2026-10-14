using System.Diagnostics.CodeAnalysis;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Ghost;
using Content.Server.Mind;
using Content.Server.Necronomicon.Components;
using Content.Server.Store.Systems;
using Content.Shared.Actions;
using Content.Shared.Atmos.Components;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Jittering;
using Content.Shared.Mind;
using Content.Shared.Mindshield.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Necronomicon;
using Content.Shared.Popups;
using Content.Shared.Store.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Server.Necronomicon;

/// <summary>
/// Core of the Necronomicon antagonist: Soul Power, the physical book, the summoning recording and book burning.
/// Possession, Fear and the Skill Store live in the other partial files.
/// </summary>
public sealed partial class NecronomiconSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private FlammableSystem _flammable = default!;
    [Dependency] private GhostSystem _ghost = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private SharedJitteringSystem _jitter = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private MindSystem _mind = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private StoreSystem _store = default!;
    [Dependency] private TransformSystem _transform = default!;

    /// <summary>Books that were deleted by something other than fire and must be restored to their demon.</summary>
    private readonly List<EntityUid> _bookRestoreQueue = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NecroDemonComponent, ExaminedEvent>(OnDemonExamined);
        SubscribeLocalEvent<NecroDemonComponent, NecroManifestBookActionEvent>(OnManifestBookAction);
        SubscribeLocalEvent<NecroDemonComponent, NecroDemanifestBookActionEvent>(OnDemanifestBookAction);
        SubscribeLocalEvent<NecroDemonComponent, ComponentShutdown>(OnDemonShutdown);

        SubscribeLocalEvent<NecronomiconComponent, EntityTerminatingEvent>(OnBookTerminating);
        SubscribeLocalEvent<NecronomiconComponent, ExaminedEvent>(OnBookExamined);

        SubscribeLocalEvent<NecroRecordingComponent, UseInHandEvent>(OnRecordingUse);
        SubscribeLocalEvent<NecroRecordingComponent, ActivateInWorldEvent>(OnRecordingActivate);
        SubscribeLocalEvent<NecroRecordingComponent, NecroRecordingDoAfterEvent>(OnRecordingDoAfter);
        SubscribeLocalEvent<NecroRecordingComponent, ExaminedEvent>(OnRecordingExamined);

        InitializePossession();
        InitializeFear();
        InitializeStore();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;

        // Restore any book deleted by non-fire means.
        if (_bookRestoreQueue.Count > 0)
        {
            foreach (var demon in _bookRestoreQueue)
            {
                if (TryComp<NecroDemonComponent>(demon, out var demonComp) && !demonComp.Banished)
                    EnsureBook((demon, demonComp));
            }
            _bookRestoreQueue.Clear();
        }

        var demons = EntityQueryEnumerator<NecroDemonComponent>();
        while (demons.MoveNext(out var uid, out var demon))
        {
            UpdateBookTransition((uid, demon), now);
            UpdateManifestationFollow((uid, demon));
            UpdateProximityFear((uid, demon), now);
        }

        UpdateBurning(frameTime);
        UpdatePrepared(now);
    }

    #region Soul Power

    public FixedPoint2 GetSoulPower(Entity<NecroDemonComponent> demon)
    {
        if (!TryComp<StoreComponent>(demon, out var store))
            return FixedPoint2.Zero;

        return store.Balance.GetValueOrDefault(demon.Comp.Currency);
    }

    public void AddSoulPower(Entity<NecroDemonComponent> demon, FixedPoint2 amount)
    {
        if (amount == FixedPoint2.Zero || demon.Comp.Banished)
            return;

        if (!TryComp<StoreComponent>(demon, out var store))
            return;

        _store.TryAddCurrency(new Dictionary<Robust.Shared.Prototypes.ProtoId<Content.Shared.Store.CurrencyPrototype>, FixedPoint2>
            { { demon.Comp.Currency, amount } }, demon, store);
    }

    /// <summary>Spends SP if the demon can afford it; shows a popup otherwise.</summary>
    public bool TrySpendSoulPower(Entity<NecroDemonComponent> demon, FixedPoint2 cost, EntityUid popupTarget)
    {
        if (cost <= FixedPoint2.Zero)
            return true;

        var current = GetSoulPower(demon);
        if (current < cost)
        {
            _popup.PopupEntity(Loc.GetString("necro-not-enough-sp", ("cost", cost), ("current", current)),
                popupTarget, popupTarget, PopupType.SmallCaution);
            return false;
        }

        AddSoulPower(demon, -cost);
        return true;
    }

    #endregion

    #region Helpers

    public bool IsKandarian(EntityUid uid)
    {
        return HasComp<KandarianEntityComponent>(uid);
    }

    public bool IsMindshielded(EntityUid uid)
    {
        return TryComp<MindShieldStatusComponent>(uid, out var status) && status.IsMindshielded;
    }

    /// <summary>
    /// A "soul": a non-Kandarian humanoid that isn't dead and is controlled by (or belongs to) a player.
    /// Disconnected players still count, since their body can still be possessed.
    /// </summary>
    public bool IsEligibleSoul(EntityUid uid)
    {
        if (!HasComp<HumanoidProfileComponent>(uid) || IsKandarian(uid))
            return false;

        if (!TryComp<MobStateComponent>(uid, out var mob) || _mobState.IsDead(uid, mob))
            return false;

        return _mind.TryGetMind(uid, out _, out var mind) && mind.UserId != null;
    }

    /// <summary>Returns the demon this entity serves, if any.</summary>
    public bool TryGetDemon(EntityUid? demonUid, [NotNullWhen(true)] out Entity<NecroDemonComponent>? demon)
    {
        demon = null;
        if (demonUid is not { } uid || !TryComp<NecroDemonComponent>(uid, out var comp))
            return false;

        demon = (uid, comp);
        return true;
    }

    /// <summary>Checks the demon may use its powers, with feedback.</summary>
    public bool CanUsePowers(Entity<NecroDemonComponent> demon, EntityUid user)
    {
        if (demon.Comp.Banished)
            return false;

        if (!demon.Comp.Awakened)
        {
            _popup.PopupEntity(Loc.GetString("necro-dormant"), user, user, PopupType.SmallCaution);
            return false;
        }

        return true;
    }

    public bool IsBookManifested(Entity<NecroDemonComponent> demon, [NotNullWhen(true)] out EntityUid? book)
    {
        book = demon.Comp.Book;
        if (book == null || TerminatingOrDeleted(book.Value))
            return false;

        return !_container.TryGetContainer(demon, NecroDemonComponent.BookContainerId, out var container)
               || !container.Contains(book.Value);
    }

    /// <summary>
    /// Walks up the transform hierarchy and returns the first mob that is carrying the book
    /// (hands, pockets, bags, worn storage...).
    /// </summary>
    public EntityUid? GetCarrier(EntityUid item)
    {
        var parent = Transform(item).ParentUid;
        while (parent.IsValid())
        {
            if (HasComp<MapGridComponent>(parent) || HasComp<MapComponent>(parent))
                return null;

            if (HasComp<MobStateComponent>(parent))
                return parent;

            parent = Transform(parent).ParentUid;
        }

        return null;
    }

    /// <summary>
    /// The critical rule: a non-Deadite crew member carrying the manifested book pins it in the physical world.
    /// </summary>
    public bool IsPinnedByCrew(EntityUid book, out EntityUid? carrier)
    {
        carrier = GetCarrier(book);
        return carrier != null && !IsKandarian(carrier.Value);
    }

    private void OnDemonExamined(Entity<NecroDemonComponent> ent, ref ExaminedEvent args)
    {
        if (args.Examiner != args.Examined)
            return;

        args.PushMarkup(Loc.GetString("necro-demon-examine",
            ("sp", GetSoulPower(ent)),
            ("charges", ent.Comp.PossessionCharges),
            ("awake", ent.Comp.Awakened ? 1 : 0)));
    }

    private void OnDemonShutdown(Entity<NecroDemonComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.Manifestation is { } form && !TerminatingOrDeleted(form))
            QueueDel(form);
    }

    #endregion

    #region Book

    /// <summary>Makes sure the demon has a book, creating it inside the demon (unmanifested) if needed.</summary>
    public EntityUid EnsureBook(Entity<NecroDemonComponent> demon)
    {
        if (demon.Comp.Book is { } existing && !TerminatingOrDeleted(existing))
            return existing;

        var container = _container.EnsureContainer<ContainerSlot>(demon, NecroDemonComponent.BookContainerId);
        var book = Spawn(demon.Comp.BookPrototype, Transform(demon).Coordinates);
        var bookComp = EnsureComp<NecronomiconComponent>(book);
        bookComp.Demon = demon;
        demon.Comp.Book = book;

        if (!_container.Insert(book, container, force: true))
            Log.Error($"Could not store Necronomicon {ToPrettyString(book)} inside demon {ToPrettyString(demon)}");

        return book;
    }

    private void OnManifestBookAction(Entity<NecroDemonComponent> ent, ref NecroManifestBookActionEvent args)
    {
        if (args.Handled || !CanUsePowers(ent, args.Performer))
            return;

        args.Handled = TryStartBookTransition(ent, NecroBookTransition.Manifesting, args.Performer);
    }

    private void OnDemanifestBookAction(Entity<NecroDemonComponent> ent, ref NecroDemanifestBookActionEvent args)
    {
        if (args.Handled || !CanUsePowers(ent, args.Performer))
            return;

        args.Handled = TryStartBookTransition(ent, NecroBookTransition.Demanifesting, args.Performer);
    }

    private bool TryStartBookTransition(Entity<NecroDemonComponent> demon, NecroBookTransition kind, EntityUid user)
    {
        if (demon.Comp.PendingTransition != NecroBookTransition.None)
        {
            _popup.PopupEntity(Loc.GetString("necro-book-transition-busy"), user, user, PopupType.SmallCaution);
            return false;
        }

        var book = EnsureBook(demon);
        var manifested = IsBookManifested(demon, out _);

        FixedPoint2 cost;
        if (kind == NecroBookTransition.Manifesting)
        {
            if (manifested)
            {
                _popup.PopupEntity(Loc.GetString("necro-book-already-manifested"), user, user);
                return false;
            }

            if (demon.Comp.Manifestation != null)
            {
                _popup.PopupEntity(Loc.GetString("necro-book-not-while-physical"), user, user, PopupType.SmallCaution);
                return false;
            }

            cost = demon.Comp.ManifestBookCost;
        }
        else
        {
            if (!manifested)
            {
                _popup.PopupEntity(Loc.GetString("necro-book-already-hidden"), user, user);
                return false;
            }

            if (IsPinnedByCrew(book, out _))
            {
                _popup.PopupEntity(Loc.GetString("necro-book-pinned"), user, user, PopupType.MediumCaution);
                return false;
            }

            cost = demon.Comp.DemanifestBookCost;
        }

        if (!TrySpendSoulPower(demon, cost, user))
            return false;

        demon.Comp.PendingTransition = kind;
        demon.Comp.TransitionEnd = _timing.CurTime + demon.Comp.BookTransitionTime;
        demon.Comp.TransitionCoords = Transform(user).Coordinates;

        var msg = kind == NecroBookTransition.Manifesting ? "necro-book-manifest-start" : "necro-book-demanifest-start";
        _popup.PopupEntity(Loc.GetString(msg), user, user, PopupType.Medium);

        if (manifested)
            _popup.PopupEntity(Loc.GetString("necro-book-stirring"), book, PopupType.MediumCaution);

        return true;
    }

    private void UpdateBookTransition(Entity<NecroDemonComponent> demon, TimeSpan now)
    {
        if (demon.Comp.PendingTransition == NecroBookTransition.None)
            return;

        var user = demon.Comp.Manifestation ?? demon.Owner;
        var book = EnsureBook(demon);

        // The pin rule applies throughout the transition, not just at the start.
        if (demon.Comp.PendingTransition == NecroBookTransition.Demanifesting && IsPinnedByCrew(book, out _))
        {
            demon.Comp.PendingTransition = NecroBookTransition.None;
            _popup.PopupEntity(Loc.GetString("necro-book-pinned-interrupted"), user, user, PopupType.MediumCaution);
            _popup.PopupEntity(Loc.GetString("necro-book-settles"), book, PopupType.Medium);
            return;
        }

        if (now < demon.Comp.TransitionEnd)
            return;

        var kind = demon.Comp.PendingTransition;
        demon.Comp.PendingTransition = NecroBookTransition.None;

        if (kind == NecroBookTransition.Manifesting)
            ManifestBook(demon, demon.Comp.TransitionCoords);
        else
            DemanifestBook(demon);
    }

    /// <summary>Places the book in the world. No cost or delay; callers handle that.</summary>
    public void ManifestBook(Entity<NecroDemonComponent> demon, EntityCoordinates coords)
    {
        var book = EnsureBook(demon);
        if (IsBookManifested(demon, out _))
            return;

        if (!_container.TryGetContainer(demon, NecroDemonComponent.BookContainerId, out var container))
            return;

        _container.Remove(book, container, destination: coords, force: true);
        _transform.AttachToGridOrMap(book);

        if (TryComp<NecronomiconComponent>(book, out var bookComp))
            _audio.PlayPvs(bookComp.ManifestSound, book);

        _popup.PopupEntity(Loc.GetString("necro-book-appears"), book, PopupType.MediumCaution);
        _popup.PopupEntity(Loc.GetString("necro-book-manifested"), demon, demon, PopupType.Medium);
    }

    /// <summary>Pulls the book back into the demon. Re-checks the pin rule; nothing may bypass it.</summary>
    public bool DemanifestBook(Entity<NecroDemonComponent> demon)
    {
        if (!IsBookManifested(demon, out var book))
            return false;

        if (IsPinnedByCrew(book.Value, out _))
        {
            _popup.PopupEntity(Loc.GetString("necro-book-pinned"), demon, demon, PopupType.MediumCaution);
            return false;
        }

        // A Deadite carrying it lets go.
        var container = _container.EnsureContainer<ContainerSlot>(demon, NecroDemonComponent.BookContainerId);

        if (TryComp<FlammableComponent>(book, out var flammable) && flammable.OnFire)
            _flammable.Extinguish(book.Value, flammable);

        if (TryComp<NecronomiconComponent>(book, out var bookComp))
        {
            bookComp.BurnProgress = 0;
            bookComp.BurnStage = 0;
        }

        _popup.PopupEntity(Loc.GetString("necro-book-vanishes"), book.Value, PopupType.MediumCaution);

        // Take it out of whatever holds it (a Deadite's hands, a bag, a locker), then store it.
        if (_container.TryGetContainingContainer(book.Value, out var holder))
            _container.Remove(book.Value, holder, force: true);

        if (!_container.Insert(book.Value, container, force: true))
            return false;

        _popup.PopupEntity(Loc.GetString("necro-book-demanifested"), demon, demon, PopupType.Medium);
        return true;
    }

    private void OnBookExamined(Entity<NecronomiconComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.BurnProgress > 0)
            args.PushMarkup(Loc.GetString("necro-book-examine-burning",
                ("percent", (int) (ent.Comp.BurnProgress / ent.Comp.BurnTime * 100))));
    }

    private void OnBookTerminating(Entity<NecronomiconComponent> ent, ref EntityTerminatingEvent args)
    {
        if (ent.Comp.Destroyed || ent.Comp.Demon is not { } demon || TerminatingOrDeleted(demon))
            return;

        if (TryComp<NecroDemonComponent>(demon, out var demonComp) && demonComp.Book == ent.Owner)
        {
            // Only fire destroys the Necronomicon. Anything else just sends it back to the ether.
            demonComp.Book = null;
            _bookRestoreQueue.Add(demon);
        }
    }

    #endregion

    #region Burning

    private void UpdateBurning(float frameTime)
    {
        var query = EntityQueryEnumerator<NecronomiconComponent, FlammableComponent>();
        while (query.MoveNext(out var uid, out var book, out var flammable))
        {
            if (book.Destroyed)
                continue;

            if (flammable.OnFire)
            {
                if (book.BurnProgress <= 0)
                    _audio.PlayPvs(book.BurnSound, uid);

                book.BurnProgress += frameTime;
            }
            else if (book.BurnProgress > 0)
            {
                book.BurnProgress = MathF.Max(0, book.BurnProgress - book.BurnRecovery * frameTime);
                if (book.BurnProgress <= 0)
                    book.BurnStage = 0;
                continue;
            }
            else
            {
                continue;
            }

            var stage = (int) (book.BurnProgress / book.BurnTime * 4);
            if (stage > book.BurnStage && stage < 4)
            {
                book.BurnStage = stage;
                _popup.PopupEntity(Loc.GetString($"necro-book-burn-stage-{stage}"), uid, PopupType.LargeCaution);

                if (book.Demon is { } demon)
                    _popup.PopupEntity(Loc.GetString("necro-book-burning-demon"), demon, demon, PopupType.LargeCaution);
            }

            if (book.BurnProgress >= book.BurnTime)
                DestroyBook((uid, book));
        }
    }

    /// <summary>The book is gone for good. Raises <see cref="NecronomiconBurnedEvent"/> for the rule to end the round.</summary>
    public void DestroyBook(Entity<NecronomiconComponent> book)
    {
        if (book.Comp.Destroyed)
            return;

        book.Comp.Destroyed = true;
        var coords = Transform(book).Coordinates;
        _audio.PlayPvs(book.Comp.DestroyedSound, coords);
        _popup.PopupCoordinates(Loc.GetString("necro-book-destroyed"), coords, PopupType.LargeCaution);
        Spawn(book.Comp.DestroyedSpawn, coords);

        if (TryGetDemon(book.Comp.Demon, out var demon))
            Banish(demon.Value);

        var ev = new NecronomiconBurnedEvent(book.Comp.Demon);
        RaiseLocalEvent(ref ev);

        QueueDel(book);
    }

    /// <summary>Strips the demon of everything once the book is burned.</summary>
    public void Banish(Entity<NecroDemonComponent> demon)
    {
        if (demon.Comp.Banished)
            return;

        demon.Comp.Banished = true;
        demon.Comp.PendingTransition = NecroBookTransition.None;
        demon.Comp.PossessionCharges = 0;

        if (demon.Comp.Manifestation is { } form)
            ReturnToEther(form, false);

        if (TryComp<StoreComponent>(demon, out var store))
        {
            store.Balance.Clear();
            Dirty(demon, store);
            _store.UpdateUserInterface(null, demon, store);
        }

        _popup.PopupEntity(Loc.GetString("necro-banished"), demon, demon, PopupType.LargeCaution);
    }

    #endregion

    #region Recording

    private void OnRecordingUse(Entity<NecroRecordingComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = TryStartRecording(ent, args.User);
    }

    private void OnRecordingActivate(Entity<NecroRecordingComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex)
            return;

        args.Handled = TryStartRecording(ent, args.User);
    }

    private bool TryStartRecording(Entity<NecroRecordingComponent> ent, EntityUid user)
    {
        if (ent.Comp.Played)
        {
            _popup.PopupEntity(Loc.GetString("necro-recording-spent"), ent, user);
            return true;
        }

        // Kandarian entities cannot summon themselves.
        if (IsKandarian(user))
            return false;

        var args = new DoAfterArgs(EntityManager, user, ent.Comp.PlayTime, new NecroRecordingDoAfterEvent(), ent, target: ent)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
        };

        if (!_doAfter.TryStartDoAfter(args))
            return false;

        _audio.PlayPvs(ent.Comp.PlaySound, ent);
        _popup.PopupEntity(Loc.GetString("necro-recording-start"), ent, PopupType.Medium);
        return true;
    }

    private void OnRecordingDoAfter(Entity<NecroRecordingComponent> ent, ref NecroRecordingDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || ent.Comp.Played)
            return;

        args.Handled = true;
        ent.Comp.Played = true;
        _audio.PlayPvs(ent.Comp.SummonSound, ent);
        _popup.PopupEntity(Loc.GetString("necro-recording-finished"), ent, PopupType.LargeCaution);

        var ev = new NecroRecordingPlayedEvent(ent, args.User);
        RaiseLocalEvent(ref ev);
    }

    private void OnRecordingExamined(Entity<NecroRecordingComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString(ent.Comp.Played ? "necro-recording-examine-spent" : "necro-recording-examine"));
    }

    /// <summary>Wakes the demon. The book appears where the summoning happened.</summary>
    public void Awaken(Entity<NecroDemonComponent> demon, EntityCoordinates? bookCoords)
    {
        if (demon.Comp.Awakened || demon.Comp.Banished)
            return;

        demon.Comp.Awakened = true;
        EnsureBook(demon);
        ManifestBook(demon, bookCoords ?? Transform(demon).Coordinates);

        _popup.PopupEntity(Loc.GetString("necro-awakened"), demon, demon, PopupType.LargeCaution);
    }

    #endregion
}

/// <summary>Raised broadcast when the Necronomicon has fully burned.</summary>
[ByRefEvent]
public readonly record struct NecronomiconBurnedEvent(EntityUid? Demon);

/// <summary>Raised broadcast when the old recording finishes playing.</summary>
[ByRefEvent]
public readonly record struct NecroRecordingPlayedEvent(EntityUid Recording, EntityUid User);
