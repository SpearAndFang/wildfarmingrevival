namespace WildFarmingRevival.ModSystem
{
    //using System;
    using System.Text;
    using Vintagestory.API.Common;
    using Vintagestory.API.Datastructures;
    //using Vintagestory.API.MathTools;
    using Vintagestory.GameContent;

    public class WildPlantBlockEntity : BlockEntity
    {
        private double plantedAt;
        private double blossomAt;
        private double growthTime = 72;
        private float minTemp;
        private float maxTemp;
        private RoomRegistry rmaker;
        private bool greenhouse;


        public override void Initialize(ICoreAPI api)
        {
            // Registers the updater
            base.Initialize(api);
            this.RegisterGameTickListener(this.UpdateStep, 1200);
            this.rmaker = api.ModLoader.GetModSystem<RoomRegistry>();
        }


        public override void OnBlockPlaced(ItemStack byItemStack)
        {
            var growthMultiplier = 1.0;
            if (BotanyConfig.Loaded.SeedlingGrowthRateMultiplier > 0.0)
            {
                growthMultiplier = 1.0 / BotanyConfig.Loaded.SeedlingGrowthRateMultiplier;
            }

            //Sets up the properties
            var block = this.Api.World.BlockAccessor.GetBlock(this.Pos);
            this.plantedAt = this.Api.World.Calendar.TotalHours;
            if (this.Api.Side == EnumAppSide.Server)
            { this.blossomAt = this.Api.World.Calendar.TotalHours + ((block.Attributes["hours"].AsDouble(this.growthTime) * 0.75) + (((block.Attributes["hours"].AsDouble(this.growthTime) * 1.25 * growthMultiplier) - (block.Attributes["hours"].AsDouble(this.growthTime) * 0.75)) * this.Api.World.Rand.NextDouble())); }
            this.minTemp = block.Attributes["minTemp"].AsFloat(-5f);
            this.maxTemp = block.Attributes["maxTemp"].AsFloat(50f);
        }


        public void UpdateStep(float step)
        {
            //Determines if the plant is ready to blossom
            if (this.Api.Side != EnumAppSide.Server)
            { return; }
            var room = this.rmaker?.GetRoomForPosition(this.Pos);
            this.greenhouse = room != null && room.SkylightCount > room.NonSkylightCount && room.ExitCount == 0;

            if (this.blossomAt > this.Api.World.Calendar.TotalHours)
            { return; }
            var conds = this.Api.World.BlockAccessor.GetClimateAt(this.Pos, EnumGetClimateMode.NowValues);

            if (conds == null)
            { return; }

            if (BotanyConfig.Loaded.HarshWildPlants && (conds.Temperature < this.minTemp || conds.Temperature > this.maxTemp) && !this.greenhouse)
            {
                this.blossomAt += 18;
                return;
            }

            var plantCode = this.Block.CodeEndWithoutParts(1);
            if (plantCode == null)
            { this.Api.World.BlockAccessor.BreakBlock(this.Pos, null); }
            else
            {
                var plant = this.Api.World.GetBlock(new AssetLocation("game:" + plantCode));
                this.Api.World.BlockAccessor.SetBlock(plant.Id, this.Pos);
            }
            this.MarkDirty();
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

            if (!BotanyConfig.Loaded.HarshWildPlants)
            { return; }

            if (this.Api.World.BlockAccessor.GetClimateAt(this.Pos, EnumGetClimateMode.NowValues).Temperature > this.maxTemp && !this.greenhouse)
            { dsc.AppendLine("Too hot to grow!"); }
            if (this.Api.World.BlockAccessor.GetClimateAt(this.Pos, EnumGetClimateMode.NowValues).Temperature < this.minTemp && !this.greenhouse)
            { dsc.AppendLine("Too cold to grow!"); }
            if (this.greenhouse)
            { dsc.AppendLine("Greenhouse bonus!"); }
        }


        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            // Saves Properties
            base.ToTreeAttributes(tree);
            tree.SetDouble("plantedAt", this.plantedAt);
            tree.SetDouble("blossomAt", this.blossomAt);
            tree.SetDouble("growthTime", this.growthTime);
            tree.SetFloat("minTemp", this.minTemp);
            tree.SetFloat("maxTemp", this.maxTemp);
        }


        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            // Gets Properties
            base.FromTreeAttributes(tree, worldAccessForResolve);
            this.plantedAt = tree.GetDouble("plantedAt");
            this.blossomAt = tree.GetDouble("blossomAt");
            this.growthTime = tree.GetDouble("growthTime");
            this.minTemp = tree.GetFloat("minTemp");
            this.maxTemp = tree.GetFloat("maxTemp");
        }
    }
}
