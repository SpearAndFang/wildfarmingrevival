namespace WildFarmingRevival.ModSystem
{
    using Newtonsoft.Json;
    using Vintagestory.API.Common;
    using Vintagestory.API.MathTools;


    [JsonObject(MemberSerialization.OptIn)]
    public class TreeFriend
    {
        [JsonProperty]
        private readonly float maxTemp = 50;
        [JsonProperty]
        private readonly float minTemp = -50;
        [JsonProperty]
        private readonly float maxRain = 1;
        [JsonProperty]
        private readonly float minRain = 0;
        [JsonProperty]
        public float weight = 1;
        [JsonProperty]
        private readonly string blockCode = "air";
        [JsonProperty]
        public bool onGround = true;
        [JsonProperty]
        public bool onUnderneath = false;
        [JsonProperty]
        private readonly bool reverseSide = false;

        private Block resolvedBlock;
        private string needsWood;


        public bool Resolve(ICoreAPI api)
        {
            this.resolvedBlock = api.World.BlockAccessor.GetBlock(new AssetLocation(this.blockCode));
            if (this.resolvedBlock != null)
            {
                this.needsWood = this.resolvedBlock.Attributes?["needsTree"].AsString(null);
                return true;
            }
            return false;
        }


        public bool CanPlant(ClimateCondition conds, string treeType)
        {
            if (conds == null)
            { return false; }
            if (conds.Temperature > this.maxTemp || conds.Temperature < this.minTemp || conds.WorldgenRainfall > this.maxRain || conds.WorldgenRainfall < this.minRain || !this.NeedsCertainTree(treeType))
            { return false; }
            return true;
        }


        public bool NeedsCertainTree(string type)
        {
            if (this.needsWood == null)
            { return true; }
            return type == this.needsWood;
        }


        public bool TryToPlant(BlockPos pos, IBlockAccessor changer, BlockFacing side)
        {
            if (this.resolvedBlock == null)
            { return false; }
            var tmpPos = pos.Copy();

            if (this.onGround)
            {
                tmpPos.Add(0, 1, 0);
                var ground = changer.GetBlock(pos);

                if (ground.Fertility > 0 && ground.SideSolid[BlockFacing.UP.Index] && changer.GetBlock(tmpPos).IsReplacableBy(this.resolvedBlock))
                {
                    changer.SetBlock(this.resolvedBlock.BlockId, tmpPos);
                    return true;
                }
            }
            else if (this.onUnderneath)
            {
                tmpPos.Add(0, -1, 0);
                if (changer.GetBlock(pos).SideSolid[BlockFacing.DOWN.Index] && changer.GetBlock(tmpPos).BlockId == 0)
                {
                    changer.SetBlock(this.resolvedBlock.BlockId, tmpPos);
                    return true;
                }
            }
            else
            {
                var rotatedBlock = changer.GetBlock(new AssetLocation(this.resolvedBlock.Code.Domain, this.resolvedBlock.CodeWithoutParts(1) + "-" + (this.reverseSide ? side.Opposite.Code : side.Code)));
                if (rotatedBlock == null)
                { return false; }

                tmpPos.Add(side);
                if (changer.GetBlock(pos).SideSolid[side.Index] && changer.GetBlock(tmpPos).BlockId == 0)
                {
                    changer.SetBlock(rotatedBlock.BlockId, tmpPos);
                    return true;
                }
            }

            return false;
        }
    }
}
