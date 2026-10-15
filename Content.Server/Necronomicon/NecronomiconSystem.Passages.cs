using Content.Server.Necronomicon.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.IdentityManagement;
using Content.Shared.Movement.Systems;
using Content.Shared.Necronomicon;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server.Necronomicon;

/// <summary>
/// Passages from the book itself. A living, unpossessed person holding the Necronomicon can read from it:
/// <list type="bullet">
/// <item><b>Rebuking</b> (at a person): casts a Deadite out of its host, as an exorcism does, without needing them
/// restrained. Takes time and pins the Deadite down while it's read.</item>
/// <item><b>Unmaking</b> (at a Demonspawn): it melts into dust.</item>
/// <item><b>Read aloud</b> (from a random page): makes nearby disguised Deadites flinch, or wakes something.</item>
/// </list>
/// Every reading costs the reader (the words burn), leaves them open to JOIN US unless mindshielded, alerts the demon,
/// and the book needs a minute to rest before it can be read again.
/// </summary>
public sealed partial class NecronomiconSystem
{
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private MovementModStatusSystem _movementMod = default!;
    [Dependency] private DemonspawnSystem _demonspawn = default!;

    private static readonly EntProtoId BookSlowEffect = "DeaditeBiteSlowdownStatusEffect";

    private static readonly SpriteSpecifier PassageIcon =
        new SpriteSpecifier.Rsi(new ResPath("Objects/Misc/necronomicon.rsi"), "icon");

    private void InitializePassages()
    {
        SubscribeLocalEvent<NecronomiconComponent, GetVerbsEvent<UtilityVerb>>(OnBookTargetVerbs);
        SubscribeLocalEvent<NecronomiconComponent, GetVerbsEvent<AlternativeVerb>>(OnBookSelfVerbs);

        SubscribeLocalEvent<NecronomiconComponent, NecroBookRebukeDoAfterEvent>(OnRebukeDoAfter);
        SubscribeLocalEvent<NecronomiconComponent, NecroBookUnmakeDoAfterEvent>(OnUnmakeDoAfter);
        SubscribeLocalEvent<NecronomiconComponent, NecroBookReadAloudDoAfterEvent>(OnReadAloudDoAfter);
    }

    /// <summary>Only the living may read it against the dead, and only with the book in their hands.</summary>
    private bool CanRead(Entity<NecronomiconComponent> book, EntityUid reader)
    {
        return !IsKandarian(reader)
               && HasComp<HumanoidProfileComponent>(reader)
               && _mobState.IsAlive(reader)
               && _hands.IsHolding(reader, book.Owner);
    }

    private bool BookRested(Entity<NecronomiconComponent> book, EntityUid reader)
    {
        var now = _timing.CurTime;
        if (now >= book.Comp.NextReading)
            return true;

        _popup.PopupCursor(Loc.GetString("necro-passage-resting",
                ("seconds", (int) Math.Ceiling((book.Comp.NextReading - now).TotalSeconds))),
            reader, PopupType.SmallCaution);
        return false;
    }

    #region Verbs

    private void OnBookTargetVerbs(EntityUid uid, NecronomiconComponent comp, GetVerbsEvent<UtilityVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Using != uid || args.Target == args.User)
            return;

        var book = new Entity<NecronomiconComponent>(uid, comp);
        var reader = args.User;
        var target = args.Target;
        if (!CanRead(book, reader))
            return;

        if (HasComp<DemonspawnComponent>(target))
        {
            args.Verbs.Add(new UtilityVerb
            {
                Text = Loc.GetString("necro-passage-unmake-verb"),
                Message = Loc.GetString("necro-passage-unmake-verb-message"),
                Icon = PassageIcon,
                Impact = LogImpact.Medium,
                Act = () => StartReading(book, reader, target, book.Comp.UnmakeTime, new NecroBookUnmakeDoAfterEvent()),
            });
            return;
        }

        // Offered on anyone living, so the verb itself never tells you who is a Deadite.
        if (!HasComp<HumanoidProfileComponent>(target) || _mobState.IsDead(target))
            return;

