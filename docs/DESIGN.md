# Old West Town — design notes

## The goal

Hospitality answers "guests are on my map, now what?" with *hospitality*: beds, comfort,
relations. This mod answers it with *commerce*: your colony is a town on a trade road, and the
buildings you put up are businesses that strangers walk into and spend money in.

The target end state is a main street — general store, saloon, hotel, bank, barber, stable,
sheriff's office — where each business is a thing colonists staff, customers use, and the
player prices and stocks.

## The one decision everything else follows from

**Pawn loops never synchronise with each other.**

The obvious way to model a sale is a handshake: the customer picks a shopkeeper, the
shopkeeper accepts, both pawns run paired jobs. That design is where multiplayer-style
mod bugs come from in RimWorld — the shopkeeper gets drafted, breaks, gets shot, or simply
finishes their shift, and the customer is left in a job whose partner no longer exists.

Instead, the two loops only ever touch shared state on the counter:

```
 colonist                       CompBusiness                         customer
 ────────                       ────────────                         ────────
 WorkGiver_ManShop              open / closed                        JobGiver_BuyFromShop
   ↓  picks a business          markup                                 ↓  picks goods or a service
 JobDriver_ManShop              stock filter, services   ← reads →   JobDriver_BuyFromShop /
   └─ every tick:                                                    JobDriver_UseService
      NotifyStaffedBy(pawn) ──→ lastStaffedTick                        ├─ walk to shelf (goods/
                                     │                                 │  a stock-consuming service)
                                     └──────── Staffed ───────────────→├─ wait to be served
                                the line (arrival order) ←─────────────┤   TakePlaceInLine(pawn)
                                                                       │   one counter, one customer
                                                                       └─ ShopTransaction.TrySell /
                                                                          TryServe
                                                                              ↓
                                                                    till, ledger, hediff/thought
```

Neither driver can strand the other. A shopkeeper who wanders off just flips `Staffed` to
false; the customer's wait toil notices, runs down its patience, drops whatever they're
carrying (if anything) and leaves annoyed. That failure is *legible to the player* — it's a
message and a reputation hit — which turns a robustness measure into a game mechanic.

A walkout ends the sale, not the relationship. The customer stops queueing at that counter, and
the refusal still dies with the visit like the rest of their record — but what *lifts* it is
neither the visit running out nor a timer: it is the counter being worked again. That is what
makes the alert honest. The one thing it asks the player for, a colonist behind the counter, has
to be the thing that lifts the refusal, or the alert is asking for a reaction that can no longer
work. Time was rejected as the key precisely because it isn't something the player did: a
cooldown marches the same customer back to the same empty counter to walk out and pay for it
again, turning the alert into a slow drain. For the same reason the dispatch side reads staffing
without the grace window the serve loop allows — a keeper who left a second ago is not somebody
to walk across town for. Nothing is refunded either: the reputation and the sale stay lost, so
serving customers before their patience runs out remains strictly better than winning them back
afterwards. The same scoping is what un-pins the shopkeeper — a refusal that holds only while the
counter is empty is one a colonist arriving actually resolves, instead of leaving them posted
while ex-customers who can never buy anything drift past inside the scan radius.

`JobDriver_BuyFromShop` and `JobDriver_UseService` share this wait/patience/walkout shape from
a common base, `JobDriver_PatronizeBusiness` — see "Services" below. Everything added later (a
hotel clerk, a bank teller, a gambling dealer) should use the same shape.

## Component map

| Piece | File | Job |
| --- | --- | --- |
| `ShopKindDef` | `Shops/ShopKindDef.cs` | Data-driven business type: default stock, price band, appeal, patience, and which services it offers. Adding a business kind is XML, not code. |
| `CompBusiness` | `Shops/CompBusiness.cs` | Makes a building a business — goods, services, or both. Owns the till, filter, markup, ledger, staff flag, the line at the counter, and the staff/customer cell pair. |
| `ServiceDef` / `ServiceWorker` | `Shops/ServiceDef.cs`, `Shops/ServiceWorker.cs` | A thing a business sells that isn't a shelf item — a drink, a meal, a haircut. The embedded worker supplies the type-specific behaviour: what it can act on, how much a customer wants it, what effect it applies once paid for. |
| `ShopStock` | `Shops/ShopStock.cs` | What's on the shelves, what a given customer would buy, which service a shop can currently perform, and whether a service has anything to work with, asked without a roll. |
| `ShopPricing` | `Shops/ShopPricing.cs` | The only place a price is decided — for goods or a service — so the inspect pane, AI and transaction can't disagree. `MaxAffordable` settles a purse against `PriceFor` itself, so the order the AI sizes is always one the counter can charge for. |
| `ShopTransaction` | `Shops/ShopTransaction.cs` | The single point where silver, goods and service effects move. Re-validates everything. |
| `TownEconomy` | `Shops/TownEconomy.cs` | `MapComponent`. Shop register, daily ledger, reputation, and `Appeal` — surveys the town every 60 ticks. |
| `JobGiver_BuyFromShop`, `JobDriver_PatronizeBusiness` (`JobDriver_BuyFromShop`, `JobDriver_UseService`) | `AI/` | Customer side: picks a business — goods or a service, whichever scores best — and runs the shared walk/wait/patience shape. |
| `WorkGiver_/JobDriver_ManShop` | `AI/` | Colonist side. Kind-agnostic: it staffs any `CompBusiness` with something to offer. |
| `JobDriver_ColonistUseService` | `AI/` | The colony's own side of a counter. Waits on the same staffing flag a stranger does, receives the same `ServiceWorker` effect, and touches nothing else — no price, no till, no ledger, no lord. |
| `LordJob_ShopVisit`, `LordToil_Shop`, `LordToil_CloseUp` | `Lords/` | The visiting group, the per-customer records, and what closing time does to a customer part-way through a purchase. |
| `IncidentWorker_ShopCustomers` | `Incidents/` | Turns town appeal into arrivals. |

