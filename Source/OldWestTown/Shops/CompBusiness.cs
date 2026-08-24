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

        /// <summary>True while somebody is attending this customer's transaction. Its complement
        /// in all but name — a customer walking or browsing is neither — and the reason both live
        /// here: what a customer is in the middle of is a fact about the business, and the two
        /// callers that need it (the unattended-counter alert, and closing time) may not name the
        /// AI namespace.</summary>
        bool BeingServed { get; }
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

        /// <summary>How often a counter may say out loud that it is turning trade away. A quarter day,
        /// far longer than a walkout's window on purpose: a walkout is something to fix this minute, a
        /// queue is something to invest against, and the counter's own pane carries the live version.</summary>
        private const int BusyMessageIntervalTicks = 15000;

        private ThingOwner<Thing> till;
        private ThingFilter stockFilter;
        private bool open = true;
        private float markup = -1f;

        private int lastStaffedTick = -99999;
        private Pawn lastShopkeeper;
        private int lastWalkoutMessageTick = -99999;
        private int lastBusyMessageTick = -99999;

        /// <summary>Everyone standing at this counter, in the order they got here. Index 0 is being
        /// served; the rest are waiting their turn.
        ///
        /// Written only by the patrons themselves, from their own wait toil. An entry is valid exactly
        /// as long as that pawn's OWN job says they are standing at this counter — which is why a
        /// patron who is drafted, downed, killed, re-tasked or simply finished leaves the line on the
        /// next read rather than holding a place the counter would have to time out. There is no lease
        /// and no timestamp because there is nothing to lease: the condition is the claim. Nothing the
        /// shopkeeper does touches this list, and nothing in it is a claim on the shopkeeper.
        ///
        /// Not saved. It rebuilds within a tick of loading as each patron ticks, and the one thing a
        /// rebuild could get wrong — bumping somebody out of the chair mid-serve — is what the
        /// mid-transaction rule in TakePlaceInLine exists to prevent.</summary>
        private List<Pawn> line = new List<Pawn>();

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

        // ---------------------------------------------------------------- the line

        /// <summary>Who this counter is attending and how many are behind them, for the inspect pane.
        ///
        /// Reads without pruning, deliberately. Drawing a pane must not change the game — the same
        /// rule that took the shelf scan off the render path — and while a stale entry here would
        /// only ever be one the next patron tick removes anyway, "only ever" is the kind of claim
        /// that stops being true later. So it skips the dead entries instead of deleting them, and
        /// leaves the pruning to the patrons whose ticks own this list.</summary>
        public int LineLength(out Pawn head)
        {
            head = null;
            int n = 0;
            for (int i = 0; i < line.Count; i++)
            {
                if (!IsPatronOfThisCounter(line[i])) continue;
                if (head == null) head = line[i];
                n++;
            }
            return n;
        }

        /// <summary>Everybody who has set out for this counter — walking, browsing, queueing or being
        /// served. What somebody deciding whether to join has to count: the walk from a shelf to the
        /// till is long enough that counting only the bodies already standing here would send a whole
        /// group to the same chair before the first of them arrived.
        ///
        /// A map walk rather than a second list, because "committed to this counter" is already written
        /// down in the patron's own job and a list would be a copy of it that could disagree. Called
        /// once per shop per customer decision, and once per right-click on the order menu. NEVER from
        /// drawing code — that is what <see cref="LineLength"/> is for.</summary>
        public int PatronsHeadedHere
        {
            get
            {
                Map map = parent.Map;
                if (map == null) return 0;
                int n = 0;
                IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned;
                for (int i = 0; i < pawns.Count; i++) { if (IsPatronOfThisCounter(pawns[i])) n++; }
                return n;
            }
        }

        /// <summary>Takes, or keeps, this patron's place and returns it: 0 means the counter is theirs
        /// this tick, anything else means they are waiting. Called every tick by a patron standing at
        /// the counter. Places go by arrival — the first to stand here is the first served — which is
        /// the only ordering rule a player can check by looking.
        ///
        /// One exception, and it is the same exception twice: a patron whose transaction is ALREADY
        /// running goes to the front. A line rebuilt after a load must not bump somebody 2000 ticks
        /// into a haircut out of the chair, and a shopkeeper arriving at an honesty-box counter must
        /// not send the customer already mid-sale to the back. The line never interrupts a transaction
        /// already running.</summary>
        public int TakePlaceInLine(Pawn patron)
        {
            PruneLine();
            int place = line.IndexOf(patron);
            if (place >= 0) return place;
            if (patron.jobs?.curDriver is IBusinessPatron p && p.BeingServed)
            {
                // After the others already mid-transaction, not in front of them. Inserting at the
                // head demotes whoever was there, and a demoted patron's serve restarts from zero
                // on its next tick — so three patrons self-serving at a counter somebody then
                // starts working would have taken turns wrecking each other's progress.
                place = 0;
                while (place < line.Count
                    && (line[place].jobs?.curDriver as IBusinessPatron)?.BeingServed == true) place++;
                line.Insert(place, patron);
                return place;
            }
            line.Add(patron);
            return line.Count - 1;
        }

        /// <summary>Gives up a place, from the patron's own finish action. The prune would catch them on
        /// the next read anyway; doing it here means the next customer is served on the tick the last
        /// one finishes rather than the tick after somebody notices.</summary>
        public void LeaveLine(Pawn patron)
        {
            line.Remove(patron);
        }

        private void PruneLine()
        {
            for (int i = line.Count - 1; i >= 0; i--)
            {
                if (!IsPatronOfThisCounter(line[i])) line.RemoveAt(i);
            }
        }

        /// <summary>The mod's one test for "this pawn is patronizing THIS counter", which the customer
        /// scan, the queue spacing and the order menu each already spell out for themselves: an
        /// IBusinessPatron driver whose job names this building at TargetIndex.B.</summary>
        private bool IsPatronOfThisCounter(Pawn p)
        {
            return p != null && !p.Dead && p.Spawned
                && p.jobs?.curDriver is IBusinessPatron
                && p.CurJob?.GetTarget(TargetIndex.B).Thing == parent;
        }

        /// <summary>At most one "this counter is turning trade away" message per counter per quarter
        /// day. Separate from the walkout throttle, and deliberately: they are different news on
        /// different cadences, and one must not silence the other.</summary>
        public bool TryClaimBusyMessage()
        {
            int now = Find.TickManager.TicksGame;
            if (now - lastBusyMessageTick < BusyMessageIntervalTicks) return false;
            lastBusyMessageTick = now;
            return true;
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

            // The bottleneck, where the player looks when a shop feels slow. "At the counter" rather
            // than "serving", because an unattended counter has a head too and the line above already
            // says so.
            int atCounter = LineLength(out Pawn head);
            if (head != null)
            {
                sb.AppendLine("OWT_AtCounterLine".Translate(head.LabelShort));
                if (atCounter > 1) sb.AppendLine("OWT_QueueLine".Translate(atCounter - 1));
            }

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

        /// <summary>The colony's own way in.
        ///
        /// A stranger is sent here by their duty's think tree. A colonist is sent by the player and
        /// by nothing else — an order on the building, the way every other one-off thing a pawn is
        /// told to do is given. That is a decision, not a stub: an hour of a colonist's day is the
        /// scarcest thing the colony owns and this feature spends two of them, the patron's and the
        /// shopkeeper's. Nothing here should spend them unasked.
        ///
        /// Only a service whose ServiceDef names a colonistJobDef is offered, so no job this menu
        /// can start has a route to the till.</summary>
        public override IEnumerable<FloatMenuOption> CompFloatMenuOptions(Pawn selPawn)
        {
            if (selPawn == null || parent.Map == null || selPawn.Map != parent.Map) yield break;
            if (selPawn.Faction != Faction.OfPlayer || selPawn.Drafted) yield break;

            foreach (ServiceDef service in AvailableServices)
            {
                if (service.colonistJobDef == null) continue;

                // One cell, decided once: the menu's reachability answer and the job's destination
                // must be the same tile, or the order offers itself and then fails on the walk.
                IntVec3 stand = CustomerCellFor(selPawn);

                string reason = CannotOrder(service, selPawn, stand);
                if (reason != null)
                {
                    yield return new FloatMenuOption(
                        "OWT_OrderServiceDisabled".Translate(service.label, reason), null);
                    continue;
                }

                yield return new FloatMenuOption("OWT_OrderService".Translate(service.label), () =>
                {
                    // Counter at B and the standing cell at C, the same indices every
                    // IBusinessPatron uses: the queue spacing, the alert and the shopkeeper's
                    // customer scan all read a patron's job by position, not by type. A is unused
                    // here — a haircut has nothing to fetch.
                    Job job = JobMaker.MakeJob(service.colonistJobDef, LocalTargetInfo.Invalid,
                        parent, stand);
                    selPawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                });
            }
        }

        /// <summary>Why this colonist cannot be sent for this service right now, or null if they
        /// can. Each branch is one of the bounds on the feature: the counter has to be trading, the
        /// pawn has to actually want it, the chair holds one person at a time, and they have to be
        /// able to walk to it.</summary>
        private string CannotOrder(ServiceDef service, Pawn selPawn, IntVec3 stand)
        {
            if (!open) return "OWT_ReasonClosed".Translate();
            if (service.worker.Desirability(selPawn) <= 0f) return "OWT_ReasonRecently".Translate();

            // One at a time at a counter, asked of the people actually waiting there rather than of
            // the reservation manager: a colonist who has claimed this building to repair or
            // deconstruct it holds a reservation too, and "already waiting here" would be a lie
            // about them. The patron's own claim is on their standing cell, not on the counter,
            // precisely so it stays out of the way of orders like those.
            Pawn waiting = ColonistWaitingHere(selPawn);
            if (waiting != null) return "OWT_ReasonReserved".Translate(waiting.LabelShort);

            // A colonist will stand behind one stranger, not two. Their whole give-up bound is one
            // patience window plus two serves — 6600 ticks at the barber — and two ahead of them
            // spends 6600 of it standing up. Refused here, at the moment the player decides, next to
            // the other bounds on this order.
            if (PatronsHeadedHere >= 2) return "OWT_ReasonBusy".Translate();

            if (!selPawn.CanReach(stand, PathEndMode.OnCell, Danger.Deadly)) return "OWT_ReasonUnreachable".Translate();
            return null;
        }

        /// <summary>A colonist other than <paramref name="asking"/> already being served, or waiting
        /// to be, at this counter.</summary>
        private Pawn ColonistWaitingHere(Pawn asking)
        {
            List<Pawn> colonists = parent.Map.mapPawns.SpawnedPawnsInFaction(Faction.OfPlayer);
            for (int i = 0; i < colonists.Count; i++)
            {
                Pawn p = colonists[i];
                if (p == asking || !IsPatronOfThisCounter(p)) continue;
                return p;
            }
            return null;
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
