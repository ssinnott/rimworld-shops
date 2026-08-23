---
title: Customers
summary: Where they come from, what they carry, how they choose a shop, and what happens when nobody serves them.
---

## Arrival

Customers arrive as a group, much like a vanilla visitor party — except that how many turn up and
how much they carry is a direct result of what you have built.

| | |
| --- | --- |
| How often | Mostly set by the [appeal clock](economy.md#the-arrival-clock), plus a small background chance |
| Minimum gap between groups | 0.6 days |
| Visit length | about 16 in-game hours — a bit over two-thirds of a day |
| Letter | *Customers arriving (N)* |

A group **will not turn up at all** unless town appeal is at least 0.5. That is what makes the
event feel earned rather than random: no shop worth walking to means no customers.

Group size scales with appeal, and with the *Customer volume* setting.

### What they bring

Each customer arrives with **120–450 silver**, multiplied by how appealing the town is (a
thriving town's customers carry roughly three times what a struggling town's do) and by the
*Customer wealth* setting. Richer towns attract richer custom, so investment in the town
compounds rather than just adding more footfall. A traveller who already has silver on them is
topped up rather than replaced.

They also arrive somewhere between **40% and 90% fed**. That is deliberate: a
[meal service](services.md#meal) wants genuinely hungry customers to sell to, not a group who all
arrive full.

## The visit

The group arrives together, shops, and leaves together. Unlike vanilla visitors — who travel to a
spot, mill about, then go — these travellers head straight for the shops. They already have
somewhere specific to be, so there is no wandering phase to get in the way of it.

Two things end the visit:

- **Time.** After about 16 in-game hours: *"The travellers from X are heading home."*
- **Violence.** Any customer being harmed stops every customer where they stand and sends the
  whole group for the exit: *"Violence in town. The customers from X are leaving."*

While they're here, they treat your open businesses as the centre of their visit and range about
30 tiles around it. They're allowed to eat, drink and sleep between purchases — they're here for
a day out, not a supply run.

### What the game remembers about each of them

For every customer in the group: how much silver they've spent, how many purchases they've made,
when they arrived, whether they've walked out of anywhere, and which businesses they've given up
on. It travels with the group and disappears when they leave, so it costs a save nothing once
they've gone.

## Choosing where to shop

A customer with money and somewhere to spend it always shops. Only when there is nothing worth
buying or using do they wander, drifting a dozen tiles or so every few seconds.

When they shop, they weigh up **every** open business they can reach — goods and services on the
same footing, with no preference for one over the other — and go to the single best:

```
      how good its prices are   (see “How price wins customers”)
   ×  1.5 if somebody is behind the counter
   ÷  how far away it is        (a shop 40 tiles off is worth half one on the doorstep)
   ×  for a service, how much this customer wants it right now
```

A customer skips a business entirely if they have no silver, if they've already given up on it
this visit, if they can't get to it, or if nothing there is within their means.

Within a shop, they pick an item by what it's worth and how much of it there is, with a random
nudge either way. That random tie-break matters: without it, a whole queue of customers would
converge on the single most expensive thing on the shelf.

## The purchase

1. **Walk** to the item on the shelf.
2. **Browse** for a few seconds, with a progress bar — this is what makes a busy shop read as
   busy.
3. **Pick up** the goods and carry them to the customer side of the counter.
4. **Wait to be served** — a few seconds of somebody actually being there.
5. **Pay.**

Everything is checked again at the moment of payment, because the walk from shelf to counter
gives the world plenty of time to invalidate what the customer decided a minute ago: an item
switched off in the Stock tab or forbidden mid-carry is no longer for sale, and the customer
leaves it behind.

A [service visit](services.md#how-a-service-visit-runs) is the same shape, with the fetch step
skipped for a service that uses up nothing.

## Walkouts

If nobody serves a customer within their patience — about an in-game hour at a general store, 50
minutes at a barber, **35 minutes at a saloon** — they give up.

What happens:

- The business and the town both record a walkout; town reputation drops **0.02**.
- The customer will **not queue there again this visit** — it clearly isn't being worked.
- Anything they were carrying is **dropped on the floor**, unpaid.
- A message names the customer and the business.

Only **one walkout message per business at a time** is posted, so a whole group giving up at once
reads as one event in the log rather than a screenful.

Patience is restored, not just paused, whenever the counter is attended — and serving has to be
*continuous*, so a shopkeeper who drifts off mid-sale starts the sale over rather than resuming
it.

### The alert

While customers are still queueing at an unattended business, a high-priority **Customers waiting
(N)** alert fires, listing the counters involved so clicking jumps the camera to them. That is
the window in which assigning a shopkeeper still saves the sale.

The alert stays quiet for a customer who could simply serve themselves while the self-service
setting is on — nobody is actually stuck. But a [haircut](services.md#haircut) never allows
self-service, so a customer waiting in a barber's chair is genuinely stuck, and still raises it.
