using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using OldWestTown.Rivals;

namespace OldWestTown.Shops
{
    public class CompProperties_Business : CompProperties
    {
        public ShopKindDef shopKind;

        /// <summary>
        /// Fallback sales-floor radius, used when the counter is outdoors or roomless.
        /// Indoors the shop's floor is its room, which is what a real storefront wants.
        /// </summary>
        public float openAirRadius = 9.9f;

        /// <summary>Silver seeded into a freshly built counter's till, once, on first spawn.
        /// Zero for every kind but the gambling hall — an ordinary shop's till legitimately
        /// starts empty, since nothing it does can ever owe a customer more than they handed
        /// over. A wager can, so its table needs a bankroll before its first customer ever sits
        /// down; see CompBusiness.PostSpawnSetup.</summary>
        public int startingTillSilver = 0;

        public CompProperties_Business()
        {
            compClass = typeof(CompBusiness);
        }
    }

    /// <summary>Marker a customer-side JobDriver implements so business-layer code (which must
    /// not depend on the AI namespace) can recognize "is this pawn patronizing ANY business"
    /// without naming a concrete driver type or JobDef.</summary>
    public interface IBusinessPatron
    {
        bool WaitingForService { get; }
    }

    /// <summary>
    /// Turns a building into a business: it owns the sales floor (goods, services, or both),
    /// the price list and the till.
    ///
    /// The two pawn-side loops (a colonist manning the counter, a customer buying something or
    /// using a service) never talk to each other directly — they only read and write state
    /// here. That keeps a shopkeeper wandering off mid-sale from stranding a customer in a
    /// broken job.
    /// </summary>
    public class CompBusiness : ThingComp, IThingHolder
    {
        private const int StaffPresenceGraceTicks = 60;
        private const int StockCacheTicks = 60;

        /// <summary>How far a faction's standing has to diverge from the town's own name
        /// before the ledger calls it out by name — a small divergence isn't a "regular" yet.</summary>
        private const float LedgerStandingDivergenceThreshold = 0.1f;

        private ThingOwner<Thing> till;
        private ThingFilter stockFilter;
        private bool open = true;
        private float markup = -1f;

        private float houseEdge = -1f;

        private int lastStaffedTick = -99999;
        private Pawn lastShopkeeper;
        private int lastWalkoutMessageTick = -99999;
        private int lastAccusationMessageTick = -99999;
        private int lastRobberyMessageTick = -99999;
        private int lastGougeMessageTick = -99999;

        /// <summary>A much shorter window than TryClaimWalkoutMessage's own patience-length
        /// one, deliberately: at a meaningful accusation chance and a wager's much shorter
        /// round length, a walkout-length throttle would flatten "this table produces constant
        /// accusations" down to one message per window — exactly the Social-skill-driven
        /// frequency swing this mechanic exists to make visible. This only collapses a genuine
        /// multi-patron burst, not the signal itself.</summary>
        private const int AccusationMessageCooldownTicks = 400;

        /// <summary>Same window as AccusationMessageCooldownTicks — collapses only a genuine
        /// same-tick multi-raider race on one till, not the signal itself.</summary>
        private const int RobberyMessageCooldownTicks = 400;

        /// <summary>Unlike an accusation or a robbery, gouging isn't a discrete burst of events —
        /// the same markup is still gouging on the next sale, and the one after that, for as long
        /// as the player leaves the slider there. A day-long window keeps the warning a periodic
        /// reminder for as long as it stays true, rather than repeating on every single sale in a
        /// busy rush or, at the other extreme, ever firing only once.</summary>
        private const int GougeMessageCooldownTicks = 60000;

        // Ledger. Daily figures are rolled over by the map's TownEconomy component.
        public int salesToday;
        public int revenueToday;
        public int lifetimeSales;
        public int lifetimeRevenue;
        public int walkoutsToday;
        public int disturbancesToday;
        public int lifetimeDisturbances;

        // Gambling-only, but kept here rather than on a bespoke type — the same reasoning
        // disturbancesToday already follows for every other kind-agnostic counter.
        public int shortfallsToday;
        public int lifetimeShortfalls;
        public int payoutsToday;
        public int lifetimePayouts;

