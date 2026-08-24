using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

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

        private int lastStaffedTick = -99999;
        private Pawn lastShopkeeper;
        private int lastWalkoutMessageTick = -99999;

        // Ledger. Daily figures are rolled over by the map's TownEconomy component.
        public int salesToday;
        public int revenueToday;
        public int lifetimeSales;
        public int lifetimeRevenue;
        public int walkoutsToday;
        public int disturbancesToday;
        public int lifetimeDisturbances;

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

        public void RollOverDay()
        {
            salesToday = 0;
            revenueToday = 0;
            walkoutsToday = 0;
            disturbancesToday = 0;
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
            if (Kind != null && Kind.services.Any(sd => sd.worker.RowdinessPerUse > 0f))
            {
                sb.AppendLine("OWT_DisturbanceLine".Translate(disturbancesToday));
            }
            sb.AppendLine("OWT_MarkupLine".Translate(Markup.ToStringPercent()));
            sb.Append("OWT_TillLine".Translate(((float)TillSilver).ToStringMoney(), ((float)revenueToday).ToStringMoney()));

            TownEconomy econ = parent.Map?.GetComponent<TownEconomy>();
            if (econ != null)
            {
                sb.AppendLine();
                sb.Append("OWT_TownLine".Translate(econ.Appeal.ToString("0.0"), econ.Reputation.ToStringPercent()));
            }
            return sb.ToString();
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

            if (TillSilver > 0)
            {
                yield return new Command_Action
                {
                    defaultLabel = "OWT_CmdCollect".Translate(((float)TillSilver).ToStringMoney()),
                    defaultDesc = "OWT_CmdCollectDesc".Translate(),
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
                if (shop.Kind != null && shop.Kind.services.Any(sd => sd.worker.RowdinessPerUse > 0f))
                {
                    sb.AppendLine("OWT_DisturbanceLine".Translate(shop.disturbancesToday));
                }
            }
            return sb.ToString().TrimEndNewlines();
        }
    }
}
