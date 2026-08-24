---
title: Roadmap
summary: What is built, what is next, and the larger directions that build on top of it.
---

The plan is staged so each step is playable on its own. All seven stages are shipped, and so are
the first two of the thematic expansions below — the gambling hall and the stagecoach line.

For the reasoning behind the shipped stages, see the [design notes](DESIGN.md).

## Staged plan

**1. Vertical slice — done.** Counter, stock, pricing, till, shopkeeping work type, customer
arrival and purchase, appeal and reputation.

**2. Services — done.** The interesting half of a town sells *time*, not items. A counter became
a general "business" that can sell goods, services, or both: the customer walks up (fetching an
item first, if the service uses one), waits to be served, pays, and gets an effect instead of
something to carry home. Shipped: **drink** and **meal** at the saloon, both poured from its own
shelves — the interesting case, since a service that still moves stock has to count as both
without being counted twice — and the **barber shop**, which sells nothing but a colonist's time
and hands back a mood boost and a new hairstyle. A bath house and a doctor's office are already
possible without new code, and wait only on the buildings to put them in.

**3. Lodging — done.** Rentable beds, sold by a new **hotel desk** business. A guest pays for the
night up front at the desk, keeps shopping, and only heads for whichever bed is free once they're
genuinely tired — there's no specific room nailed down at booking, just any vacant bed in the
same room as the desk. They sleep until rested and wake with a mood boost that scales with how
nice the room is. The rest of the group won't head home until every rented room is empty, which is
what lets a visit run past its usual length without a second lord state to manage it. Losing a
room early — the bed taken apart, or given to someone else by hand — costs the same reputation a
walkout does, with no refund. Deliberately left for later: booking several nights in advance,
unstaffed nightly billing, and a private suite tied to one specific desk.