        // Any counter can be robbed, unlike the gambling-only figures above — a stickup doesn't
        // care what a business sells, only what's sitting in its till.
        public int robberiesToday;
        public int lifetimeRobberies;
        public int stolenToday;
        public int lifetimeStolen;

        private List<Thing> cachedStock = new List<Thing>();
        private int cachedStockTick = -99999;

        public CompProperties_Business Props => (CompProperties_Business)props;

        public ShopKindDef Kind => Props.shopKind;

        public bool Open
        {
            get => open;
            set => open = value;
        }

        public float Markup
        {
            get
            {
                if (markup < 0f) markup = Kind?.defaultMarkup ?? 1.35f;
                return markup;
            }
            set
            {
                FloatRange range = Kind?.markupRange ?? new FloatRange(0.5f, 3f);
                markup = Mathf.Clamp(value, range.min, range.max);
            }
        }

        /// <summary>The house's average take, as a fraction of every silver wagered — Markup's
        /// twin dial for a business whose services include a wager. Structurally identical
        /// getter/setter to Markup on purpose: same lazy-init-from-kind-default, same
        /// clamp-to-kind-range shape, so this dial costs nothing new to maintain. Inert for
        /// every kind but the gambling hall.</summary>
        public float HouseEdge
        {
            get
            {
                if (houseEdge < 0f) houseEdge = Kind?.defaultHouseEdge ?? 0.15f;
                return houseEdge;
            }
            set
            {
                FloatRange range = Kind?.houseEdgeRange ?? new FloatRange(0f, 0.5f);
                houseEdge = Mathf.Clamp(value, range.min, range.max);
            }
        }

        public ThingFilter StockFilter
        {
            get
            {
                if (stockFilter == null) ResetStockFilterToDefault();
                return stockFilter;
            }
        }

        /// <summary>Silver sitting in the till, waiting to be collected.</summary>
        public int TillSilver => till?.TotalStackCount ?? 0;

        /// <summary>True while a colonist is actively working this counter.</summary>
        public bool Staffed =>
            lastShopkeeper != null
            && !lastShopkeeper.Dead
            && Find.TickManager.TicksGame - lastStaffedTick <= StaffPresenceGraceTicks;

        public Pawn Shopkeeper => Staffed ? lastShopkeeper : null;

        /// <summary>A shop only trades when it is open, staffed, powered (if it needs power) and has something to offer.</summary>
        public bool TradingNow => open && Staffed && Powered && HasAnythingToOffer;

        private bool Powered
        {
            get
            {
                CompPowerTrader power = parent.TryGetComp<CompPowerTrader>();
                return power == null || power.PowerOn;
            }
        }

        /// <summary>
        /// Where the colonist stands to serve. This is the building's own interaction cell,
        /// so the player positions staff simply by rotating the counter.
        /// </summary>
        public IntVec3 StaffCell => parent.InteractionCell;

        /// <summary>
        /// Where the customer stands: mirrored through the counter from the staff cell, so the
        /// two face each other across it. Falls back to any standable neighbour if that cell is blocked.
        /// </summary>
        public IntVec3 CustomerCell
        {
            get
            {
                Map map = parent.Map;
                IntVec3 mirrored = parent.Position + (parent.Position - StaffCell);
                if (map == null) return mirrored;
                if (mirrored.InBounds(map) && mirrored.Standable(map)) return mirrored;

                foreach (IntVec3 c in GenAdj.CellsAdjacent8Way(parent))
                {
                    if (c.InBounds(map) && c.Standable(map) && c != StaffCell) return c;
                }
                return mirrored;
            }
        }

        /// <summary>
        /// A standing spot for this particular customer: the customer cell if it's free, else
        /// the nearest free cell around it, so a queue fans out beside the counter instead of
        /// the whole group stacking on one tile.
        /// </summary>
        public IntVec3 CustomerCellFor(Pawn customer)
        {
            Map map = parent.Map;
            IntVec3 primary = CustomerCell;
            if (map == null || CellFreeFor(primary, customer, map)) return primary;

            // Radial cells ignore walls, so filter to the room customers actually stand in —
            // otherwise a busy counter near a wall seats the queue's tail outside the shop.
            // Outdoors (a stall) any nearby cell will do. Either way, never hand a customer a
            // cell they can't walk to: a failed goto ends the job and drops the goods.
            Room queueRoom = RegionAndRoomQuery.RoomAt(primary, map);
            bool indoors = queueRoom != null
                && !queueRoom.PsychologicallyOutdoors && !queueRoom.TouchesMapEdge;

            foreach (IntVec3 c in GenRadial.RadialCellsAround(primary, 3.9f, false))
            {
                if (!c.InBounds(map) || c == StaffCell) continue;
                if (indoors && RegionAndRoomQuery.RoomAt(c, map) != queueRoom) continue;
                if (!CellFreeFor(c, customer, map)) continue;
                if (!customer.CanReach(c, PathEndMode.OnCell, Danger.Deadly)) continue;
                return c;
            }
            return primary;
        }

