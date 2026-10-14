using Content.Server.Necronomicon.Components;
using Content.Shared.Bible.Components;
using Content.Shared.Buckle.Components;
using Content.Shared.Cuffs;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Humanoid;
using Content.Shared.IdentityManagement;
using Content.Shared.Mobs;
using Content.Shared.Necronomicon;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Audio;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server.Necronomicon;

/// <summary>
/// Chaplain exorcism. With a Bible in hand, a chaplain can perform a rite over a restrained, buckled or incapacitated
/// person (or a severed Deadite head). The rite takes time, is broken by moving or being hurt, and has a cooldown.
/// If the person is a Deadite, the curse is cast out: they come back as themselves, healed, and the book can't claim
/// them again for a while. A mindshield implanted in the Deadite weakens the demon's grip and speeds the rite up;
/// the Infiltrator tree slows it down.
/// </summary>
public sealed partial class DeaditeSystem
{
    [Dependency] private SharedCuffableSystem _cuffable = default!;

    private static readonly SoundSpecifier ExorcismStartSound = new SoundCollectionSpecifier("BibleHit");
    private static readonly SoundSpecifier ExorcismBurnSound = new SoundCollectionSpecifier("BibleSizzle");
    private static readonly SoundSpecifier ExorcismSuccessSound = new SoundCollectionSpecifier("BibleHeal");
    private static readonly EntProtoId HolyLight = "HolyLightEffect";

    private static readonly SpriteSpecifier ExorciseIcon =
        new SpriteSpecifier.Rsi(new ResPath("Objects/Specific/Chapel/bible.rsi"), "icon");

    private void InitializeExorcism()
    {
        SubscribeLocalEvent<BibleComponent, GetVerbsEvent<UtilityVerb>>(OnBibleVerbs);
        SubscribeLocalEvent<NecroExorcistComponent, DeaditeExorcismDoAfterEvent>(OnExorcismDoAfter);
    }

    private void OnBibleVerbs(EntityUid uid, BibleComponent comp, GetVerbsEvent<UtilityVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Using != uid)
            return;

        var user = args.User;
        var target = args.Target;

        // Offered on every living person, so the verb itself never gives away who is a Deadite.
        if (target == user || !HasComp<BibleUserComponent>(user) || !HasComp<HumanoidProfileComponent>(target))
            return;

        if (_mobState.IsDead(target))
            return;

