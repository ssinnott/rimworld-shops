using System.Linq;
using OldWestTown.Lords;
using OldWestTown.Shops;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace OldWestTown.Roles
{
    /// <summary>
    /// The saloon's one mechanical hook into town roles: every round of drinks nudges a
    /// customer's own OWT_Rowdy hediff; an on-duty sheriff, or a skilled shopkeeper, slows how
    /// fast that climbs; and if it tops out anyway the visit turns into a scripted disturbance —
    /// a message, a reputation hit, and a customer who stops buying for the rest of their stay.
    ///
    /// There is no second pawn anywhere in this. "Trouble" is one pawn's own hediff crossing a
    /// threshold, resolved entirely through the same shared comp/economy state every other
    /// transaction in this mod already reads and writes (CompBusiness, TownEconomy,
    /// CustomerRecord) — never a paired job, never a mental state, never a fight.
    /// </summary>
    public static class TroubleUtility
    {
        /// <summary>An on-duty sheriff roughly halves how fast rowdiness climbs, map-wide.</summary>
        private const float SheriffOnDutyFactor = 0.5f;

        /// <summary>A shopkeeper at max Social skill roughly halves it too; an unstaffed counter
        /// gets no discount at all — the same lesson self-service already teaches everywhere
        /// else in this mod.</summary>
        private const float MaxShopkeeperSocialFactor = 0.5f;

        /// <summary>Called once per successfully paid service round — the one place a "did this
        /// round tip someone over" check needs no periodic scan, tracked reference, or geometric
        /// shop search: shop and customer are already local variables in scope.</summary>
        public static void Notify_ServiceRound(Pawn customer, CompBusiness shop, float rowdinessPerUse)
        {
            if (rowdinessPerUse <= 0f || shop == null || customer == null || customer.Dead) return;
            if (customer.RaceProps?.Humanlike != true) return;
            if (customer.health?.hediffSet == null) return;

            float factor = shop.Staffed ? ShopkeeperSocialFactor(shop) : 1f;
            if (AnySheriffOnDuty(shop.parent.Map)) factor *= SheriffOnDutyFactor;

            Hediff rowdy = customer.health.hediffSet.GetFirstHediffOfDef(OWTDefOf.OWT_Rowdy)
                ?? customer.health.AddHediff(OWTDefOf.OWT_Rowdy);
            rowdy.Severity += rowdinessPerUse * factor;

            // The stage count is read dynamically off the def, never duplicated as a magic
            // number — it's the only source of truth for "which stage is the top one."
            if (rowdy.CurStageIndex < OWTDefOf.OWT_Rowdy.stages.Count - 1) return;

            StartDisturbance(customer, shop);
            // Reset in the same call the threshold is crossed: the top stage can never be
            // observed asynchronously by a WorkGiver scan, which is what makes the stage below
            // it the sheriff's real, designed window to intervene.
            rowdy.Severity = 0f;
        }

        private static float ShopkeeperSocialFactor(CompBusiness shop)
        {
            int social = shop.Shopkeeper?.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0;
            return Mathf.Lerp(1f, MaxShopkeeperSocialFactor, Mathf.Clamp01((float)social / SkillRecord.MaxLevel));
        }

        private static void StartDisturbance(Pawn pawn, CompBusiness shop)
        {
            shop.RecordDisturbance();
            shop.parent.Map?.GetComponent<TownEconomy>()?.RecordDisturbance();

            CustomerRecord record = (pawn.GetLord()?.LordJob as LordJob_ShopVisit)?.RecordFor(pawn);
            if (record != null) record.causedTrouble = true;

            Messages.Message("OWT_SaloonTrouble".Translate(pawn.LabelShort, shop.parent.Label),
                new LookTargets(shop.parent), MessageTypeDefOf.NegativeEvent);
        }

        /// <summary>True while any sheriff's office anywhere on the map has its assigned pawn
        /// actively on patrol. Read map-wide rather than per-shop: an on-duty sheriff's presence
        /// is a town-level deterrent, not tied to standing at any one saloon.</summary>
        public static bool AnySheriffOnDuty(Map map) =>
            map?.listerThings.ThingsOfDef(OWTDefOf.OWT_SheriffOffice)
                .Any(t => t.TryGetComp<CompRolePost>()?.OnDuty == true) == true;

        /// <summary>True if this specific pawn is the specifically assigned occupant of some
        /// sheriff's office on their current map — both suppression paths gate on this, not on
        /// "any colonist with the Sheriffing priority."</summary>
        public static bool IsAssignedSheriff(Pawn pawn) =>
            pawn?.Map?.listerThings.ThingsOfDef(OWTDefOf.OWT_SheriffOffice)
                .Any(t => t.TryGetComp<CompRolePost>()?.AssignedPawnsForReading.Contains(pawn) == true) == true;

        /// <summary>True for a spawned, conscious pawn currently in the "getting loud" stage or
        /// worse — the sheriff's reactive job's whole target set.</summary>
        public static bool IsWorthCalming(Pawn pawn)
        {
            if (pawn == null || !pawn.Spawned || pawn.Dead || pawn.Downed) return false;
            Hediff rowdy = pawn.health?.hediffSet?.GetFirstHediffOfDef(OWTDefOf.OWT_Rowdy);
            return rowdy != null && rowdy.CurStageIndex >= OWTDefOf.OWT_Rowdy.stages.Count - 2;
        }

        /// <summary>Talks a patron down: unilaterally resets their rowdiness. No handshake — the
        /// target's own job never knows this happened.</summary>
        public static void CalmDown(Pawn target)
        {
            Hediff rowdy = target?.health?.hediffSet?.GetFirstHediffOfDef(OWTDefOf.OWT_Rowdy);
            if (rowdy != null) rowdy.Severity = 0f;
        }
    }
}