        private bool CellFreeFor(IntVec3 c, Pawn customer, Map map)
        {
            if (!c.Standable(map)) return false;
            Pawn standing = c.GetFirstPawn(map);
            if (standing != null && standing != customer) return false;

            // A cell another customer is already queueing toward counts as taken.
            IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn p = pawns[i];
                if (p == customer || !(p.jobs?.curDriver is IBusinessPatron)) continue;
                if (p.CurJob.GetTarget(TargetIndex.C).Cell == c) return false;
            }
            return true;
        }

        // ---------------------------------------------------------------- stock

        /// <summary>
        /// Everything currently for sale. Recomputed at most once a second because the
        /// customer AI asks about it constantly while choosing where to shop.
        /// </summary>
        public List<Thing> StockOnDisplay
        {
            get
            {
                int now = Find.TickManager.TicksGame;
                if (now - cachedStockTick <= StockCacheTicks) return cachedStock;
                cachedStockTick = now;
                cachedStock = ShopStock.ScanFor(this).ToList();
                return cachedStock;
            }
        }

        /// <summary>Total shelf price of everything on display — the shop's visible richness.</summary>
        public int StockValue
        {
            get
            {
                int total = 0;
                List<Thing> stock = StockOnDisplay;
                for (int i = 0; i < stock.Count; i++)
                {
                    total += ShopPricing.PriceFor(this, stock[i], stock[i].stackCount);
                }
                return total;
            }
        }

        public void DirtyStock()
        {
            cachedStockTick = -99999;
        }

        // ---------------------------------------------------------------- services

        /// <summary>True once this business has SOMETHING a customer could pay for right now —
        /// stock on the shelf, or an available service. Feeds staffing (WorkGiver_ManShop),
        /// TradingNow, and town appeal identically.</summary>
        public bool HasAnythingToOffer => StockOnDisplay.Count > 0 || AvailableServices.Any();

        /// <summary>Services this business can currently perform. A Thought-type service is always
        /// available while the kind offers it; an Ingest-type one additionally needs a matching item
        /// on display right now — the same StockFilter the player already curates for goods.</summary>
        public IEnumerable<ServiceDef> AvailableServices
        {
            get
            {
                ShopKindDef kind = Kind;
                if (kind == null) yield break;
                foreach (ServiceDef sd in kind.services)
                {
                    if (!sd.worker.IsAvailable(this)) continue;
                    if (!sd.worker.ConsumesStock) { yield return sd; continue; }
                    if (ShopStock.ChooseService(this, sd) != null) yield return sd;
                }
            }
        }

        /// <summary>The service-side equivalent of StockValue, for town appeal. Only counts
        /// non-stock-backed services (Haircut) — a stock-backed one (Drink, Meal) is already
        /// reflected in StockValue via the item it would consume, and double-counting it here would
        /// inflate appeal from the same physical stack twice.</summary>
        public int ServiceValue
        {
            get
            {
                int total = 0;
                foreach (ServiceDef sd in AvailableServices)
                {
                    if (sd.worker.ConsumesStock) continue;
                    total += ShopPricing.PriceForService(this, sd);
                }
                return total;
            }
        }

        /// <summary>True if this business offers a wager specifically — gates the House Edge
        /// gizmo, the shortfall/payout ledger lines, and the Collect-takings description swap,
        /// the same way Kind.services.Any(... is ServiceWorker_Lodging) already gates the
        /// Rooms line for a hotel.</summary>
        public bool HasWager => Kind != null && Kind.services.Any(sd => sd.worker is ServiceWorker_Wager);

