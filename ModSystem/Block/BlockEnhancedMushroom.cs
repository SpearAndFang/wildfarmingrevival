namespace WildFarmingRevival.ModSystem
{
    using System.Text;
    using Vintagestory.API.Common;
    using Vintagestory.API.Config;
    using Vintagestory.API.MathTools;
    using Vintagestory.GameContent;

    public class BlockEnhancedMushroom : BlockMushroom
    {
        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            var preference = this.Attributes?["needsTree"].AsString();
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
            if (preference != null)
            {
                dsc.AppendLine();
                dsc.AppendLine(Lang.Get("wildfarmingrevival:mushroom-" + preference));
            }
        }


        public override bool CanPlantStay(IBlockAccessor blockAccessor, BlockPos pos)
        {
            if (this.Variant["side"] != null)
            {
                var face = BlockFacing.FromCode(this.Variant["side"]);
                var hold = blockAccessor.GetBlock(pos.AddCopy(face));
                return hold is BlockLog;
            }
            return base.CanPlantStay(blockAccessor, pos);
        }
    }
}
