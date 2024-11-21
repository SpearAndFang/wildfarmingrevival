namespace WildFarmingRevival.ModSystem
{
    using Vintagestory.API.Common;
    using Vintagestory.API.MathTools;
    using Vintagestory.GameContent;

    public class BlockEnhancedVines : BlockVines
    {
        public override void OnNeighbourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos)
        {
            if (!this.CanVineStay(world, pos))
            {
                world.BlockAccessor.BreakBlock(pos, null);
                world.BlockAccessor.TriggerNeighbourBlockUpdate(pos);
                return;
            }
        }


        private bool CanVineStay(IWorldAccessor world, BlockPos pos)
        {
            var apos = pos.AddCopy(this.VineFacing.Opposite);
            var block = world.BlockAccessor.GetBlock(world.BlockAccessor.GetBlockId(apos));

            return block.CanAttachBlockAt(world.BlockAccessor, this, apos, this.VineFacing) || world.BlockAccessor.GetBlock(pos.UpCopy()) is BlockVines;
        }
    }
}