        public void ResetStockFilterToDefault()
        {
            stockFilter = new ThingFilter();
            ShopKindDef kind = Kind;
            if (kind == null)
            {
                stockFilter.SetAllowAll(null);
                return;
            }
            foreach (ThingCategoryDef cat in kind.defaultStockCategories)
            {
                stockFilter.SetAllow(cat, true);
            }
            foreach (ThingDef def in kind.defaultStockThings)
            {
                stockFilter.SetAllow(def, true);
            }
            // Selling silver for silver is nonsense; never allow it however the filter is set.
            stockFilter.SetAllow(ThingDefOf.Silver, false);
            DirtyStock();
        }

        // ---------------------------------------------------------------- reputation

        /// <summary>
        /// Long-run price tolerance. A town customers like will bear higher prices;
        /// one with a bad reputation has to discount to move goods.
        /// </summary>
        public float ReputationPriceFactor
        {
            get
            {
                TownEconomy econ = parent.Map?.GetComponent<TownEconomy>();
                return econ == null ? 1f : Mathf.Lerp(1.15f, 0.9f, econ.Reputation);
            }
        }

        // ---------------------------------------------------------------- staffing

        /// <summary>Called every tick by the shopkeeper's job while they stand at the counter.</summary>
        public void NotifyStaffedBy(Pawn pawn)
        {
            lastShopkeeper = pawn;
            lastStaffedTick = Find.TickManager.TicksGame;
        }

        // ---------------------------------------------------------------- till

        public void AddToTill(Thing silver)
        {
            if (silver == null || silver.stackCount <= 0) return;
            till.TryAdd(silver, true);
        }

        /// <summary>Moves up to <paramref name="amount"/> silver out of the till and returns the
        /// stacks taken — AddToTill in reverse, for a wager's payout. Structurally cannot
        /// return more than the till holds: the till's own contents are the only bound on the
        /// loop, so a payout can never draw silver that doesn't exist.</summary>
        public List<Thing> TakeFromTill(int amount)
        {
            List<Thing> taken = new List<Thing>();
            if (amount <= 0 || till == null) return taken;

            // Copy first: taking from the container mutates it while we iterate — the same
            // guard ShopTransaction.TakeSilver already uses for the customer's purse.
            List<Thing> coins = new List<Thing>();
            for (int i = 0; i < till.Count; i++)
            {
                if (till[i].def == ThingDefOf.Silver) coins.Add(till[i]);
            }

            int remaining = amount;
            foreach (Thing coin in coins)
            {
                if (remaining <= 0) break;
                int take = Mathf.Min(remaining, coin.stackCount);
                Thing stack = till.Take(coin, take);
                if (stack == null) continue;
                taken.Add(stack);
                remaining -= stack.stackCount;
            }
            return taken;
        }

        /// <summary>Drops the till's contents at the counter for a hauler to pick up.</summary>
        public void CollectEarnings()
        {
            if (till == null || !till.Any) return;
            till.TryDropAll(parent.Position, parent.Map, ThingPlaceMode.Near);
        }

        public void RecordSale(int price)
        {
            salesToday++;
            revenueToday += price;
            lifetimeSales++;
            lifetimeRevenue += price;
        }

        public void RecordWalkout()
        {
            walkoutsToday++;
        }

        /// <summary>A saloon-only concern today (see TroubleUtility), but kept here rather than
        /// on a saloon-specific type — the same reasoning RecordSale/RecordWalkout already
        /// follow for every other kind-agnostic counter.</summary>
        public void RecordDisturbance()
        {
            disturbancesToday++;
            lifetimeDisturbances++;
        }

        /// <summary>The house winning a hand for the customer and then not being able to pay
        /// it out — the worst outcome the mechanic has. Counter-only here, the same shape as
        /// RecordDisturbance; the reputation/standing math lives on
        /// TownEconomy.RecordShortfall, matching the existing split between the two.</summary>
        public void RecordShortfall()
        {
            shortfallsToday++;
            lifetimeShortfalls++;
        }

        /// <summary>Counter-only, same reasoning as RecordShortfall. No TownEconomy-side
        /// counterpart: unlike a shortfall, a payout is money leaving the till exactly as
        /// intended, not a failure, so it has no town-wide reputation consequence of its own.</summary>
        public void RecordPayout(int amount)
        {
            payoutsToday += amount;
            lifetimePayouts += amount;
        }

