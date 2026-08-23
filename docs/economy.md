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
| Every day | drifts 5% back toward 0.5 |

A walkout costs twice what a sale earns, so a counter you leave unattended during a busy visit
loses ground fast. And because reputation decays toward neutral every day, a town has to keep
earning its name — a burst of good trade a quadrum ago doesn't hold the number up.

Reputation feeds two things:

- **Appeal**, through the standing multiplier above — so it changes how many customers come.
- **What everything actually costs.** Reputation shades every price either side of the markup you
  set: a town with a bad name sells at **15% above** its slider, a well-liked one at **10%
  below** it. So a good name both brings more customers in and prices a little keener for them.

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

### Partial purchases

If a customer can't afford the whole stack they wanted, the order is trimmed to what they can
pay for rather than sending them away empty-handed over a rounding difference. A service is one
unit, so it gets no such trimming — a customer either affords the drink or doesn't.

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

## The daily ledger

At local midnight the town closes the books: every business's daily figures reset, the town's
sales, walkouts and takings for the day reset, and reputation drifts back toward neutral.
Lifetime takings are kept, per business and for the town.

The **Town ledger** button, on every counter, shows appeal, reputation, today's sales, walkouts
and takings, lifetime takings, and each business's revenue and till — the numbers the economy
runs on, readable in one place.
