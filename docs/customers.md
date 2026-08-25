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
| Visit length | about 16 in-game hours — a bit over two-thirds of a day, longer for a group with guests still checked in |
| Letter | *Customers arriving (N)* |

A group **will not turn up at all** unless town appeal is at least 0.5. That is what makes the
event feel earned rather than random: no shop worth walking to means no customers.

Group size scales with appeal, and with the *Customer volume* setting.

### Which faction turns up

Every arriving group belongs to some faction, same as a vanilla visitor party — but which faction
isn't purely random. The town keeps its own **standing** with each faction it has actually done
business with, separately from the one town-wide [reputation](economy.md#reputation) number, and
a faction the town has treated well is drawn more often than one it hasn't. See
[standing with a faction](economy.md#standing-with-a-faction) for the numbers, and the town ledger
for the two relationships worth knowing about.

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

### Scheduled coach arrivals

Building a [coach depot](buildings.md#coach-depot) adds a guarantee on top of everything above:
however the ordinary arrival clock is behaving, no gap between groups — organic or scheduled —
ever runs longer than the town's current route tier allows. See [the stagecoach
line](economy.md#the-stagecoach-line) for the tiers, the numbers, and why this can never double
up with, or land on top of, an ordinary arrival.

A scheduled group is otherwise an ordinary customer group in every way that matters once they're
through the door — same shopping AI, same patience, same walkouts. All that changes is what they
bring: more silver than an ordinary arrival, and, from the weekly-coach tier up, sometimes one
**VIP passenger** riding along with the rest.

A VIP carries five times an ordinary arrival's purse and gets named in their own letter — *A VIP
passenger* — but nothing else about them is different. They queue like anyone else, they walk
out like anyone else if nobody serves them, and losing their custom costs the town exactly what
losing anyone else's does. The extra silver only buys you a bigger sale if you actually earn it.

### Gold rush prospectors

A [gold rush](economy.md#gold-rush) is otherwise the same shape again, layered on top of
everything above rather than replacing it: for as long as its boom lasts, every group that sets
out is bigger and more frequent than the same appeal would ordinarily give you, and carries
noticeably more silver on top of that. A scheduled coach group caught up in an active rush
carries both bonuses at once.

What actually sets a prospector apart isn't their purse, though — it's what they're willing to
spend it on. See [the demand basket](#the-demand-basket) for what that means for your shelves.

## The visit

The group arrives together, shops, and leaves together. Unlike vanilla visitors — who travel to a
spot, mill about, then go — these travellers head straight for the shops. They already have
somewhere specific to be, so there is no wandering phase to get in the way of it.

Two things end the visit:

- **Time.** After about 16 in-game hours: *"The travellers from X are heading home."* Everyone
  drops what they are doing and walks — except the one customer somebody is mid-sale with, who is
  left alone to finish and pay. If their shopkeeper wanders off before it's done, they give up on
  it and follow the others out. The message now also says what the group spent and what they're
  still carrying, and, when a real share of the group either never bought anything at all or gave
  up waiting for a counter, names that too — an ordinary partial-purchase visit, or a small group,
  stays quiet about all of it.
- **Violence.** Any customer being harmed stops every customer where they stand and sends the
  whole group for the exit: *"Violence in town. The customers from X are leaving."* This exit
  stays flavour-only, deliberately: an interrupted visit's leftover silver reflects when the
  interruption landed, not what the shelves had to offer, so it isn't the demand signal there
  that it is on a visit that ran its full course.

While they're here, they treat your open businesses as the centre of their visit and range about
30 tiles around it. They're allowed to eat, drink and sleep between purchases — they're here for
a day out, not a supply run.

**A group with checked-in guests waits for them.** If anyone in the party has paid for a night at
the [hotel](businesses.md#hotel), the whole group stays until every rented room is empty — a
guest still asleep (or one who's booked a room but hasn't gone to bed yet) holds up everyone
else's ride home, even after the visit's own 16-hour clock has run out. New check-ins stop being
offered once that clock runs out, so this can't stretch a visit indefinitely: every sleep job has
its own hard cap regardless of how rested the guest feels, which is what guarantees the group
eventually leaves. A guest who dies mid-stay, or one who's no longer part of the group for any
other reason, doesn't hold anyone up.

### What the game remembers about each of them

For every customer in the group: how much silver they've spent, how many purchases they've made,
when they arrived, how many times they've walked out, which counters they've given up on for as
long as those stay unattended, and which goods a counter has already turned their purse down for.
It travels with the group and disappears when they leave, so it costs a save nothing once
they've gone. A slice of it stops being invisible right at the end: the group's total spent and
held, and, when it's a real signal, how many never bought anything or gave up waiting, reach the
player in the message that sends the group home.

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
   ×  during a gold rush, whether what's on offer is what prospectors actually want
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

### The demand basket

During an active [gold rush](economy.md#gold-rush)'s boom, prospectors are all chasing the same
handful of things — tools, medicine, meals and drink — and it shows in both halves of a
purchase: which shop they walk into, and what they pick up once they're inside. An item in that
basket is worth roughly **ten times** as much to a customer's scoring as the same-priced item
sitting just outside it, so a shop stocked with what prospectors want pulls a disproportionate
share of the rush's traffic, and one stocked with none of it barely benefits from the rush at
all, whatever else it's charging. A service without a physical item behind it — a haircut, a
room for the night — scores exactly as it always did; the basket only ever judges goods, and
whatever a drink or a meal is actually poured from.

Outside an active boom — no rush running at all, or one already past its boom and into its
bust — this has no effect whatsoever: every customer scores every item exactly the way they
always have.

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

A checked-in hotel guest doesn't wait out a patience timer the same way — they've already paid,
and there's nobody to keep waiting for. Losing a room early is an **eviction** instead; see
[lodging](services.md#lodging) for how one happens, but the cost is the same as a walkout: no
refund, and a reputation hit.

### The alert

While customers are still queueing at an unattended business, a high-priority **Customers waiting
(N)** alert fires, listing the counters involved so clicking jumps the camera to them. That is
the window in which assigning a shopkeeper still saves the sale.

The alert stays quiet for a customer who could simply serve themselves while the self-service
setting is on — nobody is actually stuck. But a [haircut](services.md#haircut) never allows
self-service, so a customer waiting in a barber's chair is genuinely stuck, and still raises it.

## Trouble at the saloon and the gambling hall

A saloon left entirely to itself is a peaceful place — it's every round of [drink](services.md#drink)
it pours that isn't. A gambling hall works the same way: every hand a [wager](services.md#wager)
loses does the same thing a round of drink does. Either way it's carried as a mark on the customer
themselves, not on the business, so it travels with them from counter to counter and fades on its
own over the following hours if nobody tops it up.

Left unchecked, it climbs through three stages: **feeling good**, **getting loud**, then
**spoiling for a fight**. Cross that last line and it turns into a **disturbance**: a message
naming the patron and the business, a reputation hit worse than an ordinary
[walkout](#walkouts) — **0.05** rather than 0.02 — and that patron stops buying anything for the
rest of their visit. A meal doesn't contribute at all, and neither does a winning hand — it's
specifically the drinking, and the losing, that does it.

**At the gambling hall**, an ordinary loss adds exactly what a round of drink does, but an unlucky
one can add more: losing a hand sometimes draws a **cheating accusation** — a message naming the
gambler and the table, on top of the usual rowdiness — and a skilled dealer draws noticeably fewer
of them, the same Social skill that already slows rowdiness everywhere else cutting how often a
loss turns into an accusation as well. Worse than either is the one outcome that isn't a loss at
all: the table **winning** for a gambler and then not being able to pay them in full. See
[the wager](services.md#wager) and [the till as a bankroll](economy.md#the-till-as-a-bankroll) for
the numbers behind all three.

Two things slow the climb, and they stack:

- A **sheriff** actually on duty at a [sheriff's office](buildings.md#sheriffs-office) roughly
  halves it, town-wide, for as long as they're standing the post — at any business, not just a
  saloon.
- A **skilled shopkeeper** working the counter — the bartender pouring drinks, or the dealer
  running the table — also slows it, the better their Social skill, the closer to half. Leave the
  counter unstaffed and patrons get the full, undiscounted rate; self-serve drinks buy you nothing
  here, and a wager is never self-service to begin with.

### The alert

While a patron is "getting loud" and still calmable, a **Rowdy patrons (N)** alert fires, the
same shape as [Customers waiting](#the-alert) — a count, and a camera jump to whoever's involved.
That's the sheriff's real window: once a patron crosses into "spoiling for a fight" the
disturbance has already happened by the time anything could react to it. An assigned,
**on-duty** sheriff can walk over and talk a specific patron down before they get there — see
[sheriffing](shopkeeping.md#sheriffing) for how a colonist takes up that post in the first
place.

## Hospitality guests

If you also run the Hospitality mod — it isn't required, and everything above works exactly the
same without it — a guest Hospitality is already putting up for the night can wander over and do
business with you too: buying something off a shelf, ordering a drink or a meal, or sitting for
a haircut, the same as a travelling customer would.

This only ever happens while the guest genuinely has nothing else to do. Hospitality's own
routine for them — checking in, eating, socializing, sleeping — always comes first, and a guest
busy with any of that is never interrupted. Roughly every six in-game minutes, if one of your
businesses has something to offer and a guest happens to be idle right then, they may be sent on
a single shopping trip.

A Hospitality guest **never rents one of your hotel rooms** — Hospitality is already housing them
for the night, and there's no way for the two systems to end up disagreeing about where a guest
is supposed to sleep.

Recognizing a Hospitality guest at all rests on how Hospitality's own mod happens to be built —
something this mod has never actually been tested alongside a real copy of Hospitality to
confirm. If that turns out to be wrong, nothing breaks: guests from Hospitality simply never
wander over, exactly as if this feature didn't exist. The first time it *does* work in a save,
you'll see a one-off message naming the guest and the business.

Two settings control this, both under **Old West Town** in the mod settings menu, and both only
shown at all once Hospitality is actually detected:

- **Let Hospitality guests shop** — the on/off switch for the whole thing. On by default.
- **Give Hospitality guests spending money** — tops up a guest's pockets the same way an
  arriving customer's are, if they aren't already carrying enough to buy anything. Turn it off
  to only let guests spend silver they already have on them.
