using System.Collections.Generic;
using Verse;

namespace OldWestTown.Shops
{
    /// <summary>
    /// One thing a business can sell that isn't an item off a shelf: a drink, a meal, a
    /// haircut. Metadata common to every service lives here; what makes it behave
    /// differently lives on the embedded <see cref="ServiceWorker"/>.
    /// </summary>
    public class ServiceDef : Def
    {
        /// <summary>Job a customer runs to receive this service. One per ServiceDef, not
        /// shared — Job has no generic slot to carry a Def reference, so this is how the
        /// driver recovers which service it's running.</summary>
        public JobDef jobDef;

        /// <summary>Job a COLONIST runs to receive this service, or null when the colony's own
        /// people cannot have it. A second JobDef for the same reason as the first — Job has no
        /// generic slot to carry a Def reference — and a second driver because the two sides of
        /// the counter end differently: a stranger's visit ends in a transaction, a colonist's
        /// ends in an effect and nothing else.
        ///
        /// Null is the default and the meaningful one: a service is a stranger's until it is
        /// deliberately opened to the colony.</summary>
        public JobDef colonistJobDef;

        /// <summary>Pluggable behaviour, embedded like DutyDef.thinkNode: the concrete worker
        /// class supplies its own XML-configurable fields.</summary>
        public ServiceWorker worker;

        /// <summary>Continuous staffed ticks required to complete one visit.</summary>
        public int serveTicks = 180;

        /// <summary>Price basis used ONLY when the service has no backing Thing to read a
        /// market value from (Haircut). Ignored for a stock-consuming service.</summary>
        public float basePrice = 10f;

        /// <summary>Whether the "Allow self-service" mod setting applies to this service at
        /// all, on top of the setting itself being on.</summary>
        public bool allowsSelfService;

        /// <summary>The service a given job runs, whichever side of the counter it came from.
        /// One lookup rather than one per driver, because a JobDef-to-ServiceDef search is the
        /// only way either driver can recover what it is running and two copies could disagree
        /// about which jobs belong to which service.</summary>
        public static ServiceDef ForJob(JobDef job)
        {
            if (job == null) return null;
            List<ServiceDef> all = DefDatabase<ServiceDef>.AllDefsListForReading;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].jobDef == job || all[i].colonistJobDef == job) return all[i];
            }
            return null;
        }

        /// <summary>
        /// Both fields below are dereferenced without a null check on the hot paths that pick and
        /// run a service. A def missing either is a modding mistake, and it should surface as a
        /// red error naming the def at load time rather than as a null reference from inside a
        /// pawn's think tree an hour into the game.
        /// </summary>
        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string err in base.ConfigErrors()) yield return err;

            if (worker == null)
            {
                yield return "no <worker> — a ServiceDef with no worker has no behaviour to run";
            }
            if (jobDef == null)
            {
                yield return "no <jobDef> — a service needs its own job for the driver to recover it from";
            }
            else if (jobDef.driverClass != typeof(AI.JobDriver_UseService))
            {
                yield return $"jobDef {jobDef.defName} does not use JobDriver_UseService";
            }

            if (colonistJobDef != null)
            {
                if (colonistJobDef.driverClass != typeof(AI.JobDriver_ColonistUseService))
                {
                    yield return $"colonistJobDef {colonistJobDef.defName} does not use JobDriver_ColonistUseService";
                }
                if (worker != null && worker.ConsumesStock)
                {
                    yield return "colonistJobDef on a stock-consuming service — a colonist pays nothing, so "
                               + "there is no answer to who paid for the item it would take off the shelf";
                }
            }
        }
    }
}