        /// <summary>A stickup emptying this till. Counter-only, mirroring RecordShortfall's own
        /// shape — no reputation or standing consequence lives here: a robbery isn't something
        /// the shop or the town did wrong, so it costs nothing the way a shortfall or a walkout
        /// does. Silver already taken stays gone regardless; this is bookkeeping, not a
        /// consequence.</summary>
        public void RecordRobbery(int amount)
        {
            if (amount <= 0) return;
            robberiesToday++;
            lifetimeRobberies++;
            stolenToday += amount;
            lifetimeStolen += amount;
        }

        /// <summary>
        /// At most one walkout message per counter per patience-window, so a whole group
        /// giving up at once reads as one event in the log, not a flood.
        /// </summary>
        public bool TryClaimWalkoutMessage()
        {
            int now = Find.TickManager.TicksGame;
            int window = Kind?.customerPatienceTicks ?? 2500;
            if (now - lastWalkoutMessageTick < window) return false;
            lastWalkoutMessageTick = now;
            return true;
        }

        /// <summary>At most one cheating-accusation message per counter per
        /// AccusationMessageCooldownTicks — mirrors TryClaimWalkoutMessage's own shape, but
        /// against a much shorter window; see AccusationMessageCooldownTicks for why.</summary>
        public bool TryClaimAccusationMessage()
        {
            int now = Find.TickManager.TicksGame;
            if (now - lastAccusationMessageTick < AccusationMessageCooldownTicks) return false;
            lastAccusationMessageTick = now;
            return true;
        }

        /// <summary>At most one robbery message per counter per RobberyMessageCooldownTicks —
        /// mirrors TryClaimAccusationMessage's own shape. Collapses only a genuine same-tick
        /// multi-raider race on one till, never the signal itself.</summary>
        public bool TryClaimRobberyMessage()
        {
            int now = Find.TickManager.TicksGame;
            if (now - lastRobberyMessageTick < RobberyMessageCooldownTicks) return false;
            lastRobberyMessageTick = now;
            return true;
        }

        /// <summary>At most one gouging warning per shop per GougeMessageCooldownTicks — see
        /// that constant's own doc comment for why a full day, not the short burst-collapsing
        /// window TryClaimAccusationMessage/TryClaimRobberyMessage use.</summary>
        public bool TryClaimGougeMessage()
        {
            int now = Find.TickManager.TicksGame;
            if (now - lastGougeMessageTick < GougeMessageCooldownTicks) return false;
            lastGougeMessageTick = now;
            return true;
        }

        public void RollOverDay()
        {
            salesToday = 0;
            revenueToday = 0;
            walkoutsToday = 0;
            disturbancesToday = 0;
            shortfallsToday = 0;
            payoutsToday = 0;
            robberiesToday = 0;
            stolenToday = 0;
        }

        // ---------------------------------------------------------------- lifecycle

        public override void Initialize(CompProperties p)
        {
            base.Initialize(p);
            till = new ThingOwner<Thing>(this, false, LookMode.Deep);
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            if (stockFilter == null) ResetStockFilterToDefault();

            // A freshly built table needs a bankroll before its first customer ever sits down:
            // at default odds a win is close to a coin flip, and a win needs the payout
            // multiple of a till that would otherwise hold nothing but this round's own ante.
            // respawningAfterLoad excludes a reload (which would otherwise re-seed an
            // already-played table's till every time a save loads) and, as a side effect, a
            // pre-existing placed table from before this comp gained startingTillSilver — see
            // docs/economy.md for that one.
            if (!respawningAfterLoad && Props.startingTillSilver > 0)
            {
                Thing seed = ThingMaker.MakeThing(ThingDefOf.Silver);
                seed.stackCount = Props.startingTillSilver;
                AddToTill(seed);
            }

            parent.Map?.GetComponent<TownEconomy>()?.Register(this);
        }

