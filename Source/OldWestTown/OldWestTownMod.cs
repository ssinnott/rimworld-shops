using OldWestTown.Compat;
using UnityEngine;
using Verse;

namespace OldWestTown
{
    public class OldWestTownSettings : ModSettings
    {
        /// <summary>Let customers buy from an unstaffed counter (an honesty box), at a reputation cost.</summary>
        public bool allowSelfService;

        /// <summary>Scales how many customers the town's appeal pulls in.</summary>
        public float customerVolume = 1f;

        /// <summary>Scales the silver customers arrive carrying.</summary>
        public float customerWealth = 1f;

        /// <summary>Master switch for the Hospitality bridge (Compat/). Only ever consulted
        /// while HospitalityInterop.Present is true, so this has no effect at all on an install
        /// without Hospitality.</summary>
        public bool hospitalityBridgeEnabled = true;

        /// <summary>Whether the bridge tops up a Hospitality guest's purse the same way an
        /// arriving customer's is. Off leaves a guest to spend only silver they already carry.</summary>
        public bool hospitalityGuestsCarrySilver = true;

        /// <summary>Master switch for the stickup incident (Incidents/IncidentWorker_Stickup.cs,
        /// Shops/StickupWatch.cs). On by default, like every other risk this mod ships with.</summary>
        public bool stickupsEnabled = true;

        /// <summary>Master switch for the gold rush event (GoldRush/, Incidents/
        /// IncidentWorker_GoldRushStrike.cs). On by default, like every other event this mod
        /// ships with.</summary>
        public bool goldRushEnabled = true;
        /// <summary>Master switch for rival towns (Rivals/RivalTowns.cs). On by default, like
        /// every other risk this mod ships with.</summary>
        public bool rivalTownsEnabled = true;

        /// <summary>Scales every rival's own pull before it's weighed against this town's own —
        /// a multiplier on a sum, not a divisor, so it carries no near-zero floor the way
        /// customerVolume/customerWealth do; see TownEconomy.CompetingPull.</summary>
        public float rivalStrength = 1f;

        /// <summary>Opt-in Dev Mode telemetry: one log line per customer arrival, nightly
        /// settlement and stickup roll (DevTools/Telemetry.cs) — the real numbers this mod's own
        /// tuning constants are still guesses about. Off by default; meant for testing, not
        /// ordinary play.</summary>
        public bool telemetryLoggingEnabled = false;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref allowSelfService, "allowSelfService");
            Scribe_Values.Look(ref customerVolume, "customerVolume", 1f);
            Scribe_Values.Look(ref customerWealth, "customerWealth", 1f);
            Scribe_Values.Look(ref hospitalityBridgeEnabled, "hospitalityBridgeEnabled", true);
            Scribe_Values.Look(ref hospitalityGuestsCarrySilver, "hospitalityGuestsCarrySilver", true);
            Scribe_Values.Look(ref stickupsEnabled, "stickupsEnabled", true);
            Scribe_Values.Look(ref goldRushEnabled, "goldRushEnabled", true);
            Scribe_Values.Look(ref rivalTownsEnabled, "rivalTownsEnabled", true);
            Scribe_Values.Look(ref rivalStrength, "rivalStrength", 1f);
            Scribe_Values.Look(ref telemetryLoggingEnabled, "telemetryLoggingEnabled", false);
        }
    }

    public class OldWestTownMod : Mod
    {
        public static OldWestTownSettings Settings { get; private set; }

        public OldWestTownMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<OldWestTownSettings>();
        }

        public override string SettingsCategory() => "OWT_ModTitle".Translate();

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard list = new Listing_Standard();
            list.Begin(inRect);

            list.CheckboxLabeled("OWT_SettingSelfService".Translate(), ref Settings.allowSelfService,
                "OWT_SettingSelfServiceDesc".Translate());
            list.Gap();

            list.Label("OWT_SettingVolume".Translate(Settings.customerVolume.ToStringPercent()));
            Settings.customerVolume = list.Slider(Settings.customerVolume, 0.25f, 3f);

            list.Label("OWT_SettingWealth".Translate(Settings.customerWealth.ToStringPercent()));
            Settings.customerWealth = list.Slider(Settings.customerWealth, 0.25f, 3f);
            list.Gap();

            list.CheckboxLabeled("OWT_SettingStickupsEnabled".Translate(), ref Settings.stickupsEnabled,
                "OWT_SettingStickupsEnabledDesc".Translate());

            list.CheckboxLabeled("OWT_SettingGoldRushEnabled".Translate(), ref Settings.goldRushEnabled,
                "OWT_SettingGoldRushEnabledDesc".Translate());
            list.Gap();
            list.CheckboxLabeled("OWT_SettingRivalTownsEnabled".Translate(), ref Settings.rivalTownsEnabled,
                "OWT_SettingRivalTownsEnabledDesc".Translate());
            if (Settings.rivalTownsEnabled)
            {
                list.Label("OWT_SettingRivalStrength".Translate(Settings.rivalStrength.ToStringPercent()));
                Settings.rivalStrength = list.Slider(Settings.rivalStrength, 0.25f, 3f);
            }
            list.Gap();

            list.CheckboxLabeled("OWT_SettingTelemetryEnabled".Translate(), ref Settings.telemetryLoggingEnabled,
                "OWT_SettingTelemetryEnabledDesc".Translate());

            // Hospitality section: a status line always shown, so the player can tell the bridge
            // apart from a mod that's simply doing nothing; controls only once there's something
            // for them to do. A checkbox that could never do anything (Hospitality absent) is
            // hidden entirely rather than shown disabled.
            list.Gap();
            list.Label(HospitalityInterop.Present
                ? "OWT_HospitalityDetected".Translate()
                : "OWT_HospitalityNotDetected".Translate());

            if (HospitalityInterop.Present)
            {
                list.CheckboxLabeled("OWT_SettingHospitalityEnabled".Translate(), ref Settings.hospitalityBridgeEnabled,
                    "OWT_SettingHospitalityEnabledDesc".Translate());

                if (Settings.hospitalityBridgeEnabled)
                {
                    list.CheckboxLabeled("OWT_SettingHospitalitySilver".Translate(), ref Settings.hospitalityGuestsCarrySilver,
                        "OWT_SettingHospitalitySilverDesc".Translate());
                }
            }

            list.End();
        }
    }
}
