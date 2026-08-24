using System.Collections.Generic;
using Verse;

namespace OldWestTown.Shops
{
    /// <summary>
    /// Live per-map roster of spawned <see cref="CompFalseFront"/>s. Exists so
    /// <see cref="CompFalseFront.CurbAppealBonus"/> — reached from the customer AI's
    /// job-selection hot path once per open shop, and again per service that shop offers — walks
    /// a short list of facades instead of every Thing on the map. Same registration shape as
    /// <see cref="TownEconomy"/>'s own shop list, for the same reason.
    /// </summary>
    public class FalseFrontRegistry : MapComponent
    {
        private readonly List<CompFalseFront> facades = new List<CompFalseFront>();

        public FalseFrontRegistry(Map map) : base(map) { }

        public IReadOnlyList<CompFalseFront> Facades => facades;

        public void Register(CompFalseFront facade)
        {
            if (facade != null && !facades.Contains(facade)) facades.Add(facade);
        }

        public void Deregister(CompFalseFront facade)
        {
            facades.Remove(facade);
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            // Comps register on spawn, but a loaded map spawns them before this component
            // exists — the same fixup TownEconomy does for its own shop list.
            facades.Clear();
            foreach (Thing t in map.listerThings.AllThings)
            {
                CompFalseFront comp = t.TryGetComp<CompFalseFront>();
                if (comp != null) Register(comp);
            }
        }
    }
}
