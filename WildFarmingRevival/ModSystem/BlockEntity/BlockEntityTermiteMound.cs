namespace WildFarmingRevival.ModSystem
{
    using Vintagestory.API.Common;
    using Vintagestory.API.Datastructures;
    //using Vintagestory.API.MathTools;
    using Vintagestory.GameContent;

    public class BlockEntityTermiteMound : BlockEntity
    {
        private double lastChecked;
        private float colonySupplies;
        private POIRegistry treeFinder;


        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            if (api.Side == EnumAppSide.Server)
            {
                this.RegisterGameTickListener(this.OnServerTick, 1000);
                this.treeFinder = api.ModLoader.GetModSystem<POIRegistry>();
            }
        }


        private void OnServerTick(float dt)
        {
            if (!BotanyConfig.Loaded.TermitesEnabled || this.Api.Side != EnumAppSide.Server || this.Api.World.Calendar.TotalHours - this.lastChecked < 1)
            { return; }

            this.lastChecked = this.Api.World.Calendar.TotalHours;
            var newNest = false;

            this.treeFinder.WalkPois(this.Pos.ToVec3d().Add(0.5), 30, (poi) =>
            {
                if (!(poi is ITreePoi tree))
                { return true; }
                this.colonySupplies += tree.ConsumeOnePortion(null);
                if (this.colonySupplies > 100 && !newNest)
                {
                    var swarmPos = tree.Position.AsBlockPos.Add(-2, 0, -2);
                    for (var x = 0; x < 3; x++)
                    {
                        swarmPos.X++;
                        for (var z = 0; z < 3; z++)
                        {
                            swarmPos.Z++;
                            var candidate = this.Api.World.BlockAccessor.GetBlock(swarmPos);
                            if (!candidate.IsLiquid() && candidate.IsReplacableBy(this.Block))
                            {
                                this.Api.World.BlockAccessor.SetBlock(this.Block.Id, swarmPos);
                                newNest = true;
                                this.colonySupplies -= 100;
                            }
                            if (newNest)
                            { break; }
                        }
                        if (newNest)
                        { break; }
                        swarmPos.Z -= 3;
                    }
                }
                return true;
            });
        }


        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetDouble("lastChecked", this.lastChecked);
        }


        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);
            this.lastChecked = tree.GetDouble("lastChecked", this.Api?.World.Calendar.TotalHours ?? 1);
        }


        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            base.OnBlockPlaced(byItemStack);
            this.lastChecked = this.Api.World.Calendar.TotalHours;
        }
    }
}