### Why the lord graph is flat

Vanilla visitor groups run travel → chill → exit. This one is shopping → exit. A travel state
would fight the shopping AI: customers already have a specific place to walk to (the shop they
chose), so a `LordToil_Travel` duty would just drag them back to a chill spot between
purchases. The group state machine only handles *leaving* — because of time, or because
someone started shooting.

Per-customer state (budget spent, purchases, counters they've given up on for as long as those
stay unattended) hangs off `LordJob_ShopVisit` rather than the pawn. That means it saves and
loads with the group, needs no def patching of humanlike pawns, and disappears when the visit
does.

### Closing time

The visit clock is the only clock here that can end something the player is in the middle of, so
it is the only one that has to decide what "in the middle of" is worth. Vanilla's exit toil takes
an `interruptCurrentJob` flag and, set, ends every pawn's job the instant the group is sent home.
That is right for a group that has to be gone and wrong for a shop: it tears up the sale at the
counter one tick from the till, drops the goods on the floor, and throws away however long a
colonist had already spent on it — worst at the barber's, whose 2200-tick haircut is the longest
serve in the mod and the whole of its shop's patience window.

`LordToil_CloseUp` keeps that interrupt for everyone except the customer being served. It hands
out the same exit duty, then ends the same jobs itself, skipping any customer whose transaction is
being worked that tick. Closing is "the town stops taking new customers", not "the town empties":
the barber finishes the head in the chair, and does not start another. Keeping the interrupt for
everybody else is not incidental — customers are allowed to eat, drink and sleep between
purchases, and a sleeping visitor left to wake up on its own is a group that never leaves.

Nothing about closing is written down. A customer sent home was not served and did not give up: no
sale, no walkout, no row in the day's patron table, no weight in the nightly verdict. A walkout has
to stay a thing the player did — a counter left unstaffed for a whole patience window, with the
alert up and the time to answer it — and charging the same halved verdict because the hours ran out
would make it a tax on a clock the player cannot see, levied hardest on the counters the alert was
already pointing at. That is also why the grace has a floor under it. A spared customer is holding
a job that never completes on its own and does not yield to a duty, so a shopkeeper who walked away
would drop them back into the wait branch to run their patience down to exactly that walkout. Their
own end condition — the group has been called home and nobody is serving me — is what prevents it,
and it is what bounds the whole exemption. What it bounds is per counter, not per town: a counter
serves one customer at a time, so each one spares exactly the head it is working and sends the rest
of its queue home with everybody else. A queued customer loses nothing by that — they were not
being served, and at a counter somebody was working they had not spent a tick of patience — one at
an unattended counter has been burning it like everybody else standing there — and closing writes
nothing down either way. And "being served" means the same thing at closing as it does at noon — with the
honesty-box setting on, a counter with nobody behind it counts, so such a sale runs itself out in
its own 180 ticks and lands as the self-served half-mark it would have been at any other hour.
Each spared customer is bounded by one serve, and a keeper who walks away ends theirs inside the
staffing grace.

The grace runs the other way once. A customer who gave up at some counter earlier carries a halved
verdict all day; if the last thing that happens to them is being served out at closing, the sale
lands and their verdict is a half rather than a nought. Standing a counter at closing time is the
one thing that can partly repair a bad day.

Violence is still the exception, with one edge worth naming: the harm transition leaves the
shopping state, so once the clock has already sent the group home it cannot fire again. A raid that
starts during business hours ends every job including a serve — `TransitionAction_EndAllJobs` never
consults the exemption — while one that starts after closing leaves the last serve running until
the colonist stops working it, which drops the customer within a tick or two of the staffing grace.

The shopkeeper's side needed no change at all, which is the sign the seam is in the right place.
The customer scan already counts a pawn patronizing *this* counter whatever their purse or their
duty says — it was written for a serve outliving the shopping duty, and until now that was the one
case it could never actually see — so the colonist stays posted while the last customer is served
out and knocks off on the same idle timer as always. Neither pawn waits on the other.

Under fire none of this applies: the violence transition still ends every job after the duty swap,
mid-serve included.

### Why the sales floor is a room

A shop is defined by walls you already built, not by a zone you have to paint. It reads
naturally ("this room is the store"), it costs nothing to set up, and it makes the room-quality
stats you already care about matter commercially. Outdoors it falls back to a radius so market
stalls still work. Two counters in one room are two tills on one shop-front: the room's goods
count once, and only the first counter of a kind earns that kind's draw.

### Services: the same seam, without a `Thing` changing hands

