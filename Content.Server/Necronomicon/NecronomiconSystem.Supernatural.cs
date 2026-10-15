using System.Numerics;
using Content.Server.Necronomicon.Components;
using Content.Shared.Ghost.Components;
using Content.Shared.Humanoid;
using Content.Shared.IdentityManagement;
using Content.Shared.Item;
using Content.Shared.Necronomicon;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Random;

namespace Content.Server.Necronomicon;

/// <summary>
/// Supernatural flavour shared by every Kandarian event (possession, Deadites rising, physical manifestation,
/// Demonspawn): the Kandarian chant, flickering lights, banging lockers and objects flying around.
/// Also the physical form's own powers: Unholy Roar and Poltergeist.
/// </summary>
public sealed partial class NecronomiconSystem
{
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private ThrowingSystem _throwing = default!;

    /// <summary>The words from the recording, in order. Possession cycles through them.</summary>
    public static readonly string[] ChantLines =
    {
        "necro-chant-1", "necro-chant-2", "necro-chant-3", "necro-chant-4", "necro-chant-5", "necro-chant-6",
    };

    private static readonly TimeSpan ChantInterval = TimeSpan.FromSeconds(1.5);

    private static readonly SoundSpecifier RoarSound =
        new SoundCollectionSpecifier("DeaditeScreams", AudioParams.Default.WithVolume(8f).WithMaxDistance(25f));

    private static readonly SoundSpecifier ChantSound =
        new SoundCollectionSpecifier("NecroWhispers", AudioParams.Default.WithVolume(-2f));

    private void InitializeSupernatural()
    {
        SubscribeLocalEvent<NecroManifestationComponent, NecroUnholyRoarActionEvent>(OnUnholyRoar);
        SubscribeLocalEvent<NecroManifestationComponent, NecroPoltergeistActionEvent>(OnPoltergeist);
    }

    /// <summary>One line of the Kandarian incantation, rising over <paramref name="at"/> for everyone nearby.</summary>
    public void Chant(EntityUid at, int index)
    {
        var line = ChantLines[Math.Abs(index) % ChantLines.Length];
        _popup.PopupEntity(Loc.GetString(line), at, PopupType.LargeCaution);
        _audio.PlayPvs(ChantSound, at);
    }

    /// <summary>
    /// A poltergeist burst: lights flicker, lockers and machines rattle, and a few loose objects are flung around.
    /// </summary>
    public void SupernaturalBurst(EntityCoordinates coords, float range, int maxThrown, EntityUid? aimAt = null)
    {
        foreach (var uid in _lookup.GetEntitiesInRange(coords, range))
        {
            // Lights flicker, lockers bang, machines glitch.
            _ghost.DoGhostBooEvent(uid, GhostBooIntensity.Normal);
        }

        if (maxThrown <= 0)
            return;

        var items = new List<EntityUid>();
        foreach (var item in _lookup.GetEntitiesInRange<ItemComponent>(coords, range))
        {
            var uid = item.Owner;
            if (_container.IsEntityOrParentInContainer(uid) || Transform(uid).Anchored)
                continue;

            // The book never throws itself around.
            if (HasComp<NecronomiconComponent>(uid))
                continue;

            items.Add(uid);
        }

        _random.Shuffle(items);
        var thrown = 0;
        foreach (var uid in items)
        {
            if (thrown >= maxThrown)
                break;

            Vector2 direction;
            if (aimAt is { } target && !TerminatingOrDeleted(target))
            {
                direction = _transform.GetWorldPosition(target) - _transform.GetWorldPosition(uid);
                if (direction.LengthSquared() < 0.01f)
                    direction = _random.NextVector2();
            }
            else
            {
                direction = _random.NextVector2(1.5f, 4f);
            }

            if (_throwing.TryThrow(uid, direction, baseThrowSpeed: aimAt != null ? 9f : 5f, recoil: false, playSound: false))
                thrown++;
        }
    }

    #region Physical form powers

    private void OnUnholyRoar(Entity<NecroManifestationComponent> ent, ref NecroUnholyRoarActionEvent args)
    {
        if (args.Handled || !TryGetDemon(ent.Comp.Demon, out var demon) || demon.Value.Comp.Banished)
            return;

        args.Handled = true;

        var coords = Transform(ent).Coordinates;
        _audio.PlayPvs(RoarSound, ent);
        _jitter.DoJitter(ent, TimeSpan.FromSeconds(2), true, 12f, 8f);
        _popup.PopupEntity(Loc.GetString("necro-roar", ("demon", ent.Owner)), ent, PopupType.LargeCaution);

        foreach (var victim in _lookup.GetEntitiesInRange<HumanoidProfileComponent>(coords, demon.Value.Comp.RoarRange))
        {
            var uid = victim.Owner;
            if (IsKandarian(uid) || !_mobState.IsAlive(uid))
                continue;

            _jitter.DoJitter(uid, TimeSpan.FromSeconds(3), true, 8f, 6f);

            if (IsMindshielded(uid)
                || !_transform.InRange(coords, Transform(uid).Coordinates, demon.Value.Comp.RoarKnockdownRange))
                continue;

            _stun.TryKnockdown(uid, demon.Value.Comp.RoarKnockdownTime);
            AddFear(demon.Value, uid, NecroFearSource.Attacked);
        }

        SupernaturalBurst(coords, demon.Value.Comp.RoarRange, 4);
        AddFearAround(demon.Value, ent, demon.Value.Comp.RoarRange, NecroFearSource.Supernatural);
    }

    private void OnPoltergeist(Entity<NecroManifestationComponent> ent, ref NecroPoltergeistActionEvent args)
    {
        if (args.Handled || !TryGetDemon(ent.Comp.Demon, out var demon) || demon.Value.Comp.Banished)
            return;

        args.Handled = true;

        var coords = Transform(ent).Coordinates;

        // Aim at the nearest living person, if there is one.
        EntityUid? nearest = null;
        var best = float.MaxValue;
        var origin = _transform.GetWorldPosition(ent);
        foreach (var victim in _lookup.GetEntitiesInRange<HumanoidProfileComponent>(coords, demon.Value.Comp.PoltergeistRange))
        {
            if (IsKandarian(victim.Owner) || !_mobState.IsAlive(victim.Owner))
                continue;

            var distance = (_transform.GetWorldPosition(victim.Owner) - origin).LengthSquared();
            if (distance < best)
            {
                best = distance;
                nearest = victim.Owner;
            }
        }

        Chant(ent, _random.Next(ChantLines.Length));
        SupernaturalBurst(coords, demon.Value.Comp.PoltergeistRange, demon.Value.Comp.PoltergeistObjects, nearest);

        if (nearest is { } target)
        {
            _popup.PopupEntity(Loc.GetString("necro-poltergeist", ("target", Identity.Entity(target, EntityManager))),
                target, PopupType.MediumCaution);
            AddFear(demon.Value, target, NecroFearSource.Supernatural);
        }

        AddFearAround(demon.Value, ent, demon.Value.Comp.PoltergeistRange, NecroFearSource.Witness);
    }

    #endregion
}
