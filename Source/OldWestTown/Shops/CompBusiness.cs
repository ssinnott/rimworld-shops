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
        private int lastFloorRobberyMessageTick = -99999;
        private int lastGougeMessageTick = -99999;
        private int lastBusyMessageTick = -99999;

        /// <summary>A much shorter window than TryClaimWalkoutMessage's own patience-length
        /// one, deliberately: at a meaningful accusation chance and a wager's much shorter
        /// round length, a walkout-length throttle would flatten "this table produces constant
        /// accusations" down to one message per window — exactly the Social-skill-driven
        /// frequency swing this mechanic exists to make visible. This only collapses a genuine
        /// multi-patron burst, not the signal itself.</summary>
        private const int AccusationMessageCooldownTicks = 400;

        /// <summary>Same window as AccusationMessageCooldownTicks — collapses only a genuine
        /// same-tick multi-raider race on one till (or one floor stack; see
        /// TryClaimFloorRobberyMessage), not the signal itself. Shared by both message-tick
        /// fields below rather than split into two constants: it's one throttle window used
        /// twice, not two different policies.</summary>
        private const int RobberyMessageCooldownTicks = 400;

        /// <summary>Unlike an accusation or a robbery, gouging isn't a discrete burst of events —
        /// the same markup is still gouging on the next sale, and the one after that, for as long
        /// as the player leaves the slider there. A day-long window keeps the warning a periodic
        /// reminder for as long as it stays true, rather than repeating on every single sale in a
        /// busy rush or, at the other extreme, ever firing only once.</summary>
        private const int GougeMessageCooldownTicks = 60000;

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

        /// <summary>Loose silver stacks sitting on this shop's own sales floor — what Collect
        /// takings just dropped there, or anything else that ended up there. Same cache shape as
        /// cachedStock, same cadence (RefreshStock), and just as derived: never scribed, rebuilt
        /// the instant it's needed. See FloorSilverStacks/FloorSilver.</summary>
        private List<Thing> cachedFloorSilver = new List<Thing>();

        // What the shelves are worth, priced when the answer can change rather than when
        // somebody looks at it. See EnsurePriced.
        private int stockVersion;
        private int pricedVersion = -1;
        private float pricedMarkup;
        private float pricedReputation;
        private int cachedStockPrice;
        private int cachedStockMarket;

        public CompProperties_Business Props => (CompProperties_Business)props;

        public ShopKindDef Kind => Props.shopKind;

        public bool Open
        {
            get => open;
            set => open = value;
        }

        /// <summary>Price band for a business whose kind names none.</summary>
        private static readonly FloatRange DefaultMarkupRange = new FloatRange(0.5f, 3f);

        /// <summary>What the player may charge here, as a multiple of market value.</summary>
        public FloatRange MarkupRange => Kind?.markupRange ?? DefaultMarkupRange;

        public float Markup
        {
            get
            {
                if (markup < 0f) markup = Kind?.defaultMarkup ?? 1.35f;
                return markup;
            }
            set => markup = Mathf.Clamp(value, MarkupRange.min, MarkupRange.max);
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

        /// <summary>What <see cref="Alerts.Alert_StickupRisk"/> treats as this shop's own
        /// actionable exposure: till silver above this business's own declared starting capital
        /// (<see cref="CompProperties_Business.startingTillSilver"/>), floored at zero. Identical
        /// to <see cref="TillSilver"/> for every ordinary shop (startingTillSilver is 0); a
        /// gambling hall's seeded bankroll is the one case where it differs, because that silver
        /// has to stay in the till to cover a payout and isn't something the player can safely
        /// pull out. <see cref="StickupWatch"/>'s own risk clock is deliberately unaffected by
        /// this property's existence — it keeps counting every till silver, capital included,
        /// because a robber can still take it even though the player can't safely collect
        /// it.</summary>
        public int TillSilverAboveCapital => Mathf.Max(0, TillSilver - Props.startingTillSilver);

        /// <summary>Loose silver stacks on this shop's own sales floor right now — refreshed at
        /// the same cadence StockOnDisplay is (RefreshStock), including immediately after
        /// CollectEarnings moves silver out of the till and onto the floor.</summary>
        public List<Thing> FloorSilverStacks => cachedFloorSilver;

        /// <summary>Total loose silver on the floor, skipping any stack the cache still
        /// references but that isn't actually there any more — hauled off, or grabbed by a
        /// raider, since the last refresh. RefreshStock drops it from the list for good on its
        /// next pass; this getter just has to not count it twice-hauled silver in the
        /// meantime.</summary>
        public int FloorSilver
        {
            get
            {
                int total = 0;
                for (int i = 0; i < cachedFloorSilver.Count; i++)
                {
                    Thing t = cachedFloorSilver[i];
                    if (t != null && t.Spawned && !t.Destroyed) total += t.stackCount;
                }
                return total;
            }
        }

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

        /// <summary>Total shelf price of everything on display — what this counter is asking for
        /// its stock, and the shop's visible richness.</summary>
        public int StockValue { get { EnsurePriced(); return cachedStockPrice; } }

        /// <summary>What those same goods are worth at market: the yardstick an asking price is
        /// set against. This counter's shelves alone — the town's own offer total counts a stack
        /// two counters can both see once, and adds what a service is reckoned to be worth, so
        /// the two figures are the same quantity but not the same number.</summary>
        public int StockMarketValue { get { EnsurePriced(); return cachedStockMarket; } }

        /// <summary>Prices the shelves — a MarketValue stat lookup per stack — when the answer
        /// can have changed, rather than when somebody looks. Both readers, the inspect pane and
        /// the Stock tab, draw every frame. The answer moves only when the shelf snapshot is
        /// retaken (the town's survey does that every 60 ticks; a sale or a filter edit does it
        /// at once), when the player moves the markup, or when the town's name moves at midnight,
        /// and all three are in the key below. So the markup and the town's name are exact to the
        /// frame, while the shelves are as fresh as the snapshot: a stack that shrinks or burns
        /// inside an unchanged one keeps the price the survey last saw, for a second at worst —
        /// and the stack count printed beside it is stale by exactly the same amount, so the two
        /// figures never disagree on screen.
        ///
        /// This derives rather than snapshots: it reads the list the survey already took and
        /// rolls no dice, which is what makes it safe on a draw path where RefreshStock is
        /// not.</summary>
        private void EnsurePriced()
        {
            float rep = ReputationPriceFactor;
            if (pricedVersion == stockVersion && pricedMarkup == Markup && pricedReputation == rep) return;

            float market = 0f;
            int price = 0;
            for (int i = 0; i < cachedStock.Count; i++)
            {
                Thing t = cachedStock[i];
                market += ShopPricing.UnitValue(t) * t.stackCount;
                price += ShopPricing.PriceFor(this, t, t.stackCount);
            }
            cachedStockMarket = Mathf.RoundToInt(market);
            cachedStockPrice = price;
            pricedVersion = stockVersion;
            pricedMarkup = Markup;
            pricedReputation = rep;
        }

        /// <summary>Re-reads the shelves. Called once per shop by the town's survey, and again the
        /// moment a player action changes what is on sale — a filter edit, or a sale. The counter
        /// is what tells the priced totals above that the shelves they were priced from are gone.
        ///
        /// Deliberately NOT done from the getter. A refreshing getter meant that drawing this
        /// counter's inspect pane could decide WHEN the snapshot was taken, and the customer AI
        /// draws one random number per stack it scores, so merely having a counter selected changed
        /// how many numbers came off RimWorld's shared seeded stream — and therefore what the
        /// storyteller rolled next. Looking at a shop must not change the game.</summary>
        public void RefreshStock()
        {
            stockVersion++;
            cachedStock = ShopStock.ScanFor(this).ToList();
            cachedFloorSilver = ShopStock.LooseSilverOnFloor(this).ToList();
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
                    if (!sd.worker.IsAvailable(this)) continue;
                    if (!sd.worker.ConsumesStock) { yield return sd; continue; }
                    if (ShopStock.HasStockFor(this, sd)) yield return sd;
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

        /// <summary>Drops the till's contents at the counter for a hauler to pick up. This moves
        /// silver from "in the till" to "loose on the floor" — both count toward stickup risk
        /// (see StickupWatch.TotalSilverAtRisk), so collecting is not the same as clearing it.
        /// The floor cache is refreshed immediately after the drop, not left for the next
        /// scheduled survey, so every reader of FloorSilver sees the moved silver the same tick
        /// it moves rather than reading a stale, momentarily-lower total.
        ///
        /// The drop search is constrained to CellOnSalesFloor so it can never land in a cell
        /// ShopStock.ThingsOnFloor's own room filter would exclude — a doorway tile or a cell
        /// just past a wall, which a cramped or doorway-adjacent counter could otherwise pick
        /// with nothing nearer free. Silver that landed there would be just as unhauled and
        /// exposed as any other floor pile, but invisible to the risk clock, to a robber's own
        /// scoring, and to the player's own floor-silver reading — a silent hole in the exact
        /// tracking this fix exists to close.</summary>
        public void CollectEarnings()
        {
            if (till == null || !till.Any) return;
            till.TryDropAll(parent.Position, parent.Map, ThingPlaceMode.Near,
                nearPlaceValidator: CellOnSalesFloor);
            RefreshStock();
        }

        /// <summary>Whether <paramref name="c"/> is actually part of this shop's own sales
        /// floor — the same test ShopStock.ThingsOnFloor applies when deciding what's on it,
        /// used here to keep CollectEarnings's own placement search from ever landing outside
        /// it. A narrower nearPlaceValidator can only ever make TryDropAll refuse a candidate
        /// cell it would otherwise have tried — it cannot make an already-safe drop unsafe, so
        /// this is a pure tightening of where the silver can land, not a new failure mode.</summary>
        private bool CellOnSalesFloor(IntVec3 c)
        {
            Room room = SalesFloorRoom;
            if (room != null) return RegionAndRoomQuery.RoomAt(c, parent.Map) == room;
            return c.InHorDistOf(parent.Position, Props.openAirRadius);
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

        /// <summary>At most one till-robbery message per counter per
        /// RobberyMessageCooldownTicks — mirrors TryClaimAccusationMessage's own shape.
        /// Collapses only a genuine same-tick multi-raider race on one till, never the signal
        /// itself. On its own clock, separate from TryClaimFloorRobberyMessage: a till crack and
        /// a floor grab are two distinct thefts of two distinct piles of silver, and a two-raider
        /// crew can land one of each at the same counter well within one cooldown window — sharing
        /// a single tick field between them let the second, equally real theft claim the message
        /// and silently lose it.</summary>
        public bool TryClaimRobberyMessage()
        {
            int now = Find.TickManager.TicksGame;
            if (now - lastRobberyMessageTick < RobberyMessageCooldownTicks) return false;
            lastRobberyMessageTick = now;
            return true;
        }

        /// <summary>The floor-grab twin of TryClaimRobberyMessage, on its own tick field — see
        /// that method's own doc comment for why a till crack and a floor grab must not share
        /// one throttle.</summary>
        public bool TryClaimFloorRobberyMessage()
        {
            int now = Find.TickManager.TicksGame;
            if (now - lastFloorRobberyMessageTick < RobberyMessageCooldownTicks) return false;
            lastFloorRobberyMessageTick = now;
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
            if (FloorSilver > 0)
            {
                sb.AppendLine("OWT_FloorSilverLine".Translate(((float)FloorSilver).ToStringMoney()));
            }
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

            if (HasWager)
            {
                // Markup has its own dial on the Stock tab (ITab_ShopStock) now, not a gizmo — but
                // that tab only ever grew a Markup slider, not a HouseEdge one, so HouseEdge keeps
                // its dial here as a gizmo: same Command_Action/Dialog_Slider shape Markup's used
                // to, same icon (there is no dedicated one, and "the markup slider's twin" is
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
                sb.AppendLine();
            }
            return sb.ToString().TrimEndNewlines();
        }
    }
}