A service (a drink, a meal, a haircut) is priced and served through the identical seam as a
sale — there just isn't a `Thing` changing hands at the end of it, or, for a haircut, no
`Thing` involved at all. `ShopKindDef.services` is a list of `ServiceDef`s alongside its stock
categories. Each `ServiceDef` embeds one `ServiceWorker` — the same "instance carries its own
XML fields" idiom the customer duty's think tree already uses for `DutyDef.thinkNode` — which
decides what it can act on (`CanUse`), how much a given customer wants it right now
(`Desirability`, weighed against a need like Food or Joy), and what happens once it's paid for
(`ApplyEffect`: a hediff via vanilla's own ingestion outcome, a thought, a visible hair change).

Two core worker classes cover the three services this stage ships, plus a three-line subclass
for Haircut's visual flourish. `ServiceWorker_Ingest` is one class parameterized two ways —
Drink wants `FoodTypeFlags.Liquor`, Meal wants `IngestibleProperties.IsMeal` — because a drink
and a meal are the same mechanic (consume a matching item already on the shelf, then let
`FoodUtility.IngestFromInventoryNow` do what vanilla already does for anyone eating from their
inventory) with different filters and a different need behind the demand curve.
`ServiceWorker_Thought` is a bare "grant a thought" primitive; `ServiceWorker_Haircut` adds a
visible hair change on top of it, because a business that changes nothing visible is a weaker
proof that a service happened.

Every `ServiceDef` gets its own `JobDef` rather than sharing one — `Verse.AI.Job` has no
generic slot to carry a `Def` reference, so a service driver has no other reliable way to
recover which service it's running. The cost is one small XML stanza per service; the
alternative is a driver that has to guess.

The customer side factors the shared "walk up, wait to be served, get served or walk out" shape
into `JobDriver_PatronizeBusiness`, with `JobDriver_BuyFromShop` and `JobDriver_UseService` as
its two concrete shapes — a goods sale (or a stock-consuming service) fetches an item first; a
stock-free service (Haircut) skips straight to waiting. A small business-facing view of a patron,
`IBusinessPatron`, lets code outside the AI namespace ask the two things about a patron that are
not its own business to work out — are they waiting, and is their transaction running — for
queue-spacing, the waiting-customers alert and closing time, without the Shops layer ever depending
on the AI namespace.

A service business counts toward town appeal the same way a stocked one does
(`CompBusiness.HasAnythingToOffer`, `AvailableServices`), with one wrinkle: a stock-consuming
service's value is already counted once, as stock — the town's survey only adds services with
no `Thing` behind them at all, so a saloon's beer isn't counted twice for being sellable two
ways.

### The town's own people

Every path into a business used to be a stranger's. The shopping job giver runs from the `OWT_Shop`
duty and only a visiting group's lord hands that out, so a colony could build a barber shop its own
people were locked out of, and the `+5` haircut thought could never land on the pawns it was written
for. The mod's whole surface was a service the colony provided and could not use.

A colonist is not a customer, and the sharpest way to say so is that nothing on their path touches
money. Silver moving from the colony into the colony's own till is a wash that buys a bookkeeping
entry and a wealth illusion. Worse, the town's name is now one verdict per person per day: a
colonist counted as a patron would be a reputation printer — send ten pawns to the chair, earn a
perfect day, and buy the arrival rate and the price band with labour that was going to be spent
anyway. So a colonist leaves no row in the patron table, no sale in any ledger, no walkout and no
reputation, and the guarantee is structural rather than a flag. `JobDriver_ColonistUseService` does
not name `ShopTransaction`, `TownEconomy` or the `Lords` layer at all; there is nothing to leak
through, and a reviewer can check that with a grep rather than by reasoning about branches. The
patron table is guarded a second time at its own door — `NotePatron` refuses a player-faction pawn —
which is unreachable from anything shipped today and is meant to be: it makes the rule true of the
next path into a business as well as this one.

Which is why the colonist path does not extend `JobDriver_PatronizeBusiness`. That base is a
*visit*: it ends itself when the group's duty is swapped away — a colonist has no duty, so it would
end on the first tick — and its patience branch is a walkout, which is a shop ledger entry, a patron
row and a reputation hit in one call. The reuse that pays is a layer lower: the `ServiceDef` and its
worker's effect, the counter's staffing flag, the customer cell and its queue spacing, and the small
`IBusinessPatron` view of a patron. Everything above that is a stranger's, and a colonist wants none
of it.

What a colonist costs is time, twice. Their own, waiting and then sitting; and a second colonist's
whole shift standing behind the counter. That second cost is not incidental — it is the feature.
A colonist is never allowed to serve themselves, even at a counter whose service permits the honesty
box and with the setting on, because the honesty box is a bargain priced in reputation and a
colonist leaves no reputation to pay it with. For the colony's own it would simply be free, and no
counter would ever be staffed for its own people again. Being served by somebody is also the entire
content of the thought.

That second cost is also the dispatch mechanism, and it needed exactly one widening. The
shopkeeper's customer scan already counted anybody patronizing *this* counter whatever their purse
or duty said, because a serve outliving its duty was the case that clause was written for; it now
runs that test before the player-faction filter instead of after it. That is the whole of a
colonist's visibility to the business layer — scoped to the counter they stand at, so they can never
pull staff across town — and it is what breaks the circle the gap concealed: nobody posted, so
nobody served, so nobody posted. It is also what keeps a barber at their post through a 2200-tick
haircut, which outlasts a shopkeeper's 1250-tick idle patience. Both pawns are in the player's
faction for the first time in this mod and they still never wait on each other: the patron reads
`Staffed`, the shopkeeper never learns who they are serving, and a keeper who wanders off empties
the progress bar rather than stranding anybody. The one new rule is that `Staffed` alone is not
enough — the keeper must not be the patron, because the flag's 60-tick grace is comfortably long
enough for a colonist to stop minding a counter, walk three tiles and cut their own hair.

The player sends them, and nothing else does. That is a decision about whose hours these are. An
automatic urge would need a demand model and, worse, a thrash guard: with nobody free to staff the
chair it marches the same colonist back to wait out patience again, and bounding that means scribed
per-pawn, per-counter cooldown state invented to fix a dispatcher nobody asked for. An order has no
such loop. It also answers "what stops ten colonists queueing at one chair" without inventing
anything: the player clicks once per pawn, the counter is reserved so vanilla refuses the second
claim and the menu names who holds it, and a pawn who already carries the thought cannot be ordered
at all — `Desirability` returning zero is one answer read by both the stranger's scoring and the
order menu, and it puts the rate limit on the thought's own `durationDays`, in XML, where a modder
retuning the reward retunes the pacing with it. The claim is on the standing cell rather than on the
counter, so it cannot grey out a work order on the counter itself — which is the one order a player
reaches for when nobody has come to serve their colonist. Travellers are unaffected by any of it,
not because reservations are per-faction (RimWorld respects them between non-hostile factions too)
but because no visitor path in this mod reserves anything at all, so a counter can serve the town
and one of its own at the same time.

A colonist ordered into the chair also comes out of it with a different, randomly chosen hairstyle,
which is worth saying out loud in the building's own description: on a stranger passing through it
is a flourish nobody will see again, and on a pawn the player designed and has looked at for two
hundred hours it is a change they did not choose and, without Ideology's styling station, cannot
undo.

Only the haircut is opened this way, and the restriction is enforced at load: a `colonistJobDef` on
a service that consumes stock is a config error. A drink or a meal is an item on a shelf that
vanilla's own recreation and food jobs already send colonists to *under the player's drug policy* —
routing it through here would either override that policy or reimplement it, and would destroy a
saleable item with nobody to answer for it. The stock-free service is the only one vanilla cannot
already do for a colonist, and it is the one that ships.

### One counter, one customer

`Staffed` was a property of the shop, so every patron's wait toil read it and advanced its own serve
independently: one colonist behind the barber's chair cut five heads in the time of one haircut. The
fan-out `CustomerCellFor` builds — spacing patrons around the counter so they do not stack on one
tile — was decoration over a queue that did not exist, and a second till bought a second sales floor
and nothing else. A counter now serves the head of its line.

The line is the patrons standing at that counter in the order they arrived, and it is deliberately
not a claim, a lease or a pairing. Nobody picks anybody. The shopkeeper's side needed no change at
all — neither `JobDriver_ManShop` nor `WorkGiver_ManShop` knows the line exists — which is again
the sign the seam is in the right place. An entry is valid exactly as long as that pawn's own job
says they are standing here, so there is nothing to expire and no timeout to get wrong: the condition IS
the claim. A patron drafted, downed, killed, re-tasked or simply finished gives up their place, and
the giving up is done by the pawn that holds it rather than by anything noticing they are gone. The
counter cannot hold a reference to somebody who no longer exists, because the only thing it holds is
a list it re-validates against those pawns' own jobs on every read.

Nothing is saved, and the line survives a load anyway — it rebuilds within a tick as each patron
ticks. The one thing a rebuild could get wrong is bumping somebody two thousand ticks into a haircut
out of the chair, and one rule prevents it: a patron whose transaction is already running goes to
the front. That rule earns its keep twice, because it is also what stops a shopkeeper arriving at an
honesty-box counter from sending the customer already mid-sale to the back. The line never
interrupts a transaction already running.

Queueing behind a moving head costs nothing at all: no clock, no memory, no message, no reputation.
Patience is a promise about being *ignored*, and a counter busy with somebody else is not ignoring
anyone. Spending patience on a queue would halve a verdict, slide the town's name and raise an alarm
whose one fix — a colonist behind the counter — the player has already applied, for the crime of
being popular. It would also repeat: a walkout's refusal lifts the moment the counter is staffed, so
under a serialisation that charged for waiting the same customers would march back and walk out
again every window for the rest of their visit. The only counterplay to that is to attract fewer
customers, which is no counterplay at all.

A queued customer needs no give-up clock either, and the reason is structural rather than bought.
The head is never queued, so it always runs one of the two clocks that end — the serve, or its
patience at a counter nobody is working — and a place is released by the pawn that holds it. Neither
of those two clocks is monotone on its own: being served zeroes the patience one and being ignored
zeroes the serve one, so a shopkeeper who takes the post and loses it over and over, faster than
either window, would advance neither. That is what the driver's absolute backstop is for — a single
forward-only clock, long enough to outlast any queue the door lets a customer join, ending in the
walkout that being messed about for hours actually deserves. With it, someone at place *k* reaches
the head within *k* terminating waits, waiting on a list index and never on a pawn. That is worth more than the same bound bought with a scribed
per-visit queue budget, which is why there is no new saved state here at all.

The decision that matters is made at the door rather than in the line. A customer counts everyone
already committed to a counter — walking, browsing, queueing or being served — and will not join a
wait of 6000 ticks or more; below that, a crowd shades that counter's *staffing bonus* and nothing
else. The "nothing else" is load-bearing. A crowd that discounted the whole shop would score a busy
staffed counter below an unworked one, and the rule meant to spread custom between tills would
become a rule that pushes people onto counters nobody is standing at — manufacturing the exact
walkouts it exists to prevent.

What a bottleneck costs is the sale, and only the sale. Somebody who never joins never stands at a
counter, so there is no walkout, no row in the day's patron table and no weight in the nightly
verdict; popularity cannot cost the town its name. The player's moves run cheapest first. Do
nothing, and the fourth customer buys dry goods instead — a real option, which is what makes the
mechanic fair rather than a tax. Raise the barber's markup, so fewer customers chase the same
capacity and each one pays more; that costs no code, because `ShopPricing.ValueAppeal` was already
the first term in the score. Build a second till and staff it: the shaded staffing bonus is what
sends the second customer to the free counter rather than the busy one, so the second till earns
from the second customer instead of the fourth, and the claim in "Why the sales floor is a room" —
that what a second counter buys is serving two customers at once — stops being aspirational here.
Appeal is still right to pay it nothing: capacity is not a reason to come to town.

Where this bites today is a tuning fact, not an architectural one. Depth is `serveTicks`, in XML
that already exists: a counter holds `ceil(6000 / serveTicks)`, which is 34 at a 180-tick goods
counter and 40 at a 150-tick saloon bar — lines nobody will ever see — and exactly 3 at the barber's
2200-tick chair. The haircut is the one trade this mod ships where a counter is genuinely scarce.

## The economy loop

```
     player stocks + prices + staffs shops
                     │
                     ▼
         TownEconomy.Appeal  ◄──── reputation ◄──── one verdict per customer, settled nightly
                     │                                        ▲
                     ▼                                        │
   IncidentWorker_ShopCustomers: how often, how many ◄ appeal │
                             how rich ◄ goods on offer        │
                     │                                        │
                     ▼                                        │
        customers arrive ──── buy goods or use a service ─────┘
                     │
                     ▼
                silver in the till
```

Appeal is the compounding-investment number, and it measures one thing: what a traveller would
find here. Three terms multiplied — the businesses, the goods on offer, the town's standing —
which is how the ledger shows it, because a number the player is meant to grow has to say which
lever moves it.

The unit of a business is a **sales floor**, not a counter. Walls already define a shop (see "Why
the sales floor is a room"); a second counter in the same room is a second till, and what it buys
is serving two customers at once, not a second reason to come to town. Goods are counted the same
way — every stack on sale counts once, however many counters can see it. Before, a shared room was
added once per counter: a second counter in the general store was worth +91% appeal for nothing.
A counter of a *different* kind sharing a floor does still earn its kind's draw — a bar in the
corner of the trading post genuinely offers a drink where there was none — but the goods under it
are counted once, so what it adds is the kind, never the shelves.

Breadth still beats depth, and now at every scale. A kind the town does not have is worth its full
draw; each further front of a kind you already run is worth 35% of *the one before it*. The flat
"35% of the first, forever" it replaces summed without limit — five general stores out-earned the
shipped three-kind main street, inverting the rule it was written to enforce. Geometric decay
converges: a kind is worth at most 1.54x its first front however many you build, so no amount of
one trade catches a street with three, and depth still pays honestly through the goods term.

Goods count at **market value, not the shelf price**. Appeal is what the town offers; markup
decides what it asks. Pricing appeal through `ShopPricing` swung a lone store's appeal by 2.45x
across the markup slider's range — gouging drew more and richer customers as well as taking more
from each — and being generous could switch the town's trade off entirely by dropping it under the
0.5 threshold. Price still decides which shop a customer walks into: that is
`ShopPricing.ValueAppeal`, settled at the counter, which is the right altitude for it.

### Two numbers, not one

How *often* travellers come and how *rich* they are are different questions and now read
different numbers. Appeal — breadth, goods, standing — drives the arrival clock and the size of
the group. The goods term alone drives what arrivals carry: three trades do not fatten a purse
and a good name does not either, but a rack of rifles does. Scaling purses off appeal meant every
town above appeal 4 sat pinned at the top of the range, so the investment the number was supposed
to teach became invisible exactly when the player started making it. Both are market value, so
neither can be bought with the markup slider.

### Appeal is surveyed, not computed on demand

The expensive, map-walking half is something the town does every 60 ticks; what is left is a
square root and a lerp, cheap enough for a counter's inspect pane to draw every frame. On demand
it re-walked and re-priced the whole town per rendered frame while a counter was selected — and
rolled the shared seeded game stream while doing it, because it asked "is there stock for this
service" through the picker that rolls a tie-break, so whether a counter was selected changed what
the storyteller did next. The existence question is now answered without a roll, and the picker
can no longer be called without a customer to roll for. A survey rather than a cache because a
cache would need an invalidation contract — stock, walls, room merges, markup, opening and closing
— and hauling a crate into a store fires none of those; the survey is authoritative by definition,
the same way reputation is authoritative because it settles at midnight.

Arrival frequency is the town's own doing, not the storyteller's. `TownEconomy` runs an MTB
clock that shortens as appeal grows (roughly one group every 3.5 days at the 0.5 threshold,
most days at high appeal) and fires the incident through the storyteller, so `minRefireDays`
still caps the rate. The `IncidentDef` keeps a small `baseChance` as a background trickle.

Reputation is a record of *service*, not of sales. Each caller leaves exactly one verdict a day
however many times they opened their purse — full marks for being served by somebody, half for
helping themselves off an unwatched counter, and whatever they were owed is halved again if they
gave up waiting anywhere in town. The day's average is blended into the town's name at midnight,
weighted by how many people the day actually heard from, so one traveller giving up on a dead
Tuesday is bad luck rather than a scandal. Counting receipts instead made reputation a function
of how much stock was on the shelves: one group of four crossed the whole range in an afternoon,
after which both of the things that read reputation were reading a constant. No per-sale constant
survives that, however it is tuned, because the granularity is the defect and not the size of the
step. A day with no custom at all drifts back toward neutral instead: a town nobody trades with is
forgotten, not hated — and that branch is also what stops a ruined name being a trap, since bad
standing thins the crowd and a thin crowd stops producing walkouts.

The direction is the obvious one and was, for a long time, backwards in the code: a good name
buys higher prices and more footfall, while a town with a bad one has to discount to move goods.
That discount is deliberately the way back rather than a second punishment — a cheaper shelf
stretches a purse, so the few customers a poor town still draws buy more of what is on it, and
each one served in person earns the name back. (It does *not* work by making the town look better
value: the factor is town-wide, so it scales every shop's `ValueAppeal` alike and cancels out of
the comparison a customer actually makes between them.) The two consequences are not the same
size. The price band is ±10% while the arrival term runs 0.5x–1.5x, so what a good name mostly
buys is *who comes*, not what they pay. Folding reputation into the price at all is only
defensible because the number now moves once a night rather than on every sale in town: a price
can still shift under a customer mid-walk when a visit spans midnight, but no longer because
somebody else bought a beer while they were choosing.

### Setting a price where you can see it

The Stock tab is where a business's goods are decided, and until now it said nothing about money:
not what the shelves were worth, not what the counter was asking for them, and not what the price
control — a separate gizmo opening a modal slider over the top of the goods it repriced — was doing
to either. The player set a price blind and found out by watching the till.

So the price moved into the tab and the gizmo went. A markup is a setting, not an action, and the
one screen that can show its consequence is the screen listing the goods it applies to. Two rows
above vanilla's filter tree carry the decision: what is on the shelves, what it would fetch at
market value and what this counter is asking for it; then the slider itself.

Those two money figures are the comparison a price *is*, so they share a line, and their ratio is
the multiplier actually charged — including the part of it the player did not set. Reputation moves
the real price by up to a tenth either way, so a screen printing only the markup would state a
number the till does not charge. Money cannot disagree with the till, and where the slider's
percentage and the charged one are genuinely different numbers a third line says the charged one —
drawn by comparing the two percentages as they will be *printed*, so it never appears to repeat the
line above it. In a town of neutral standing it never appears at all.

A fourth line exists only for a counter that sells something no shelf can price. The barber's whole
trade is a haircut, so without it that tab is a slider with no visible effect; the saloon's drink is
priced off a bottle the player would not think to look at, so it is named without a price, because
it hasn't got one of its own.

Everything else was left out. A price cannot be shown against vanilla's filter tree because that
tree lists defs and a price is a property of a stack — quality, stuff and hit points are all inside
`Thing.MarketValue` — so a figure beside "Fine apparel" would be wrong for almost every garment
behind it. A full per-stack price list was built and rejected: it answers browsing rather than the
decision, and it costs the map three hundred pixels no vanilla tab takes. A per-row percentage would
be one number printed forty times, since every shelf price is market value times the same two
factors. What a customer can afford is a town figure fixed when a group spawns, and the ledger is
where town figures live. What is selling would be a new saved per-def record on every counter, for a
question a shrinking stack already answers.

The tab draws every frame it is open, and pricing a shelf costs a `MarketValue` lookup per stack, so
nothing is priced while drawing. The counter keeps its two totals against the three things that can
move them — the shelf snapshot, the markup, and the town's name — and recomputes only when one of
them has. The markup and the town's name are exact to the frame; the shelves are as fresh as the survey that
read them, which is a second at worst. That makes the figures exact against the snapshot rather than
merely fresh: there is no window in which the tab
shows yesterday's name. It is also a removal rather than an addition, because the inspect pane used
to sum the whole shelf every frame to print one line, and now reads the same memo; the pane and the
tab cannot disagree, because there is one number. Deriving totals from a snapshot somebody else took
is safe on a draw path in a way that retaking the snapshot is not: no dice are rolled, and nothing is
re-read from the world.

`ITab` instances are built once per tab type, not per building, so the quick-search box and scroll
position in the filter tree were shared by every counter in town: a search typed at the saloon
silently filtered the general store's tree from a box scrolled out of sight. Vanilla's own storage
tab holds the same shared state and scrubs it when the tab opens and when the player clicks away —
neither of which happens when the *selection* changes under an already-open tab, which is the case
that leaks. This tab remembers which counter its filter state belongs to and starts clean when the
selection moves, by id rather than by holding the building, so a demolished counter is not kept
alive by the tab that last drew it.

## Roadmap

Staged so each step is playable on its own.

**1. Vertical slice — done.** Counter, stock, pricing, till, shopkeeping work type, customer
arrival and purchase, appeal and reputation.

**2. Services (no goods change hands) — done.** The interesting half of a town sells *time*,
not items. `CompShopCounter` is generalised into `CompBusiness` with a pluggable
`ServiceWorker`; the customer job becomes walk to the service point (skipped for a service
that consumes nothing), wait to be served, pay, receive a hediff or a thought instead of an
item — see "Services" above. Shipped: `_Ingest`, parameterized as both **Drink** (a Liquor item
off the saloon's own shelves, feeding Joy) and **Meal** (any meal, feeding Food) — the
interesting hybrid case, since a service that still moves stock has to answer to both the
goods loop and the service loop without double-counting; and `_Haircut` (a new **barber shop**
business, pure time, a mood thought plus a visible hair change). Bath and Doctor are left as
XML-only additions once their buildings exist — same seam, no new lesson to teach.

**3. Lodging.** A `CompRentableBed`: customers with no bed of their own pay per night and stay
across the day boundary. Requires extending the visit duration past one day and giving the
lord a "settled in town" state.

**4. Town roles.** Sheriff, barkeep, banker as assignable posts with their own work givers and
gizmos. A sheriff suppresses the drunk/brawl events a saloon starts generating.

**5. Reputation with depth.** Split the single reputation float into per-faction standing, so
specific factions become regulars. Feeds arrival frequency by faction.

**6. Old west content pass.** Boardwalk terrain, false-front facades, hitching posts, batwing
doors, faro tables, a gallows. Mostly XML; the point is that step 1–5 already make a town
*function*, and this makes it *look* like one.

**7. Hospitality bridge (optional).** A soft-dependency assembly that gives Hospitality guests
the `OWT_Shop` duty, so a single group can both lodge and shop.

### Beyond the staged plan — thematic expansions

Larger directions that build on the finished stages rather than slotting between them. Each is
listed with what it reuses, roughly cheapest first.

**Gambling hall.** A faro/poker table as the first business where the "transaction" is a wager
rather than a purchase: patrons buy in, and a player-set house edge (the markup slider's twin)
determines the expected take. Set it greedy and patrons lose fast, get angry, and reputation
drops; set it fair and they stay all evening buying drinks. Colonist dealers use the
Shopkeeping work type, with Social skill reducing cheating accusations. Mostly a step-2
`ServiceWorker` plus a payout roll — it reuses the wait-to-be-served toil and the till
wholesale, and adds the first income stream that isn't stock-driven.

**Outlaws and the law.** A rich town becomes a target: the more silver sitting in tills
(already tracked per counter), the higher the chance of a *stickup* — a small raider band that
heads for counters instead of colonists, empties tills, and leaves unless resisted.
Counterplay is the step-4 sheriff, plus a wanted board (bounty quests on recurring outlaw
leaders) and a jail that converts captured outlaws into silver or reputation. Turns "collect
the takings" from a chore into a real risk-management decision. New incident and lord job on
the existing shapes.

**Stagecoach line.** A coach depot that puts the town on a scheduled route: guaranteed
high-budget customers every few days, outgoing mail contracts (deliver parcels for silver),
and the occasional VIP passenger — a quest-giver or a shopper with a 5× budget. Appeal raises
the route's tier, from irregular freight wagons up to a daily express, giving the compounding
economy a visible milestone ladder on top of the shortening MTB clock.

**Gold rush.** A map-wide *strike nearby* event that floods the town with prospectors for a
quadrum: arrivals triple and budgets rise, but they only want a specific demand basket (tools,
meals, booze, medicine) and they bring brawls and claim disputes. Price-gouging during the
boom decays reputation faster; when the vein dries up, arrivals crash below baseline until
reputation recovers. Exercises the markup slider and the breadth-over-depth appeal math
dramatically, and gives long saves a narrative arc.

**Rival towns.** One or two NPC towns as world-map neighbours with their own abstract appeal
score. Customer groups *choose* between towns — your share of regional traffic is your appeal
relative to theirs, so the arrival clock has an opponent. Rivals undercut prices, poach your
best shopkeeper with a job-offer event, or send saboteurs; out-compete one long enough and it
becomes a ghost town you can salvage. The most ambitious of the five (it adds world-map
state), but the one that most directly deepens the pricing-and-appeal loop — it gives
`TownEconomy`'s single appeal float an external yardstick and makes pricing genuinely
competitive rather than solitaire.

## Known risks

- **None of this has run in RimWorld.** It compiles against the 1.6 reference assemblies and
  passes `tools/validate_defs.py`, but job drivers, lord graphs and duty think trees are
  exactly the code that static checking can't validate. First-play bugs are expected.
- The Stock tab's two new rows are laid out against widgets whose internals cannot be read off the
  reference assemblies: `Widgets.HorizontalSlider` decides for itself where inside its rect the
  label and the rail go, and `Widgets.LabelFit` decides how to shrink an over-long money line. Both
  get rects with room to spare, the font is set back before the filter tree in case the slider does
  not restore it, and no measurement is taken from either — so being wrong costs a cosmetic surprise
  rather than a broken tab. Neither has been seen in a live game.
- `CustomerCell` mirrors the interaction cell through the counter. For an unusually shaped or
  awkwardly placed counter this can pick a cell the player didn't intend; there's a fallback to
  any standable neighbour, and queueing customers fan out to free cells around it
  (`CustomerCellFor`), but a dedicated "customer side" marker would still be better.
- Customers can't reserve items against colonists (RimWorld reservations are per-faction).
  Goods a colonist has already reserved are excluded from the shelves, which removes most of
  the churn, but a hauler can still start a job on goods (or a service's consumable) a customer
  is mid-walk toward — and two customers can race for the same stack. The loser's job fails
  gracefully: it ends on the goto/carry fail conditions, before the counter. A customer whose
  purse comes up short *at* the counter — the markup slider moved while they walked, or their
  visit spanned the midnight reputation roll — remembers "this kind of goods, at this counter,
  this visit", so it costs them one trip instead of repeating. It is not a walkout and costs no
  reputation. The other refusals need no memory: goods pulled from the filter or forbidden simply
  leave the shelf scan until the player puts them back.
- Staffing a counter lifts every refusal standing against it at once, so a colonist who takes the
  post and then leaves for a full patience window can cost a second walkout from the same
  customer. It is bounded rather than prevented: the customer is only ever dispatched to a counter
  somebody is standing at *that tick*, the wait toil resets on any staffed tick after that, so the
  colonist has to be continuously absent for a whole window, and each further walkout needs a
  further staffing episode the player caused. Capping it would mean scribed per-(customer,
  counter) state, which is a worse trade than an outcome the player can see themselves producing.
  At the town level a second walkout from the same customer on the same day now costs nothing
  extra, because the ledger records the disappointed customer rather than the number of times they
  were disappointed. The per-shop count and the walkout message still fire, so the player still
  sees it happen.
- A customer who has spent their last silver, or who is asleep, no longer holds a colonist at a
  counter — but nothing sends them home either. They keep the shopping duty and wander the town
  centre until the visit clock runs out. Letting a spent-out customer leave early is a lord-graph
  change, and belongs in its own commit.
- The town's survey (`TownEconomy.TakeStock`) re-reads every sales floor and prices every
  stack. It runs once every 60 ticks, and it is the only thing that takes that snapshot, so
  appeal and the shelves can lag the world by up to a second of game time but never disagree
  with each other. While the game is paused nothing surveys at all, so a filter edit refreshes
  its own shop's shelves directly rather than waiting for a tick that will not come. It
  everything that wants appeal reads the two numbers it recorded. This is a new steady cost:
  before, a shop nobody was looking at or shopping at never scanned its room. Fine for a main
  street; worth revisiting at a hundred counters.
- Appeal fell about 18% and customer purses were moved onto the goods term, so a stocked
  three-kind street draws groups about 18% smaller carrying about 31% less each. Both changes are
  deliberate — the old goods term counted the player's own markup, and the old purse scale read
  2.2 for every town above appeal 4, which is every functioning town — but neither has been
  played. The knobs are `BasePurse` and `MinPurseFactor`, not the appeal terms.
- Two vanilla calls the services path leans on — `FoodUtility.IngestFromInventoryNow` for
  Drink/Meal, and the `PawnStyleItemChooser.RandomHairFor` + `SetAllGraphicsDirty` pair for a
  Haircut's visible hair change — are exercised by this mod for the first time. Every signature
  involved is confirmed against the real 1.6 reference assembly, but the exact in-game outcome
  (whether a customer visibly gets `AlcoholHigh`, whether a hair change reliably repaints a
  transient visitor) hasn't been confirmed in a live game.
- Closing lets every customer whose transaction is already running outlive the group's "heading
  home" line while they are served out — now one customer per counter rather than a whole queue at
  one counter, and up to a haircut's 2200 ticks — so the lord and its per-customer records live that
  much longer than the message suggests. Each one is bounded by its own serve and by the counter
  staying staffed (or, with the honesty box on, by the 180 ticks a self-served sale takes, which is
  the one case where a counter can still spare several at once), but it has not been watched in a
  live game.
- A colonist waiting for a service reserves the counter, so the building cannot be deconstructed,
  repaired or uninstalled while it lasts. Bounded by one patience window plus one serve, and the
  player can cancel the job — but it is a lock the counter did not have before.
- `ServiceWorker_Thought.Desirability` now returns zero for a pawn already carrying the thought.
  That is the point of the thought's duration and it makes the order menu and the stranger's scoring
  read one answer, but it also changes shipped behaviour: a visitor who was in the chair earlier
  will no longer be scored for a second haircut.
- The `NotePatron` faction guard is unreachable from anything this mod ships. It is there so the
  rule survives the next author, at the cost of a branch no test can exercise.
- `ThingComp.CompFloatMenuOptions` is confirmed present in the 1.6 reference assemblies, confirmed
  reachable through `FloatMenuOptionProvider_FromThing`, and confirmed to be the hook vanilla's own
  building comps use — but that a right-click on a 2x1 impassable counter reaches it has not been
  run in a live game, like everything else here.
- The give-up message has no rate limit, unlike a walkout's. A player who re-orders a haircut into
  an unstaffed shop gets a message each time, which is right: they caused each one.
- Only the barber forms a line anybody will ever see. Goods (180-tick serve) and saloon service
  (150) allow depths of 34 and 41, which no group this mod produces reaches, so the visible surface
  of "one counter, one customer" in shipped content is one building. That is correct — a bartender
  pouring five drinks in 750 ticks is not implausible — but the mechanic is under-exercised until a
  business with a long serve ships.
- The wait a customer estimates before joining charges *their own* serve time for everyone ahead of
  them. Exact for every shipped kind, since each counter runs one trade, but a counter mixing a
  150-tick drink with a 2200-tick haircut would misjudge it badly in whichever direction the mix
  ran. There is no shipped instance, so it is named rather than defended against.
- The line is not saved, so a load rebuilds it in pawn-tick order: the order among patrons who are
  merely *waiting* can change across a save. Invisible but real. The one case that would cost
  progress — somebody mid-serve losing the chair — is prevented by the rule that a running
  transaction goes to the front, which is also the least-exercised branch in the mechanic.
- That same front-insert fires in normal play with the honesty box on: a shopkeeper arriving at a
  counter where three patrons are serving themselves puts all three at the head in arbitrary order,
  one keeps serving and two restart. Bounded at 180 ticks, in a setting that is off by default — but
  it is a fairness rule that quietly stops being first-come in that one configuration.
- The dispatch cap applies to an unworked counter as well as a worked one, because it counts who is
  headed there and not who is being served. The wait side of an unattended counter is unchanged —
  everyone standing at it, head and queue alike, burns patience and walks out at the window — but at
  the barber at most three now pile on rather than a whole group, so the alert counts three at a
  time. It staggers the damage rather than reducing it: once those three walk out they refuse the
  shop, the two who balked see an empty line, join, and walk out in their turn, so the day's ledger
  still records five. At the 180- and 150-tick counters the cap is far above
  any real group and the pile-on is exactly as it was. This is the one path that still costs
  reputation, and it is deliberately the one path the line does not soften.
- A colonist ordered to a counter can still end up two deep, because places go by arrival and the
  order menu checks at order time: a stranger dispatched after them can arrive first. They then
  spend up to 6600 ticks and give up with the message. Bounded, player-caused, and the message names
  the fix.
- With the honesty box on, a self-serving patron leaves the line but still counts as headed for that
  counter, so they slightly discourage others from choosing it. At a 180-tick serve that is a
  twentieth of the queue budget; not worth a second scan to fix.
- Counting who is headed for a counter walks every spawned pawn, once per shop per customer think
  tick and once per right-click on the order menu. It rolls no dice, so the rule that looking at a
  shop must not change the game still holds, but it is a new steady cost, linear in counters — the
  same shape as the town survey's known cost, and the same place to revisit it.
- The staffing draw now decays with the crowd (1.50, 1.25, 1.17), so custom spreads between shops
  and between tills more than it did. Which shop gets a given sale in a multi-shop town has changed,
  and that has not been measured.
- A customer turned away with nowhere else worth going gets no job and wanders, re-deciding on every
  think tick. Cheap, and the message is throttled to a quarter day, but in a one-business town it
  means visitors doing nothing visible — the same shape as the spent-out customer already in this
  list.
