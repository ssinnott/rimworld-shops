---
title: Getting started
summary: From an unresearched colony to silver in the till, in seven steps.
---

## Before anything else

Old West Town adds nothing to a colony until you research it. Almost everything in the mod is
gated behind one project.

| | |
| --- | --- |
| **Research** | Frontier commerce |
| **Cost** | 500 |
| **Tech level** | Medieval |
| **Unlocks** | The whole **Commerce** build category — every counter, the [sheriff's office](buildings.md#sheriffs-office), the hotel bed, boardwalk and the rest of [main street](buildings.md#main-street) |

There are no prerequisites, so it is available from the first day of a colony if you want it.

One building sits behind a second research on top of this one: the [coach
depot](buildings.md#coach-depot) needs **Stagecoach line** (cost 800), which itself needs
Frontier commerce first. It's a milestone building for a town that's already trading, not a
starter one.

## The seven steps

### 1. Research Frontier commerce

Scales, a ledger, a lockable till and the habit of writing prices down.

### 2. Build a business

Under the new **Commerce** category in the build menu. Start with a [shop
counter](buildings.md#shop-counter) — it is the cheapest and the most forgiving.

**Rotation matters.** One side of a counter is the **staff** side and the other is the
**customer** side, and rotating the counter is how you choose which is which. Rotate it so the
staff side faces into the shop's back room; customers will queue on the opposite side. Get this
backwards and your shopkeeper stands in the street.

### 3. Put goods in the same room

Anything sellable in the counter's room is on display. Shelves work; so does the floor. If the
counter is outdoors or in a room that touches the map edge, the sales floor falls back to a
radius around it instead, so a market stall on the boardwalk still trades. See [what counts as
stock](economy.md#what-counts-as-stock) for the exact rules.

### 4. Choose what it sells

Open the counter's **Stock** tab. It is the same allowed-items list a stockpile uses, and reads
exactly the same way. Each business kind starts with a sensible default selection — a general
store opens with foods, manufactured goods, raw resources, medicine, apparel, textiles and
leathers switched on. **Reset** puts the defaults back.

Silver is never sellable, whatever the filter says.

### 5. Set a price

The **Set prices** button opens a markup slider. It is a percentage of each item's market
value — 100% is break-even against a trader, and the default for a general store is 135%.

Cheaper genuinely wins trade: customers weigh up every shop they can reach and pick the best,
and price is the biggest thing they weigh. Undercutting the shop across the street pulls its
customers to you. See [pricing](economy.md#pricing).

### 6. Assign a shopkeeper

Give a colonist a priority on the **Shopkeep** work type in the Work tab. It is switched on by
default for new colonists, but it sits low in the natural priority order, so a busy colonist may
never get to it — give it an explicit priority if you want the counter reliably worked.

A colonist will only walk to a counter that is open, has something to offer, and has a customer
within 25 tiles. Nobody stands behind an empty store all day. You can override that by
right-clicking the counter and prioritizing the job by hand.

### 7. Wait for customers

Once the town's [appeal](economy.md#appeal) passes **0.5**, groups start arriving on their own —
roughly one group every 3.5 days at that threshold, most days once the town is thriving. You'll
get a *Customers arriving* letter.

Then watch the till. The counter's inspect pane shows what's on sale, the markup, the till and
today's takings; **Collect takings** drops the silver on the floor for a hauler.

## If nothing is happening

| Symptom | Likely cause |
| --- | --- |
| No *Customers arriving* letter, ever | Town appeal is under 0.5. Stock more, or open a second *different* kind of business — [breadth counts for more than depth](economy.md#appeal). |
| Customers arrive, browse, then leave without buying | Nobody behind the counter. Check the *Customers waiting* alert, and see [shopkeeping](shopkeeping.md). |
| A colonist never picks up shopkeeping | The counter has nothing to offer, no customer is within 25 tiles, or Shopkeep is outranked by other work. |
| Goods in the room aren't listed as on sale | They're forbidden, reserved by a colonist's own job, worth nothing, on fire, or switched off in the Stock tab. |
| A saloon serves drinks but never meals | A meal service needs an actual cooked meal on the shelves. Ingredients don't count. |
| The barber chair is idle | A haircut never allows self-service. It needs a colonist standing at the chair's staff side, whatever the mod setting says. |
| A gambling hall stopped taking bets | Its till couldn't cover a win in full, and it force-closed itself. Check [the till as a bankroll](economy.md#the-till-as-a-bankroll), then reopen it with the **Open for business** toggle when you're ready. |

## Mod settings

Three sliders and a checkbox, under Options → Mod settings → Old West Town.

| Setting | Default | What it does |
| --- | --- | --- |
| **Allow self-service** | off | Customers buy from an unattended counter instead of walking out. Every self-service sale quietly erodes reputation. A haircut ignores this setting entirely. |
| **Customer volume** | 100% | Scales both how often groups arrive and how large they are. Range 25%–300%. |
| **Customer wealth** | 100% | Scales the silver each customer arrives carrying. Range 25%–300%. |
