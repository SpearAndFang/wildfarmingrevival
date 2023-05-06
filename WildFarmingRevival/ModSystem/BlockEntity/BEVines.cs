namespace WildFarmingRevival.ModSystem
{
    using System.Text;
    using Vintagestory.API.Common;
    using Vintagestory.API.Datastructures;
    using Vintagestory.GameContent;

    public class BEVines : BlockEntity
    {
        private double plantedAt;
        private double blossomAt;
        private double growthTime = 12;
        private float minTemp;
        private float maxTemp;
        private RoomRegistry rmaker;
        private bool greenhouse;
        public long growthTick;


        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            this.rmaker = api.ModLoader.GetModSystem<RoomRegistry>();
            this.growthTick = this.RegisterGameTickListener(this.GrowthMonitor, 3000);
        }


        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            base.OnBlockPlaced(byItemStack);
            var block = this.Api.World.BlockAccessor.GetBlock(this.Pos);
            this.plantedAt = this.Api.World.Calendar.TotalHours;
            if (this.Api.Side == EnumAppSide.Server)
            {
                this.blossomAt = this.Api.World.Calendar.TotalHours + ((block.Attributes["hours"].AsDouble(this.growthTime) * 0.75) + (((block.Attributes["hours"].AsDouble(this.growthTime) * 1.25) - (block.Attributes["hours"].AsDouble(this.growthTime) * 0.75)) * this.Api.World.Rand.NextDouble()));
            }
            this.minTemp = block.Attributes["minTemp"].AsFloat(-5f);
            this.maxTemp = block.Attributes["maxTemp"].AsFloat(50f);
        }


        public void GrowthMonitor(float dt)
        {
            //Determines if the plant is ready to blossom
            if (this.Api.World.BlockAccessor.GetBlock(this.Pos.DownCopy()).Id != 0)
            { return; }

            //FIX Random Crashes by losing the room registry crap
            //var room = this.rmaker?.GetRoomForPosition(this.Pos);
            //this.greenhouse = room != null && room.SkylightCount > room.NonSkylightCount && room.ExitCount == 0;
            this.greenhouse = false;

            if (this.blossomAt > this.Api.World.Calendar.TotalHours)
            { return; }
            var temperature = this.Api.World.BlockAccessor.GetClimateAt(this.Pos, EnumGetClimateMode.NowValues).Temperature;

            if (temperature < this.minTemp || temperature > this.maxTemp)
            {
                this.blossomAt += 18;
                return;
            }

            var self = this.Api.World.BlockAccessor.GetBlock(this.Pos);
            if (self == null)
            { return; }

            var plantCode = self.CodeWithPart("section", 1);
            var plant = this.Api.World.GetBlock(plantCode);

            if (plant == null)
            { return; }

            this.Api.World.BlockAccessor.SetBlock(plant.Id, this.Pos);
            this.Api.World.BlockAccessor.SetBlock(self.Id, this.Pos.DownCopy());
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

            if (this.Api.World.BlockAccessor.GetClimateAt(this.Pos, EnumGetClimateMode.NowValues).Temperature > this.maxTemp && !this.greenhouse)
            { dsc.AppendLine("Too hot to grow!"); }
            if (this.Api.World.BlockAccessor.GetClimateAt(this.Pos, EnumGetClimateMode.NowValues).Temperature < this.minTemp && !this.greenhouse)
            { dsc.AppendLine("Too cold to grow!"); }
            if (this.greenhouse)
            { dsc.AppendLine("Greenhouse bonus!"); }
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
