namespace WildFarmingRevival.ModSystem
{
    using Vintagestory.API.Common;
    using Vintagestory.API.Config;
    using Vintagestory.API.MathTools;
    //using Vintagestory.GameContent;

    public class BlockTrunk : Block
    {
        public override string GetPlacedBlockName(IWorldAccessor world, BlockPos pos)
        {
            return Lang.Get("wildfarmingrevival:block-trunk-" + this.Variant["wood"]);
        }


        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            return new ItemStack(world.GetBlock(new AssetLocation(this.Attributes["deathState"].AsString("game:air"))));
        }
    }
}
