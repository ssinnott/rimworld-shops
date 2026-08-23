---
title: Shopkeeping
summary: The work type, when a counter actually asks for staff, and what running an honesty box costs you.
---

## The work type

`OWT_Shopkeeping` — *"Stand behind a shop counter and serve customers who come to town."*

| | |
| --- | --- |
| Short label | shopkeep |
| Pawn label | shopkeeper |
| Natural priority | 460 |
| Active by default | yes (`alwaysStartActive`) |
| Relevant skill | **Social** |
| Work tags | Social |
| Required capacities | Manipulation, Talking |

Because it is on by default but sits fairly low in the natural priority order, a busy colonist
may never reach it. Give the work an explicit priority in the Work tab if you want a counter
reliably staffed.

Serving a customer — goods or a service alike — grants **35 Social XP**, so a dedicated
shopkeeper trains the skill that gates the job.

## When a counter asks for staff

`WorkGiver_ManShop` is kind-agnostic: it will staff any business with something to offer. It
offers the job when **all** of these are true:

- the business is **open**;
- nobody else is already working it;
- the staff cell is in bounds and standable, and the colonist can reserve and reach it;
- the business **has something to offer** — stock on the shelf or an available service;
- there is at least one **non-hostile visitor holding the `OWT_Shop` duty within 25 cells**.

That last condition is why nobody stands behind an empty store all day. A **forced** job
(right-click → prioritize) skips the stock and customer checks entirely, so you can post a
colonist at a counter ahead of an expected group.

## What "staffed" means

The two pawn loops — a colonist working a counter, a customer buying something — never talk to
each other directly. They only read and write shared state on the business:

```
 colonist                       CompBusiness                         customer
 ────────                       ────────────                         ────────
 WorkGiver_ManShop              open / closed                        JobGiver_BuyFromShop
   ↓  picks a business          markup                                 ↓  picks goods or a service
 JobDriver_ManShop              stock filter, services   ← reads →   JobDriver_BuyFromShop /
   └─ every tick:                                                    JobDriver_UseService
      NotifyStaffedBy(pawn) ──→ lastStaffedTick                        ├─ walk to shelf
                                     │                                 ├─ wait to be served
                                     └──────── Staffed ───────────────→└─ pay
```

The shopkeeper's job pings `NotifyStaffedBy` every tick it stands at the counter. `Staffed` is
true while that ping is **less than 60 ticks old** and the shopkeeper is alive — a one-second
grace window, so a momentary hitch doesn't read as abandonment.

Neither driver can strand the other. A shopkeeper who wanders off just flips `Staffed` to false;
the customer's wait toil notices, runs down its patience, drops whatever it is carrying and
[leaves annoyed](customers.md#walkouts). That failure is *legible to the player* — a message and
a reputation hit — which turns a robustness measure into a game mechanic.

A business trades only when it is **open, staffed, powered** (if it has a power comp at all) and
has something to offer.

## Self-service

The *Allow self-service* mod setting (off by default) turns every counter into an honesty box:
customers buy from an unattended counter instead of walking out.

It is convenient, and it has a price. Every self-service sale costs **0.005 reputation** instead
of earning 0.01 — so an unstaffed town slowly slides even while the till fills. Nobody remembers
a shop nobody works.

Two things limit it:

- A service opts in individually via `ServiceDef.allowsSelfService`. Drink and meal allow it;
  **a haircut never does**, whatever the setting says — an empty chair can't cut anyone's hair.
- Self-service sales never train Social, because no colonist was involved.

## Closing a business

The **Open for business** toggle on any counter closes it. A closed business:

- is skipped by the customer AI entirely;
- is skipped by `WorkGiver_ManShop`, so no colonist walks to it;
- contributes **nothing** to town [appeal](economy.md#appeal);
- still keeps its till, filter, markup and ledger.

Closing does not stop your colonists using the room for anything else.
