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

Each customer arrives with **120–450 silver**, multiplied by what the town has out on its
shelves — the market value of it, so a well-stocked town's customers carry roughly three times
what a bare one's do — and by the *Customer wealth* setting. Stock a rack of rifles and you get
customers who can afford a rifle. Note what that leaves out: a good name and a third trade bring
you more customers rather than richer ones, and raising your markup brings neither. A traveller
who already has silver on them is topped up rather than replaced.

They also arrive somewhere between **40% and 90% fed**. That is deliberate: a
[meal service](services.md#meal) wants genuinely hungry customers to sell to, not a group who all
arrive full.

## The visit

The group arrives together, shops, and leaves together. Unlike vanilla visitors — who travel to a
spot, mill about, then go — these travellers head straight for the shops. They already have
somewhere specific to be, so there is no wandering phase to get in the way of it.

Two things end the visit:

- **Time.** After about 16 in-game hours: *"The travellers from X are heading home."* Everyone
  drops what they are doing and walks — except the one customer somebody is mid-sale with, who is
  left alone to finish and pay. If their shopkeeper wanders off before it's done, they give up on
  it and follow the others out.
- **Violence.** Any customer being harmed stops every customer where they stand and sends the
  whole group for the exit: *"Violence in town. The customers from X are leaving."*

While they're here, they treat your open businesses as the centre of their visit and range about
30 tiles around it. They're allowed to eat, drink and sleep between purchases — they're here for
a day out, not a supply run.

### What the game remembers about each of them

For every customer in the group: how much silver they've spent, how many purchases they've made,
when they arrived, how many times they've walked out, which counters they've given up on for as
long as those stay unattended, and which goods a counter has already turned their purse down for.
It travels with the group and disappears when they leave, so it costs a save nothing once
they've gone.

## Choosing where to shop

A customer with money and somewhere to spend it always shops. Only when there is nothing worth
buying or using do they wander, drifting a dozen tiles or so every few seconds.

When they shop, they weigh up **every** open business they can reach — goods and services on the
same footing, with no preference for one over the other — and go to the single best:

```
      how good its prices are   (see “How price wins customers”)
   ×  1.5 if somebody is behind the counter — shared out as a crowd gathers there,
      so a free till wins the next customer
   ÷  how far away it is        (a shop 40 tiles off is worth half one on the doorstep)
   ×  for a service, how much this customer wants it right now
```

A customer with an empty purse shops nowhere at all. Beyond that, they skip a business if they
can't get to it, if nothing there is within their means, if they gave up waiting there earlier and
nobody has taken the counter since, or if the crowd already headed there would keep them waiting
more than about two and a half hours. Being turned down over one item is not the same as writing
off the shop: they remember that particular thing at that particular counter and take the
next-best stack instead.

Within a shop, they pick an item by what it's worth and how much of it there is, with a random
nudge either way. Without that tie-break a whole queue of customers would converge on the single
most expensive thing on the shelf.

### Too busy to take them

A counter you are working can have exactly what somebody wants, at a price they can pay, and
still lose the sale: if the crowd already headed there is more than they will queue behind, they
walk past and spend the silver elsewhere. A message names the counter, at most one per counter per
quarter day.

This is not a walkout — nobody was neglected and the town's name is untouched. What it costs you
is the sale, and the answer is a second counter or a second shopkeeper, not an apology.

## The purchase

1. **Walk** to the item on the shelf.
2. **Browse** for a few seconds, with a progress bar — this is what makes a busy shop read as
   busy.
3. **Pick up** the goods and carry them to the customer side of the counter.
4. **Wait their turn.** A counter serves one customer at a time, first come, first served.
   Standing behind somebody who is being served costs nothing — no patience burned, no walkout —
   because the shop is working. The patience clock only runs when there is nobody behind the
   counter at all.
5. **Be served** — a few seconds of somebody actually attending to them.
6. **Pay.**

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

- The business and the town both note it. Nothing is docked on the spot: it halves what that
  customer thinks of the town when the day is [judged at midnight](economy.md#reputation), and one
  person giving up twice is still one disappointed customer.
- They **won't go back to that counter while it is still unattended** — but put somebody behind it
  and they come back on their next thought, which is exactly what the message tells you.
- Anything they were carrying is **dropped on the floor**, unpaid.
- A message names the customer and the business.

Only **one walkout message per business at a time** is posted, so a whole group giving up at once
reads as one event in the log rather than a screenful.

Patience is restored, not just paused, whenever the counter is attended — and serving has to be
*continuous*, so a shopkeeper who drifts off mid-sale starts the sale over rather than resuming
it. There is a backstop for the shopkeeper who keeps taking the counter and losing it again, which
would otherwise stall a sale forever: a customer who has stood at one counter for hours — about
three and a half at a general store — gives up anyway, and it counts as a walkout like any
other.

### The alert

While customers are still queueing at an unattended business, a high-priority **Customers waiting
(N)** alert fires, listing the counters involved so clicking jumps the camera to them. That is
the window in which assigning a shopkeeper still saves the sale.

The alert stays quiet for a customer who could simply serve themselves while the self-service
setting is on — nobody is actually stuck. But a [haircut](services.md#haircut) never allows
self-service, so a customer waiting in a barber's chair is genuinely stuck, and still raises it.
