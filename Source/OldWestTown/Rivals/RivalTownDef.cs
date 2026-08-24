using System.Collections.Generic;
using Verse;

namespace OldWestTown.Rivals
{
    /// <summary>
    /// One archetype of rival town — pure data, the same "a business or a service is a stanza,
    /// not a class" idiom <c>ShopKindDef</c> and <c>CoachTierDef</c> already use. A loaded
    /// instance never ranks itself against another town; see <see cref="RivalTowns"/> for the
    /// live, growing, occasionally-undercutting state each def seeds.
    /// </summary>
    public class RivalTownDef : Def
    {
        /// <summary>Starting value — and floor — for a freshly seeded rival's live appeal.</summary>
        public float baseAppeal = 0.2f;

        /// <summary>Ceiling the rival's live appeal grows toward and never exceeds.</summary>
        public float maxAppeal = 2.0f;

        /// <summary>How much the rival's live appeal advances toward <see cref="maxAppeal"/> per world-day.</summary>
        public float growthPerDay = 0.003f;

        /// <summary>Mean days between this rival entering an undercutting swing, while it isn't already in one.</summary>
        public float undercutMTBDays = 14f;

        /// <summary>How many days an undercutting swing lasts once triggered.</summary>
        public float undercutDurationDays = 4f;

        /// <summary>
        /// This rival's price-competitiveness number while undercutting — the same units as
        /// <c>ShopPricing.ValueAppeal</c> (above 1.0 means "pricing under market rate"). The
        /// not-undercutting case is a flat, hardcoded 1.0 ("an honest, unremarkable competitor")
        /// on <see cref="RivalTown.PriceIndex"/> — there's nothing to tune about the honest half.
        /// </summary>
        public float undercutPriceIndex = 1.3f;

        /// <summary>
        /// Not strictly load-bearing — a misconfigured rival just grows oddly or never
        /// undercuts, rather than crashing anything — but it catches an obvious typo at load time
        /// instead of as confusing in-game behaviour, mirroring <c>CoachTierDef.ConfigErrors</c>.
        /// </summary>
        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string err in base.ConfigErrors()) yield return err;

            if (maxAppeal <= baseAppeal)
            {
                yield return "maxAppeal must be greater than baseAppeal";
            }
            if (growthPerDay < 0f)
            {
                yield return "growthPerDay must not be negative";
            }
            if (undercutMTBDays <= 0f)
            {
                yield return "undercutMTBDays must be greater than zero";
            }
            if (undercutDurationDays <= 0f)
            {
                yield return "undercutDurationDays must be greater than zero";
            }
            if (undercutPriceIndex <= 0f)
            {
                yield return "undercutPriceIndex must be greater than zero";
            }
        }
    }
}
