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

        private List<Thing> cachedStock = new List<Thing>();

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

        /// <summary>Staffed with no grace at all — somebody is behind this counter right now.
        ///
        /// <see cref="Staffed"/> forgives a 60-tick gap on purpose: a shopkeeper who blinks out
        /// for a moment is still working the counter, and a sale in progress shouldn't be torn up
        /// over it. Deciding to walk across town is a different question, and wants the stricter
        /// answer — a customer sent on the strength of a keeper who left a second ago arrives at
        /// an empty counter and waits out their patience for nothing. One tick of slack, not
        /// zero, because the shopkeeper's job may not have ticked yet this frame.</summary>
        public bool StaffedNow =>
            lastShopkeeper != null
            && !lastShopkeeper.Dead
            && Find.TickManager.TicksGame - lastStaffedTick <= 1;

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

        /// <summary>The room this counter trades from, or null when it trades in the open air — a
        /// stall, a boardwalk table, or a counter in a "room" that opens onto the map edge.
        ///
        /// One definition of "the sales floor", used twice: <see cref="ShopStock.ScanFor"/> decides
        /// what is on the shelves from it, and the town's survey counts a floor's goods once however
        /// many counters stand on it. Two counters that return the same room are two tills on one
        /// shop-front.</summary>
        public Room SalesFloorRoom
        {
            get
            {
                if (parent == null || !parent.Spawned) return null;
                Room room = parent.GetRoom();
                if (room == null || room.PsychologicallyOutdoors || room.TouchesMapEdge) return null;
                return room;
            }
        }

        // ---------------------------------------------------------------- stock

        /// <summary>
        /// Everything currently for sale. Recomputed at most once a second because the
        /// customer AI asks about it constantly while choosing where to shop.
        /// </summary>
        public List<Thing> StockOnDisplay => cachedStock;

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

        /// <summary>Re-reads the shelves. Called once per shop by the town's survey, and again the
        /// moment a player action changes what is on sale — a filter edit, or a sale.
        ///
        /// Deliberately NOT done from the getter. A refreshing getter meant that drawing this
        /// counter's inspect pane could decide WHEN the snapshot was taken, and the customer AI
        /// draws one random number per stack it scores, so merely having a counter selected changed
        /// how many numbers came off RimWorld's shared seeded stream — and therefore what the
        /// storyteller rolled next. Looking at a shop must not change the game.</summary>
        public void RefreshStock()
        {
            cachedStock = ShopStock.ScanFor(this).ToList();
        }

        // ---------------------------------------------------------------- services

        /// <summary>True once this business has SOMETHING a customer could pay for right now —
        /// stock on the shelf, or an available service. Feeds staffing (WorkGiver_ManShop),
        /// TradingNow, and town appeal identically.</summary>
        public bool HasAnythingToOffer => StockOnDisplay.Count > 0 || AvailableServices.Any();

        /// <summary>Services this business can currently perform. A Thought-type service is always
        /// available while the kind offers it; an Ingest-type one additionally needs a matching item
        /// on display right now — the same StockFilter the player already curates for goods.
        ///
        /// Asks whether any stack qualifies, not which one — picking one is the customer's business
        /// and the only place a roll belongs. That keeps <see cref="HasAnythingToOffer"/>, which the
        /// inspect pane and the town's survey both reach, free of dice.</summary>
        public IEnumerable<ServiceDef> AvailableServices
        {
            get
            {
                ShopKindDef kind = Kind;
                if (kind == null) yield break;
                foreach (ServiceDef sd in kind.services)
                {
                    if (!sd.worker.ConsumesStock) { yield return sd; continue; }
                    if (ShopStock.HasStockFor(this, sd)) yield return sd;
                }
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
            RefreshStock();
        }

        // ---------------------------------------------------------------- reputation

        /// <summary>
        /// Long-run price tolerance. A town customers like will bear higher prices;
        /// one with a bad reputation has to discount to move goods. A neutral name is exactly 1.0,
        /// so on a fresh save the markup slider means precisely what it says.
        /// </summary>
        public float ReputationPriceFactor
        {
            get
            {
                TownEconomy econ = parent.Map?.GetComponent<TownEconomy>();
                return econ == null ? 1f : Mathf.Lerp(0.9f, 1.1f, econ.Reputation);
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
            RefreshStock();
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
            sb.AppendLine("OWT_LedgerAppealBusinessesLine".Translate(econ.BusinessScore.ToString("0.0")));
            sb.AppendLine("OWT_LedgerAppealGoodsLine".Translate(
                econ.OfferValue.ToStringMoney(), econ.GoodsFactor.ToString("0.00")));
            sb.AppendLine("OWT_LedgerAppealStandingLine".Translate(
                econ.Reputation.ToStringPercent(), econ.StandingFactor.ToString("0.00")));

            // The trades the town does not run at all — almost always the biggest single move
            // available.
            List<string> missing = new List<string>();
            List<ShopKindDef> kinds = DefDatabase<ShopKindDef>.AllDefsListForReading;
            for (int i = 0; i < kinds.Count; i++)
            {
                if (!econ.HasTrade(kinds[i])) missing.Add(kinds[i].label);
            }
            if (missing.Count > 0) sb.AppendLine("OWT_LedgerAppealMissingLine".Translate(missing.ToCommaList(true)));

            // The wealth setting is part of what a customer actually arrives carrying, so the
            // ledger prints the product rather than the half of it the town earned.
            sb.AppendLine("OWT_LedgerPurseLine".Translate(
                (econ.PurseFactor * OldWestTownMod.Settings.customerWealth).ToString("0.00")));
            sb.AppendLine("OWT_LedgerReputationLine".Translate(econ.Reputation.ToStringPercent()));
            sb.AppendLine();
            sb.AppendLine("OWT_LedgerTodayLine".Translate(
                econ.PatronsToday, econ.UnservedToday, ((float)econ.revenueToday).ToStringMoney()));
            sb.AppendLine(econ.PatronsToday > 0
                ? "OWT_LedgerServiceLine".Translate(econ.ServiceScoreToday.ToStringPercent())
                : "OWT_LedgerQuietLine".Translate());
            sb.AppendLine("OWT_LedgerLifetimeLine".Translate(((float)econ.lifetimeRevenue).ToStringMoney()));
            sb.AppendLine();
            foreach (CompBusiness shop in econ.Shops)
            {
                if (shop?.parent == null || !shop.parent.Spawned) continue;
                sb.Append("OWT_LedgerShopLine".Translate(
                    shop.parent.LabelCap,
                    ((float)shop.revenueToday).ToStringMoney(),
                    ((float)shop.TillSilver).ToStringMoney()));
                if (shop.walkoutsToday > 0)
                {
                    sb.Append(" ").Append("OWT_LedgerShopWalkouts".Translate(shop.walkoutsToday));
                }
                sb.AppendLine();
            }
            return sb.ToString().TrimEndNewlines();
        }
    }
}
