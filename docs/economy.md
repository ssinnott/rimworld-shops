---
title: The town economy
summary: Appeal, reputation, pricing and the arrival clock — the four numbers that decide whether a shop becomes a town.
---

Four numbers decide how well your town trades: how much it **appeals** to travellers, what
**reputation** it has with them, what you **charge**, and how often they **arrive**. The **Town
ledger** button on any counter shows the town's side of that in one place; what you charge is set
on each business's own **Stock** tab.

## Appeal

**Appeal** is how much trade the town attracts — 0 for a town with nothing to sell, and about **5**
for a stocked main street with three trades on it. It is worked out from what you have actually
built.

```
       for each open business with something to offer:
         its kind's appeal, counted in full the first time that kind appears,
         then at 35% of the one before it for every repeat  →  the kinds score

       everything on your shelves at market value, plus the value of any service
         that sells no goods, counted thirty times over and discounted for
         repeats the same way                            →  the wealth score

       kinds score  ×  square root of (wealth score / 1000), capped between 0.25 and 3
                    ×  standing, which runs from 0.5 at rock bottom to 1.5 at a spotless
                       reputation
       =  appeal      (zero if you have no open business at all)
```

Four things follow from that, and they are the whole strategic layer:

**Breadth beats depth.** A second business of a kind you already run counts for 35% of the first, a
third for 35% of the second, and so on — so however many general stores you line up, the kind is
worth at most about **one and a half** of one. A second counter of a kind you already sell, on a
floor you already trade from, isn't even that: it is a second till, and what it buys you is serving
two customers at once. (A counter of a *different* kind on that floor is a new trade, and counts in
full.) One giant general store should not out-earn a street with a store, a saloon and a barber.
That is the pressure that turns a colony into a town.

**Wealth has diminishing returns.** Because it's a square root, doubling what's on your shelves
does not double your draw. Getting the first shelf stocked matters enormously; getting the tenth
matters much less. Goods are counted at **market value**, and every stack counts once however many
counters can see it — so putting your prices up never brings you more customers, only more silver
per sale.

**Reputation is a multiplier, not a bonus.** Standing runs from 0.5 to 1.5, so a well-run town
draws *three times* the trade of a badly-run one with identical stock.

**A closed or empty business contributes nothing.** Appeal only counts businesses that are open
and have something to offer — stock on the shelf, or an available service.

> **Why services count for thirty times their price.** A service that sells no goods has no
> "quantity on the shelf" the way stock does; its value is the price of one visit, an order of
> magnitude below what a stocked shelf is worth. Scaling it up before it joins the same wealth
> curve is what lets one barber shop clear the customer threshold on its own.

## Reputation

**Reputation** is the town's service record, from 0 to 1, starting at **0.5**. It is settled once a
night rather than sale by sale: every person who came to a counter today leaves exactly one
verdict, however much they bought.

| That customer's day | Their verdict |
| --- | --- |
| Somebody stood behind the counter for them | **1.0** |
| They took goods off an unwatched counter | **0.5** |
| Nobody served them at all | **0** |
| They gave up waiting anywhere | halves whatever the rest of it was worth |

At midnight the town averages those verdicts and moves its name toward the average — by at most a
**fifth** of the gap, and less on a thin day, six callers counting as a full day's evidence. On a
day when nobody came to a counter at all, it drifts **5%** back toward 0.5 instead.

