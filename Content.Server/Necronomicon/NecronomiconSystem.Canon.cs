using System.Linq;
using System.Numerics;
using System.Text;
using Content.Server.Necronomicon.Components;
using Content.Shared.Chat;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Hands;
using Content.Shared.Humanoid;
using Content.Shared.IdentityManagement;
using Content.Shared.Item;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Necronomicon;
using Content.Shared.Popups;
using Content.Shared.Speech;
using Content.Shared.Verbs;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Necronomicon;

/// <summary>
/// Pieces taken straight from the films (The Evil Dead, Evil Dead II, Army of Darkness):
/// <list type="bullet">
/// <item><b>The professor's log.</b> The recorder plays Professor Raymond Knowby's log aloud, and can be rewound.</item>
/// <item><b>Klaatu Barada Nikto.</b> Whoever takes the book from where it lies had better say the words first.
/// Fumble them, or don't say them, and the book bites, the dead rise and the demon grows stronger.</item>
/// <item><b>The lost pages.</b> Two passages: the first conjures the evil into the flesh and holds it there; the second
/// opens a rift that swallows the demon and the book. A second way for the crew to win, besides fire.</item>
/// <item><b>The living room.</b> When the demon's presence is felt, the furniture laughs.</item>
/// </list>
/// </summary>
public sealed partial class NecronomiconSystem
{
    private static readonly SoundSpecifier BookShriek = new SoundCollectionSpecifier("DeaditeScreams");
    private static readonly SoundSpecifier RoomLaugh = new SoundCollectionSpecifier("DeaditeLaughs", AudioParams.Default.WithVolume(-2f));
    private static readonly SoundSpecifier RiftSound = new SoundPathSpecifier("/Audio/Effects/Lightning/lightningbolt.ogg");
    private static readonly EntProtoId RiftEffect = "EffectFlashBluespace";

    private void InitializeCanon()
    {
        SubscribeLocalEvent<NecronomiconComponent, ListenEvent>(OnBookListen);
        SubscribeLocalEvent<NecronomiconComponent, GotEquippedHandEvent>(OnBookPickedUp);

        SubscribeLocalEvent<NecroLostPagesComponent, GetVerbsEvent<AlternativeVerb>>(OnPagesVerbs);
        SubscribeLocalEvent<NecroLostPagesComponent, NecroPagesConjureDoAfterEvent>(OnConjureDoAfter);
        SubscribeLocalEvent<NecroLostPagesComponent, NecroPagesRiftDoAfterEvent>(OnRiftDoAfter);
    }

    #region The professor's log

    public void StartRecordingLog(Entity<NecroRecordingComponent> recording)
    {
        recording.Comp.LogIndex = 0;
        recording.Comp.NextLogLine = _timing.CurTime + TimeSpan.FromSeconds(2);
    }

    private void UpdateRecordingLogs(TimeSpan now)
    {
        var query = EntityQueryEnumerator<NecroRecordingComponent>();
        while (query.MoveNext(out var uid, out var tape))
        {
            if (tape.LogIndex < 0 || now < tape.NextLogLine)
                continue;

            if (tape.LogIndex >= tape.LogLines.Count)
            {
                tape.LogIndex = -1;
                _popup.PopupEntity(Loc.GetString("necro-recording-log-ends"), uid, PopupType.Small);
                continue;
            }

            var line = Loc.GetString(tape.LogLines[tape.LogIndex]);
            tape.LogIndex++;
            tape.NextLogLine = now + tape.LogLineInterval;

            _chat.TrySendInGameICMessage(uid, line, InGameICChatType.Speak, hideChat: false, hideLog: true,
                nameOverride: Loc.GetString("necro-tape-speaker"), checkRadioPrefix: false, ignoreActionBlocker: true);
        }
    }

    #endregion

    #region Klaatu Barada Nikto

    /// <summary>The book lies in the world, not inside its demon.</summary>
    private bool IsBookLoose(EntityUid book)
    {
        return !_container.TryGetContainingContainer(book, out var container)
               || !HasComp<NecroDemonComponent>(container.Owner);
    }

    private static string Normalize(string message)
    {
        var builder = new StringBuilder(message.Length);
        foreach (var c in message.ToLowerInvariant())
        {
            builder.Append(char.IsLetter(c) ? c : ' ');
        }

        return builder.ToString();
    }

    private void OnBookListen(EntityUid uid, NecronomiconComponent comp, ListenEvent args)
    {
        if (comp.Destroyed || comp.Taken || !IsBookLoose(uid) || IsKandarian(args.Source))
            return;

        var words = Normalize(args.Message).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var klaatu = words.Contains("klaatu");
        var barada = words.Contains("barada");
        var nikto = words.Contains("nikto");

        if (!klaatu && !barada && !nikto)
            return;

        if (klaatu && barada && nikto)
        {
            comp.SpokeWords[args.Source] = _timing.CurTime;
            _popup.PopupEntity(Loc.GetString("necro-words-spoken"), uid, PopupType.Medium);
            return;
        }

        // Every... necessary word. Not quite.
        comp.Taken = true;
        _popup.PopupEntity(Loc.GetString("necro-words-fumbled", ("user", Identity.Entity(args.Source, EntityManager))),
            uid, PopupType.LargeCaution);
        BookCurse((uid, comp), args.Source);
    }

