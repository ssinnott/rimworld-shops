---
title: Design notes
summary: The reasoning behind the mod's shape — the trade-offs considered, and why each one landed where it did.
---

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

## Where the parts live

The file-by-file component map moved to the wiki, so it sits next to the reference tables and
stays under CI's eye: **[Code map](architecture.md)**. That page says what each source file
owns; the sections below are the reasoning behind the shape it describes.

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

### The departure report

`CreateGraph()`'s pre-actions are built once, whenever the graph is (re)built — which is why the
plain `OWT_CustomersLeaving` and `OWT_CustomersScared` strings only ever named the faction. Spend
and held silver are only knowable at the moment the group actually leaves, so the sentence
reporting them has to be composed inside `DoAction`, at fire time, not baked into the `Transition`
object at graph-build time. `TransitionAction_Custom` is the vanilla primitive built for exactly
that: no new subclass, no Harmony — "prefer XML and vanilla over code" applied one level further
than usual, since even the mechanism here is borrowed rather than written.

`AnnounceDeparture` reads four numbers off `lord.ownedPawns` and each pawn's `CustomerRecord`:
what the group spent, what they still hold, how many bought nothing, how many gave up waiting
somewhere. Two of `CustomerRecord`'s other fields were deliberately left out, and both exclusions
answer the same attribution-honesty question the brief itself raised. `RefusedGoodsAt` — a
customer's per-visit memory of what a specific counter turned them down for — traces to exactly
one call site (`JobDriver_BuyFromShop.CompleteSale`, on a `CannotAfford` result after the price
already passed a first check against the same purse), and that is a rare shelf-to-counter price
race, not a general "too expensive" signal; folding it in would tell the player a pricing story the
data doesn't actually support. `causedTrouble` is excluded for a different reason: it already has
its own message and its own reputation hit (`OWT_SaloonTrouble`), so it is a behaviour-and-policing
fact, not a demand signal — and since its only gameplay write site (`TroubleUtility.
Notify_ServiceRound`, called from `JobDriver_UseService` immediately after a paid service round)
always follows a purchase, a pawn who caused trouble already has `purchases > 0` in practice, so
counting them as "never bought" would print something the record itself contradicts. What's left —
`purchases == 0` and `walkouts > 0` — are the two things the record can actually prove happened to
a specific pawn, which is the bar this report holds itself to throughout: say the number and stop
where the cause isn't provable.

The harmed exit is untouched on purpose. `Trigger_PawnHarmed` can fire minutes into a
40,000-tick visit, and held-versus-spent at that point measures how early the interruption landed,
not whether the shelf satisfied demand — exactly the confident-wrong-explanation the brief warns
against, on the single most frequent departure path in a rough game. A raid that cuts a visit short
after two sales and one that cuts it short after twenty look identical to this accounting, and only
one of those is actually a comment on the shelves.

