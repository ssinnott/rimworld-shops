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
                                                                       └─ ShopTransaction.TrySell /
                                                                          TryServe
                                                                              ↓
                                                                    till, ledger, hediff/thought
```

Neither driver can strand the other. A shopkeeper who wanders off just flips `Staffed` to
false; the customer's wait toil notices, runs down its patience, drops whatever they're
carrying (if anything) and leaves annoyed. That failure is *legible to the player* — it's a
message and a reputation hit — which turns a robustness measure into a game mechanic.

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

Per-customer state (budget spent, purchases, shops they've given up on) hangs off
`LordJob_ShopVisit` rather than the pawn. That means it saves and loads with the group, needs
no def patching of humanlike pawns, and disappears when the visit does.

### Why the sales floor is a room

A shop is defined by walls you already built, not by a zone you have to paint. It reads
naturally ("this room is the store"), it costs nothing to set up, and it makes the room-quality
stats you already care about matter commercially. Outdoors it falls back to a radius so market
stalls still work.

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
stock-free service (Haircut) skips straight to waiting. A small marker interface,
`IBusinessPatron`, lets `CompBusiness` recognize "a pawn is patronizing something" for
queue-spacing and the waiting-customers alert without the Shops layer ever depending on the AI
namespace.

A service business counts toward town appeal the same way a stocked one does
(`CompBusiness.HasAnythingToOffer`, `AvailableServices`), with one wrinkle: a stock-consuming
service's value is already counted once, as stock — `ServiceValue` only adds the services with
no `Thing` behind them at all, so a saloon's beer isn't counted twice for being sellable two
ways.

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
this mod's first `[DebugAction]`, kept to exactly that one exception to its otherwise
zero-`Log.Message` history — a Dev Mode diagnostic that dumps every pawn's detection result,
`Lord`/`LordJob` type and full comp list, for whoever eventually corrects the guesses above
against a real Hospitality install.

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

## The economy loop

```
     player stocks + prices + staffs shops
                     │
                     ▼
         TownEconomy.Appeal  ◄──── reputation ◄──── served vs. walked-out customers
                     │                                        ▲
                     ▼                                        │
   IncidentWorker_ShopCustomers: how often, how many, how rich│
                     │                                        │
                     ▼                                        │
        customers arrive ──── buy goods or use a service ─────┘
                     │
                     ▼
                silver in the till
```

Appeal deliberately rewards **breadth over depth**: a second shop of a kind you already run is
worth 35% of the first. One giant general store should not out-earn a street with a store, a
saloon and a hotel. That's the pressure that turns a colony into a town.

Arrival frequency is the town's own doing, not the storyteller's. `TownEconomy` runs an MTB
clock that shortens as appeal grows (roughly one group every 3.5 days at the 0.5 threshold,
most days at high appeal) and fires the incident through the storyteller, so `minRefireDays`
still caps the rate. The `IncidentDef` keeps a small `baseChance` as a background trickle.

## Roadmap

Moved to the wiki: **[Roadmap](roadmap.md)** — the staged plan, and the larger thematic
expansions that build on top of it.

## Known risks

Moved to the wiki, where they sit beside the code they apply to:
**[Code map → Known risks](architecture.md#known-risks)**.