    private void OnBookPickedUp(EntityUid uid, NecronomiconComponent comp, GotEquippedHandEvent args)
    {
        if (comp.Destroyed || comp.Taken || IsKandarian(args.User))
            return;

        comp.Taken = true;

        if (comp.SpokeWords.TryGetValue(args.User, out var spoke) && _timing.CurTime - spoke <= comp.WordsWindow)
        {
            _popup.PopupCursor(Loc.GetString("necro-words-safe"), args.User, PopupType.Medium);
            return;
        }

        _popup.PopupEntity(Loc.GetString("necro-words-forgotten", ("user", Identity.Entity(args.User, EntityManager))),
            uid, PopupType.LargeCaution);
        _damageable.TryChangeDamage(args.User, comp.TakenCurse, origin: uid);
        BookCurse((uid, comp), args.User);
    }

    /// <summary>
    /// The book was taken carelessly: it shrieks, its demon grows stronger, every one of its Deadites gets back up,
    /// and the one who did it is left open to possession.
    /// </summary>
    private void BookCurse(Entity<NecronomiconComponent> book, EntityUid culprit)
    {
        _audio.PlayPvs(BookShriek, book);
        Chant(book, 3);
        SupernaturalBurst(Transform(book).Coordinates, 6f, 4);

        if (!TryGetDemon(book.Comp.Demon, out var demon) || demon.Value.Comp.Banished)
            return;

        AddSoulPower(demon.Value, book.Comp.FumbleReward);
        _popup.PopupCursor(Loc.GetString("necro-words-demon", ("user", Name(culprit))), demon.Value, PopupType.LargeCaution);

        if (!IsMindshielded(culprit) && !HasComp<NecroBlessedComponent>(culprit))
            Prepare(demon.Value, culprit);

        // The dead rise.
        var query = EntityQueryEnumerator<DeaditeComponent, MobStateComponent>();
        while (query.MoveNext(out var uid, out var deadite, out var mob))
        {
            if (deadite.Demon != demon.Value.Owner || deadite.Severed || mob.CurrentState == MobState.Alive)
                continue;

            _damageable.ClearAllDamage(uid);
            _mobState.ChangeMobState(uid, MobState.Alive, mob);
            _popup.PopupEntity(Loc.GetString("deadite-rises", ("target", Identity.Entity(uid, EntityManager))),
                uid, PopupType.LargeCaution);
        }
    }

    #endregion

    #region The living room

    /// <summary>A few things nearby (a lamp, a chair, a clock) start laughing.</summary>
    public void LivingRoomLaughter(EntityCoordinates coords, float range, int count)
    {
        var candidates = new List<EntityUid>();
        foreach (var uid in _lookup.GetEntitiesInRange(coords, range))
        {
            if (HasComp<MobStateComponent>(uid) || HasComp<NecronomiconComponent>(uid) || _container.IsEntityInContainer(uid))
                continue;

            if (!HasComp<ItemComponent>(uid) && !Transform(uid).Anchored)
                continue;

            candidates.Add(uid);
        }

        _random.Shuffle(candidates);
        foreach (var uid in candidates.Take(count))
        {
            _audio.PlayPvs(RoomLaugh, uid);
            _popup.PopupEntity(Loc.GetString("necro-room-laughs", ("thing", uid)), uid, PopupType.MediumCaution);
        }
    }

    #endregion

    #region The lost pages

    private bool CanRecite(EntityUid reader, EntityUid pages)
    {
        return !IsKandarian(reader)
               && HasComp<HumanoidProfileComponent>(reader)
               && _mobState.IsAlive(reader)
               && _hands.IsHolding(reader, pages);
    }

    /// <summary>The book, out in the world near the reader.</summary>
    private Entity<NecronomiconComponent>? FindBookNear(EntityUid reader, float range)
    {
        foreach (var book in _lookup.GetEntitiesInRange<NecronomiconComponent>(Transform(reader).Coordinates, range))
        {
            if (!book.Comp.Destroyed && IsBookLoose(book))
                return book;
        }

        return null;
    }

    private void OnPagesVerbs(EntityUid uid, NecroLostPagesComponent comp, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !CanRecite(args.User, uid))
            return;

