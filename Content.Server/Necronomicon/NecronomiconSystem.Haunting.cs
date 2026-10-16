using Content.Server.Fluids.EntitySystems;
using Content.Server.Necronomicon.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Humanoid;
using Content.Shared.Popups;
using Robust.Shared.Random;

namespace Content.Server.Necronomicon;

/// <summary>
/// While the demon is awake the station itself turns: walls seep blood, things laugh, voices whisper to people who
/// are alone. It happens on its own, more often with every soul the book has consumed, and costs the demon nothing.
/// The book also works on whoever holds it: hold it long enough without a mindshield and it opens you to possession.
/// </summary>
public sealed partial class NecronomiconSystem
{
    [Dependency] private PuddleSystem _puddle = default!;

    private static readonly string BloodReagent = "Blood";

    private void UpdateHaunting(Entity<NecroDemonComponent> demon, TimeSpan now)
    {
        if (!demon.Comp.Awakened || demon.Comp.Banished || now < demon.Comp.NextHaunt)
            return;

        var interval = demon.Comp.HauntInterval - demon.Comp.HauntIntervalPerSoul * demon.Comp.Conversions;
        if (interval < demon.Comp.HauntMinimumInterval)
            interval = demon.Comp.HauntMinimumInterval;
        demon.Comp.NextHaunt = now + interval;

        // Someone living, on the demon's map.
        var map = Transform(demon).MapID;
        var candidates = new List<EntityUid>();
        var query = EntityQueryEnumerator<HumanoidProfileComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.MapID == map && IsEligibleSoul(uid))
                candidates.Add(uid);
        }

        if (candidates.Count == 0)
            return;

        var victim = _random.Pick(candidates);
        var coords = Transform(victim).Coordinates;
        var roll = _random.NextFloat();

        if (roll < 0.4f)
        {
            // The walls bleed.
            var spot = coords.Offset(_random.NextVector2(1f, 2.5f));
            _puddle.TrySpillAt(spot, new Solution(BloodReagent, 15), out _, sound: false);
            _popup.PopupCoordinates(Loc.GetString("necro-haunt-blood"), spot, PopupType.MediumCaution);
        }
        else if (roll < 0.7f)
        {
            // A voice, for them alone.
            _audio.PlayGlobal(demon.Comp.WhisperSound, victim);
            _popup.PopupCursor(Loc.GetString($"necro-haunt-whisper-{_random.Next(1, 6)}"), victim, PopupType.MediumCaution);
        }
        else
        {
            LivingRoomLaughter(coords, 4f, 1);
        }

        AddFear(demon, victim, NecroFearSource.Supernatural);
    }

    /// <summary>The book whispers to whoever holds it, and eventually it gets in.</summary>
    private void UpdateTemptation(TimeSpan now)
    {
        var query = EntityQueryEnumerator<NecronomiconComponent>();
        while (query.MoveNext(out var uid, out var book))
        {
            if (book.Destroyed || !IsPinnedByCrew(uid, out var holder))
            {
                book.TemptedHolder = null;
                continue;
            }

            if (book.TemptedHolder != holder)
            {
                book.TemptedHolder = holder;
                book.TemptedSince = now;
                book.NextTemptWhisper = now + book.TemptWhisperInterval;
                book.TemptedThisHold = false;
                continue;
            }

            if (holder is not { } carrier || book.TemptedThisHold)
                continue;

            if (now >= book.NextTemptWhisper)
            {
                book.NextTemptWhisper = now + book.TemptWhisperInterval;
                _popup.PopupCursor(Loc.GetString($"necro-tempt-{_random.Next(1, 5)}"), carrier, PopupType.Medium);
            }

            if (now - book.TemptedSince < book.TemptTime)
                continue;

            book.TemptedThisHold = true;
            if (IsMindshielded(carrier) || HasComp<NecroBlessedComponent>(carrier) || !TryGetDemon(book.Demon, out var demon))
            {
                _popup.PopupCursor(Loc.GetString("necro-tempt-resisted"), carrier, PopupType.Medium);
                continue;
            }

            Prepare(demon.Value, carrier);
            _audio.PlayGlobal(demon.Value.Comp.WhisperSound, carrier);
            _popup.PopupCursor(Loc.GetString("necro-tempt-opened"), carrier, PopupType.LargeCaution);
            _popup.PopupCursor(Loc.GetString("necro-tempt-demon", ("holder", Name(carrier))), demon.Value, PopupType.Medium);
        }
    }
}