A [disturbance](customers.md#trouble-at-the-saloon) is charged on top, at **5%** of the town's name
for each one, and it is charged the same night rather than as the brawl happens — an unpoliced
saloon is the fastest way to burn through a good name.

Two things follow. Volume never enters into it: a customer who opened their purse six times is one
satisfied customer, not six, so what moves the number is how many *people* you looked after, not
how much they spent. And no single afternoon settles the question — a busy main street earns a
strong name over about a week, and a bad day at one counter is one verdict among the day's callers.

Reputation feeds two things:

- **Appeal**, through the standing multiplier above — so it changes how many customers come.
- **What everything actually costs.** Reputation shades every price either side of the markup you
  set: a town with a bad name has to let its goods go at **10% below** its slider, a well-liked one
  gets away with **10% above**. So a good name brings more customers in *and* lets you charge each
  of them a little more. The **Stock** tab prints what customers actually pay whenever that differs
  from the slider.

## Standing with a faction

Reputation is still one number for the whole town — it's the honest answer to "should anyone set
out for this town at all," and no single faction gets a different answer to that. But the town
also keeps a private **standing** with each faction it has actually done business with, 0 to 1,
sitting quietly alongside the town-wide number.

| Event | Effect on that faction's standing |
| --- | --- |
| A staffed sale or service | rises **sharply** |
| A customer walks out, or a hotel guest is evicted | falls **sharply** |
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
unstaffed one — though a queue eats into exactly that bonus and nothing else, so a counter with
three people already headed for it is worth only about 12% more, and custom spreads itself between
your tills. Distance counts against a shop (a counter 40 tiles away is worth half one on the
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
pay for rather than sending them away empty-handed over a rounding difference. What they pay for
is what they are actually holding when they reach the counter, not the order they placed at the
shelf — if one of your haulers takes half the pile while they are walking over, they pay for the
half they carried. A customer whose purse comes up short remembers it: that item is off their list
at that counter for the rest of the visit, but they will happily try the next-best thing on the
same shelf rather than walking out. A service is one unit, so it gets no such trimming — a
customer either affords the drink or doesn't.

A single shopper also can't strip a shelf: purchases are capped at a quarter of the item's stack
limit, or one unit for anything that doesn't stack.

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

The town walks its sales floors about once a second, and that walk is what refreshes both the shelf
list and the money figures beside it — so a stack that has just been sold or burned can sit on the
pane for a moment longer. A sale, or a change on the Stock tab, re-reads it at once.

## The arrival clock

How often customers arrive is the town's own doing, not the storyteller's.

The town checks its clock every ten seconds or so. Below appeal **0.5** nothing happens at all.
Above it, the average gap between customer groups slides from **3.5 days** at the threshold down
to **0.8 days** at appeal **4.0**, divided by the *Customer volume* setting — so a town scraping
past the threshold sees a group every few days, and a booming main street sees one most days.

Appeal decides how *many* come as well as how often, and that half keeps climbing past 4.0: the
size of a group scales straight off appeal, up to a cap. So past 4.0 a better town stops getting
more frequent trade and starts getting bigger groups.

Arrivals still go out through the storyteller, which won't let events pile on top of each other:
it holds groups at least 0.6 days apart, so a booming town gets frequent trade, never a flood.
There is also a small background chance of a group turning up regardless.

### What customers bring

How rich a traveller is, is a different question from how many of them come. Each one sets out with
**120–450 silver**, multiplied by the same diminishing-returns figure appeal takes off your shelves
— never by less than 0.9, so a town's first customers can always afford its first shelf — and by
the *Customer wealth* setting. That reads the goods on offer and nothing else: not the town's
breadth, not its name, not your markup. Stock a rack of rifles and you don't get more customers,
you get customers who can afford a rifle.

## The daily ledger

At local midnight the town closes the books: every business's daily figures reset, the town's
customers, walkouts and takings for the day reset, and the day's verdicts are settled into
reputation — toward the day's service score if anyone came, or 5% back toward neutral if nobody
did. Lifetime takings are kept, per business and for the town.

The **Town ledger** button, on every counter, shows appeal broken into the three terms that make
it, which trades the town runs none of at all, what customers are turning up carrying, reputation
and today's service record as it forms, how many people came and how many gave up, today's and
lifetime takings, and each business's revenue, till and walkouts. Today's figures count people, not
receipts: somebody who gave up at three counters is one disappointed customer.

Your own people are not customers. A colonist you send for a service pays nothing, puts nothing in
the till, and leaves no row in the books — the town's takings and its name are about the strangers
who came to it.