**4. Town roles — done.** The saloon generated no trouble at all before this stage, so trouble
had to exist before anyone could be posted to suppress it. Now every round of drinks makes that
one patron rowdier; left alone, it eventually boils over into a **disturbance** — a message, a
reputation hit worse than a walkout, and that patron stops spending for the rest of their visit.
The **sheriff** is the role that made it into this stage: assign a colonist to the new
[sheriff's office](buildings.md#sheriffs-office) the same way you'd assign an owner to a bed,
give them a Sheriffing priority, and they'll patrol the office — slowing how fast the whole town
gets rowdy — and step in to calm a specific patron down before it turns. Of the two other roles
this stage once named, **barkeep** turned out not to need a separate post: a skilled shopkeeper
behind the bar already gets its own discount on rowdiness, no badge required. **Banker** is cut
outright — there's no bank yet for one to run.

**5. Reputation with depth — done.** The town's one reputation number is unchanged, and still the
honest answer to "should anyone bother setting out for this town at all." Alongside it, each
faction now keeps its own **standing** with the town — untouched until that faction's own
customers are actually served or turned away. Treat one faction's customers well often enough and
they become **regulars**, showing up more than everybody else; mistreat them and they taper off,
without punishing anyone you haven't dealt with. The town ledger names your best and worst
relationship once either has genuinely pulled away from the town's own name. A faction you've
never dealt with specifically just reads as the town's reputation, so an existing save needs
nothing seeded for this to make sense on the very first load.

**6. Old west content pass — done.** Boardwalk terrain, false-front facades, a hitching post,
batwing doors, a faro table and a gallows — mostly content rather than code, dressing a street
that steps 1–5 already made *function*. The one exception is the **false front**: standing near a
shop, it gives that shop's prices a small, capped edge in how appealing they look to a passing
customer — enough to win a close call between two similarly-priced rivals, never enough to sell a
shop that's genuinely overpriced. The faro table shipped purely decorative in this stage; the
gambling hall below is what promotes it into a real business.

**7. Hospitality bridge — done.** Hospitality is not installed anywhere this mod has ever been
built or tested, so this stage is narrower than "guests shop too" might suggest, and honest about
where its edges are. Roughly every six in-game minutes, on a map where Hospitality is actually
running, an idle Hospitality guest — one whose own AI has already decided it has nothing better
to do right now — may be handed a single shopping trip: buy something off a shelf, get a drink, a
meal or a haircut. Never a room; Hospitality is already housing them, and the two mods can't end
up fighting over the same guest, [by construction](DESIGN.md#the-hospitality-bridge). Nothing
about a guest's stay with Hospitality — their bed, their duty, their `Lord` — is ever touched.
Hospitality itself is recognized without ever naming one of its types: by which assembly a
guest's governing `Lord` or any of their `ThingComp`s belongs to, a guess this mod's own sandbox
has no way to check against a real install. If that guess is wrong, the bridge stays
permanently and silently switched off — indistinguishable from Hospitality never having been
there at all. See [Hospitality guests](customers.md#hospitality-guests) for what this looks like
in play, and the [code map's known risks](architecture.md#known-risks) for the full account of
what is, and isn't, verified here.

## Beyond the staged plan — thematic expansions

Larger directions that build on the finished stages rather than slotting between them. Each is
listed with what it reuses, roughly cheapest first.

**Gambling hall — done.** The first business where the "sale" is a wager rather than a
purchase: a patron buys in at the [faro table](buildings.md#faro-table), and everything past that
is a win, loss or shortfall roll, resolved entirely inside the same service seam every other
business already uses — no new queueing, no new till primitive beyond a payout. **House edge**, a
second slider living right next to markup, is exactly the fraction of every silver wagered the
house keeps on average, whatever the payout or the odds behind it: set it low and a table pays out
almost as often as it takes, keeping gamblers around all evening; set it high and it pays rarely
but keeps far more per hand — genuinely tempting, and genuinely self-defeating, since the same
angrier losers who fund that richer take are also the ones who stop sticking around. Losing a hand
feeds the same rowdiness the saloon's drink already does, and an unlucky loss can additionally draw
a Social-skill-gated cheating accusation against the dealer — the one place in the mod a dealer's
skill visibly changes something round to round, not just a background rate. The one genuinely new
piece of plumbing is the payout itself: a win pays straight out of the same till every sale already
fills, hard-capped at whatever silver it actually holds, and a table that can't cover a win closes
its doors until reopened by hand — the worst reputation and standing hit anywhere in the mod, and
the reason a freshly built table is seeded with a bankroll of its own rather than opening with an
empty till. The stage-6 faro table is promoted into this, not duplicated alongside it: there is
exactly one faro table in the build menu, not two confusingly similar ones.

**Outlaws and the law.** A rich town becomes a target: the more silver sitting in tills
(already tracked per counter), the higher the chance of a *stickup* — a small raider band that
heads for counters instead of colonists, empties tills, and leaves unless resisted.
Counterplay is the step-4 sheriff, plus a wanted board (bounty quests on recurring outlaw
leaders) and a jail that converts captured outlaws into silver or reputation. Turns "collect the
takings" from a chore into a real risk-management decision. A new event and a new kind of raid,
both built on shapes the mod already has.

**Stagecoach line — done.** A [coach depot](buildings.md#coach-depot) puts the town on a
scheduled route: a guarantee, layered onto the existing arrival clock as a ceiling rather than a
second roll, that a big-spending group won't be more than a few days apart. Appeal raises the
route through three tiers — irregular freight wagons, a weekly coach, then a daily express —
each with its own arrival ceiling, purse boost, and chance of a VIP passenger carrying five times
an ordinary purse, giving the compounding economy a visible milestone ladder on top of what used
to be a quietly shortening, invisible clock; the depot's own inspect pane always names the
current tier and what the next one needs. Of the two other ideas this entry once named, **mail
contracts are cut outright** — every transaction this mod already has is a stranger walking in
and paying at the counter, and a contract that pays out later for goods committed up front has no
pawn on either side of it for [the one architectural
rule](DESIGN.md#the-one-decision-everything-else-follows-from) to even apply to. **The
quest-giver framing of the VIP passenger is cut too**, keeping only the cheaper alternative this
entry's own original wording already offered — "a shopper with a 5× budget" — since that delivers
the payoff for zero new subsystems, where a real quest would mean taking on a large, effectively
unverifiable API surface in a mod that has never run in a live game. See [the stagecoach
line](economy.md#the-stagecoach-line) for how it plays, and [the design
notes](DESIGN.md#stagecoach-line-a-ceiling-not-a-second-clock) for the reasoning and the worked
math behind the ceiling.

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
becomes a ghost town you can salvage. The most ambitious of the four, since it adds state to the world map,
but the one that most directly deepens the pricing-and-appeal loop — it gives your town's appeal
something to be measured against, and makes pricing genuinely competitive rather than
solitaire.
