using Content.Server.Necronomicon.Components;
using Content.Shared.Alert;
using Content.Shared.Atmos.Components;
using Content.Shared.IdentityManagement;
using Content.Shared.Necronomicon;
using Content.Shared.Popups;
using Robust.Shared.Prototypes;

namespace Content.Server.Necronomicon;

/// <summary>
/// The demon's HUD: Soul Power and charge counters plus a book-state alert, and popups when the book changes hands
/// or catches fire. Mirrored onto the physical form while the demon wears it.
/// </summary>
public sealed partial class NecronomiconSystem
{
    [Dependency] private AlertsSystem _alerts = default!;

    private static readonly ProtoId<AlertPrototype> SoulPowerAlert = "NecroSoulPower";
    private static readonly ProtoId<AlertPrototype> ChargesAlert = "NecroCharges";
    private static readonly ProtoId<AlertPrototype> BookAlert = "NecroBook";

    private static readonly TimeSpan StatusInterval = TimeSpan.FromSeconds(0.25);

    public NecroBookState GetBookState(Entity<NecroDemonComponent> demon, out EntityUid? carrier)
    {
        carrier = null;
        if (!IsBookManifested(demon, out var book))
            return NecroBookState.Ether;

        if (TryComp<FlammableComponent>(book, out var flammable) && flammable.OnFire)
            return NecroBookState.Burning;

        return IsPinnedByCrew(book.Value, out carrier) ? NecroBookState.Held : NecroBookState.Manifested;
    }

    private void UpdateStatus(Entity<NecroDemonComponent> demon, TimeSpan now)
    {
        if (now < demon.Comp.NextStatusUpdate)
            return;

        demon.Comp.NextStatusUpdate = now + StatusInterval;

        EntityUid? carrier = null;
        var bookState = demon.Comp.Banished ? NecroBookState.Ether : GetBookState(demon, out carrier);
        var sp = GetSoulPower(demon).Int();
        var charges = demon.Comp.PossessionCharges;

        // Tell the demon when its book changes hands or catches fire.
        if (!demon.Comp.Banished && bookState != demon.Comp.LastBookState)
        {
            var viewer = demon.Comp.Manifestation ?? demon.Owner;
            switch (bookState)
            {
                case NecroBookState.Held when carrier is { } holder:
                    _popup.PopupEntity(Loc.GetString("necro-status-book-held",
                        ("holder", Identity.Entity(holder, EntityManager))), viewer, viewer, PopupType.LargeCaution);
                    break;
                case NecroBookState.Manifested when demon.Comp.LastBookState == NecroBookState.Held:
                    _popup.PopupEntity(Loc.GetString("necro-status-book-released"), viewer, viewer, PopupType.Medium);
                    break;
                case NecroBookState.Burning:
                    _popup.PopupEntity(Loc.GetString("necro-status-book-burning"), viewer, viewer, PopupType.LargeCaution);
                    break;
                case NecroBookState.Manifested when demon.Comp.LastBookState == NecroBookState.Burning:
                    _popup.PopupEntity(Loc.GetString("necro-status-book-extinguished"), viewer, viewer, PopupType.Medium);
                    break;
            }

            demon.Comp.LastBookState = bookState;
        }

        ApplyStatus(demon.Owner, sp, charges, bookState, demon.Comp.Banished);

        if (demon.Comp.Manifestation is { } form && !TerminatingOrDeleted(form))
            ApplyStatus(form, sp, charges, bookState, demon.Comp.Banished);
    }

    private void ApplyStatus(EntityUid uid, int sp, int charges, NecroBookState bookState, bool banished)
    {
        EnsureComp<AlertsComponent>(uid);
        var status = EnsureComp<NecroDemonStatusComponent>(uid);
        if (status.SoulPower != sp || status.Charges != charges || status.BookState != bookState)
        {
            status.SoulPower = sp;
            status.Charges = charges;
            status.BookState = bookState;
            Dirty(uid, status);
        }

        if (banished)
        {
            _alerts.ClearAlert(uid, SoulPowerAlert);
            _alerts.ClearAlert(uid, ChargesAlert);
            _alerts.ClearAlert(uid, BookAlert);
            return;
        }

        _alerts.ShowAlert(uid, SoulPowerAlert);
        _alerts.ShowAlert(uid, ChargesAlert);
        _alerts.ShowAlert(uid, BookAlert, (short) ((int) bookState + 1));
    }
}
