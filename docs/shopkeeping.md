---
title: Shopkeeping
summary: The work type, when a counter actually asks for staff, and what running an honesty box costs you.
---

## The work type

**Shopkeep** — *"Stand behind a shop counter and serve customers who come to town."*

| | |
| --- | --- |
| Column in the Work tab | shopkeep |
| What a colonist doing it is called | shopkeeper |
| Where it sits by default | fairly low in the natural priority order |
| Switched on for new colonists | yes |
| Relevant skill | **Social** |
| Needs | working hands and the ability to talk |

Because it is on by default but sits fairly low in the natural order, a busy colonist may never
reach it. Give the work an explicit priority in the Work tab if you want a counter reliably
staffed.

Serving a customer — goods or a service alike — grants **35 Social XP**, so a dedicated
shopkeeper trains the skill their job runs on.

The same priority staffs a [gambling hall](businesses.md#gambling-hall)'s faro table with zero new
mechanics — a dealer is a shopkeeper like any other. Social skill does double duty there: it
already slows how fast every business's patrons get rowdy, and for a dealer specifically it also
cuts how often an unlucky loss turns into a [cheating
accusation](customers.md#trouble-at-the-saloon-and-the-gambling-hall).

## Sheriffing

Shopkeeping is a *priority* — any colonist who has it can staff any counter. **Sheriffing** is a
*post* — one specific colonist, and only that colonist, does anything with it at all. The two are
built the same way a game gives you a floor job and a named role: assigning nobody to a
[sheriff's office](buildings.md#sheriffs-office) leaves the Sheriffing column sitting there,
switched off, doing nothing for every other colonist in the colony.

Getting a sheriff on duty is two separate steps:

1. **Assign the post.** Right-click the sheriff's office and choose **Assign sheriff**, the same
   gizmo you'd use to assign an owner to a throne or a grave. Only one colonist can hold it at a
   time; assigning a second person first requires unassigning the one already there.
2. **Give them the priority.** The assigned colonist still needs Sheriffing switched on in their
   own Work tab, same as any other job — being assigned the post doesn't put them to work on its
   own, it just makes them *eligible* to be.

| | |
| --- | --- |
| Column in the Work tab | sheriff |
| What a colonist doing it is called | sheriff |
| Switched on for new colonists | **no** — it would sit inert for everyone but the one badge-holder |
| Relevant skill | **Social** |
| Needs | working hands and the ability to talk |

With both in place, the sheriff patrols their office and steps in to calm down
[rowdy patrons](customers.md#trouble-at-the-saloon-and-the-gambling-hall) before they cause a disturbance — walking
someone down grants **35 Social XP**, the same training a shopkeeper earns for a sale. Reassign
the post, or take away their Sheriffing priority, and they simply stop — there's no notice to
give and nothing to undo.

An on-duty sheriff also has a second, unrelated job: they lower how often a
[stickup](outlaws.md) happens at all, and shorten one that does. That's a passive effect of being
on duty, not a new patrol or a combat job — the Shopkeeping work type itself is untouched by any
of this. Staffing a counter, on its own, does nothing to protect its till.

## When a counter asks for staff

Any business with something to offer will ask for staff; it doesn't matter what kind it is. A
colonist is offered the job when **all** of these are true:

- the business is **open**;
- nobody else is already working it;
- the staff side is clear, reachable, and not claimed by another colonist;
- the business **has something to offer** — stock on the shelf or an available service;
- there is at least one **visitor within 25 tiles who could still spend something** — awake, not
  hostile, and with silver left in their purse — including a [Hospitality
  guest](customers.md#hospitality-guests) the bridge has already sent your way, though recognizing
  one takes a little longer than a native customer since there's no arrival moment to key off.

Somebody who has spent their last coin, or who is having a nap on your floor, does not hold a
colonist at a counter that cannot make a sale. The exception is anyone already standing at *this*
counter: a customer part-way through a sale, or one of your own colonists sent for a haircut,
counts whatever their purse says — which is what gets somebody posted to serve them.

That last condition is why nobody stands behind an empty store all day. Right-clicking the
counter and prioritizing it by hand skips the stock and customer checks entirely, so you can post
a colonist at a counter ahead of a group you're expecting — though not indefinitely. With nobody
in sight the shopkeeper gives the post up after about half an in-game hour and goes back to other
work, whether you sent them by hand or not.

## What "staffed" means

A shopkeeper and a customer never talk to each other directly, and never run paired jobs. They
each just read and write the counter's own state: whether it's open, what it charges, what's for
sale, and whether somebody is currently standing at it.

The shopkeeper's job says "I'm here" to the counter continuously while they stand at it. The
counter counts itself **staffed** while that is less than a second old and the shopkeeper is
still alive — a small grace window, so a momentary hitch doesn't read as the shop being
abandoned.

One shopkeeper serves one person at a time, in the order they walked up; the counter's inspect
pane names who is at the counter and how many are waiting their turn. Waiting a turn behind
somebody who is being served costs a customer no patience, so a queue is not a failure you have
to fix. What it costs you is the people who [walk past rather than join
it](customers.md#too-busy-to-take-them) — a second counter is what serves two at once.

Neither side can strand the other. A shopkeeper who wanders off simply leaves the counter
unstaffed; the waiting customer notices, runs down their patience, drops whatever they were
carrying and [leaves annoyed](customers.md#walkouts). That failure is *visible to you* — a
message, and one disappointed caller in tonight's reckoning of the town's name — which turns a
robustness measure into a game mechanic. It is not final, either: they won't queue at that counter
again while it is still unattended, but put somebody behind it and they come back.

A business trades only when it is **open, staffed, powered** (if it needs power at all) and has
something to offer.

## Self-service

The *Allow self-service* mod setting (off by default) turns every counter into an honesty box:
customers buy from an unattended counter instead of walking out.

It is convenient, and it costs you something — not a sale, but the welcome. At midnight the town
weighs up everyone who came to a counter that day, and somebody who helped themselves at an
unwatched one counts half of somebody who was served across it. One attended sale is enough to
make that customer count in full. So a town run entirely on honesty boxes never earns a good name;
it settles for a middling one. Nobody remembers a shop nobody works.

Three things limit it:

- Each service decides for itself whether the setting applies. Drink and meal allow it; **a
  haircut never does**, whatever the setting says — an empty chair can't cut anyone's hair.
- Self-service sales never train Social, because no colonist was involved.
- It never covers your own people. A colonist you send for a haircut waits for somebody else to
  stand the chair, whatever the setting says — the honesty box is paid for in reputation, and a
  colonist leaves none behind to pay with.

## Closing a business

The **Open for business** toggle on any counter closes it. A closed business:

- is ignored by customers entirely;
- drops any sale it was in the middle of — the customer leaves and whatever they were holding
  lands on the floor, unpaid. The town's own closing time spares a sale already being worked; this
  switch does not, so flip it between customers where you can;
- never asks a colonist to staff it;
- contributes **nothing** to town [appeal](economy.md#appeal);
- still keeps its till, stock settings, markup and ledger.

Closing does not stop your colonists using the room for anything else.