The report also inherits [closing time](#closing-time)'s own grace, and the one gap that grace has
always carried. The snapshot runs the instant `timeUp` fires, before `LordToil_CloseUp` spares
whichever customers are already mid-serve — and since that grace is [per counter, not per
town](#closing-time), `spent` can under-report by one transaction per counter still serving
someone at that instant, not by a fixed amount. That is the same timing gap the plain
`OWT_CustomersLeaving` line has always had; closing it would mean blocking the whole group's
departure on however many sales are still running, which defeats the point of the grace in the
first place.

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
### Lodging: a service whose effect outlives the transaction

Renting a room (`OWT_Lodging`, worked by `ServiceWorker_Lodging`) is a service for exactly the
same reason a haircut is — no `Thing` changes hands, a colonist's time behind the desk is what's
being sold, and check-in reuses `JobDriver_UseService` / `ShopTransaction.TryServe` completely
unmodified. What makes it different from every service before it is that paying for it isn't the
whole experience: paying for a haircut *is* the haircut, but paying for a room is just the
booking — the stay happens later, unattended, quite possibly after the shopkeeper who sold it
has gone home for the night.

That gap is bridged by widening `ServiceWorker.ApplyEffect` to return a `Thing` a service has
claimed for longer than the sale itself. Every worker from before this stage returns `null`;
`ServiceWorker_Lodging` is the first to return something — the bed it just booked, found by
`ShopStock.ChooseVacantBed`, which generalizes the same room-or-radius traversal `ScanFor`
already used for sellable goods into "everything on this floor," filtered by type instead of by
stock rules. `JobDriver_UseService.CompleteService` is the one place a Shops-layer output
crosses into Lords-layer state: whatever got claimed, if it's a bed, goes straight onto the
guest's own `CustomerRecord.rentedBed`. Nothing else in the mod needs to know a stay is a
two-part transaction.

`CompRentableBed` is the passive comp that remembers the claim — it mirrors `CompBusiness`'s own
`lastShopkeeper` / `lastStaffedTick` pair on purpose: a plain fact for other code to read, never
a job, never a reservation. `JobGiver_SleepInRentedBed` and `JobDriver_SleepInRentedBed` are the
active side: a guest goes to bed once tired, and the driver re-checks every tick that the claim
is still theirs, using the bed's live occupancy (`Building_Bed.CurOccupants`) rather than
vanilla bed ownership — deliberately, so a colonist who simply climbs into the same bed is
caught with no assignment mechanism involved at all. No handshake exists anywhere in this: a bed
that's destroyed, a bed a colonist takes, and a guest harmed in a raid all end the sleep job the
same one way — the claim releases, a reputation cost lands if the stay was cut short, and the
group's own exit trigger notices the claim is gone. A claim that goes stale *before* the guest
ever starts sleeping (the bed destroyed while they're still out shopping) is cleared by
`JobGiver_SleepInRentedBed` itself rather than handed to the driver — creating a job whose very
first toil reads a despawned bed's position is a crash risk, not just a stale-claim one.

A stay is exactly one paid night per transaction. There is no multi-night booking and no
unstaffed nightly billing: a guest who wants another night simply queues and pays again once
awake, through the same purchase-repeat machinery every other service already gets for free.
That also means there is no "can't afford night two" failure mode to design around — it isn't
reachable.

### Settled in town: the day boundary without a second lord state

An overnight guest needs the group's visit to survive past its base duration, which is the one
genuinely load-bearing change: `LordJob_ShopVisit` needed a "settled in town" state distinct
from shopping. It turns out not to need a second `LordToil` — staffing, duties and the harmed
transition are all identical whether anyone's asleep or not, so a second toil would do nothing a
`Trigger` couldn't already decide by itself. Instead, the single existing toil is untouched; only
its exit condition changed. `Trigger_VisitComplete` replaces the flat `Trigger_TicksPassed` and
additionally requires every currently-owned pawn's `CustomerRecord.rentedBed` to be null. For a
group with nobody lodging — still the overwhelming majority of visits — this is bit-for-bit the
same trigger it replaces, since that condition is vacuously true from the first tick. New check-
ins are cut off once the group's base visit duration has elapsed (`PastCheckInCutoff`), and each
sleep job carries its own hard tick cap independent of `Need_Rest` ever reporting rested — between
the two, the trigger is always reachable in finite time. A departed or dead guest's own stale
claim is excluded from the check entirely (records are never removed from the lord, so a pawn
who died holding one would otherwise hold the whole group hostage), and `ServiceWorker_Lodging`
refuses to sell a room to a non-humanlike pawn at all, since nothing would ever make it tired
enough to check out.

This is also the first time a customer group has members doing genuinely different things at
once — one pawn asleep in a bed while another is still haggling over the price of a shirt.
Nothing about that needs the duty think tree to know: `JobGiver_SleepInRentedBed` simply runs
ahead of `JobGiver_BuyFromShop` in the same `OWT_Shop` duty every pawn in the group already
carries, and a pawn who isn't tired, or hasn't rented anywhere, falls through to the existing
logic completely unchanged.

### Town roles: a badge, not a work type

Shopkeeping already answers "who works this counter" for any counter — it's a priority, not an
identity, and any colonist who has it can staff any business. A *role* has to answer a different
question: which colonist, specifically. `CompRolePost` answers it by being a thin subclass of
vanilla's own `CompAssignableToPawn` — the same base class a throne room, a grave or a meditation
spot already build "this pawn, and only this pawn, owns this" on, rather than a bespoke
assignment system invented for this mod. It needs almost no code of its own: reflection against
the real 1.6 assemblies confirms `CompAssignableToPawn` isn't abstract, and only two members are
worth overriding — `AssigningCandidates` (narrowed here to free colonists) and `CanAssignTo`.
The latter isn't optional: `MaxAssignedPawnsCount` is a plain, non-virtual property, set with an
XML field (`<maxAssignedPawnsCount>`) rather than an override because it genuinely can't be
overridden, but the base class only *reads* that count — it never checks it against
`AssignedPawnsForReading` itself, unlike the bed/grave comps this stage's design cites, which
enforce their own capacity by delegating to a pawn-side ownership tracker. `CanAssignTo` rejects
once the post is full, which is the one thing standing between the XML field and a second
"Assign" click quietly making two pawns the sheriff.

Two of the three roles the roadmap once named didn't survive being asked "does this add
something beyond a work priority?" **Barkeep** folds into the existing Shopkeeping loop as a
Social-skill factor on the saloon's own trouble math (see below) rather than a second badge —
there was nothing left for a separate post to do. **Banker** is cut outright: there's no bank yet
to be a banker of. **Sheriff** is the one role that clears the bar, because the roadmap gave it an
actual mechanic — suppressing trouble a saloon generates — and that trouble didn't exist before
this stage either, so it had to be built alongside the badge, not after.

`OWT_Rowdy` is a bespoke hediff, deliberately not vanilla's own `AlcoholHigh` (a system this mod
doesn't own, and can't cleanly reach into to calm someone down) and deliberately not a real
`MentalState_SocialFighting` (whose opponent-selection and harmed-transition timing this mod
doesn't control either — if it resolves near-instantly, the sheriff's whole suppression window
collapses to nothing). A drink service bumps it (`ServiceWorker.RowdinessPerUse`, read by
`TroubleUtility.Notify_ServiceRound` from inside `JobDriver_UseService.CompleteService` — the one
place a shop and its customer are already local variables in scope, so no periodic scan or
tracked reference is needed); vanilla's own `HediffCompProperties_SeverityPerDay` decays it back
down on its own, with no custom `HediffComp` anywhere in this. Crossing the top stage fires a
scripted disturbance — a message, a reputation hit, a per-shop counter, and the offender stops
buying for the rest of their visit — and resets severity to zero in the very same call. That's
also why the top stage is never the sheriff's target: nothing outside `Notify_ServiceRound` can
ever observe it before it's gone. The stage below it ("getting loud") is the real, designed
window, and `TroubleUtility.IsWorthCalming` is what the sheriff's reactive job scans for.

Suppression is two read-only checks, both gated on `TroubleUtility.IsAssignedSheriff` — the
specific badge-holder, never "anyone with a Sheriffing priority" — and neither is a handshake.
Ambient: `JobDriver_Patrol` calls `CompRolePost.NotifyOnDuty` every tick it stands the post, the
same shape `CompBusiness.NotifyStaffedBy` already established, and while any office reads
`OnDuty` the accrual rate is halved map-wide. Reactive: `JobDriver_CalmTrouble` walks up to one
specific rowdy pawn and unilaterally zeroes their severity. The patron's own job never
references a sheriff and has no idea one exists; if the sheriff is drafted, downed, or
reassigned mid-walk, that patron's rowdiness simply keeps accruing or decaying on its own passive
schedule — the same failure shape an unattended counter already has for a customer waiting on a
shopkeeper who wandered off. The disturbance itself never involves a second pawn either: no
fight, no mental break, just a scripted event resolved entirely through the same shared
comp/economy state (`CompBusiness`, `TownEconomy`, `CustomerRecord`) every other transaction in
this mod already reads and writes.

### The Hospitality bridge

Hospitality is not installed anywhere this mod has ever been built, run, or tested. There is no
assembly to reference, no way to decompile one, and no way for `tools/refdump` to confirm a
single Hospitality type or member name — it reads RimWorld's own reference assemblies only.
Every design choice below has to hold up against that constraint, not just against what would be
convenient if the assembly were in hand.

**A hard or optional assembly reference, a compiled stub, an XML patch, and Harmony were all
rejected**, in that order of how tempting they look and how bad an idea each one turns out to be.
A reference (hard or `MayRequire`-gated) needs a second `.csproj`, a `loadFolders.xml` this mod
has never shipped, and a second committed DLL, for a "loads fine without Hospitality" guarantee a
single in-process boolean already gives for free. A compiled stub typed against recalled
Hospitality signatures would *look* like a checked, compiler-verified contract to the next
reader, when it would actually be unverified memory wearing a compiler's coat — strictly more
misleading than a reflection string, which visibly announces itself as a guess. An XML patch has
nothing to patch: this design never needs to change anything Hospitality itself defines. Harmony
is the one this mod has never taken as a dependency at all, and the one thing it would buy —
surgically overriding whatever assigns a guest's duty or next job inside code this mod can't
see — is exactly the invasive move the next two paragraphs already avoid on their own merits.
Taking on this mod's first-ever Harmony dependency to serve a use case the design does without is
a real, permanent cost for nothing.

**Detection is structural, not a guess at Hospitality's namespace or class names.**
`HospitalityInterop.Present` resolves once, by scanning loaded assemblies for one whose simple
name is `"Hospitality"` (case-insensitive) — a guess about Hospitality's own build output, not a
verified fact, and the one thing every downstream check sits behind. Once that assembly is
resolved, recognizing a guest never again involves guessing a type or member name: a pawn is a
Hospitality guest if the `LordJob` governing them, or any `ThingComp` attached to them, belongs
to that same assembly — checked by `System.Type.Assembly` reference equality, not a namespace
string. Either signal alone is enough (they're OR'd), so detection only fails completely if both
guesses are wrong at once — a meaningfully more forgiving bar than leaning on one signal, for
free. If the assembly-name guess itself is wrong, every downstream check is moot:
`HospitalityInterop.Present` is false forever, and the bridge is permanently, silently inert —
indistinguishable from Hospitality not being installed, and no more expensive than that to
carry. See [the code map's known risks](architecture.md#known-risks) for the full confidence
accounting, signal by signal.

**A job is force-handed through `Pawn_JobTracker.TryTakeOrderedJob`, gated on
`Pawn_MindState.IsIdle`, rather than the guest ever being given the `OWT_Shop` duty.** The
roadmap's own original wording — "gives Hospitality guests the `OWT_Shop` duty" — turned out to
be the wrong shape once weighed against how aggressively this mod's *own* `LordToil_Shop`
reasserts that duty onto every pawn it owns, every toil re-entry (`UpdateAllDuties()`): there is
no way to know from here how often Hospitality's own equivalent does the same, and overwriting a
foreign pawn's duty would be exactly the paired, fragile coordination [the one rule](#the-one-decision-everything-else-follows-from)
this whole mod exists to avoid — now against a partner whose code can't even be inspected to know
what breaks. `TryTakeOrderedJob` is different in kind: it's the same generic, vanilla-sanctioned
door a player's own forced order on any pawn already uses, and gating the call on `IsIdle` means
it only ever fires in a window the guest's own AI has already vacated — nothing running to
interrupt, and nothing to resume afterward, because nothing was pre-empted in the first place.
Once the call returns, the bridge's involvement with that pawn is over; Hospitality's own AI
reassesses on its own schedule next, exactly as it must already tolerate after any other
forced order.

**Lodging is categorically excluded, by two checks inside `PickShoppingJob`'s own scoring
loop** — not, as an earlier draft of this section claimed, by an independent structural fact
about `CustomerRecord` that would make either guard redundant on its own. That claim doesn't
survive tracing the actual lodging code: `ServiceWorker_Lodging`'s
`IsAvailable`/`Desirability`/`ApplyEffect`, `ShopStock.ChooseVacantBed` and
`CompRentableBed.Claim` never read `CustomerRecord` at all — a bed is claimed by a customer and a
shop, nothing else, and `CustomerRecord` lives one layer up, on `LordJob_ShopVisit`. Nothing
downstream of the scoring loop ever looks there. So the scoring loop is not a second belt on top
of a guarantee that already held independently of it; it is the *only* thing standing between a
Lord-less pawn and a claimed bed, and it earns that job with two checks, not the single one this
section used to describe. The explicit guard: `PickShoppingJob` (the scoring pass
`JobGiver_BuyFromShop` and the bridge now share — see
[the code map](architecture.md#compat--soft-dependencies)) takes a `lodgingAllowed` parameter,
`true` for the duty-driven native caller and `false` for the bridge, which removes Lodging from
the set of candidates a bridged guest's trip can ever score into. The unconditional guard,
checked regardless of what a caller passes: the same loop also skips Lodging outright whenever
the pawn's own `Lord` isn't running `LordJob_ShopVisit` — the identical condition that already
means `CustomerRecord` resolves to null for them. Skipping this check would be worse than an
ordinary double-booking: `JobGiver_SleepInRentedBed` also requires a resolvable `CustomerRecord`
before it will ever send a pawn to sleep in, and eventually vacate, a claimed bed, so a pawn who
somehow claimed one with no record to hang it on would never check out through this mod's own
systems either — the bed would sit "occupied" until a player found the evict gizmo. Today, with
the bridge as the only second caller, the two guards happen to agree: a bridged guest always has
both `lodgingAllowed: false` *and* `lordJob == null`, for the identical single-`Lord`-per-pawn
reason `IsHospitalityGuest` relies on (see `HospitalityInterop`). The unconditional guard is what
keeps that agreement from depending on some future caller remembering to pass the parameter.

**That same single-`Lord` invariant is what keeps the two mods from ever fighting over one
guest**, in either direction. "Who is housing this pawn" and "is this pawn a Hospitality guest"
are the same underlying question — which `Lord`/`LordJob` owns them — read two ways, not two
independently tracked flags that could drift out of sync with each other. One of this mod's own
customers can never satisfy `IsHospitalityGuest`: its LordJob signal can't match a customer
running `LordJob_ShopVisit`, for the single-`Lord` reason above, and `IsHospitalityGuest` checks
that explicitly, before its second, weaker signal (a matching `ThingComp`) ever runs — that
second signal has no equivalent guarantee on its own (see `HospitalityInterop`). A Hospitality
guest can never hold one of this mod's rented beds, for the mirrored reason — but, as corrected
above, that "mirrored reason" is `PickShoppingJob`'s own unconditional `lordJob == null` guard,
not an independent fact about `CustomerRecord`. Neither mod has to cooperate with the other for
either half to hold; both are pinned to vanilla's own single-`Lord`-per-pawn guarantee, which
neither mod has a reason to break. The one honest limit on this guarantee: it only proves *this
mod's* side never double-books a guest Hospitality already houses. It says nothing about whether
Hospitality's own code might, independently, try to do something with a staffed counter or a
customer of this mod's own — that is outside what a one-directional, read-only-of-Hospitality
bridge can observe or prevent.

**A bridged guest is throttled per-shop rather than given a full `CustomerRecord` of its own.**
`refusedShops` and `causedTrouble` both live on `CustomerRecord`, and a Hospitality-owned pawn
structurally can't have one (see above) — so a naive bridge would keep re-offering the same
chronically unstaffed shop to the same idle guest, once per scan, indefinitely. Rather than
rebuilding that bookkeeping for a pawn that can't carry it, `HospitalityBridge` keeps one small,
deliberately unpersisted `(pawn, shop) → tick` cooldown: once a pair has been dispatched —
bought something, or found nothing — that shop is off the table for that guest for one of the
shop's own `customerPatienceTicks`. It's a blunter instrument than the real thing: a guest who
successfully buys from a good, staffed shop is throttled from immediately buying there again
too, which a native customer never is. Accepted as the honest cost of a targeted fix over a
parallel bookkeeping system that would only imitate, and could drift from, the one it's copying.
`causedTrouble` gets no equivalent stand-in at all — a bridged guest who tips a saloon into a
disturbance can, in principle, be offered another round later in the same stay. Left as-is
deliberately: nothing in the existing scoring loop gates a *native* customer on rowdiness before
they cross into `causedTrouble` either, so a bridge-only gate would make bridged guests behave
more conservatively than native ones for no principled reason — and the natural slow climb back
up (`TroubleUtility` zeroes the hediff the instant a disturbance fires) already keeps a repeat
rare in practice.

**Whether the guest carries any silver at all is deliberately not guessed either way.** Rather
than assume Hospitality guests do or don't already carry spending money, the bridge's silver
top-up reuses `IncidentWorker_ShopCustomers.GivePurse` completely unmodified — the identical
formula and settings scaling a native customer's arrival purse already gets. `GivePurse` only
ever tops up a shortfall, so if guests turn out to already carry plenty, this simply adds
nothing. A settings checkbox (`hospitalityGuestsCarrySilver`) lets a cautious player turn it off
regardless.

**The one place the player can tell any of this actually worked** is a single one-time message,
the first time in a save that the bridge successfully hands a guest a job — the in-fiction
confirmation that the entire unverified detection chain above matched at least once. Everything
else about detection state is available on request rather than announced: an always-visible
settings-window status line (`OWT_HospitalityDetected` / `OWT_HospitalityNotDetected`), and —
this mod's first `[DebugAction]`, and the first of what's now several Dev Mode/telemetry uses of
`Log.Message` in this codebase (see [`DevTools/`](architecture.md#devtools--developer-tooling-and-telemetry))
— a Dev Mode diagnostic that dumps every pawn's detection result, `Lord`/`LordJob` type and full
comp list, for whoever eventually corrects the guesses above against a real Hospitality install.

### Gambling hall: a till that pays out

Every transaction before this one moves silver exactly one way: into a till. A wager is the first
that can send it back out, which meant re-examining an assumption `ShopTransaction` had never had
to question — that money only ever enters — rather than routing around it. The answer is a mirror
primitive next to the existing one: `CompBusiness.TakeFromTill` walks the till's own silver stacks
the same way `ShopTransaction`'s private `TakeSilver` already walks a customer's purse, and
`ShopTransaction.PayOutFromTill` hands the result to the winner the same way `TrySell`/`TryServe`
already hand goods to a buyer. Neither can return more than the till physically holds — the loop
bound *is* the till's own contents — which is what makes "the house can't pay" a reachable, legible
outcome rather than a bug to guard against separately. A shortfall closes the table and costs more
reputation and standing than anything else in the mod, on purpose: reneging on a paid bet is a
sharper trust break than slow service, a walkout, or even a saloon disturbance.

**House edge** is `Markup`'s structural twin — same lazy-init-from-kind-default, same
clamp-to-kind-range setter, same slider gizmo — because it answers the identical question a price
does: how much of this transaction does the house keep. The maths is deliberately simple enough to
state exactly: win chance is `(1 - HouseEdge) / payoutMultiplier`, so a player's expected return
per silver wagered comes out to exactly `-HouseEdge`, for any payout multiple. That's not tuned to
be *approximately* the edge; it falls out of the algebra, which is what makes the slider mean what
its label says.

A wager also has to answer to `Desirability` the same way `Drink` does: `NeedDesirability` scores
it against the customer's own Joy need, floored the same 2.5×→1× way a round of drinks is, so a
bored gambler wants another hand more than a satisfied one does. That scoring only means anything
if playing a hand actually moves Joy, though — `ApplyEffect` grants a flat `joyGainPerHand`
regardless of win, loss or shortfall, the same unconditional shape `ServiceWorker_Ingest` already
uses for nutrition. Without it, a wager would be the one service in the mod whose Desirability
input never responds to the need it's supposedly satisfying, and nothing would ever taper a bored
customer back off the table on its own.

The per-hand algebra above is only half the check a wager needs — the other half is throughput,
since a customer can be dealt back-to-back for as long as the table stays their best option,
unlike a one-shot purchase. A hand takes `serveTicks` (200) once a dealer's working the table, so
one continuously-played customer can run at most 12.5 hands an hour (2,500 ticks to the hour). At
the shipped defaults — 20 silver ante, 15% edge — that's an expected 3 silver a hand, or about
37.5 silver an hour in house profit from that one customer alone, before the till or their own
purse runs dry. No shop counter can match that: a customer's whole purse for the visit starts at
only 120–450 silver (`BasePurse`, see economy.md), spent once across however many shelves they
visit — a shop has no mechanic that lets it sell to the same customer twice for the same item.
That asymmetry is the tempting half by design. It's also the self-defeating half: the same odds
mean more hands lose than win, and a loss is what feeds `OWT_Rowdy` — an unsupervised, unskilled
dealer's table pushes one unlucky customer from calm to a disturbance in five losses, roughly 40
minutes of continuous play, well before that table would drain even an average purse (~340 silver
at the gambling hall's own 1.3 appeal weight, or ~9 hours at 37.5 silver an hour). Anger closes a
greedy table's account long before money does.

Fitting a wager into `ServiceWorker` rather than inventing a new business primitive meant widening
`ApplyEffect` twice: it now receives the price `TryServe` already charged (so a payout is
computed against the same number that was actually taken, not a value recomputed a moment later
and trusted to agree), and it now returns the round's rowdiness instead of `TryServe` reading a
fixed `RowdinessPerUse` — because a wager's rowdiness is outcome-dependent (a win adds none; a
loss, or worse, a shortfall, adds more) in a way no single constant can express. Every worker from
before this stage simply echoes its own `RowdinessPerUse` back through the new parameter, so
nothing about Drink, Meal, Haircut or Lodging's behavior changed.

None of this touches the non-synchronising-loops rule. The dealer is read exactly once, for their
Social skill, by a call that cannot block or fail — if the table is unattended, `TryServe` has
already refused the round before `ApplyEffect` ever runs, so there is no path where a wager's
payout waits on a specific pawn. A shortfall force-closing the table is the same shape a shop
running out of stock already has: a plain flag another pawn's own `FailOn` notices on its own next
tick, never a message sent to anyone.

The building itself is the stage-6 faro table, promoted rather than duplicated: same defName, same
art, a `CompProperties_Business` where a `CompGatherSpot` used to be, and a one-time silver cost
that seeds the till a wager's first customer needs to exist at all — an empty till and a coin-flip
win chance would otherwise make the very first bet ever placed at a fresh table the likeliest one
to be shorted.

### Outlaws and the law: a third visitor to the same till

The roadmap named three things for this expansion — a stickup incident, a wanted board with
bounty quests, and a jail that converts prisoners into silver or reputation. Only the first
shipped. The other two were cut deliberately, for reasons specific to each rather than a single
"keep it small" instinct: a wanted board needs a recurring, *named* outlaw leader, which needs a
kind of state this codebase has never had — an identity that survives across incidents and saves,
unlike `CustomerRecord`, which is deliberately built to die with the visit it belongs to — sitting
on top of RimWorld's quest system, a large, effectively invisible surface from this sandbox
(reference-assembly metadata carries no Def content and no IL) with no graceful failure mode the
way this mod's other guesses have one. A bespoke jail turned out to need nothing built at all: the
moment `LordJob_Stickup.GuiltyOnDowned` returns true, a downed raider is an ordinary hostile-
faction humanlike pawn, already capturable, holdable and ransomable through completely unmodified
vanilla prisoner mechanics. Writing a parallel comp that also converts prisoners to silver on a
schedule would have duplicated a decision space vanilla's own prisoner interface already owns.

The incident itself leans on vanilla raid machinery as hard as it can, rather than re-deriving
faction selection, pawn generation and gear from scratch the way an earlier draft of this feature
considered. `IncidentWorker_Stickup` subclasses `IncidentWorker_RaidEnemy` and touches five hooks
— `CanFireNowSub`, `ResolveRaidPoints`, `ResolveRaidStrategy`, `ResolveRaidArriveMode`, and the
letter pair — leaving `base.TryExecuteWorker` to do everything else, unmodified. Two of those five
overrides turned out to need a different access modifier than a first pass guessed
(`ResolveRaidStrategy` and `ResolveRaidArriveMode` are `public` on `IncidentWorker_Raid`, not
`protected`) — caught immediately by the compiler as a `CS0507`, not silently, which is exactly
why this codebase leans on "does it compile as `override`" as a cheap, real check on an assumption
`refdump` cannot make: reference-assembly metadata reports a member's existence and signature, but
never its accessibility or virtual/override modifiers. What compiling clean still can't confirm —
because reference assemblies carry no IL — is whether `IncidentWorker_Raid`'s own internal call
order actually consults these overrides before generating the raid's pawns and gear, so the values
this file sets land on the same raid rather than a later one. See [known
risks](architecture.md#known-risks) for the honest account of what's confirmed and what's still
inferred.

Sizing the crew off silver actually at risk (`ResolveRaidPoints`, clamped small at both ends)
rather than off colony wealth is the concrete answer to the brief's own worry about turning a
shopkeeper sim into a combat mod: a stickup is small and focused by construction, whatever the
colony is worth. `RaidStrategyWorker_Stickup.MakeLordJob` is the one place a custom `LordJob` is
genuinely needed — a stickup's state machine (rob, flee on being harmed, leave once the clock or
the tills run out) doesn't fit any existing vanilla strategy — and `LordJob_Stickup`/
`LordToil_Stickup` are close enough to `LordJob_ShopVisit`/`LordToil_Shop`'s own shape that the
customer visit's flat-graph reasoning above applies here verbatim, just with a hostile duty in
place of a shopping one.

The sheriff's entire contribution to this mechanic is two passive reads of
`TroubleUtility.AnySheriffOnDuty` — once inside `StickupWatch`'s own clock tick, halving how often
it rolls, and once inside `RaidStrategyWorker_Stickup.MakeLordJob` at raid creation, halving the
raid's duration. Neither is a job, a reference, or a wait; it's the identical mechanism that
already suppresses saloon rowdiness, pointed at a second, unrelated bad outcome. That is a
deliberate answer to the brief's own framing — the sheriff was built to calm drunks, not shoot
outlaws, and a toothless combat job would have been worse than none. Self-defense against a
stickup crew is entirely vanilla's own `JobGiver_AIFightEnemies`, run ahead of `JobGiver_RobTill`
in the crew's duty think tree; this mod contributes zero coordination code between "a raider" and
whoever is shooting at them. `JobDriver_RobTill` deliberately does not implement
`IBusinessPatron` — the one place this feature actively *prevents* a synchronisation the business
layer would otherwise fall into, since without that guard `WorkGiver_ManShop` would dispatch an
unarmed colonist to staff the very counter being robbed.

A robber is a third kind of visitor to a primitive two others already share safely: `TillSilver`,
moved through `CompBusiness.TakeFromTill`. A gambling hall's payout, the player's own Collect
gizmo, and `ShopTransaction.RobTill` can all touch the same till in overlapping windows, and all
three already degrade the same way — `TakeFromTill` re-reads the till fresh on every call, so it
can never be over-drawn or duplicated, only found emptier than a given caller expected. Adding a
robber to that mix needed no new discipline, only `ShopTransaction.RobTill`'s own re-validate-
immediately-before-taking check, mirroring the same rule the rest of this file already lives by.

**A later correction: the clock and the till drifted apart.** `StickupWatch` was built to read
`TillSilver`, and `CollectEarnings` was built to empty a till onto the floor for a hauler to carry
off — each correct in isolation, and wrong together: clicking Collect moved silver from "counted
by the clock" to "not counted", without moving it one step closer to safety. The clock reset to
nothing, and the identical silver sat on the shop floor waiting on hauling priorities. The fix
isn't a mechanic bolted on beside the old one; it's treating "at risk" as a fact about where
silver is exposed rather than about which container currently holds it.
`StickupWatch.TotalSilverAtRisk` reads a till and its shop's own floor as one quantity, deduped
across a shared sales floor the same way `TownEconomy.TakeStock` already dedupes appeal, and
`JobGiver_RobTill` extends its own existing scoring pass to weigh a floor pile against a till in
one loop, rather than gaining a second, competing `JobGiver` — `OWT_StickupDuty` is a
`ThinkNode_Priority`, which takes the first non-null job and never compares scores across
siblings, so a second job giver would let a small, far till always beat a bigger, closer floor
pile purely by which one happened to sit first in the XML. The thing deliberately not built
alongside this fix is any automation of Collect. The gizmo's own description already promised a
hauler finishes the job — "empty the till onto the floor beside the counter, where a hauler can
pick it up" — and the bug was never that the second half of that promise was missing as a
mechanic; vanilla hauling already does exactly that. The bug was that nothing ever checked
whether the second half had actually happened, so the game silently treated the first half as if
it were the whole job. Automating collection now would let one spare hauler quietly buy back, in
code, the exact one-click loophole this pass exists to close. The honest fix teaches the promise
the tooltip already made; it does not stand a new mechanic in front of it.

### Stagecoach line: a ceiling, not a second clock

The roadmap named three things for this expansion — guaranteed high-budget arrivals, outgoing
mail contracts, and an occasional VIP passenger, framed either as a quest-giver or as "a shopper
with a 5× budget." Two of the three shipped; the third is cut outright, and the reasoning is
worth stating because it's the same reasoning that shapes everything else here.

**Mail contracts don't fit this mod's own shape.** Every transaction that already exists is a
stranger walking in and `ShopTransaction` moving silver and goods at the point of exchange — a
pull. A mail contract is a push: the colony commits goods up front, and an abstracted coach pays
out later, with no pawn on either side of it at all. That isn't a smaller version of the existing
seam, it's a different mechanic wearing this mod's name — there is no pawn loop for [the one
rule](#the-one-decision-everything-else-follows-from) to even apply to, and no file this feature
could plausibly extend (`ShopTransaction`, `ShopPricing`) has any shape for money leaving the
colony rather than silver arriving in a till. A flat-silver timer with no parcel, no risk and no
delivery behind it would just be a disguised income tick wearing a mechanic's name; a real one
needs a commit/deliver/pay lifecycle this single-map mod has no scaffolding for anywhere. Cut,
not deferred.

**The quest-giver framing is cut; the cheaper alternative in the same sentence of the roadmap is
not.** The roadmap's own wording already gives an escape hatch — "a quest-giver *or* a shopper
with a 5× budget" — and the second half costs nothing new to build: one pawn in an
already-spawned customer group gets a bigger purse and a name-drop in the letter, through
machinery that already exists. The first half would mean taking on `QuestScriptDef`, `Slate` and
`QuestNode` — a large, XML-and-code-interleaved surface `tools/refdump` can confirm member
*existence* on, but not confirm actually generates a working quest, in a mod that has never run
in a live game (see Known Context in `CLAUDE.md`). Paying that cost for a payoff the roadmap's
own cheaper option already delivers is not a good trade.

**The guarantee is one incident with an extra way to fire, not a second incident with its own
clock.** The obvious shape for "no gap longer than N days" is a second `IncidentDef` alongside
the existing `OWT_ShopCustomers` — its own worker, its own `minRefireDays`, fired on its own
schedule. That shape was rejected: a second incident's cooldown only ever throttles itself, so
nothing structurally stops it landing close to an organic arrival — exactly the stacking risk
this expansion has to answer for. Instead, `TownEconomy.GuaranteedArrivalDue` is a plain `bool`,
OR'd into the *existing* early-return inside `TryAttractCustomers`:

```
if (!Rand.MTBEventOccurs(mtbDays, 60000f, ArrivalCheckInterval) && !GuaranteedArrivalDue) return;
```

Because this is an OR added to an early-return that was already there, it can only ever **add** a
firing attempt where the organic roll would otherwise have stayed quiet past the active tier's
own ceiling — never suppress one, never duplicate one, never add a second, independent roll. And
because both the organic roll and the guarantee fire through the identical `OWT_ShopCustomers`
incident, the shipped `minRefireDays` (0.6 days) stays a hard structural cap on the *combined*
rate, not merely an expected-value argument that happens to hold on average. That cap already
covered two origins of the same incident before this feature existed — the deliberate
`TryAttractCustomers` call and the incident's own small ambient `baseChance` storyteller roll
(see [the economy loop](#the-economy-loop) below). The guarantee is a third origin funnelled
through the identical door, not a new kind of risk.

The ceiling itself lives in data, not in a constant on `TownEconomy`: `CoachTierDef` is one rung
of the route ladder — `minAppeal`, `arrivalCeilingDays`, `purseMultiplier`, `vipChance` — the same
"a business or a service is a stanza, not a class" instinct behind `ShopKindDef` and `ServiceDef`,
applied one level up. `TownEconomy.RouteTier` reads the active tier live off current appeal on
every call, uncached and non-ratcheting, exactly the way `Appeal` itself is recomputed from
current stock rather than tracked — so a route can regress the same way appeal can, a legible,
named consequence (a demotion message) on top of the arrival clock's previously invisible
slowdown. `CoachTierUtility.CurrentTier`/`NextTier` are the only two places that ranking logic
lives; the depot's own inspect string and the tier-announcement check both go through them, so
neither can drift out of sync with the other about which tier is active.

**The math was checked, not assumed.** Modelling organic arrivals as memoryless with mean gap
`M = mtbDays` — the same approximation `Rand.MTBEventOccurs` itself already leans on — imposing a
hard ceiling `C` and resetting the clock on every arrival makes each gap `X' = min(X, C)`, and for
an exponential `X`: `E[X'] = M · (1 − e^(−C/M))`, giving a new rate of `1 / E[X']`. Worked out
against the shipped MTB curve and the three tiers' own numbers, the uplift peaks at a tier's own
entry point — biggest right at the weekly-coach tier's threshold, around +30%, the single largest
engineered number anywhere in this feature — and decays toward single digits by that tier's own
ceiling, never coming close to doubling footfall at any appeal tested, including the top tier's
own long-run plateau. That is the number [the town economy](economy.md#a-ceiling-not-a-second-clock)
states in plain, rounded terms for a player; this is where it came from. Worth confirming against
real inter-arrival gaps in play — the model is an approximation of `Rand.MTBEventOccurs`'s real
behaviour, not a proof of it, the same caveat the *existing*, shipped MTB clock already carries.

**The depot is a marker, not a business, and needs no registry.** `CompCoachDepot` overrides
exactly one member, `CompInspectStringExtra`, and persists nothing — every number it shows is read
live off `TownEconomy` and `CoachTierUtility` on the tick it's asked. Whether a depot exists at
all is answered the same way `TroubleUtility.AnySheriffOnDuty` already answers "is there a
sheriff on duty": a stateless `map.listerThings.ThingsOfDef(...).Any()` scan, read at most twice
per arrival check, with no `Register`/`Deregister` pair and nothing to rebuild in `FinalizeInit`.
A `CompProperties_Business` was never on the table for this building — a depot sells nothing, is
never staffed, and never enters `TownEconomy.Appeal`'s own math, the same "not a business" shape
the sheriff's office already established for a building whose only job is to change what the
player sees and how the town's own systems behave around it.

**Nothing here adds a second pawn loop.** The entire mechanism resolves before any pawn job
exists: `TryAttractCustomers` decides whether to fire, `TryExecuteWorker` decides what the group
looks like — size from the unmodified `ResolveParmsPoints`, purse from a widened `GivePurse`, one
pawn possibly flagged VIP for that call only. Once `LordMaker.MakeNewLord` runs, a scheduled or
VIP customer is spawned into the identical, unmodified `LordJob_ShopVisit` →
`JobGiver_BuyFromShop` loop every other customer already uses. `CustomerRecord` gains no
VIP-shaped field, and nothing downstream — `ShopTransaction`, `TroubleUtility`, the standing
ledger — is touched at all. [The one rule](#the-one-decision-everything-else-follows-from) isn't
just preserved here; it's structurally inapplicable, because this feature never creates a second
loop for it to govern.

### Gold rush: one condition, not two clocks

The roadmap named a *strike nearby* event with a boom that triples arrivals and a bust that
crashes them, a demand basket that makes stocking decisions matter again, and gouging that costs
more than it usually would. All of it shipped; nothing here was cut for scope the way a mail
contract or a wanted board were elsewhere in this file. The interesting design choices are how
the boom and bust share one clock, how the demand basket stays a pure multiplier nothing has to
special-case, and how a bust that must not become a trap is actually guaranteed not to be one.

**A `GameCondition`, not a hand-rolled `MapComponent` timer.** RimWorld already has the vanilla
idiom for "the whole map is in a temporary state for a while" — `GameCondition`, created by
`GameConditionMaker.MakeCondition` and registered with `Map.gameConditionManager`, ticked by the
engine, shown in the conditions bar with its own live `Description`, and torn down automatically
once its `Duration` elapses. Every one of those members, plus `IncidentWorker_MakeGameCondition`
(the base class that fires one) and the `Expired`/`TicksPassed`/`SingleMap` properties this
feature reads, is confirmed to exist and match this feature's call shapes via `tools/refdump` —
see [the code map's known risks](architecture.md#known-risks) for exactly what that check does
and doesn't prove. A bespoke `MapComponent` timer would have reinvented all of that by hand for
no benefit: no conditions-bar entry, no automatic teardown, and a second, parallel place a save
has to persist a start tick and a duration that `GameCondition` already owns.

**One condition, self-phasing, rather than two chained conditions or a second incident with its
own clock.** The obvious shape for "a boom, then a bust" is two `GameConditionDef`s, the first
handing off to the second when it ends — the same shape [the stagecoach
line](#stagecoach-line-a-ceiling-not-a-second-clock) rejected for its own guarantee, and rejected
here for the identical reason: a second condition's own lifecycle only ever answers to itself,
so nothing stops it drifting out of step with the first, or with the incident that was supposed
to own the whole event. `GameCondition_GoldRush` is one instance for the event's entire life —
`bustStarted` is the only state that distinguishes its two phases, read by `GoldRushUtility` and
by its own `Description` override, never a second `Def` or a second registration. The firing
`IncidentDef`, `OWT_GoldRushStrike`, only ever runs once, at the very start, the same "one
incident, one letter, done" shape `IncidentWorker_Stickup` and `IncidentWorker_ShopCustomers`
both already have; everything after that is the condition ticking itself, not a second incident
waking up on its own schedule.

**The demand basket is a plain `float` multiplier, gated on the boom alone, wired into every
place something is already scored.** `GoldRushUtility.DemandFactor(map, thing)` is `1f` — a
provable no-op — whenever no rush's boom is active, or the `Thing` is `null` (a stock-free
service, which this mechanic deliberately never touches: prospectors want a full pack mule, not
a haircut). Otherwise it's `InBasketDemandFactor` (4) for tools, medicine, a meal or a drink, and
`OutOfBasketDemandFactor` (0.4) for anything else — a 10× spread, chosen because that is also
exactly the spread `TownEconomy`'s own gouging penalty below has to outweigh (see the next
section). "Tools" has no confirmed literal category to check against from this sandbox, so
`InDemandBasket` reads it loosely, the same way the general store's own flavor text already
does, as `ThingCategoryDefOf.Manufactured` — a guess in the same spirit as `OWT_BatwingDoor`'s
`ParentName="Door"` assumption elsewhere in this codebase, not a verified fact.

Being a pure multiplier is what let this factor go everywhere a purchase or a service is already
scored without any of those call sites needing to know a rush exists: `ShopStock.ChoosePurchase`
and `ShopStock.ChooseService` fold it into the per-item score that decides what a customer picks
up *inside* a shop, and `JobGiver_BuyFromShop`'s own scoring pass folds it into the score that
decides *which shop* a customer walks into in the first place, multiplying the specific item (or
service's consumable) that shop would sell them. Both matter: a demand basket that only steered
item choice inside a shop a customer had already entered for other reasons wouldn't actually
reward stocking for the rush the way the roadmap's own framing promises — a shop selling nothing
prospectors want would still pull exactly as much foot traffic as one that does, and only lose
out once someone was already standing in it. Folding the same factor into the shop-choice score
too is what makes a general store that restocked for the rush genuinely outperform a saloon that
didn't, not merely sell differently to whoever happened to wander in regardless.

**Gouging is relative to a shop's own kind, not a flat number.** `ShopPricing.GougeSeverity`
reads 0 at a shop's own kind's default markup and 1 at that kind's own configured ceiling — the
same `Markup`/`markupRange` pair the price slider already clamps against, reused rather than a
second, parallel "is this too expensive" threshold. That is a deliberate choice: a flat gouging
threshold would either let an already-dear kind (a saloon, priced to sting a little on purpose)
gouge for free right up to some arbitrary number, or punish a naturally cheap kind (a general
store) for charging what a saloon charges every day without anyone calling it gouging. Relative
severity means gouging is always and only "further above what's normal for *this* business than
the player usually pushes it" — a judgment that holds regardless of which kind is doing it.

`TownEconomy.RecordSale` applies the penalty — extra reputation and standing cost, scaled by
severity — only while a boom is active, for a direct, load-bearing reason: the demand basket's
10× spread structurally overpowers `ShopPricing.ValueAppeal`'s own price sensitivity (a roughly
2× spread across a kind's normal markup range), so without an explicit extra brake, a rush would
make price stop mattering to where it's sold at all. The penalty is that brake, sized to roughly
match the spread it has to outweigh. It also answers the roadmap's own two-sided ask directly:
gouging has to actually earn well in the short run (it does — the demand basket alone already
guarantees that shop the traffic) and the cost has to be legible (a per-shop warning message, at
most once a day, naming the counter, backed by the reputation and standing hit the town ledger
already shows).

**The bust must not be a trap, and the numbers were checked, not assumed.** Because gouging is
gated on the boom (`GoldRushUtility.BoomActive`), it structurally cannot apply during the bust —
whatever damage a boom's gouging did is the most the bust ever has to recover from, and nothing
during the bust itself can add to it. `TownEconomy.RollOverDay`'s existing daily decay toward
0.5 reputation (5% of the remaining gap, already shipped for every other feature) then works
*for* recovery unconditionally once the bust starts, since `BustRecoveryReputation` (0.45) sits
just under that same resting point. Worked out in days: even from a full crash to 0 reputation,
`0.5 - 0.5 × 0.95ⁿ ≥ 0.45` first holds at `n ≈ 45` days — comfortably inside the ~55–65 days of
bust the firing incident's own 70–80-day total duration cap allows before forcing the condition
closed regardless, and that is the worst case: any ordinary staffed trade during the bust adds
its own +0.01-per-sale on top of the passive decay, and a town that never gouged hard enough to
push reputation below 0.45 in the first place clears the bar the very first tick-check after the
bust begins, since there's nothing to recover from. The hard duration cap exists purely as a
safety net for a scenario the math above says shouldn't occur in practice, never the intended
exit — see `Defs/IncidentDefs/Incidents_GoldRush.xml`'s own comment.

That hard cap forces `End()` through vanilla's own automatic expiry, bypassing
`GameConditionTick` entirely — the same "reached from two places, only one of them earns the
letter" shape this section's own bug fix exists to get right: `recoveredByReputation` is set only
by `GameConditionTick`'s own reputation check, immediately before it calls `End()` itself, and
`End()`'s "recovered" letter is gated on that flag rather than on `bustStarted` alone, which is
true on both paths. The timeout path gets no letter — an honest silence rather than a claim that
reputation recovered when the real reason the rush ended was the safety net running out.

**The arrival and purse multipliers ride on top of the existing clocks, never inside the
stagecoach guarantee's own ceiling.** `GoldRushUtility.ArrivalMtbMultiplier` multiplies straight
into `TryAttractCustomers`'s own `mtbDays` — the same number the stagecoach guarantee's
`CeilingTicks` is a ceiling *on top of*, not a value the ceiling itself is computed from. A rush
speeding up or slowing down the organic roll changes how often the ceiling has anything to do
(rarely, during a boom that's already faster than most tiers' ceilings; more often, during a
bust slow enough that a depot's own promise becomes the practical floor under footfall) without
the two ever multiplying against each other — exactly the "compose, don't multiply" shape the
roadmap asked for. `GoldRushUtility.PurseMultiplier` is threaded into `GivePurse` itself rather
than as a third caller-supplied parameter, so it stacks with a stagecoach tier's own purse boost
or the flat VIP multiplier automatically, and reaches the Hospitality bridge's own reuse of
`GivePurse` for free — a prospector's purse is a prospector's purse, whichever door they came in.

**Brawls and claim disputes, the roadmap's own other headline, are folded into the trouble
mechanic that already exists rather than built as a second one.** A rush intensifying trade at a
saloon or a gambling hall already makes both busier, and both already have their own escalating
trouble mechanic — `OWT_Rowdy`, the disturbance it fires, the sheriff's suppression — built for
exactly this "a business getting busier makes it rowdier" shape. A parallel "claim dispute"
mechanic would need its own building or service to attach to (there is no claims office, and
inventing one only for this event contradicts "prefer XML to code" by adding a whole new
business kind to make one event's flavor text literal) and would duplicate machinery that already
does the job it would be built to do. Deliberately cut, the same way stage 4 named barkeep and
banker as cut rather than silently dropped, and for the same kind of reason: there was nothing
left for a second system to do that the first one doesn't already cover.
### Rival towns: an opponent, not a second town

The roadmap named five things for this expansion — relative appeal, price undercutting, staff
poaching, saboteurs, and a ghost town you can eventually salvage. Two shipped: relative appeal
(`RegionalShare`) as a bounded slowdown on the arrival clock, and undercutting, the one mechanic
that gives a rival something to *do* rather than being a static number. The other three are cut,
each for its own reason, below.

**A rival is abstract world-state — never a `Faction`, a `Settlement`, a world-tile
`WorldObject`, or a single pawn.** The same reasoning that cut mail contracts from the stagecoach
expansion applies here with even more force: every mechanic this mod has ever shipped either is a
pawn on this map, or a number a pawn on this map reads. A rival town with a real world-tile
presence would need a faction, a settlement, and eventually caravan-arrival and loot machinery
this mod has never touched, to answer a question — "does relative appeal change the arrival
clock?" — that a plain float on a `WorldComponent` already answers completely. `RivalTown` is not
a place the player can point a caravan at; it is a number that grows, and occasionally undercuts.

**Relative appeal is the whole mechanism, and it lives inside `TownEconomy`, not a second file.**
`TownEconomy.PriceIndex` is the unweighted mean of `ShopPricing.ValueAppeal` across every open,
stocked shop on the map — the identical score `JobGiver_BuyFromShop` already computes per shop to
let a customer pick between yours, now averaged into one town-wide number. That reuse is what
makes price-sensitivity free rather than a second pricing model to maintain: nothing about a
rival's own competitiveness, or the player's own, is invented for this feature — both read the
same `ValueAppeal` convention (`>1` means "pricing under market rate") that has existed since the
very first stage. `MarketPull` (`Appeal × PriceIndex`) is the player's own score in those units;
`RivalTown.Pull` (`currentAppeal × PriceIndex`, its own `PriceIndex` a flat 1.0 except while
undercutting) is a rival's. `RegionalShare` is `MarketPull / (MarketPull + CompetingPull)`,
clamped to exactly `1f` — "as good as no competition exists" — whenever either side of that ratio
is non-positive: rivals disabled, no rival has grown past zero yet, or the town itself has no
appeal yet. That single guard clause is what keeps a brand-new colony untouched (see below) and
what makes the number safe to show directly in the UI with no separate "is this meaningful yet"
check anywhere else.

**Price-sensitivity is load-bearing, not decorative — it is the concrete answer to the brief's
own "makes pricing genuinely competitive rather than solitaire."** A version of this feature that
compared only *appeal* (kind score × stock, ignoring markup) would be a second, independent
solitaire game running next to the existing one: a player could out-appeal a rival by building
more shops without ever having to think about price relative to anyone. Folding
`ShopPricing.ValueAppeal` into both sides of the comparison is what makes undercutting *this
mod's own shops* — not just building more of them — the lever that actually moves
`RegionalShare`.

**The arrival-clock slowdown is a structurally proven bound, not a tuned one.**
`TryAttractCustomers` multiplies its existing `mtbDays` by `Mathf.Lerp(1f, MaxRegionalSlowdown,
1f - RegionalShare)`, where `MaxRegionalSlowdown = 1.6f`. `Mathf.Lerp` clamps its own interpolant
to `[0, 1]` regardless of the magnitude of its inputs, so this multiplier is bounded to `[1.0×,
1.6×]` for *any* `RegionalShare` a rival configuration could ever produce — not a tuning promise
checked against the shipped defaults, a consequence of the function itself. Combined with the
untouched `Appeal < MinAppealForCustomers` early-return that still runs first, a brand-new colony
is byte-for-byte unaffected by this feature regardless of how many rivals exist or how the
player's own `rivalStrength` setting is dialed. The multiplier is also one-directional — it can
only ever stretch `mtbDays`, never shrink it — so no rival configuration, including a broken or
absent `WorldComponent`, can make arrivals *faster* than today's baseline either. And the
[stagecoach line](#stagecoach-line-a-ceiling-not-a-second-clock)'s own guarantee is completely
immune to all of this: `GuaranteedArrivalDue`'s ceiling check runs against
`TicksSinceLastArrival` and a tier's own `arrivalCeilingDays`, neither of which this feature
touches, so a coach depot remains exactly the slowdown-proof floor it always was.

**Undercutting is a discrete, MTB-rolled event, deliberately not a continuous drift.** The
alternative — a rival's price competitiveness randomly walking up and down a little every day —
was rejected for the reason a wandering, invisible number is rejected everywhere else in this
mod: it fails "legible... whether they are winning" outright. A player can't point at a smooth
random walk and say what changed, or when. `RivalTown.Undercutting` is instead a hard on/off
state with a start message and an end message, sized by `RivalTownDef.undercutMTBDays` and
`undercutDurationDays` — a named, dated event a player can actually reason about, the same "a
number becoming a milestone" shift the stagecoach route tiers already made for the arrival clock
itself.

**One shared `RivalTowns`, not one per colony.** Rivals are regional, not personal — the same two
NPC towns compete against every player colony that happens to be loaded, which is also the only
sane answer to "what happens when the player settles a second colony": both colonies read the
identical rival roster, because a rival town has no reason to know or care how many places the
player happens to be trading from. What *does* differ per colony is whether, and when, that
colony's own town has taken the regional lead — and that tracking (`TownEconomy.lastRegionLead`,
`regionLeadKnown`) deliberately lives on the per-map `TownEconomy`, not on the shared
`RivalTowns`. A shared boolean there would let two simultaneously-loaded colonies stomp each
other's lead state and fire spurious "you've fallen behind" messages for a change that happened
on a map the player wasn't even looking at. Keeping it per-map mirrors `lastAnnouncedTier`'s own
placement, for the identical reason: two colonies can each have their own opinion about their own
route tier, and now their own opinion about their own regional standing, without either one able
to corrupt the other's.

**Four things are cut, each for its own reason.** **Staff poaching** needs per-pawn shopkeeping
performance — this codebase has only ever recorded sales per-business, never per-colonist — the
identical missing-state reason this file already used to cut the wanted board from outlaws and
the law, above. A per-pawn sales or skill record would need to exist first, for its own reasons,
before a rival's job-offer event could target a specific colonist meaningfully. **Saboteurs** need
a hostile pawn group on the player's own map — a lord graph, a duty think tree, job drivers — the
same category of new surface the stickup crew needed, now for a second, independent raid-adjacent
mechanic layered on top of an already-ambitious world-map feature; out of scope for the smallest
set of changes that makes competition real. **Literal ghost-town salvage** needs a real
`Settlement` or world-tile site, plus loot and caravan-arrival machinery this mod has never
touched — the identical unproven vanilla surface this codebase has consistently declined to take
on for comparable payoff (mail contracts, the quest-giver VIP passenger). If salvage is ever
wanted, the decision to keep rivals as pure abstract state — never a `Settlement` — is the thing
to revisit first; salvage needs a real world-tile presence to salvage. And, beyond what either
source design considered cutting, **rival decline or concession** is cut too: `RegionalShare`'s
own ceiling already delivers "out-compete a rival" as a genuine, player-caused state — the
multiplier bottoms out at exactly `1.0×`, zero rival penalty, the moment a town's own pull is at
least as large as every rival's combined — without needing a third mechanic, a reactive "observed
player appeal" tracked the other way, or a letter that could flip-flop against a rival whose own
undercutting keeps it near parity. The brief asks for "one or two" mechanics beyond relative
appeal, not three. A future pass could let sustained dominance bias a rival's own `growthPerDay`
toward zero or negative — cheap to add on top of `RivalTown.currentAppeal` once it's actually
wanted.

**Nothing here adds a second pawn loop, and the rule is not just preserved but structurally
inapplicable.** `Rivals/` creates no `Pawn`, no `Job`, no `JobDriver`, no `Lord`, no `Duty`.
`IncidentWorker_ShopCustomers.cs`, `LordJob_ShopVisit.cs`, and every file under `AI/` are
untouched. The one place player-visible behavior changes is a single multiplier inside
`TownEconomy.TryAttractCustomers`, the exact chokepoint both existing pawn loops already treat as
shared, read-only truth. `Shops/` gains one new, one-directional dependency on `Rivals/`
(`TownEconomy` and `CompBusiness` read `RivalTowns`/`RivalTown`); `Rivals/` never references
`Shops/`, `AI/`, or `Incidents/` at all. It does read one setting directly:
`RivalTowns.WorldComponentTick` checks the rival towns master switch and freezes every rival's
growth and undercut rolling while it's off, rather than letting disabled days silently pile up
into a jump when the player re-enables it — the day counter itself still advances either way, so
there is never a debt to catch up on. The *magnitude* of a rival's effect stays decided in
exactly one place regardless: `TownEconomy.CompetingPull` is the only site that reads
rivalStrength, so the world state's own meaning stays independent of any one map's settings, or
even existence — only whether it's currently ticking at all is settings-dependent.

### Dev tools: debug levers and telemetry, without a new pawn loop

Twelve shipped systems and zero minutes of runtime is the problem `DevTools/` exists to fix — not
by building a faster way to play, but by giving Dev Mode direct access to the same levers a real
session pulls, so a short first-play run can reach a stickup, a gold rush and its bust, a
route-tier promotion and a rival undercut in minutes rather than days.

Every lever in `DebugActions.cs` is one of exactly two shapes, and there is no third:

- **It fires the real incident**, through `Storyteller.TryFire` — the identical call
  `TownEconomy.TryAttractCustomers` and `StickupWatch.MapComponentTick` already make. A
  debug-spawned customer group is a completely ordinary `LordJob_ShopVisit`; nothing downstream
  (the counter's line, `WorkGiver_ManShop`, the waiting-customers alert) needs a special case for
  "this one was debug-triggered," because there isn't one to need.
- **It writes directly to state a real transaction already writes to** — a till
  (`CompBusiness.AddToTill`), a pawn's inventory (`IncidentWorker_ShopCustomers.GivePurse`), an
  economy field (`TownEconomy`'s `reputation`, `lastArrivalTick`) — the same shared board both
  pawn loops already read and write independently, never a new one.

The one lever shape deliberately not built: anything that force-assigns a job to a customer or
colonist pawn directly. A hastily-built debug job could read state a real dispatch path would
have populated first, but a shortcut didn't — exactly [the stranded-job hazard](#the-one-decision-everything-else-follows-from)
this mod's one rule exists to prevent. Firing the real incident, or writing to the real shared
state, sidesteps that risk entirely by never introducing a new pawn loop for it to apply to.

Firing an incident this way inherits that incident's own `minRefireDays`: a forced lever can
legitimately do nothing shortly after the same incident already fired, organically or from
another debug call, and every lever reports `Storyteller.TryFire`'s own boolean honestly rather
than assuming success. That is a cooldown behaving correctly, not a bug in the lever — bypassing
it would mean bypassing the exact throttle a real session runs under, which defeats the purpose of
reusing the production incident in the first place.

`Telemetry.cs` is the passive half: three log lines — one per customer arrival, one per nightly
settlement, one per stickup roll whether it fired or not — gated on a single opt-in setting, each
one early-returning before doing any work while it's off. It exists because this wiki's own known
risks keep asking, in so many words, for real inter-arrival gaps to be confirmed in play; now
there is a way to actually log them, rather than reasoning about `Rand.MTBEventOccurs` from first
principles a second time.

Both files log through `Log.Message`, never `Messages.Message` — the same choice
`HospitalityInterop.LogDetectionState` made first: this is instrumentation for whoever is reading
the player log, not an in-fiction event for whoever is playing the game. `DevTools/` is what turns
that from a one-off exception into an established pattern.

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

Moved to the wiki: **[Roadmap](roadmap.md)** — the staged plan, and the larger thematic
expansions that build on top of it.

## Known risks

Moved to the wiki, where they sit beside the code they apply to:
**[Code map → Known risks](architecture.md#known-risks)**.
