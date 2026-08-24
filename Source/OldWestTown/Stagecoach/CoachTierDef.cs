using System.Collections.Generic;
using Verse;

namespace OldWestTown.Stagecoach
{
    /// <summary>
    /// One rung of the stagecoach route ladder — pure data, the same "a business or a service is
    /// a stanza, not a class" idiom <see cref="ShopKindDef"/> and <see cref="ServiceDef"/> already
    /// use. Which tier is active for a given appeal is decided entirely by
    /// <see cref="CoachTierUtility.CurrentTier"/>; nothing here ranks itself against its
    /// neighbours.
    /// </summary>
    public class CoachTierDef : Def
    {
        /// <summary>Appeal at or above which this tier can be the active one.</summary>
        public float minAppeal = 0f;

        /// <summary>Longest gap, in days at 1.0x Customer volume, this tier lets pass between
        /// arrivals of any kind — organic or scheduled — before forcing one.</summary>
        public float arrivalCeilingDays = 7f;

        /// <summary>Multiplies every ordinary customer's purse in a group this tier's ceiling
        /// forced into being. Inert for an organically-rolled group.</summary>
        public float purseMultiplier = 1.25f;

        /// <summary>Chance, once this tier forces an arrival, that one pawn in that group is a
        /// VIP carrying a much larger purse (see <c>VipPurseMultiplier</c> in
        /// <c>IncidentWorker_ShopCustomers</c>).</summary>
        public float vipChance = 0f;

        /// <summary>
        /// Not strictly load-bearing — a misconfigured ceiling just degrades to "attempts a fire
        /// every arrival check, minRefireDays rejects most of them" rather than crashing or
        /// spamming — but it catches an obvious typo at load time instead of as confusing
        /// in-game behaviour, mirroring <see cref="ServiceDef.ConfigErrors"/>'s own reasoning.
        /// </summary>
        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string err in base.ConfigErrors()) yield return err;

            if (arrivalCeilingDays <= 0f)
            {
                yield return "arrivalCeilingDays must be greater than zero";
            }
        }
    }
}
