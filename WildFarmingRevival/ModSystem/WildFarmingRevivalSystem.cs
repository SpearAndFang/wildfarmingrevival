namespace WildFarmingRevival.ModSystem
{
    using Vintagestory.API.Common;
    using HarmonyLib;
    using System.Linq;
    using Vintagestory.API.Client;
    using Vintagestory.API.Util;

    public class WildFarmingRevivalSystem : ModSystem
    {
        private readonly Harmony harmony = new Harmony("com.jakecool19.wildfarming.lootvessel");

        private ICoreClientAPI capi;

        //Static references
        public static string[] Conifers = new string[] { "pine", "baldcypress", "larch", "redwood", "greenspirecypress" };
        public static string[] Decidious = new string[] { "birch", "oak", "maple", "ebony", "walnut", "crimsonkingmaple" };
        public static string[] Tropical = new string[] { "kapok", "purpleheart" };


        public override void StartPre(ICoreAPI api)
        {
            base.StartPre(api);
            try
            {
                BotanyConfig fromDisk;
                if ((fromDisk = api.LoadModConfig<BotanyConfig>("WildFarmingConfig.json")) == null)
                { api.StoreModConfig(BotanyConfig.Loaded, "WildFarmingConfig.json"); }
                else
                { BotanyConfig.Loaded = fromDisk; }
            }
            catch
            { api.StoreModConfig(BotanyConfig.Loaded, "WildFarmingConfig.json"); }

            api.World.Config.SetBool("WFflowersEnabled", BotanyConfig.Loaded.FlowersEnabled);
            api.World.Config.SetBool("WFseedPanningEnabled", BotanyConfig.Loaded.SeedPanningEnabled);
            api.World.Config.SetBool("WFcropsEnabled", BotanyConfig.Loaded.CropSeedsEnabled);
            api.World.Config.SetBool("WFbushesEnabled", BotanyConfig.Loaded.BushSeedsEnabled && !api.ModLoader.IsModEnabled("wildcraft"));
            api.World.Config.SetBool("WFcactiEnabled", BotanyConfig.Loaded.CactiSeedsEnabled);
            api.World.Config.SetBool("WFmushroomsEnabled", BotanyConfig.Loaded.MushroomFarmingEnabled);
            api.World.Config.SetBool("WFvinesEnabled", BotanyConfig.Loaded.VineGrowthEnabled);
            api.World.Config.SetBool("WFlogScoringEnabled", BotanyConfig.Loaded.LogScoringEnabled);
            api.World.Config.SetBool("WFreedsEnabled", BotanyConfig.Loaded.ReedCloningEnabled);
            api.World.Config.SetBool("WFseaweedEnabled", BotanyConfig.Loaded.SeaweedGrowthEnabled);
            api.World.Config.SetBool("WFtermitesEnabled", BotanyConfig.Loaded.TermitesEnabled);
        }


        public override void Start(ICoreAPI api)
        {
            base.Start(api);
            api.World.Logger.Event("started 'Wild Farming Revival' mod");
            api.RegisterItemClass("wildseed", typeof(WildSeed));
            api.RegisterItemClass("ItemMushroomSpawn", typeof(ItemMushroomSpawn));

            api.RegisterBlockClass("BlockEnhancedVines", typeof(BlockEnhancedVines));
            api.RegisterBlockClass("BlockTrunk", typeof(BlockTrunk));
            api.RegisterBlockClass("BlockLivingLogSection", typeof(BlockLivingLogSection));
            api.RegisterBlockClass("BlockMushroomSubstrate", typeof(BlockMushroomSubstrate));
            api.RegisterBlockClass("BlockEnhancedMushroom", typeof(BlockEnhancedMushroom));

            api.RegisterBlockEntityClass("WildPlant", typeof(WildPlantBlockEntity));
            api.RegisterBlockEntityClass("BEMushroomSubstrate", typeof(BlockEntityMushroomSubstrate));
            api.RegisterBlockEntityClass("BEVines", typeof(BEVines));
            api.RegisterBlockEntityClass("BESeaweed", typeof(BESeaweed));
            api.RegisterBlockEntityClass("RegenSapling", typeof(BlockEntityRegenSapling));
            api.RegisterBlockEntityClass("TreeTrunk", typeof(BlockEntityTrunk));
            api.RegisterBlockEntityClass("TermiteMound", typeof(BlockEntityTermiteMound));

            api.RegisterBlockBehaviorClass("Score", typeof(BlockBehaviorScore));

            //this.harmony = new Harmony("com.jakecool19.wildfarming.lootvessel");
            //this.harmony.PatchAll(Assembly.GetExecutingAssembly());
        }


        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(api);
            this.capi = api;
            var wFRBlockGetPlacedBlockInfoOriginal = typeof(Block).GetMethod(nameof(Block.GetPlacedBlockInfo));
            var wFRBlockGetPlacedBlockInfoPostfix = typeof(WFR_BlockGetPlacedBlockInfo_Patch).GetMethod(nameof(WFR_BlockGetPlacedBlockInfo_Patch.WFRBlockGetPlacedBlockInfoPostfix));
            this.harmony.Patch(wFRBlockGetPlacedBlockInfoOriginal, postfix: new HarmonyMethod(wFRBlockGetPlacedBlockInfoPostfix));
        }


        public override void Dispose()
        {
            var wFRBlockGetPlacedBlockInfoOriginal = typeof(Block).GetMethod(nameof(Block.GetPlacedBlockInfo));
            this.harmony.Unpatch(wFRBlockGetPlacedBlockInfoOriginal, HarmonyPatchType.Postfix, "*");
            //this.harmony.UnpatchAll(this.harmony.Id);
            base.Dispose();
        }


        public static string GetTreeFamily(string tree)
        {
            if (Conifers.Contains(tree))
            { return "conifer"; }
            if (Decidious.Contains(tree))
            { return "decidious"; }
            if (Tropical.Contains(tree))
            { return "tropical"; }
            return null;
        }
    }

    // display mod name in the hud for blocks
    //[HarmonyPatch(typeof(Block), nameof(Block.GetPlacedBlockInfo))]
    public class WFR_BlockGetPlacedBlockInfo_Patch
    {
        [HarmonyPostfix]
        public static void WFRBlockGetPlacedBlockInfoPostfix(ref string __result, IPlayer forPlayer) //IWorldAccessor world, BlockPos pos
        {
            var domain = forPlayer.Entity?.BlockSelection?.Block?.Code?.Domain;
            if (domain != null)
            {
                if (domain == "wildfarmingrevival")
                {
                    //forPlayer.Entity?.Api?.ModLoader?.GetMod(domain).Info.Name
                    __result += "\n\n<font color=\"#eae4a3\"><i>Wild Farming - Revival</i></font>\n\n";
                }
            }
        }
    }
}
