using Content.Shared.Store;
using Content.Shared.Store.Components;
using Robust.Shared.Prototypes;

namespace Content.Server.Necronomicon;

/// <summary>
/// Store condition: at most <see cref="Max"/> of <see cref="Listings"/> may be bought. Used by the Deadite
/// progression tree so a demon commits to two of the four paths. A listing that has already been bought always
/// passes this check (its own stock condition stops repeat purchases).
/// </summary>
public sealed partial class NecroPathLimitCondition : ListingCondition
{
    [DataField(required: true)]
    public HashSet<ProtoId<ListingPrototype>> Listings = new();

    [DataField]
    public int Max = 2;

    public override bool Condition(ListingConditionArgs args)
    {
        if (!args.EntityManager.TryGetComponent<StoreComponent>(args.StoreEntity, out var store))
            return false;

        var bought = 0;
        foreach (var listing in store.FullListingsCatalog)
        {
            if (listing.PurchaseAmount <= 0 || !Listings.Contains(listing.ID))
                continue;

            if (listing.ID == args.Listing.ID)
                return true;

            bought++;
        }

        return bought < Max;
    }
}