        var reader = args.User;
        var pages = new Entity<NecroLostPagesComponent>(uid, comp);

        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("necro-pages-conjure-verb"),
            Message = Loc.GetString("necro-pages-conjure-verb-message"),
            Impact = LogImpact.High,
            Priority = 2,
            Act = () => StartReciting(pages, reader, comp.ConjureTime, new NecroPagesConjureDoAfterEvent()),
        });

        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("necro-pages-rift-verb"),
            Message = Loc.GetString("necro-pages-rift-verb-message"),
            Impact = LogImpact.High,
            Priority = 1,
            Act = () => StartReciting(pages, reader, comp.RiftTime, new NecroPagesRiftDoAfterEvent()),
        });
    }

    private void StartReciting(Entity<NecroLostPagesComponent> pages, EntityUid reader, TimeSpan duration, SimpleDoAfterEvent ev)
    {
        if (!CanRecite(reader, pages))
            return;

        if (FindBookNear(reader, pages.Comp.BookRange) is not { } book)
        {
            _popup.PopupCursor(Loc.GetString("necro-pages-no-book"), reader, PopupType.SmallCaution);
            return;
        }

        var doAfter = new DoAfterArgs(EntityManager, reader, duration, ev, pages, book, pages)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
            BreakOnHandChange = true,
            BreakOnDropItem = true,
            DistanceThreshold = pages.Comp.BookRange + 1f,
            BlockDuplicate = true,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return;

        Chant(reader, _random.Next(ChantLines.Length));
        _popup.PopupEntity(Loc.GetString("necro-pages-start", ("user", Identity.Entity(reader, EntityManager))),
            reader, Filter.PvsExcept(reader), true, PopupType.LargeCaution);
        _popup.PopupCursor(Loc.GetString("necro-pages-start-self", ("seconds", (int) duration.TotalSeconds)),
            reader, PopupType.Medium);

        if (TryGetDemon(book.Comp.Demon, out var demon))
            AlertDemon(demon.Value, Loc.GetString("necro-pages-demon-alert", ("reader", Name(reader))));
    }

    private bool TryPagesDoAfter(DoAfterEvent args, out EntityUid reader, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out Entity<NecronomiconComponent>? book)
    {
        reader = args.User;
        book = null;

        if (args.Cancelled)
        {
            _popup.PopupCursor(Loc.GetString("necro-passage-interrupted"), reader, PopupType.SmallCaution);
            return false;
        }

        if (args.Target is not { } target || !TryComp<NecronomiconComponent>(target, out var comp) || comp.Destroyed)
            return false;

        book = (target, comp);
        return true;
    }

    /// <summary>First passage: the evil is dragged into a body of flesh and held there.</summary>
    private void OnConjureDoAfter(Entity<NecroLostPagesComponent> ent, ref NecroPagesConjureDoAfterEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (!TryPagesDoAfter(args, out var reader, out var book)
            || !TryGetDemon(book.Value.Comp.Demon, out var demon) || demon.Value.Comp.Banished)
            return;

        var now = _timing.CurTime;
        demon.Value.Comp.FleshBoundUntil = now + demon.Value.Comp.FleshBindDuration;

        if (demon.Value.Comp.Manifestation == null)
        {
            EntityUid? mind = _mind.TryGetMind(demon.Value, out var mindId, out _) ? mindId : null;
            ManifestPhysicalForm(demon.Value, mind);
        }

        var form = demon.Value.Comp.Manifestation ?? demon.Value.Owner;
        _popup.PopupCursor(Loc.GetString("necro-pages-conjured-demon",
                ("minutes", (int) demon.Value.Comp.FleshBindDuration.TotalMinutes)),
            form, PopupType.LargeCaution);
        _popup.PopupCursor(Loc.GetString("necro-pages-conjured"), reader, PopupType.Large);
        SupernaturalBurst(Transform(reader).Coordinates, 6f, 4);
    }

    /// <summary>Second passage: the rift opens and swallows the demon and its book, if the demon is in the flesh.</summary>
    private void OnRiftDoAfter(Entity<NecroLostPagesComponent> ent, ref NecroPagesRiftDoAfterEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (!TryPagesDoAfter(args, out var reader, out var book)
            || !TryGetDemon(book.Value.Comp.Demon, out var demon) || demon.Value.Comp.Banished)
            return;

        var riftCoords = Transform(reader).Coordinates;
        Spawn(RiftEffect, riftCoords);
        _audio.PlayPvs(RiftSound, riftCoords);

        // The rift can only take what has a body, and only if that body is close enough to be dragged in.
        if (demon.Value.Comp.Manifestation is not { } form
            || !_transform.InRange(riftCoords, Transform(form).Coordinates, ent.Comp.RiftPullRange * 2))
        {
            _popup.PopupCoordinates(Loc.GetString("necro-pages-rift-empty"), riftCoords, PopupType.LargeCaution);
            return;
        }

        // Everything loose is dragged toward the vortex.
        var riftPos = _transform.ToMapCoordinates(riftCoords).Position;
        foreach (var uid in _lookup.GetEntitiesInRange(riftCoords, ent.Comp.RiftPullRange))
        {
            if (uid == reader || _container.IsEntityInContainer(uid) || Transform(uid).Anchored)
                continue;

            if (!HasComp<ItemComponent>(uid) && !HasComp<MobStateComponent>(uid))
                continue;

            var direction = riftPos - _transform.GetWorldPosition(uid);
            if (direction.LengthSquared() > 0.01f)
                _throwing.TryThrow(uid, direction, baseThrowSpeed: 7f, recoil: false, playSound: false);
        }

        _popup.PopupEntity(Loc.GetString("necro-pages-rift-swallows", ("demon", form)), form, PopupType.LargeCaution);
        DestroyBook(book.Value, rift: true);
    }

    #endregion
}
