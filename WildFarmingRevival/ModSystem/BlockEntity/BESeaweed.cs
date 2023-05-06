namespace WildFarmingRevival.ModSystem
{
    using System.Text;
    using Vintagestory.API.Common;
    using Vintagestory.API.Datastructures;

    public class BESeaweed : BlockEntity
    {
        private double plantedAt;
        private double blossomAt;
        private double growthTime = 12;
        private float minTemp;
        private float maxTemp;
        public long growthTick;
        private Block waterBlock;


        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            this.waterBlock = api.World.BlockAccessor.GetBlock(new AssetLocation("game:water-still-7"));
            this.growthTick = this.RegisterGameTickListener(this.GrowthMonitior, 3000);
        }


        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            base.OnBlockPlaced(byItemStack);
            var block = this.Api.World.BlockAccessor.GetBlock(this.Pos);
            this.plantedAt = this.Api.World.Calendar.TotalHours;

            if (block.Attributes == null)
            {
                this.minTemp = 1;
                this.maxTemp = 50f;
                this.blossomAt = this.Api.World.Calendar.TotalHours + this.growthTime;

                return;
            }

            if (this.Api.Side == EnumAppSide.Server)
            { this.blossomAt = this.Api.World.Calendar.TotalHours + ((block.Attributes["hours"].AsDouble(this.growthTime) * 0.75) + (((block.Attributes["hours"].AsDouble(this.growthTime) * 1.25) - (block.Attributes["hours"].AsDouble(this.growthTime) * 0.75)) * this.Api.World.Rand.NextDouble())); }

            this.minTemp = block.Attributes["minTemp"].AsFloat(-5f);
            this.maxTemp = block.Attributes["maxTemp"].AsFloat(50f);
        }


        public void GrowthMonitior(float dt)
        {
            //Determines if the plant is ready to blossom
            if (this.Api.World.BlockAccessor.GetBlock(this.Pos.UpCopy()).Id != this.waterBlock.Id || this.Api.World.BlockAccessor.GetBlock(this.Pos.UpCopy(2)).Id != this.waterBlock.Id)
            { return; }

            if (this.blossomAt > this.Api.World.Calendar.TotalHours)
            { return; }

            var temperature = this.Api.World.BlockAccessor.GetClimateAt(this.Pos, EnumGetClimateMode.NowValues).Temperature;

            if (temperature < this.minTemp || temperature > this.maxTemp)
            {
                this.blossomAt += 18;
                return;
            }

            var self = this.Api.World.BlockAccessor.GetBlock(this.Pos);
            var plantCode = self.CodeWithPart("section", 1);
            var plant = this.Api.World.GetBlock(plantCode);

            if (plant == null)
            { return; }

            this.Api.World.BlockAccessor.SetBlock(plant.Id, this.Pos);
            this.Api.World.BlockAccessor.SetBlock(self.Id, this.Pos.UpCopy());
        }


        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            //Displays how much time is left
            var daysleft = (this.blossomAt - this.Api.World.Calendar.TotalHours) / this.Api.World.Calendar.HoursPerDay;
            if (daysleft >= 1)
            {
                dsc.AppendLine((int)daysleft + " days until mature.");
            }
            else
            {
                dsc.AppendLine("Less than one day until mature.");
            }

            if (this.Api.World.BlockAccessor.GetClimateAt(this.Pos, EnumGetClimateMode.NowValues).Temperature > this.maxTemp)
            { dsc.AppendLine("Too hot to grow!"); }
            if (this.Api.World.BlockAccessor.GetClimateAt(this.Pos, EnumGetClimateMode.NowValues).Temperature < this.minTemp)
            { dsc.AppendLine("Too cold to grow!"); }
        }


        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetDouble("plantedAt", this.plantedAt);
            tree.SetDouble("blossomAt", this.blossomAt);
            tree.SetDouble("growthTime", this.growthTime);
            tree.SetFloat("minTemp", this.minTemp);
            tree.SetFloat("maxTemp", this.maxTemp);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);
            this.plantedAt = tree.GetDouble("plantedAt");
            this.blossomAt = tree.GetDouble("blossomAt");
            this.growthTime = tree.GetDouble("growthTime");
            this.minTemp = tree.GetFloat("minTemp");
            this.maxTemp = tree.GetFloat("maxTemp");
        }
    }
}
