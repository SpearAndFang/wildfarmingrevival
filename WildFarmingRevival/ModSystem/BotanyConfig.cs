namespace WildFarmingRevival.ModSystem
{
    public class BotanyConfig
    {
        public static BotanyConfig Loaded { get; set; } = new BotanyConfig();

        //Tree settings
        public int MaxTreeGrowthStages { get; set; } = 4;

        // Spang added
        public int FernRepopRate { get; set; } = 20;

        public bool PropogateIntoWater { get; set; } = false;

        public bool PropogateIntoClaims { get; set; } = false;

        // End Spang added

        public int GrownTreeRepopMinimum { get; set; } = 8;

        public int GrownTreeRepopVertSearch { get; set; } = 5;

        public float SaplingToTreeSize { get; set; } = 0.6f;

        public float TreeSizePerGrowthStage { get; set; } = 0.125f;

        public float TreeRevertGrowthTempThreshold { get; set; } = 5f;

        public float TreeRegenMultiplier { get; set; } = 1f;

        public float TreeRepopChance { get; set; } = 0.2f;

        public float TreeFoilageChance { get; set; } = 1f;

        public int TreeFoilageTriesPerDay { get; set; } = 5;

        //Wild Plants settings
        public bool HarshWildPlants { get; set; } = true;

        //Enable/Disable settings
        public bool FlowersEnabled { get; set; } = true;

        public bool SeedPanningEnabled { get; set; } = true;

        public bool CropSeedsEnabled { get; set; } = true;

        public bool BushSeedsEnabled { get; set; } = true;

        public bool CactiSeedsEnabled { get; set; } = true;

        public bool MushroomFarmingEnabled { get; set; } = true;

        public bool VineGrowthEnabled { get; set; } = true;

        public bool LogScoringEnabled { get; set; } = true;

        public bool ReedCloningEnabled { get; set; } = true;

        public bool LivingTreesEnabled { get; set; } = true;

        public bool HarshSaplingsEnabled { get; set; } = true;

        public bool SeaweedGrowthEnabled { get; set; } = true;

        public bool TermitesEnabled { get; set; } = true;
    }
}
