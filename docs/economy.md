---
title: The town economy
summary: Appeal, reputation, pricing and the arrival clock — the four numbers that decide whether a shop becomes a town.
---

Four numbers decide how well your town trades: how much it **appeals** to travellers, what
**reputation** it has with them, what you **charge**, and how often they **arrive**. The **Town
ledger** button on any counter shows all of them in one place.

## Appeal

**Appeal** is how much trade the town attracts — roughly 0 to 3+. It is worked out from what you
have actually built.

```
       for each open business with something to offer:
         its kind's appeal, counted in full the first time that kind appears
         and at 35% for every repeat            →  the kinds score

       everything on your shelves, plus the value of any service
         that sells no goods, counted thirty times over  →  the wealth score

       kinds score  ×  square root of (wealth score / 1000), capped between 0.25 and 3
                    ×  standing, which runs from 0.5 at rock bottom to 1.5 at a spotless
                       reputation
       =  appeal      (zero if you have no open business at all)
```

Four things follow from that, and they are the whole strategic layer:

**Breadth beats depth.** A second business of a kind you already run counts for only 35% of the
first. One giant general store should not out-earn a street with a store, a saloon and a barber.
That is the pressure that turns a colony into a town.

**Wealth has diminishing returns.** Because it's a square root, doubling what's on your shelves
does not double your draw. Getting the first shelf stocked matters enormously; getting the tenth
matters much less.

**Reputation is a multiplier, not a bonus.** Standing runs from 0.5 to 1.5, so a well-run town
draws *three times* the trade of a badly-run one with identical stock.

**A closed or empty business contributes nothing.** Appeal only counts businesses that are open
and have something to offer — stock on the shelf, or an available service.

> **Why services count for thirty times their price.** A service that sells no goods has no
> "quantity on the shelf" the way physical stock does; its value is the price of one visit, an
> order of magnitude below what a stocked shelf is worth. Scaling it up before it joins the same
> wealth curve is what lets one barber shop clear the customer threshold on its own, the way a
> modestly stocked general store already can, instead of being drowned out by a scale tuned for
> goods.

## Reputation

**Reputation** is a rolling satisfaction score from 0 to 1, starting at **0.5**.

| Event | Change |
| --- | --- |
| A staffed sale or service | **+0.01** |
| A self-service sale | **−0.005** |
| A customer walks out | **−0.02** |
| A saloon or gambling-hall disturbance | **−0.05** |
| A gambling-hall shortfall | **−0.08** |
| Every day | drifts 5% back toward 0.5 |

