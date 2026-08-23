---
title: Customers
summary: Where they come from, what they carry, how they choose a shop, and what happens when nobody serves them.
---

## Arrival

Customers arrive as a group through the `OWT_ShopCustomers` incident, which is a neutral-group
incident like a vanilla visitor party — but its size and its purse are a direct function of what
the player has built.

| | |
| --- | --- |
| Def | `OWT_ShopCustomers` |
| Category | Misc, `Map_PlayerHome` |
| Base chance | 3 (background trickle; most arrivals come from the [appeal clock](economy.md#the-arrival-clock)) |
| Min refire | 0.6 days |
| Group kind | `PawnGroupKindDefOf.Peaceful` |
| Visit length | 40,000 ticks — a bit over two-thirds of a day |
| Letter | *Customers arriving (N)* |

The incident **refuses to fire** unless town appeal is at least 0.5. That is what makes the
event feel earned rather than random: no shop worth walking to means no customers.

Group size scales with appeal: `points = clamp(appeal × 60 × volumeSetting, 40, 900)`.

### What they bring

Each customer is topped up to a purse of `RandomInRange(120, 450)` silver, scaled by
`lerp(0.7, 2.2, clamp01(appeal / 4))` and by the *Customer wealth* setting, with a floor of 20.
Richer towns attract richer custom, so investment in the town compounds rather than just adding
more footfall. Pawns who already carry silver are topped up rather than replaced.

Their food need is randomized to **40%–90%** on arrival. That is deliberate: a
[meal service](services.md#meal) wants genuinely hungry customers to sell to, not a group who
all arrive fully fed.

## The visit

The group is handed to `LordJob_ShopVisit`, whose state graph is deliberately **flat** —
one shopping state and an exit, rather than the travel/chill/exit chain vanilla visitors use.
Customers already have a reason to walk somewhere specific (the shop they picked), so an extra
travel state would only fight the shopping AI for control of where they stand.

Two things end the visit:

- **Time.** After 40,000 ticks: *"The travellers from X are heading home."*
- **Violence.** Any customer being harmed ends every customer's job and sends the whole group
  for the exit: *"Violence in town. The customers from X are leaving."*

The lord's toil hands every member the `OWT_Shop` duty, centred on the average position of the
town's open businesses, with a 30-cell shopping radius. Long needs are allowed — customers are
here for a day out, so they may eat, drink and rest between purchases.

### Per-customer records

Each customer has a `CustomerRecord` hanging off the lord, **not** off the pawn: silver spent,
purchases made, arrival tick, walkouts, and the list of businesses they have given up on. Living
on the lord means it saves and loads with the group, needs no def patching of humanlike pawns,
and disappears when the visit does.

## Choosing where to shop

The `OWT_Shop` duty's think tree is two nodes, in priority order:

1. `JobGiver_BuyFromShop` — buy something or use a service.
2. `JobGiver_WanderNearDutyLocation` — wander within 12 cells, every 180–420 ticks.

So a customer with money and somewhere to spend it always shops, and only loiters when there is
nothing to buy or use.

`JobGiver_BuyFromShop` scores **every** reachable open business, considering goods and services
on the same footing with no ordering bias, and takes the single best:

```
goods:    score = valueAppeal(shop) × staffBonus / distanceFactor
service:  score = valueAppeal(shop) × worker.Desirability(customer) × staffBonus / distanceFactor

  valueAppeal    = clamp(1 / (markup × reputationFactor), 0.1, 2.0)
  staffBonus     = 1.5 if someone is behind the counter, else 1.0
  distanceFactor = 1 + distance / 40
```

A customer skips a business entirely if they have no silver, if the business is on their refused
list, if they can't path to it, or if nothing there is within their means.

Within a business, the item chosen is scored by `unitValue × quantity × random(0.6, 1.4)`. The
random tie-break matters: without it a queue of customers all converge on the single most
expensive item on the shelf.

## The purchase

`JobDriver_BuyFromShop`:

1. **Walk** to the item on the shelf.
2. **Browse** for 240 ticks with a progress bar — this is what makes a busy shop read as busy.
3. **Pick up** the goods and carry them to the customer cell.
4. **Wait to be served** — 180 continuous staffed ticks.
5. **Pay.**

Everything is re-validated at the moment of payment, because the walk from shelf to counter gives
the world plenty of time to invalidate what the customer decided a minute ago: an item pulled
from the Stock filter or forbidden mid-carry is no longer for sale, and the customer leaves it
behind.

A [service visit](services.md#how-a-service-visit-runs) is the same shape, with the fetch step
skipped for a service that consumes nothing.

## Walkouts

If nobody serves a customer within the business kind's `customerPatienceTicks` — 2500 for a
general store, 2200 for a barber, **1500 for a saloon** — they give up.

What happens:

- The business and the town both record a walkout; town reputation drops **0.02**.
- The customer adds the business to their **refused list** and will not queue there again this
  visit — it clearly isn't being worked.
- Anything they were carrying is **dropped on the floor**, unpaid.
- A message names the customer and the business.

Only **one walkout message per business per patience window** is posted, so a whole group giving
up at once reads as one event in the log rather than a screenful.

Patience is restored, not just paused, whenever the counter is attended — and service has to be
*continuous*, so a shopkeeper who drifts off mid-sale starts the serve over rather than resuming
it.

### The alert

While customers are still queueing at an unattended business, a high-priority **Customers
waiting (N)** alert fires, listing the affected counters as culprits so clicking jumps the camera
to them. That is the window in which assigning a shopkeeper still saves the sale.

The alert stays silent for a patron whose service honours the global self-service setting while
it is on — nobody is actually stuck there. But a service that opts out of the setting (a
[haircut](services.md#haircut), always) still leaves its patron stuck, and still raises it.
