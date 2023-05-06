namespace WildFarmingRevival.ModSystem
{
    using System.Text;
    using Vintagestory.API.Common;
    using Vintagestory.API.MathTools;
    using Vintagestory.GameContent;

    public class BlockLivingLogSection : BlockLogSection
    {
        public override string GetPlacedBlockInfo(IWorldAccessor world, BlockPos pos, IPlayer forPlayer)
        {
            var rot = this.LastCodePart(1);
            if (rot == "nw")
            { return base.GetPlacedBlockInfo(world, pos, forPlayer); }

            var dsc = new StringBuilder();
            BlockEntityTrunk tr;
            BlockPos tmpPos;

            switch (rot)
            {
                case "se":
                    tmpPos = pos.AddCopy(-1, 0, -1);
                    tr = world.BlockAccessor.GetBlockEntity(tmpPos) as BlockEntityTrunk;
                    if (tr != null)
                    {
                        tr.GetBlockInfo(forPlayer, dsc);
                        return dsc.ToString();
                    }
                    break;

                case "ne":
                    tmpPos = pos.AddCopy(-1, 0, 0);
                    tr = world.BlockAccessor.GetBlockEntity(tmpPos) as BlockEntityTrunk;
                    if (tr != null)
                    {
                        tr.GetBlockInfo(forPlayer, dsc);
                        return dsc.ToString();
                    }
                    break;

                default: //case "sw":
                    tmpPos = pos.AddCopy(0, 0, -1);
                    tr = world.BlockAccessor.GetBlockEntity(tmpPos) as BlockEntityTrunk;
                    if (tr != null)
                    {
                        tr.GetBlockInfo(forPlayer, dsc);
                        return dsc.ToString();
                    }
                    break;
            }
            return base.GetPlacedBlockInfo(world, pos, forPlayer);
        }
    }
}