        public override void PostDeSpawn(Map map, DestroyMode mode)
        {
            map?.GetComponent<TownEconomy>()?.Deregister(this);
            base.PostDeSpawn(map, mode);
        }

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            // Never swallow the day's takings when the counter is deconstructed.
            if (till != null && till.Any && previousMap != null)
            {
                till.TryDropAll(parent.Position, previousMap, ThingPlaceMode.Near);
            }
            base.PostDestroy(mode, previousMap);
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Deep.Look(ref till, "till", this);
            Scribe_Deep.Look(ref stockFilter, "stockFilter");
            Scribe_Values.Look(ref open, "open", true);
            Scribe_Values.Look(ref markup, "markup", -1f);
            Scribe_Values.Look(ref salesToday, "salesToday");
            Scribe_Values.Look(ref revenueToday, "revenueToday");
            Scribe_Values.Look(ref lifetimeSales, "lifetimeSales");
            Scribe_Values.Look(ref lifetimeRevenue, "lifetimeRevenue");
            Scribe_Values.Look(ref walkoutsToday, "walkoutsToday");
            Scribe_Values.Look(ref disturbancesToday, "disturbancesToday");
            Scribe_Values.Look(ref lifetimeDisturbances, "lifetimeDisturbances");
            Scribe_Values.Look(ref houseEdge, "houseEdge", -1f);
            Scribe_Values.Look(ref shortfallsToday, "shortfallsToday");
            Scribe_Values.Look(ref lifetimeShortfalls, "lifetimeShortfalls");
            Scribe_Values.Look(ref payoutsToday, "payoutsToday");
            Scribe_Values.Look(ref lifetimePayouts, "lifetimePayouts");
            Scribe_Values.Look(ref robberiesToday, "robberiesToday");
            Scribe_Values.Look(ref lifetimeRobberies, "lifetimeRobberies");
            Scribe_Values.Look(ref stolenToday, "stolenToday");
            Scribe_Values.Look(ref lifetimeStolen, "lifetimeStolen");
            Scribe_References.Look(ref lastShopkeeper, "lastShopkeeper");

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (till == null) till = new ThingOwner<Thing>(this, false, LookMode.Deep);
                if (stockFilter == null) ResetStockFilterToDefault();
            }
        }

        // ---------------------------------------------------------------- IThingHolder

        public void GetChildHolders(List<IThingHolder> outChildren)
        {
            ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());
        }

        public ThingOwner GetDirectlyHeldThings() => till;

        // ---------------------------------------------------------------- UI

        public override string CompInspectStringExtra()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(open ? "OWT_StatusOpen".Translate() : "OWT_StatusClosed".Translate());
            if (open && !Staffed) sb.Append(" (" + "OWT_Unattended".Translate() + ")");
            sb.AppendLine();

            List<Thing> stock = StockOnDisplay;
            sb.AppendLine("OWT_StockLine".Translate(stock.Count, ((float)StockValue).ToStringMoney()));
            if (Kind != null && Kind.services.Count > 0)
            {
                sb.AppendLine("OWT_ServicesLine".Translate(
                    string.Join(", ", Kind.services.Select(s => s.LabelCap))));
            }
            if (Kind != null && Kind.services.Any(sd => sd.worker is ServiceWorker_Lodging))
            {
                int total = ShopStock.CountBeds(this, out int vacant);
                sb.AppendLine("OWT_RoomsLine".Translate(vacant, total));
            }
            if (Kind != null && Kind.services.Any(sd => sd.worker.CanCauseTrouble))
            {
                sb.AppendLine("OWT_DisturbanceLine".Translate(disturbancesToday));
            }
            sb.AppendLine("OWT_MarkupLine".Translate(Markup.ToStringPercent()));
            if (HasWager)
            {
                sb.AppendLine("OWT_HouseEdgeLine".Translate(HouseEdge.ToStringPercent()));
            }
            sb.AppendLine("OWT_TillLine".Translate(((float)TillSilver).ToStringMoney(), ((float)revenueToday).ToStringMoney()));
            if (HasWager)
            {
                sb.AppendLine("OWT_PayoutLine".Translate(((float)payoutsToday).ToStringMoney()));
                sb.AppendLine("OWT_ShortfallLine".Translate(shortfallsToday));
            }
            if (lifetimeStolen > 0)
            {
                sb.AppendLine("OWT_RobberyLine".Translate(((float)lifetimeStolen).ToStringMoney(), lifetimeRobberies));
            }

            TownEconomy econ = parent.Map?.GetComponent<TownEconomy>();
            if (econ != null)
            {
                sb.Append("OWT_TownLine".Translate(econ.Appeal.ToString("0.0"), econ.Reputation.ToStringPercent()));
                // RegionalShare only collapses to exactly 1f once MarketPull is non-positive --
                // Appeal literally zero, not merely under the threshold that gates arrivals at
                // all -- so it can't carry "does this town qualify to compete yet" on its own.
                // Gate on that threshold explicitly, mirroring CheckRegionalLeadChange's own
                // guard, so a brand-new shop stays silent about regional trade the way the
                // ledger's comment below already promises the inspect line does.
                if (econ.Appeal >= TownEconomy.MinAppealForCustomers && econ.RegionalShare < 1f)
                {
                    sb.AppendLine();
                    sb.Append("OWT_RegionalShareLine".Translate(econ.RegionalShare.ToStringPercent()));
                }
            }
            return sb.ToString().TrimEndNewlines();
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo g in base.CompGetGizmosExtra()) yield return g;

            yield return new Command_Toggle
            {
                defaultLabel = "OWT_CmdOpen".Translate(),
                defaultDesc = "OWT_CmdOpenDesc".Translate(),
                icon = TexCommand.ForbidOff,
                isActive = () => open,
                toggleAction = () => open = !open
            };

            yield return new Command_Action
            {
                defaultLabel = "OWT_CmdMarkup".Translate(),
                defaultDesc = "OWT_CmdMarkupDesc".Translate(),
                icon = TexCommand.DesirePower,
                action = () =>
                {
                    FloatRange range = Kind?.markupRange ?? new FloatRange(0.5f, 3f);
                    Find.WindowStack.Add(new Dialog_Slider(
                        pct => "OWT_MarkupSlider".Translate((pct / 100f).ToStringPercent()),
                        Mathf.RoundToInt(range.min * 100f),
                        Mathf.RoundToInt(range.max * 100f),
                        pct => Markup = pct / 100f,
                        Mathf.RoundToInt(Markup * 100f)));
                }
            };

            if (HasWager)
            {
                // Markup's own twin dial, verbatim — same Command_Action/Dialog_Slider shape,
                // same icon (there is no dedicated one, and "the markup slider's twin" is
                // exactly what this is), just pointed at HouseEdge and its own kind-configured
                // range instead.
                yield return new Command_Action
                {
                    defaultLabel = "OWT_CmdHouseEdge".Translate(),
                    defaultDesc = "OWT_CmdHouseEdgeDesc".Translate(),
                    icon = TexCommand.DesirePower,
                    action = () =>
                    {
                        FloatRange range = Kind?.houseEdgeRange ?? new FloatRange(0f, 0.5f);
                        Find.WindowStack.Add(new Dialog_Slider(
                            pct => "OWT_HouseEdgeSlider".Translate((pct / 100f).ToStringPercent()),
                            Mathf.RoundToInt(range.min * 100f),
                            Mathf.RoundToInt(range.max * 100f),
                            pct => HouseEdge = pct / 100f,
                            Mathf.RoundToInt(HouseEdge * 100f)));
                    }
                };
            }

            if (TillSilver > 0)
            {
                yield return new Command_Action
                {
                    defaultLabel = "OWT_CmdCollect".Translate(((float)TillSilver).ToStringMoney()),
                    defaultDesc = HasWager ? "OWT_CmdCollectDescWager".Translate() : "OWT_CmdCollectDesc".Translate(),
                    icon = ThingDefOf.Silver.uiIcon,
                    action = CollectEarnings
                };
            }

            TownEconomy econ = parent.Map?.GetComponent<TownEconomy>();
            if (econ != null)
            {
                yield return new Command_Action
                {
                    defaultLabel = "OWT_CmdLedger".Translate(),
                    defaultDesc = "OWT_CmdLedgerDesc".Translate(),
                    icon = TexButton.Info,
                    action = () => Find.WindowStack.Add(new Dialog_MessageBox(TownLedgerText(econ)))
                };
            }
        }

        /// <summary>
        /// The town's books, readable in one place: the two numbers that drive the economy
        /// (appeal and reputation), today's trading, and each shop's takings.
        /// </summary>
        private static TaggedString TownLedgerText(TownEconomy econ)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("OWT_LedgerTitle".Translate());
            sb.AppendLine();
            sb.AppendLine("OWT_LedgerAppealLine".Translate(econ.Appeal.ToString("0.0")));
            sb.AppendLine("OWT_LedgerReputationLine".Translate(econ.Reputation.ToStringPercent()));

            List<KeyValuePair<Faction, float>> tracked = econ.TrackedStandings.ToList();
            if (tracked.Count > 0)
            {
                KeyValuePair<Faction, float> best = tracked.OrderByDescending(kv => kv.Value).First();
                KeyValuePair<Faction, float> worst = tracked.OrderBy(kv => kv.Value).First();
                if (best.Value - econ.Reputation > LedgerStandingDivergenceThreshold)
                {
                    sb.AppendLine("OWT_LedgerRegularLine".Translate(best.Key.Name, best.Value.ToStringPercent()));
                }
                // No key-equality guard needed here: a single value can't be simultaneously
                // above Reputation+threshold and below Reputation-threshold, so this condition
                // and the one above it can never both fire for the same faction — including
                // when tracked.Count == 1, where best and worst are literally the same entry
                // and only one of the two checks can possibly pass.
                if (econ.Reputation - worst.Value > LedgerStandingDivergenceThreshold)
                {
                    sb.AppendLine("OWT_LedgerColdLine".Translate(worst.Key.Name, worst.Value.ToStringPercent()));
                }
            }

            // Gated on the setting alone, deliberately not also on econ.Appeal -- the ledger is
            // the opt-in "show me everything" tier, so a player who opens it can usefully see
            // what they're up against before their own town even qualifies to compete. The
            // always-visible inspect line and every push message stay silent until it does.
            if (OldWestTownMod.Settings.rivalTownsEnabled)
            {
                RivalTowns rivalsComp = Find.World?.GetComponent<RivalTowns>();
                if (rivalsComp != null && rivalsComp.Rivals.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("OWT_LedgerRivalsHeader".Translate());
                    foreach (RivalTown rival in rivalsComp.Rivals)
                    {
                        if (rival.def == null) continue;   // orphaned instance from a since-removed def; nothing to show
                        if (rival.Undercutting)
                        {
                            sb.AppendLine("OWT_LedgerRivalLineUndercutting".Translate(
                                rival.def.LabelCap, rival.currentAppeal.ToString("0.0")));
                        }
                        else
                        {
                            sb.AppendLine("OWT_LedgerRivalLine".Translate(
                                rival.def.LabelCap, rival.currentAppeal.ToString("0.0")));
                        }
                    }
                    sb.AppendLine("OWT_LedgerRegionalShareLine".Translate(econ.RegionalShare.ToStringPercent()));
                }
            }

            sb.AppendLine();
            sb.AppendLine("OWT_LedgerTodayLine".Translate(
                econ.customersServedToday, econ.walkoutsToday, ((float)econ.revenueToday).ToStringMoney()));
            sb.AppendLine("OWT_LedgerLifetimeLine".Translate(((float)econ.lifetimeRevenue).ToStringMoney()));
            sb.AppendLine();
            foreach (CompBusiness shop in econ.Shops)
            {
                if (shop?.parent == null || !shop.parent.Spawned) continue;
                sb.AppendLine("OWT_LedgerShopLine".Translate(
                    shop.parent.LabelCap,
                    ((float)shop.revenueToday).ToStringMoney(),
                    ((float)shop.TillSilver).ToStringMoney()));
                if (shop.Kind != null && shop.Kind.services.Any(sd => sd.worker is ServiceWorker_Lodging))
                {
                    int total = ShopStock.CountBeds(shop, out int vacant);
                    sb.AppendLine("OWT_RoomsLine".Translate(vacant, total));
                }
                if (shop.Kind != null && shop.Kind.services.Any(sd => sd.worker.CanCauseTrouble))
                {
                    sb.AppendLine("OWT_DisturbanceLine".Translate(shop.disturbancesToday));
                }
                if (shop.HasWager)
                {
                    sb.AppendLine("OWT_HouseEdgeLine".Translate(shop.HouseEdge.ToStringPercent()));
                    sb.AppendLine("OWT_PayoutLine".Translate(((float)shop.payoutsToday).ToStringMoney()));
                    sb.AppendLine("OWT_ShortfallLine".Translate(shop.shortfallsToday));
                }
                if (shop.lifetimeStolen > 0)
                {
                    sb.AppendLine("OWT_RobberyLine".Translate(((float)shop.lifetimeStolen).ToStringMoney(), shop.lifetimeRobberies));
                }
            }
            return sb.ToString().TrimEndNewlines();
        }
    }
}
