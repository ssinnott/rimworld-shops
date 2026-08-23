---
title: The town economy
summary: Appeal, reputation, pricing and the arrival clock — the four numbers that decide whether a shop becomes a town.
---

`TownEconomy` is a `MapComponent`: one per map, holding the live register of businesses, the
daily ledger, the town's reputation, and the clock that decides when customers set out. It is
the only place the mod's economic numbers are computed, and the **Town ledger** gizmo on any
counter shows them all in one dialog.

## Appeal

**Appeal** is how much trade the town attracts — roughly 0 to 3+. It is recomputed on demand
from what the player has actually built.

```
kindScore  = Σ over open businesses with something to offer:
                 kind.appeal × (1.0 for the first business of a kind, 0.35 for each repeat)

stockScore = Σ  shopStockValue + serviceValue × 30

goods      = clamp( sqrt(stockScore / 1000), 0.25, 3.0 )
standing   = lerp(0.5, 1.5, reputation)

Appeal     = kindScore × goods × standing        (0 if kindScore is 0)
```

Four things follow from that formula, and they are the whole strategic layer:

**Breadth beats depth.** A second business of a kind you already run counts for only 35% of the
first. One giant general store should not out-earn a street with a store, a saloon and a barber.
That is the pressure that turns a colony into a town.

**Wealth has diminishing returns.** The square root on `stockScore` means doubling what's on
your shelves does not double your draw. Getting the first shelf stocked matters enormously;
getting the tenth matters much less.

**Reputation is a multiplier, not a bonus.** `standing` runs from 0.5 to 1.5, so a well-run town
draws *three times* the trade of a badly-run one with identical stock.

**A closed or empty business contributes nothing.** Appeal only counts businesses that are open
and have something to offer — stock on the shelf, or an available service.

> **Why services are weighted ×30.** A stock-free service has no "quantity on the shelf" the way
> physical stock does; its value is the price of one visit, an order of magnitude below a shelf's
> total. Scaling it up before it joins the same wealth curve means one barber shop can clear the
> customer threshold on its own, the way a modestly-stocked general store already can, instead
> of being drowned out by a normalization tuned for physical stock.

### Appeal is cached, roughly

`StockOnDisplay` — which appeal walks for every open business — is recomputed at most once a
second per business, because the customer AI asks about it constantly while choosing where to
shop. That is fine for a main street and would want revisiting for a hundred counters.

## Reputation

**Reputation** is a rolling satisfaction score from 0 to 1, starting at **0.5**.

| Event | Change |
| --- | --- |
| A staffed sale or service | **+0.01** |
| A self-service sale | **−0.005** |
| A customer walks out | **−0.02** |
| Daily rollover | drifts 5% back toward 0.5 |

A walkout costs twice what a sale earns, so a counter you leave unattended during a busy visit
loses ground fast. And because reputation decays toward neutral every day, a town has to keep
earning its name — a burst of good trade a quadrum ago doesn't hold the number up.

Reputation feeds two things:

- **Appeal**, through the `standing` multiplier above — so it changes how many customers come.
- **Price tolerance**, through `ReputationPriceFactor` = `lerp(1.15, 0.9, reputation)`. A town
  customers like will bear **15% above** your set markup; one with a bad name has to sell at
  **10% below** it to move goods.

## Pricing

Every price in the mod — for goods or a service — is decided in one place, so the UI, the
customer AI and the transaction can never disagree.

```
unit price = thing.MarketValue × shop.Markup × ReputationPriceFactor
```

`MarketValue` already folds in quality, stuff and remaining hit points, which is exactly what a
shopper would judge. A stock-free service substitutes `ServiceDef.basePrice` for the market
value; nothing else changes. No price is ever below **1 silver**.

### How price wins customers

A customer scoring a business uses `ValueAppeal` — `1 / effectiveMarkup`, clamped to 0.1–2.0.
So a shop charging market value scores 1.0, one charging double scores ~0.5, one charging triple
scores ~0.33. Multiply that by a **×1.5 staffed bonus**, divide by a distance penalty
(`1 + distance/40`), and for a service also multiply by the worker's desirability.

The practical upshot: **undercutting a rival shop genuinely pulls customers away from it**, and
a staffed counter beats an unstaffed one worth 50% more.

### Partial purchases

If a customer can't afford the whole stack they wanted, the order is trimmed to what they can
pay for rather than sending them away empty-handed over a rounding difference. A service is one
unit, so it gets no such trimming — a customer either affords the drink or doesn't.

A single shopper also can't strip a shelf: purchases are capped at a quarter of the item's stack
limit, or one unit for anything unstackable.

## What counts as stock

The sales floor is a **room**, not a zone you paint. A shop is defined by walls you already
built: it reads naturally, it costs nothing to set up, and it makes the room-quality stats you
already care about matter commercially.

Indoors, everything inside the counter's room is on display. Outdoors — or in a room that
touches the map edge — it falls back to a radius (`openAirRadius`, 9.9 for a shop counter, 7.9
for a saloon bar), so a market stall on the boardwalk still trades.

An item is on display if **all** of these hold:

- it is a spawned, undestroyed item with a stack count above zero;
- it is **not silver** (selling silver for silver is nonsense, and no filter can enable it);
- it is **not forbidden** — forbidding is the player's way of saying "not for sale";
- it is **not reserved by a colonist** — goods a hauler is already on the way for would churn
  both jobs if sold out from under them;
- it is not burning;
- its market value is above zero;
- the business's **Stock filter** allows it.

## The arrival clock

Arrival frequency is the town's own doing, not the storyteller's.

`TownEconomy` checks its clock every 600 ticks. Below appeal **0.5** nothing happens at all.
Above it, the mean time between customer groups is
`lerp(3.5 days, 0.8 days, clamp01((appeal − 0.5) / 3.5))`, divided by the *Customer volume*
setting — so a town scraping past the threshold sees a group every few days, and a booming main
street sees one most days.

The incident is fired *through the storyteller*, so `minRefireDays` (0.6) still caps the
combined rate. A booming town gets frequent groups, never a flood of them. The `IncidentDef`
also keeps a small `baseChance` of 3 as a background trickle.

## The daily ledger

At local midnight, `TownEconomy` rolls the day: every business's daily counters reset,
the town's `revenueToday` / `customersServedToday` / `walkoutsToday` reset, and reputation
drifts toward neutral. Lifetime revenue is kept, per-business and town-wide.

The **Town ledger** gizmo, on every counter, shows appeal, reputation, today's sales, walkouts
and takings, lifetime takings, and each business's revenue and till — the numbers the economy
runs on, readable in one place.