A walkout costs twice what a sale earns, so a counter you leave unattended during a busy visit
loses ground fast. A [disturbance](customers.md#trouble-at-the-saloon-and-the-gambling-hall) costs
more than double a walkout again — an unpoliced saloon or gambling hall is one of the fastest ways
to burn through a town's good name. Worse still is a [gambling-hall
shortfall](#the-till-as-a-bankroll) — the table winning a bet for a customer and then not being
able to pay it — the single worst reputation hit the mod has. And because reputation decays toward
neutral every day, a town has to keep earning its name — a burst of good trade a quadrum ago
doesn't hold the number up.

Reputation feeds two things:

- **Appeal**, through the standing multiplier above — so it changes how many customers come.
- **What everything actually costs.** Reputation shades every price either side of the markup you
  set: a town with a bad name sells at **15% above** its slider, a well-liked one at **10%
  below** it. So a good name both brings more customers in and prices a little keener for them.

## Standing with a faction

Reputation is still one number for the whole town — it's the honest answer to "should anyone set
out for this town at all," and no single faction gets a different answer to that. But the town
also keeps a private **standing** with each faction it has actually done business with, 0 to 1,
sitting quietly alongside the town-wide number.

| Event | Effect on that faction's standing |
| --- | --- |
| A staffed sale or service | rises **sharply** |
| A customer walks out, or a hotel guest is evicted | falls **sharply** |
| A gambling-hall shortfall | falls **hardest of all** — twice as hard as a walkout |
| A self-service sale | no effect — nobody chose to serve *this* customer in particular |
| Every day | drifts back toward the town's own reputation |

A faction the town has never dealt with specifically simply **reads as the town's own
reputation** — there's nothing to seed, and nothing for an existing save to migrate. Once a
faction's standing has genuinely pulled away from that shared number, though, it starts to
matter: **standing, not reputation, decides which faction's customers turn up next** — see
[which faction turns up](customers.md#which-faction-turns-up). Treat one faction's customers
well often enough and they become **regulars**, showing up more often than everybody else;
mistreat them and they taper off, without punishing anyone you haven't touched.

The **Town ledger** names the single best and single worst relationship on record, but only once
either has actually pulled clear of the town's own name — a fresh game, or a town that's treated
every visiting faction about the same, says nothing extra.

Hostile factions, the player's own faction, and any faction with nowhere in the world to actually
send customers from never accrue standing at all — there's no meaningful "relationship" to have
with them. A faction that turns hostile mid-visit (a relations swing unrelated to anything
happening in your town) simply stops being tracked from that moment on.

## Pricing

Every price in the mod — for goods or a service — is worked out the same way, so the price you
see on the counter is the price the customer judges and the price they pay.

```
unit price = market value × your markup × the town's reputation
```

Market value already folds in quality, material and remaining hit points, which is exactly what
a shopper would judge. A service that sells no goods uses a flat price of its own in place of the
market value; nothing else changes. No price is ever below **1 silver**.

### How price wins customers

A customer judging a shop scores its prices at **1 ÷ your effective markup**, held between 0.1
and 2.0. So a shop charging market value scores 1.0, one charging double scores about 0.5, one
charging triple about 0.33. On top of that, a **staffed counter is worth 50% more** than an
unstaffed one, distance counts against a shop (a counter 40 tiles away is worth half one on the
doorstep), and for a service, how much that particular customer wants it counts too.

The practical upshot: **undercutting a rival shop genuinely pulls customers away from it**, and
staffing a counter beats leaving it open and empty.

### Curb appeal

A [false front](buildings.md#false-front) near a shop's customer-facing side folds a small,
**capped** bonus into that scoring — enough to win a close call between two similarly-priced
rivals, never enough on its own to make an overpriced shop look like a bargain. One qualifying
facade nearby is worth something; a second is worth a little more; a street dressed up with a
false front on every building is worth no more than one with two. It's advertising, and
advertising has diminishing returns.

### Partial purchases

If a customer can't afford the whole stack they wanted, the order is trimmed to what they can
pay for rather than sending them away empty-handed over a rounding difference. A service is one
unit, so it gets no such trimming — a customer either affords the drink or doesn't.

A single shopper also can't strip a shelf: purchases are capped at a quarter of the item's stack
limit, or one unit for anything that doesn't stack.

## The till as a bankroll

Every business's till only ever fills up — a customer's silver goes in, and nothing but the
player's own **Collect takings** button ever takes it back out. A
[gambling hall](businesses.md#gambling-hall) is the one exception: a win pays straight out of that
same till, so for the first time in the mod, money leaves a till because a customer earned it, not
because the player collected it.

**The stake.** A hand's ante is priced exactly like a haircut — a flat base price, then the
table's own markup and the town's reputation — and recorded as a sale the moment it's paid, win or
lose. What happens to it afterward is a separate roll.

**The odds.** [House edge](businesses.md#gambling-hall) is exactly the fraction of every silver
wagered the house keeps on average — not an approximation; the maths behind it comes out exact
however big a win pays. At the table's default settings, a hand is close to even odds, tipped just
enough toward the house to give it its edge. Wind the slider up toward its greedy end and wins get
rarer while the house's average take per hand climbs sharply — genuinely tempting, and genuinely
self-defeating, since the same angrier losers who fund that take are also the ones who stop
sticking around to lose more. Wind it down toward zero and the table is genuinely fair: no expected
profit for the house at all, just variance — which means even a perfectly fair table can run cold
and empty its own till over a long enough losing streak, not just an unfair one.

**The throughput.** A hand only takes a few seconds, so a table that's never sitting idle can run
one gambler through a dozen or so hands in a single hour. At the table's default settings that
adds up to an expected house profit on the order of 35–40 silver an hour from that one gambler alone
— a rate no ordinary shop can match, since nothing about a shelf sale repeats: a customer's whole
stake for the visit is spent once, split across however many counters they visit, never handed
over twice for the same thing. That's exactly what makes a greedy table tempting. It's also what
makes it self-defeating: those same odds mean more hands lose than win, and a short losing streak
is enough to tip a gambler into a
[disturbance](customers.md#trouble-at-the-saloon-and-the-gambling-hall) well before the table
could ever actually work through a typical gambler's whole purse — so in practice, anger closes a
greedy table's account long before its own till does.

**The payout.** A win is hard-capped at whatever silver the till actually holds: it can never go
negative, and it can never conjure silver that was never there. If the till comes up short, the
gambler gets whatever's actually in it, the shortfall that follows is the single worst reputation
and standing hit anywhere in the mod — worse than a [saloon
disturbance](customers.md#trouble-at-the-saloon-and-the-gambling-hall) — and the table **closes its
doors** until reopened by hand, the same legible "something's wrong here" signal a business already
gives for running out of stock to sell.

**Starting capital.** A freshly built [faro table](buildings.md#faro-table) is seeded with its own
bankroll rather than opening with an empty till, paid for once, up front, as part of what the table
costs to build. Without it, the very first bet ever placed at a brand-new table would have a real
chance of winning a payout the till has no way to cover — not a rare accident, close to a coin flip,
on transaction one.

**Letting it sit is a risk, too.** A till only ever fills up on its own — collecting it is still
entirely up to you, and nothing before this forced the question. Leave enough silver sitting
uncollected across your tills for long enough, though, and it starts drawing armed attention: see
[outlaws and the law](outlaws.md) for how that risk builds, what it targets, and what actually
slows it down.

## What counts as stock

The sales floor is a **room**, not a zone you paint. A shop is defined by walls you already
built: it reads naturally, it costs nothing to set up, and it makes the room-quality stats you
already care about matter commercially.

Indoors, everything inside the counter's room is on display. Outdoors — or in a room that touches
the map edge — it falls back to a radius instead (about 10 tiles for a shop counter, 8 for a
saloon bar), so a market stall on the boardwalk still trades.

An item is on display if **all** of these hold:

- it is an actual item lying in the shop, with at least one left in the stack;
- it is **not silver** (selling silver for silver is nonsense, and no setting can enable it);
- it is **not forbidden** — forbidding is your way of saying "not for sale";
- it is **not reserved by a colonist** — goods a hauler is already on the way for would churn
  both jobs if sold out from under them;
- it is not burning;
- its market value is above zero;
- the business's **Stock** tab allows it.

## The arrival clock

How often customers arrive is the town's own doing, not the storyteller's.

The town checks its clock every ten seconds or so. Below appeal **0.5** nothing happens at all.
Above it, the average gap between customer groups slides from **3.5 days** at the threshold down
to **0.8 days** for a thriving town, divided by the *Customer volume* setting — so a town
scraping past the threshold sees a group every few days, and a booming main street sees one most
days.

Arrivals still go out through the storyteller, which won't let events pile on top of each other:
it holds groups at least 0.6 days apart, so a booming town gets frequent trade, never a flood.
There is also a small background chance of a group turning up regardless.

## The stagecoach line

A **coach depot** puts the town on a scheduled route: a promise that a big-spending group won't
be too many days apart, layered on top of everything the arrival clock above already does on its
own. It needs its own research past Frontier commerce — see [buildings](buildings.md#coach-depot)
— and does nothing at all unless one is actually standing on the map.

### The ladder

The route climbs through three tiers as appeal grows, and the depot's own inspect pane always
shows which one is active, what the next one needs, and a rough countdown to the next guaranteed
coach:

| Tier | Needs this much appeal | Longest gap between arrivals | Extra on every scheduled purse | Chance of a VIP passenger |
| --- | --- | --- | --- | --- |
| Irregular freight wagons | **0.5** | **8 days** | **+25%** | — |
| Weekly coach | **1.5** | **4 days** | **+60%** | **8%** |
| Daily express | **3.5** | **2 days** | **double** | **20%** |

Appeal decides the tier the same way it decides everything else about arrivals: climb past a
threshold and the route visibly upgrades, with a letter naming the new tier; slip back below one
and it visibly demotes instead, with a quieter message. Unlike the arrival clock's own quiet
slowdown, a route change is a milestone the game actually tells you about.

### A ceiling, not a second clock

The guarantee doesn't run a second dice roll alongside the arrival clock above — it puts a
ceiling on the one the town already has. Once the active tier's own longest-gap number has
passed with no group of any kind showing up, organic or scheduled, the very next check forces
one, through the identical event the ordinary clock already fires. The minimum gap between
groups still applies no matter which condition caused a given group to turn up, so a scheduled
arrival can never land on top of an organic one, or double up the town's total footfall.

In practice the ceiling rarely has anything to do — the ordinary clock is already faster than it
at any appeal a tier is active for. Where it does contribute, it's a top-up, not a flood: right
as a town first qualifies for a tier, expect something like 10–30% more groups than the clock
alone would have given it that day, biggest at the weekly-coach tier's own threshold; that
uplift tapers toward the single digits the longer a town sits comfortably inside a tier, and it
never comes close to doubling how many customers show up, at any tier, at any appeal.

Scheduled groups also carry more silver — the purse boost in the table above, stacked on top of
the appeal scaling every arrival already gets — and, from the weekly-coach tier up, occasionally
include one passenger carrying a great deal more than the rest of the party. See [scheduled
coach arrivals](customers.md#scheduled-coach-arrivals) for what that looks like from the
customer's side of the counter.

## Gold rush

A **gold rush** is a map-wide event, not a building: a strike nearby floods the town with
prospectors for a while, then leaves it quiet again. It runs in two phases, and the game always
tells you which one you're in and roughly how much of it is left — a status line on the event
itself, readable the same way you'd check the weather. A settings-menu switch (`Gold rush
events`, on by default) turns the whole thing off if you'd rather not have it.

### The boom

For a quadrum (15 days) after the letter arrives:

| | |
| --- | --- |
| Arrivals | roughly **three times** as often as [the arrival clock](#the-arrival-clock) would otherwise give the same appeal |
| Purses | an extra **50%** on top of everything else that already scales a customer's silver |
| What they want | tools, medicine, meals and drink, above everything else on the shelf — see [the demand basket](customers.md#the-demand-basket) |

The boom doesn't replace the ordinary arrival clock or [the stagecoach guarantee](#the-stagecoach-line)
— it speeds up the same clock the guarantee is already a ceiling on top of. A coach depot's own
promise is completely unaffected either way: it's a floor, not something a rush multiplies
against, so a scheduled route keeps the exact guarantee it always had, boom or bust.

### Gouging the rush

The demand basket above is strong enough to swing which shop a customer walks into by roughly
ten to one, which is what makes stocking for the rush pay off — but it also means the ordinary
"customers avoid pricey shops" pressure that [keeps prices honest](#how-price-wins-customers)
gets badly outweighed: a shop selling what prospectors want can charge nearly anything and still
have a line out the door. So there's a second brake, active only during the boom: sell above
what's normal for *your kind* of business, and every sale at that counter costs you a little
reputation and standing with that customer's own faction, on top of whatever else the sale
already did — nothing extra at your kind's own usual markup, the most at your kind's own ceiling.
A shop that keeps doing it draws a warning message naming it, at most once a day, so it never
comes as a surprise.

The temptation is genuine — gouging a well-stocked shop during a boom earns well in the short
run, since almost nobody walks away over price while the rush is on. The reputation cost is what
keeps that from being free money.

### The bust

Once the boom ends, the average gap between customer groups stretches to roughly **two and a
half times** what [the arrival clock](#the-arrival-clock) would otherwise give the same
appeal — a real dip below what your town's appeal alone would predict, not just a return to
normal. That lasts until the town's reputation climbs back to a point just
under its own resting level, or, failing that, a very long backstop (on the order of two months
from the rush's very start) ends it regardless.

That backstop is a safety net, not something meant to bite: gouging only ever happens during the
boom above, never during the bust, so nothing during the slow period can keep pushing reputation
back down. A town that didn't push its prices up during the boom is typically already past the
recovery bar the moment the bust begins — there's nothing to recover from. A town that gouged
hard enough to crash its reputation all the way to the floor still clears the bar in roughly a
month and a half of ordinary daily drift alone, comfortably inside the backstop above, and
sooner still with any ordinary staffed trade on top of that drift. Either way, the bust is a
genuine slowdown, not a hole you can fall into and never climb back out of.

When it ends, a message tells you so — a letter if reputation genuinely earned its way back, a
quieter word if the backstop was what actually closed it out. Either way, arrivals return to
whatever the ordinary arrival clock and any stagecoach guarantee already give the town on their
own.

## The daily ledger

At local midnight the town closes the books: every business's daily figures reset, the town's
sales, walkouts and takings for the day reset, and reputation drifts back toward neutral.
Lifetime takings are kept, per business and for the town.

The **Town ledger** button, on every counter, shows appeal, reputation, today's sales, walkouts
and takings, lifetime takings, and each business's revenue and till — the numbers the economy
runs on, readable in one place.