        args.Verbs.Add(new UtilityVerb
        {
            Text = Loc.GetString("necro-passage-rebuke-verb"),
            Message = Loc.GetString("necro-passage-rebuke-verb-message"),
            Icon = PassageIcon,
            Impact = LogImpact.Medium,
            Act = () => StartReading(book, reader, target, book.Comp.RebukeTime, new NecroBookRebukeDoAfterEvent()),
        });
    }

    private void OnBookSelfVerbs(EntityUid uid, NecronomiconComponent comp, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var book = new Entity<NecronomiconComponent>(uid, comp);
        var reader = args.User;
        if (!CanRead(book, reader))
            return;

        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("necro-passage-aloud-verb"),
            Message = Loc.GetString("necro-passage-aloud-verb-message"),
            Icon = PassageIcon,
            Impact = LogImpact.Low,
            Act = () => StartReading(book, reader, null, book.Comp.ReadAloudTime, new NecroBookReadAloudDoAfterEvent()),
        });
    }

    #endregion

    private void StartReading(Entity<NecronomiconComponent> book, EntityUid reader, EntityUid? target,
        TimeSpan duration, SimpleDoAfterEvent ev)
    {
        if (!CanRead(book, reader) || !BookRested(book, reader))
            return;

        var doAfter = new DoAfterArgs(EntityManager, reader, duration, ev, book, target, book)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
            BreakOnHandChange = true,
            BreakOnDropItem = true,
            DistanceThreshold = target != null ? book.Comp.ReadRange + 0.5f : null,
            BlockDuplicate = true,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return;

        Chant(reader, _random.Next(ChantLines.Length));
        _popup.PopupEntity(Loc.GetString("necro-passage-start", ("user", Identity.Entity(reader, EntityManager))),
            reader, Filter.PvsExcept(reader), true, PopupType.Medium);
        _popup.PopupCursor(Loc.GetString("necro-passage-start-self"), reader, PopupType.Medium);

        if (target is not { } victim)
            return;

        // Whatever is inside them feels the words: it writhes and can barely move.
        if (TryComp<DeaditeComponent>(victim, out var deadite))
        {
            _jitter.DoJitter(victim, duration, true, 14f, 10f);
            _movementMod.TryUpdateMovementSpeedModDuration(victim, BookSlowEffect, duration, book.Comp.RebukeSlow);
            _popup.PopupCursor(Loc.GetString("necro-passage-rebuke-burns"), victim, PopupType.LargeCaution);

            if (TryGetDemon(deadite.Demon, out var demon))
            {
                demon.Value.Comp.ExorcismAttempts++;
                AlertDemon(demon.Value, Loc.GetString("necro-passage-demon-alert", ("target", Name(victim))));
            }
        }
        else if (HasComp<DemonspawnComponent>(victim))
        {
            _jitter.DoJitter(victim, duration, true, 14f, 10f);
            _movementMod.TryUpdateMovementSpeedModDuration(victim, BookSlowEffect, duration, book.Comp.RebukeSlow);
        }
    }

    /// <summary>Common cost of a finished reading: the book rests, the reader pays, the demon notices.</summary>
    private void FinishReading(Entity<NecronomiconComponent> book, EntityUid reader)
    {
        book.Comp.NextReading = _timing.CurTime + book.Comp.ReadCooldown;

        _damageable.TryChangeDamage(reader, book.Comp.ReaderToll, ignoreResistances: true, interruptsDoAfters: false);
        _jitter.DoJitter(reader, TimeSpan.FromSeconds(3), true, 6f, 5f);
        _popup.PopupCursor(Loc.GetString("necro-passage-toll"), reader, PopupType.MediumCaution);

        // Speaking the words opens a door. Only a mindshield keeps it shut.
        if (TryGetDemon(book.Comp.Demon, out var demon) && !IsMindshielded(reader) && !HasComp<NecroBlessedComponent>(reader))
            Prepare(demon.Value, reader);
    }

    private void AlertDemon(Entity<NecroDemonComponent> demon, string message)
    {
        _popup.PopupCursor(message, demon, PopupType.LargeCaution);
        if (demon.Comp.Manifestation is { } form)
            _popup.PopupCursor(message, form, PopupType.LargeCaution);
    }

    #region Completion

    private void OnRebukeDoAfter(Entity<NecronomiconComponent> ent, ref NecroBookRebukeDoAfterEvent args)
    {
        if (args.Handled || args.Target is not { } target)
            return;

        args.Handled = true;

        if (args.Cancelled)
        {
            _popup.PopupCursor(Loc.GetString("necro-passage-interrupted"), args.User, PopupType.SmallCaution);
            return;
        }

        FinishReading(ent, args.User);
        Chant(target, 0);

        if (!TryComp<DeaditeComponent>(target, out var deadite))
        {
            _popup.PopupCursor(Loc.GetString("necro-passage-rebuke-nothing", ("target", Identity.Entity(target, EntityManager))),
                args.User, PopupType.Medium);
            return;
        }

        _deadite.Exorcise((target, deadite), args.User, ent.Comp.RebukeBlessing);
        SupernaturalBurst(Transform(target).Coordinates, 4f, 3);
    }

    private void OnUnmakeDoAfter(Entity<NecronomiconComponent> ent, ref NecroBookUnmakeDoAfterEvent args)
    {
        if (args.Handled || args.Target is not { } target)
            return;

        args.Handled = true;

        if (args.Cancelled)
        {
            _popup.PopupCursor(Loc.GetString("necro-passage-interrupted"), args.User, PopupType.SmallCaution);
            return;
        }

        FinishReading(ent, args.User);

        if (TryComp<DemonspawnComponent>(target, out var spawn))
            _demonspawn.StartMelting((target, spawn), args.User);
    }

    private void OnReadAloudDoAfter(Entity<NecronomiconComponent> ent, ref NecroBookReadAloudDoAfterEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (args.Cancelled)
        {
            _popup.PopupCursor(Loc.GetString("necro-passage-interrupted"), args.User, PopupType.SmallCaution);
            return;
        }

        FinishReading(ent, args.User);

        var reader = args.User;
        var coords = Transform(reader).Coordinates;
        var roll = _random.NextFloat();

        if (roll < 0.4f)
        {
            // The dead stir: every disguised Deadite nearby flinches, and its eyes flash white.
            var found = 0;
            foreach (var deadite in _lookup.GetEntitiesInRange<DeaditeComponent>(coords, 7f))
            {
                if (deadite.Comp.Revealed)
                    continue;

                found++;
                _jitter.DoJitter(deadite, TimeSpan.FromSeconds(2), true, 10f, 8f);
                _popup.PopupEntity(Loc.GetString("necro-passage-aloud-flinch", ("target", Identity.Entity(deadite, EntityManager))),
                    deadite, PopupType.MediumCaution);
            }

            _popup.PopupCursor(Loc.GetString(found > 0 ? "necro-passage-aloud-stir" : "necro-passage-aloud-quiet"),
                reader, PopupType.Medium);
        }
        else if (roll < 0.75f)
        {
            // Something wakes. The book is fed.
            Chant(reader, 3);
            SupernaturalBurst(coords, 6f, 6);
            if (TryGetDemon(ent.Comp.Demon, out var demon))
            {
                AddSoulPower(demon.Value, 3);
                AlertDemon(demon.Value, Loc.GetString("necro-passage-aloud-demon", ("reader", Name(reader))));
            }

            _popup.PopupEntity(Loc.GetString("necro-passage-aloud-wake"), reader, PopupType.LargeCaution);
        }
        else
        {
            // Only whispers.
            _audio.PlayGlobal(new Robust.Shared.Audio.SoundCollectionSpecifier("NecroWhispers"), reader);
            _popup.PopupCursor(Loc.GetString("necro-passage-aloud-whispers"), reader, PopupType.Medium);
        }
    }

    #endregion
}
