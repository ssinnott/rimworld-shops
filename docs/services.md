---
title: Services
summary: Drink, meal, haircut, lodging and the wager — businesses that sell time, or a bet, rather than goods.
---

A **service** is something a business sells that isn't an item off a shelf. It is priced, queued
for and paid for through exactly the same steps as a sale — there just isn't anything to carry
home at the end of it, or, for a haircut or a hand of cards, nothing physical involved at all.

Each [business kind](businesses.md) lists the services it offers. A general store offers none; a
saloon offers two; a barber shop, a hotel and a gambling hall are each nothing but one.

## The five that ship

### Drink

A round at the [saloon bar](buildings.md#saloon-bar).

| | |
| --- | --- |
| Serve time | a couple of seconds at normal speed |
| Uses up stock | yes — one bottle of liquor off the bar's own shelves |
| Price | the drink's market value × the shop's markup |
| Self-service | allowed, if the setting is on |
| Effect | the drink is really drunk — mood, the usual alcohol effects, the usual hangover |

Patrons want a drink more when their recreation is low, but never so little that a contented one
won't order.

Every round poured also makes that one patron a little rowdier — see
[trouble at the saloon](customers.md#trouble-at-the-saloon-and-the-gambling-hall) for what that
leads to, and how a sheriff or a skilled shopkeeper keeps it in check. A meal doesn't have this
effect.

### Meal

A hot meal at the bar.

| | |
| --- | --- |
| Serve time | a couple of seconds at normal speed |
| Uses up stock | yes — any cooked meal on the shelves. Ingredients don't count |
| Price | the meal's market value × the shop's markup |
| Self-service | allowed, if the setting is on |
| Effect | eaten for real — nutrition, and whatever the meal itself does for mood |

Drink and meal are the same mechanic pointed at a different appetite: one sells against
recreation, the other against hunger.

Customers arrive somewhere between 40% and 90% fed — a spread rather than a full stomach,
specifically so a meal service has genuinely hungry customers to sell to.

### Haircut

The [barber shop](businesses.md#barber-shop)'s whole trade.

| | |
| --- | --- |
| Serve time | **about 35 seconds at normal speed** — much longer than pouring a drink |
| Uses up stock | **no** — nothing but time and chair space |
| Price | a flat **16 silver**, then the shop's markup and the town's reputation |
| Self-service | **never**, whatever the setting says — an empty chair can't cut anyone's hair |
| Effect | a **+5 mood** thought for 1.5 days, **and a visibly different hairstyle** |

The hair change is deliberate: a business that changes nothing visible is a weaker proof that
anything happened. The new style is picked the same way the game picks one when a colonist
restyles, so it suits the customer's age and gender.

### Lodging

A night's stay at the [hotel](businesses.md#hotel).

| | |
| --- | --- |
| Serve time | a few seconds at normal speed — this is just the check-in |
| Uses up stock | no — it claims a vacant [hotel bed](buildings.md#hotel-bed) instead |
| Price | a flat **40 silver**, then the shop's markup and the town's reputation |
| Self-service | **never** — an empty desk can't hand anyone a key |
| Effect | books the bed; the mood payoff comes later, on waking |

Lodging is the odd one out: paying for it isn't the whole experience. Every other service in this
list is over the moment it's paid for — the drink is drunk, the hair is cut — but a room is just
booked at the desk. The stay itself happens later, unattended, once the guest is actually tired,
possibly long after the colonist who checked them in has gone off shift.

**Checking in.** A guest only books a room once they're a little sleepy, and never while wide
awake — booking a bed you don't need yet is a weak impulse next to an occasional drink. A desk
with no vacant bed on its own sales floor has nothing to sell, exactly like a shop with an empty
shelf. A guest with no need for rest at all (which, in practice, means a non-humanlike visitor)
never books a room in the first place — nothing would ever make them tired enough to check out,
which would otherwise strand the whole group in town indefinitely.

**Sleeping it off.** Once genuinely tired, a checked-in guest heads for their bed ahead of any
more shopping, sleeps until well-rested (or until a generous night's cap runs out regardless), and
wakes with a mood thought — **staged by the room's own Impressiveness**: a plain bunkroom earns a
smaller boost than a well-appointed suite. A stay is exactly one paid night: there's no
pre-booking several nights at once, and a guest who wants another simply queues and pays again
once they're up.

**Eviction.** A booking can end before the guest ever wakes rested: someone deconstructs the bed,
a colonist climbs into it, or you use the bed's own **Evict guest** button. However it happens,
the guest loses the room they already paid for — **no refund** — and it costs the town
[reputation](economy.md#reputation) exactly like any other walkout. A whole customer group won't
leave town while any of its members is still checked in, so an evicted guest is free to leave with
the rest of the party rather than being stuck waiting on a room that no longer exists.

### Wager

A hand at the [gambling hall's](businesses.md#gambling-hall) faro table.

| | |
| --- | --- |
| Serve time | a few seconds at normal speed |
| Uses up stock | no — nothing but a dealer's time and a deck of cards |
| Price | a flat **20 silver**, then the shop's markup and the town's reputation — this is the stake |
| Self-service | **never** — an empty table can't deal a hand |
| Effect | win double the stake back, lose it outright, or — rarely — draw a cheating accusation; relieves a little boredom either way |

Wager is the odd one out among the five: every other service moves silver only one way, into the
till. This one can send it back out again, doubled, straight into the gambler's own purse — the
first business in the mod where the customer can come away richer than they sat down. What
decides how often that happens is the table's own **house edge**, a second slider living right
next to **Set prices** on the same gizmo row. See [the gambling
hall](businesses.md#gambling-hall) and [the till as a bankroll](economy.md#the-till-as-a-bankroll)
for exactly what it does and the numbers behind it.

Like a round at the bar, a hand also scratches the same recreational itch that pulled the gambler
to the table in the first place: a bored patron wants another hand more than a contented one
does, and playing one relieves a little of that boredom regardless of how it comes out — win,
lose, or shortfall.

**Losing.** A losing hand makes that gambler a little rowdier, the same
[trouble](customers.md#trouble-at-the-saloon-and-the-gambling-hall) a round of drink causes at a
saloon. Every so often an unlucky loss goes further and draws a **cheating accusation** — a
message naming the gambler and the dealer, and a sharper jump in rowdiness than an ordinary loss
gives. A skilled dealer draws noticeably fewer of these; an unstaffed table never deals a hand at
all, so self-service buys nothing here even if the setting is on.

**Winning, and the house falling short.** A win is paid straight out of the same till every sale
already fills — there's no separate pot to draw from. If the till doesn't hold enough to cover a
win in full, the gambler gets whatever's actually there, the shortfall costs the town more
reputation than anything else in the mod, and the table **closes its doors** until reopened by
hand. A freshly built [faro table](buildings.md#faro-table) starts with a bankroll of its own for
exactly this reason.

## Services that consume stock

A drink is the interesting case: a service that still moves goods off the shelf, and so has to
count as both without being counted twice.

- **Availability.** A service that uses up nothing is normally available whenever the business
  offers it. One that uses up stock also needs something matching on display right now — filtered
  by the same Stock tab you already curate for goods. [Lodging](#lodging) is the one exception on
  the stock-free side: it needs nothing from the Stock tab, but it does need an actual vacant
  [hotel bed](buildings.md#hotel-bed) somewhere on the desk's sales floor.
- **Pricing.** A service that uses up stock is priced from whatever it actually consumes. Only a
  service with nothing behind it has a price of its own.
- **Appeal.** A saloon's beer already counts once, as stock on the shelf. Only services with
  nothing physical behind them add to [appeal](economy.md#appeal) separately, so a bottle isn't
  counted twice for being sellable two ways.
- **The trip.** A customer buying a drink fetches the bottle first and carries it to the bar; a
  customer wanting a haircut or a room goes straight to waiting.

## How a service visit runs

A service visit and a goods purchase are the same four steps:

1. **Fetch** — walk to the item and pick it up. *(Skipped entirely for a haircut or a room —
   neither has anything to carry.)*
2. **Walk** to the customer side of the counter.
3. **Wait to be served.** Patience burns down while the business is unstaffed; being attended
   restores it. Serving has to be **continuous** — a shopkeeper who drifts off halfway through
   starts the service over rather than resuming it.
4. **Pay**, and the effect lands.

If patience runs out first, the customer [walks out](customers.md#walkouts): the shop takes a
reputation hit, the customer refuses to queue there again this visit, and anything they were
carrying is dropped on the floor unpaid.

---

Adding a service of your own is mostly a matter of editing files rather than writing code — see
[adding content](extending.md#add-a-service).
