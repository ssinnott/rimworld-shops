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

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref allowSelfService, "allowSelfService");
            Scribe_Values.Look(ref customerVolume, "customerVolume", 1f);
            Scribe_Values.Look(ref customerWealth, "customerWealth", 1f);
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

            list.End();
        }
    }
}
