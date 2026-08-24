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

## When a counter asks for staff

Any business with something to offer will ask for staff; it doesn't matter what kind it is. A
colonist is offered the job when **all** of these are true:

- the business is **open**;
- nobody else is already working it;
- the staff side is clear, reachable, and not claimed by another colonist;
- the business **has something to offer** — stock on the shelf or an available service;
- there is at least one **visiting customer within 25 tiles**.

That last condition is why nobody stands behind an empty store all day. Right-clicking the
counter and prioritizing it by hand skips the stock and customer checks entirely, so you can post
a colonist at a counter ahead of a group you're expecting.

## What "staffed" means

A shopkeeper and a customer never talk to each other directly, and never run paired jobs. They
each just read and write the counter's own state: whether it's open, what it charges, what's for
sale, and whether somebody is currently standing at it.

The shopkeeper's job says "I'm here" to the counter continuously while they stand at it. The
counter counts itself **staffed** while that is less than a second old and the shopkeeper is
still alive — a small grace window, so a momentary hitch doesn't read as the shop being
abandoned.

Neither side can strand the other. A shopkeeper who wanders off simply leaves the counter
unstaffed; the waiting customer notices, runs down their patience, drops whatever they were
carrying and [leaves annoyed](customers.md#walkouts). That failure is *visible to you* — a
message and a reputation hit — which turns a robustness measure into a game mechanic.

A business trades only when it is **open, staffed, powered** (if it needs power at all) and has
something to offer.

## Self-service

The *Allow self-service* mod setting (off by default) turns every counter into an honesty box:
customers buy from an unattended counter instead of walking out.

It is convenient, and it has a price. Every self-service sale costs **0.005 reputation** instead
of earning 0.01 — so an unstaffed town slowly slides even while the till fills. Nobody remembers
a shop nobody works.

Two things limit it:

- Each service decides for itself whether the setting applies. Drink and meal allow it; **a
  haircut never does**, whatever the setting says — an empty chair can't cut anyone's hair.
- Self-service sales never train Social, because no colonist was involved.

## Closing a business

The **Open for business** toggle on any counter closes it. A closed business:

- is ignored by customers entirely;
- never asks a colonist to staff it;
- contributes **nothing** to town [appeal](economy.md#appeal);
- still keeps its till, stock settings, markup and ledger.

Closing does not stop your colonists using the room for anything else.