        args.Verbs.Add(new UtilityVerb
        {
            Text = Loc.GetString("deadite-exorcise-verb"),
            Message = Loc.GetString("deadite-exorcise-verb-message"),
            Icon = ExorciseIcon,
            Impact = LogImpact.Medium,
            Act = () => TryStartExorcism(user, target, uid),
        });
    }

    /// <summary>The rite needs a subject who can't simply walk away.</summary>
    private bool IsRestrained(EntityUid target)
    {
        if (TryComp<DeaditeComponent>(target, out var deadite) && deadite.Severed)
            return true;

        // Someone the demon is speaking through is lying helpless on the floor anyway.
        if (HasComp<NecroPuppetHostComponent>(target))
            return true;

        if (_mobState.IsIncapacitated(target))
            return true;

        if (_cuffable.IsCuffed(target))
            return true;

        return TryComp<BuckleComponent>(target, out var buckle) && buckle.Buckled;
    }

    public bool TryStartExorcism(EntityUid user, EntityUid target, EntityUid bible)
    {
        var exorcist = EnsureComp<NecroExorcistComponent>(user);
        var now = _timing.CurTime;

        if (now < exorcist.NextExorcism)
        {
            _popup.PopupEntity(Loc.GetString("deadite-exorcise-cooldown",
                    ("seconds", (int) Math.Ceiling((exorcist.NextExorcism - now).TotalSeconds))),
                user, user, PopupType.SmallCaution);
            return false;
        }

        if (HasComp<NecroBeingExorcisedComponent>(target))
        {
            _popup.PopupEntity(Loc.GetString("deadite-exorcise-already"), user, user, PopupType.SmallCaution);
            return false;
        }

        if (!IsRestrained(target))
        {
            _popup.PopupEntity(Loc.GetString("deadite-exorcise-not-restrained",
                    ("target", Identity.Entity(target, EntityManager))),
                user, user, PopupType.SmallCaution);
            return false;
        }

        var duration = GetExorcismDuration(exorcist, target);
        var doAfter = new DoAfterArgs(EntityManager, user, duration, new DeaditeExorcismDoAfterEvent(), user, target, bible)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
            BreakOnHandChange = true,
            BreakOnDropItem = true,
            DistanceThreshold = exorcist.Range,
            BlockDuplicate = true,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return false;

        var marker = EnsureComp<NecroBeingExorcisedComponent>(target);
        marker.Exorcist = user;
        marker.ExpiresAt = now + duration + TimeSpan.FromSeconds(5);

        var userIdentity = Identity.Entity(user, EntityManager);
        var targetIdentity = Identity.Entity(target, EntityManager);

        _audio.PlayPvs(ExorcismStartSound, target);
        _popup.PopupEntity(Loc.GetString("deadite-exorcise-start", ("user", userIdentity), ("target", targetIdentity)),
            user, Filter.PvsExcept(user), true, PopupType.Medium);
        _popup.PopupEntity(Loc.GetString("deadite-exorcise-start-self", ("target", targetIdentity),
                ("seconds", (int) duration.TotalSeconds)),
            user, user, PopupType.Medium);

        if (TryComp<DeaditeComponent>(target, out var deadite))
        {
            // The thing inside fights back: it's obvious to everyone watching.
            _audio.PlayPvs(ExorcismBurnSound, target);
            _audio.PlayPvs(ScreamSound, target);
            _jitter.DoJitter(target, duration, true, 14f, 10f);
            _popup.PopupEntity(Loc.GetString("deadite-exorcise-writhe", ("target", targetIdentity)),
                target, Filter.PvsExcept(target), true, PopupType.LargeCaution);
            _popup.PopupEntity(Loc.GetString("deadite-exorcise-burns"), target, target, PopupType.LargeCaution);

            if (_necro.TryGetDemon(deadite.Demon, out var demon))
            {
                demon.Value.Comp.ExorcismAttempts++;
                var alert = Loc.GetString("deadite-exorcise-demon-alert", ("target", Name(target)));
                _popup.PopupEntity(alert, demon.Value, demon.Value, PopupType.LargeCaution);
                if (demon.Value.Comp.Manifestation is { } form)
                    _popup.PopupEntity(alert, form, form, PopupType.LargeCaution);
            }
        }
        else
        {
            _popup.PopupEntity(Loc.GetString("deadite-exorcise-start-victim"), target, target, PopupType.Medium);
        }

        return true;
    }

    private TimeSpan GetExorcismDuration(NecroExorcistComponent exorcist, EntityUid target)
    {
        var duration = exorcist.Duration;

        if (!TryComp<DeaditeComponent>(target, out var deadite))
            return duration;

        // The demon's grip is weaker on a mind that has a shield in it again.
        if (_necro.IsMindshielded(target))
            duration *= exorcist.MindshieldMultiplier;

        if (HasTree(deadite, NecroUpgrade.InfiltratorII))
            duration *= deadite.InfiltratorExorcismMultiplier;

        return duration;
    }

    private void OnExorcismDoAfter(Entity<NecroExorcistComponent> ent, ref DeaditeExorcismDoAfterEvent args)
    {
        if (args.Target is not { } target)
            return;

        RemComp<NecroBeingExorcisedComponent>(target);

        if (args.Handled)
            return;

        args.Handled = true;

        if (args.Cancelled)
        {
            _popup.PopupEntity(Loc.GetString("deadite-exorcise-interrupted"), args.User, args.User, PopupType.SmallCaution);
            if (HasComp<DeaditeComponent>(target))
                _popup.PopupEntity(Loc.GetString("deadite-exorcise-interrupted-deadite"), target, target, PopupType.Medium);
            return;
        }

        // Whatever the result, the chaplain needs to recover before trying again.
        ent.Comp.NextExorcism = _timing.CurTime + ent.Comp.Cooldown;

        if (!IsRestrained(target) || _mobState.IsDead(target))
        {
            _popup.PopupEntity(Loc.GetString("deadite-exorcise-not-restrained",
                    ("target", Identity.Entity(target, EntityManager))),
                args.User, args.User, PopupType.SmallCaution);
            return;
        }

        if (!TryComp<DeaditeComponent>(target, out var deadite))
        {
            // Demonic possession (puppeting) is driven out too.
            if (TryComp<NecroPuppetHostComponent>(target, out var host) && _necro.TryGetDemon(host.Demon, out var puppeteer))
            {
                _necro.EndPuppet(puppeteer.Value);
                Spawn(HolyLight, Transform(target).Coordinates);
                _audio.PlayPvs(ExorcismSuccessSound, target);
                _popup.PopupEntity(Loc.GetString("deadite-exorcise-puppet", ("target", Identity.Entity(target, EntityManager))),
                    target, PopupType.Large);
                return;
            }

            _popup.PopupEntity(Loc.GetString("deadite-exorcise-nothing", ("target", Identity.Entity(target, EntityManager))),
                args.User, args.User, PopupType.Medium);
            return;
        }

        Exorcise((target, deadite), args.User, ent.Comp.BlessingDuration);
    }

    /// <summary>
    /// Casts the demon out of a Deadite: every Kandarian trait is stripped, their own face comes back, their wounds
    /// close, and they are blessed against being claimed again for <paramref name="blessing"/>.
    /// </summary>
    public void Exorcise(Entity<DeaditeComponent> ent, EntityUid? exorcist, TimeSpan blessing)
    {
        var target = ent.Owner;
        var name = Name(target);
        var demonUid = ent.Comp.Demon;

        // Put their face back before the component (and the stored colours) go away.
        if (ent.Comp.Revealed)
            RestoreOriginalLook(ent.Comp, target);

        StripDeadite(target, ent.Comp);

        _damageable.ClearAllDamage(target);
        _mobState.ChangeMobState(target, MobState.Alive);
        // Wrung out: they collapse for a moment once the thing leaves them.
        _stun.TryKnockdown(target, TimeSpan.FromSeconds(4));

        var blessed = EnsureComp<NecroBlessedComponent>(target);
        blessed.ExpiresAt = _timing.CurTime + blessing;

        var coords = Transform(target).Coordinates;
        Spawn(HolyLight, coords);
        _audio.PlayPvs(ExorcismSuccessSound, target);
        _audio.PlayPvs(ScreamSound, target);
        _popup.PopupEntity(Loc.GetString("deadite-exorcise-success", ("target", Identity.Entity(target, EntityManager))),
            target, Filter.PvsExcept(target), true, PopupType.Large);
        _popup.PopupEntity(Loc.GetString("deadite-exorcise-success-victim"), target, target, PopupType.Large);

        if (exorcist is { } user)
        {
            _popup.PopupEntity(Loc.GetString("deadite-exorcise-success-self", ("target", Identity.Entity(target, EntityManager))),
                user, user, PopupType.Large);
        }

        if (_necro.TryGetDemon(demonUid, out var demon))
        {
            demon.Value.Comp.Exorcisms++;
            demon.Value.Comp.ExorcisedNames.Add(name);

            var lost = Loc.GetString("deadite-exorcise-demon-lost", ("target", name));
            _popup.PopupEntity(lost, demon.Value, demon.Value, PopupType.LargeCaution);
            if (demon.Value.Comp.Manifestation is { } form)
                _popup.PopupEntity(lost, form, form, PopupType.LargeCaution);
        }
    }

    private void UpdateExorcism(TimeSpan now)
    {
        var blessed = EntityQueryEnumerator<NecroBlessedComponent>();
        while (blessed.MoveNext(out var uid, out var comp))
        {
            if (now < comp.ExpiresAt)
                continue;

            RemCompDeferred<NecroBlessedComponent>(uid);
            _popup.PopupEntity(Loc.GetString("deadite-blessing-faded"), uid, uid, PopupType.Small);
        }

        var exorcising = EntityQueryEnumerator<NecroBeingExorcisedComponent>();
        while (exorcising.MoveNext(out var uid, out var comp))
        {
            if (now >= comp.ExpiresAt)
                RemCompDeferred<NecroBeingExorcisedComponent>(uid);
        }
    }
}
