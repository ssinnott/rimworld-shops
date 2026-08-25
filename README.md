# Old West Town

A RimWorld 1.6 mod that lets your colony run actual businesses: shop counters with stock,
prices and a till, colonists working behind them, and travellers who show up with silver and
spend it.

Hospitality gets guests onto your map. This is about what they do once they're there.

**Status: the whole staged plan is built.** A customer arrives, then either picks something off
your shelves, orders a drink, a meal or a haircut, or checks into a hotel room for the night —
queues, and pays either way. A guest who rents a room stays until they've slept it off, so the
whole visit can run past a single day. Leave a saloon unwatched long enough and it starts real
trouble; assign a sheriff to keep the peace. Factions you treat well become regulars and come
back more often. There's boardwalk to lay and a false front to nail over your store while you
wait for them. If you also run Hospitality, an idle guest it's already housing can wander over
and shop too — narrowly, and honestly built against a mod this codebase has never had installed
to test with; see [Hospitality guests](https://ssinnott.github.io/rimworld-shops/customers.html#hospitality-guests).
See [docs/DESIGN.md](docs/DESIGN.md) for the architecture and
[the roadmap](https://ssinnott.github.io/rimworld-shops/roadmap.html) for what's left.

📖 **[Read the wiki](https://ssinnott.github.io/rimworld-shops/)** — every building, business,
service and system in the mod, plus the [code map](https://ssinnott.github.io/rimworld-shops/architecture.html),
the [design notes](https://ssinnott.github.io/rimworld-shops/DESIGN.html), the
[roadmap](https://ssinnott.github.io/rimworld-shops/roadmap.html) and the
[changelog](https://ssinnott.github.io/rimworld-shops/changelog.html). Its source is `docs/`.

This mod is standalone. It does not require Hospitality, and is written to sit alongside it.

## What works right now

- **Shop counters and service posts** (`shop counter`, `saloon bar`, `barber chair`, `hotel
  desk` and `hotel bed`) under a new *Commerce* build category, unlocked by the *Frontier
  commerce* research.
- **A sales floor is a room.** Anything sellable in the same room as the counter is on
  display. Outdoors, it falls back to a radius, so a market stall on the boardwalk trades too.
- **Per-shop stock control** via a Stock tab that reuses vanilla's storage filter widget.
- **Player-set prices.** A markup slider from 50% to 300%+ of market value. Undercutting a
  rival shop genuinely pulls customers away from it. Services are priced the same way.
- **Services, not just goods.** The saloon bar also serves a **drink** or a **meal**,
  consuming a matching item already on its own shelves; the **barber shop** sells a
  **haircut**, which needs no stock at all — just a colonist's time. Each produces a real
  effect: a drink or meal is actually eaten (mood, nutrition, the usual vanilla outcomes), and
  a haircut leaves a customer with a fresh mood thought and a visibly different hairstyle.
- **A Shopkeeping work type.** Colonists stand the counter and serve, goods or services alike;
  serving trains Social.
- **Real transactions.** Silver moves out of the customer's inventory and into the counter's
  till. Collect the takings with a gizmo. Deconstructing a counter drops its till rather than
  voiding it.
- **Customers who react to how you run the place.** An unattended counter makes shoppers wait,
  then walk out — leaving any goods by the counter — which costs you town reputation. An alert
  fires while customers are still queueing, so you can staff up before they give up.
- **A town economy that compounds.** Appeal is computed from how many *distinct* stocked or
  serviced businesses you run, what's on display or on offer, and your reputation. Appeal
  directly drives how often customers arrive — the town runs its own arrival clock, which
  shortens as appeal grows — and how much silver they bring, so investment pays forward.
- **A town ledger.** Every counter has a *Town ledger* gizmo showing appeal, reputation,
  today's sales and walkouts, and each shop's takings — the numbers the economy runs on,
  readable in one place. Counters also show appeal, reputation and their services on offer in
  their inspect pane.
- **Lodging.** A **hotel desk** checks a guest into any vacant **hotel bed** on its own sales
  floor; the guest pays for the night up front, then actually sleeps until rested — the visit
  runs past its usual length for as long as anyone's still checked in. A bed shows who's in it
  and its own *Evict guest* gizmo; hotels show their room occupancy alongside every other
  counter's stock and services. A guest whose bed is taken out from under them (deconstructed,
  or claimed by a colonist) is evicted outright — no refund, and word gets around.
- **A saloon that can turn ugly, and a sheriff to keep the peace.** Every round of drinks a
  saloon serves makes that patron a little rowdier; leave it unwatched long enough and it boils
  over into a real disturbance — a reputation hit, a message, and that patron is done spending
  for the rest of their visit. Build a **sheriff's office** and assign a colonist to it, the same
  way you'd assign an owner to a throne or a grave, to push back: on duty, they slow down how
  fast the whole town's patrons get rowdy, and can walk over and calm a specific patron down
  before it's too late. A skilled shopkeeper behind the bar helps too — an unstaffed saloon gets
  no such discount, same as everywhere else in this mod.
- **A main street that looks like one.** Boardwalk underfoot, false-front facades, a hitching
  post, batwing doors and a gallows — all under the same Commerce category and Frontier commerce
  research. A false front is the one with teeth: it gives a shop a small, capped edge in the
  customer AI's own scoring, so a dressed-up storefront pulls trade from an undecorated rival at
  a similar price.
- **A gambling hall, and the first transaction you can win.** A **faro table** deals a hand for a
  price like anything else, but the payout is a roll of the dice — a win doubles the stake back
  out of the same till every sale fills, a loss makes that gambler a little rowdier (occasionally
  with a Social-skill-gated cheating accusation thrown in), and the table's own **house edge**
  slider, right next to the price one, sets exactly how much of every wager the house keeps on
  average. Set it fair and a table stays busy all evening; set it greedy and it pays out richer
  hands but burns through patience — and its own till — faster. A table that can't cover a win
  in full closes its own doors until reopened by hand, the worst reputation hit in the mod.
- **Regulars.** Reputation isn't one number any more. Alongside the town's own name, every
  faction you actually trade with keeps its own standing, moved by how its own customers were
  treated — served, walked out on, or evicted from a rented room. Favor one faction and they
  show up more often than everyone else; mistreat them and only their own trade dries up. The
  town ledger names your best and worst relationship once either one actually stands out.
- **A Hospitality bridge, optional and honestly narrow.** If Hospitality is also installed, one
  of its idle guests can wander off to shop, drink or get a haircut — never a room, since
  Hospitality is already housing them. This is the one feature built against a mod that has
  never actually run alongside this one in this sandbox; it degrades to doing nothing at all,
  silently, if its one guess about how Hospitality is put together turns out wrong.
- **Outlaws and the law.** Leave enough silver sitting exposed — in a till, or loose on a sales
  floor — for long enough and it draws a *stickup* — a small, armed band, sized off the silver
  actually at risk rather than your colony's total wealth, that heads straight for counters
  instead of colonists and takes whatever it can reach, a floor pile as readily as a till. An
  alert shows the risk climbing well before it's live, and names exactly where the money is:
  collecting a till only moves its silver onto the floor, so the risk doesn't actually fall until
  a hauler carries it off to a stockpile — that's the real risk-management call. Staffing a
  counter doesn't protect it; a sheriff on duty does — halving both how often a stickup happens
  and how long one lasts. Fight back and the crew routs, though whatever they've already taken
  stays gone; a captured raider works through the same ordinary prisoner options any other downed
  hostile does.
- **A stagecoach line, and the town's first visible milestone ladder.** A **coach depot**, behind
  its own research past Frontier commerce, switches on a guarantee: whatever the ordinary arrival
  clock is doing on its own, no gap between customer groups — organic or scheduled — ever runs
  longer than the town's current route tier allows. Appeal climbs the route through three tiers —
  irregular freight wagons, a weekly coach, then a daily express — each one tightening the
  ceiling, richer purses on the customers a tier forces into being, and from the second tier up,
  a chance of a **VIP passenger** carrying five times an ordinary purse. Crossing a tier fires a
  letter on the way up and a quieter message on the way down; the depot's own inspect pane always
  names the current tier and what the next one needs. Built as one extra way for the existing
  customer incident to fire, not a second, independent one, so it can never double up with, or
  land on top of, an ordinary arrival.
- **A gold rush, and the hangover after it.** Word of a strike nearby floods the town with
  prospectors for a quadrum: arrivals roughly triple and purses swell, but the crowd only really
  wants tools, meals, booze and medicine — a general store stocked for ordinary custom is
  suddenly stocked for the wrong people, and reading that is worth real silver. The boom is also
  a standing invitation to gouge, and gouging is measured against what's normal for *that kind*
  of business rather than a flat number, so a saloon isn't punished for being a saloon. Charge
  what the traffic will bear and you'll make a fortune and spend the bust paying for it: when the
  vein dries up, trade falls below its old baseline until the town's name recovers.
- **Rival towns, and an opponent for the arrival clock.** One or two NPC towns sit on the wider
  map with an appeal of their own, priced the identical way your own shops are, and your share of
  regional trade — your pull against theirs — now slows or leaves alone how often customers set
  out for you. Worked out, not assumed: it can never stretch the gap by more than **60%**, and
  never speeds it up, however many rivals exist or how far a *Rival strength* setting is dialed. A
  rival occasionally undercuts prices for a several-day stretch, a named, messaged event; a
  message also tells you the first time the regional lead actually changes hands. Any counter's
  inspect pane shows your current share, and the Town ledger names every known rival by its own
  appeal and posture. A coach depot's own arrival guarantee is completely immune to all of this.

## Installing

Copy this repository into `RimWorld/Mods/OldWestTown` and enable it in the mod list. The
compiled assembly is committed at `1.6/Assemblies/OldWestTown.dll`, so no build step is needed
to play.

## Playing it

1. Research **Frontier commerce**.
2. Build a **shop counter**. Rotate it so its interaction cell — the staff side — faces into
   the shop's back room. Customers stand on the opposite side.
3. Put goods in the same room. Shelves work; so does the floor.
4. Open the counter's **Stock** tab and choose what it sells.
5. Set a price with **Set prices**.
6. Give a colonist a **Shopkeep** priority in the Work tab.
7. Wait for the *Customers arriving* event, then watch the till.

If nobody is behind the counter, nothing sells. That's deliberate — it's what makes the
Shopkeeping assignment matter. You can turn it off with the *Allow self-service* mod setting,
at a price: every unattended sale quietly erodes the town's reputation (a haircut never allows
self-service, no matter the setting — an empty chair can't cut anyone's hair).

A **saloon bar** works the same way but also serves drinks and meals straight from its own
shelves — stock it with liquor or meals as well as (or instead of) general goods. A **barber
chair** needs no stock at all: build one, staff it, and a passing customer with money to spare
will sit for a haircut.

A **hotel desk** sells lodging instead: build one alongside a room of **hotel beds**, staff the
desk, and a tired traveller with money to spare will pay for the night and go find a free bed.
They sleep until rested, so don't be surprised if some of a customer group is still in town well
past when the rest have gone home — the whole visit now waits for every rented room to empty out
before anyone leaves.

A **faro table** runs a gambling hall: build one, staff it, and a gambler with money to spare
will sit down for a hand. Set its **Set house edge** slider, right next to **Set prices**, to
decide how greedy the house is — a low edge keeps a table fair and its gamblers coming back all
evening, a high one wins more per hand but angers losers faster and burns through the till's own
bankroll quicker. A win pays double the stake straight out of that till; if the till ever can't
cover one, the table closes its own doors until you reopen it, so keep an eye on it the way you
would any other counter's stock.

A saloon or a gambling hall left to run itself eventually gets rowdy — build a **sheriff's
office** and assign a colonist to it from the office's own gizmo to keep the peace. Unlike
Shopkeeping, that colonist also needs a **Sheriffing** priority in the Work tab — being assigned
is who holds the post, the work priority is whether they're currently out there doing it, same as
any other job. While they're on duty, patrons town-wide get rowdy more slowly; if one starts
"getting loud" anyway, the sheriff can break off and walk over to calm them down before it turns
into a disturbance. A skilled dealer also draws fewer cheating accusations at the gambling hall,
the same Social skill that trains behind any counter.

## Building from source

Requires the .NET SDK (8.0 is fine — the project targets `net472` and pulls the RimWorld
reference assemblies from NuGet, so you do **not** need the game or Mono installed to compile).

```sh
dotnet build Source/OldWestTown/OldWestTown.csproj -c Release
```

Output goes straight to `1.6/Assemblies/OldWestTown.dll`.

### Checking your defs without launching the game

RimWorld resolves XML into C# types and def references at load time and reports failures as
red errors in-game, which is a slow way to find a typo. `tools/validate_defs.py` catches most
of them statically:

```sh
python3 tools/validate_defs.py
```

It verifies that every C# type named in XML exists, that every def reference resolves (or is
on an explicit known-vanilla list), and that every `.Translate()` key has an English string.

Checking a *vanilla* type needs something that can read RimWorld's assemblies. `tools/refdump`
does that, using the same reference assemblies the build already restores from NuGet, so it
needs neither the game nor a running RimWorld:

```sh
dotnet build tools/refdump/refdump.csproj -c Release   # once
python3 tools/validate_defs.py                         # now checks vanilla types for real
```

Without it the validator still runs, but downgrades "is this a real vanilla type?" to a note.
`refdump` is also useful on its own when you are unsure an API exists:

```sh
dotnet tools/refdump/bin/Release/net8.0/refdump.dll Thing.Ingested '=CompPowerTrader' '~Hediff'
```

### Making art for a new building

Textures are flat programmer art in a shared frontier palette. `tools/make_textures.py` draws
them from one table, so adding a building is a row rather than an art task — and CI fails if a
building in that table has no texture on disk:

```sh
pip install Pillow
python3 tools/make_textures.py     # draw art for anything that has none
```

It never overwrites existing art unless you pass `--force`.

### Testing without waiting on real-time or in-game-day clocks

Several systems are slow to reach in an ordinary session — a stickup needs an hour of real-time
till accumulation, a gold rush is an MTB roll, a route-tier promotion takes in-game days. With
Dev Mode on, the **Old West Town** category in RimWorld's own Debug Actions menu covers all of
them: spawn a customer group, fire a stickup, start (and force the bust of) a gold rush, force a
rival undercut, roll straight to the nightly settlement, top up a selected till or pawn's purse,
and more — every lever reuses the mod's own production code path rather than faking one. An
opt-in **Telemetry logging** mod setting (off by default) logs real arrival gaps, settlement
verdicts and stickup rolls to the player log. See
[Contributing → Dev Mode kit](https://ssinnott.github.io/rimworld-shops/contributing.html#dev-mode-kit).

CI (`.github/workflows/ci.yml`) runs all of the above plus a full build on every push.
`.github/workflows/pages.yml` builds the wiki on every pull request and publishes it on
every push to `main`.

### Keeping the wiki current

The wiki is checked, not just written: `tools/validate_docs.py` fails the build if a def, source
file or translation key is undocumented, if an internal wiki link is broken, or if the changelog
has no *Unreleased* section. A change to the mod updates the page it affects and the changelog in
the same commit — see
[Contributing](https://ssinnott.github.io/rimworld-shops/contributing.html).

```sh
python3 tools/validate_docs.py
```

## Caveats

- **This has not been run in RimWorld.** It compiles against the 1.6 reference assemblies and
  passes the static checks above, but the pawn AI — job drivers, the lord graph, the duty
  think tree — is the kind of code that only really proves itself in game. Expect to shake out
  bugs on first play. The full list is in the
  [wiki's known risks](https://ssinnott.github.io/rimworld-shops/architecture.html#known-risks).
- Textures are programmer-art placeholders.

## Licence

Code is MIT. RimWorld is © Ludeon Studios; this mod is not affiliated with Ludeon.
