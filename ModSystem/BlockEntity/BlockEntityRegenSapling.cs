namespace WildFarmingRevival.ModSystem
{
    using System.Text;
    using Vintagestory.API.Client;
    using Vintagestory.API.Common;
    using Vintagestory.API.Common.Entities;
    using Vintagestory.API.Config;
    using Vintagestory.API.Datastructures;
    using Vintagestory.API.MathTools;
    using Vintagestory.API.Server;
    using Vintagestory.API.Util;
    using Vintagestory.GameContent;


    public class BlockEntityRegenSapling : BlockEntity, ITreePoi
    {
        private double totalHoursTillGrowth;
        private long growListenerId;
        private EnumTreeGrowthStage stage;
        private bool plantedFromSeed;
        private float maxTemp;
        private float minTemp;
        public IBulkBlockAccessor changer;
        private POIRegistry treeFinder;

        private MeshData DirtMoundMesh
        {
            get
            {
                if (!(this.Api is ICoreClientAPI capi))
                { return null; }

                return ObjectCacheUtil.GetOrCreate(this.Api, "dirtMoundMesh", () =>
                {

                    var shape = capi.Assets.TryGet(AssetLocation.Create("shapes/block/plant/dirtmound.json", this.Block.Code.Domain))?.ToObject<Shape>();
                    capi.Tesselator.TesselateShape(this.Block, shape, out var mesh);

                    return mesh;
                });
            }
        }


        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            this.minTemp = this.Block.Attributes["minTemp"].AsFloat(0f);
            this.maxTemp = this.Block.Attributes["maxTemp"].AsFloat(60f);
            this.changer = this.Api.World.GetBlockAccessorBulkUpdate(true, true);
            this.changer.ReadFromStagedByDefault = true;

            if (api is ICoreServerAPI)
            {
                this.growListenerId = this.RegisterGameTickListener(this.CheckGrow, 2000);
                this.treeFinder = api.ModLoader.GetModSystem<POIRegistry>();
                this.treeFinder.AddPOI(this);
            }
        }

        private NatFloat NextStageDaysRnd
        {
            get
            {
                if (this.stage == EnumTreeGrowthStage.Seed)
                {
                    var sproutDays = NatFloat.create(EnumDistribution.UNIFORM, 1.5f, 0.5f);
                    if (this.Block?.Attributes != null)
                    {
                        return this.Block.Attributes["growthDays"].AsObject(sproutDays);
                    }
                    return sproutDays;
                }

                var matureDays = NatFloat.create(EnumDistribution.UNIFORM, 7f, 2f);
                if (this.Block?.Attributes != null)
                {
                    return this.Block.Attributes["matureDays"].AsObject(matureDays);
                }
                return matureDays;
            }
        }

        private float GrowthRateMod => this.Api.World.Config.GetString("saplingGrowthRate").ToFloat(1);

        public string Stage => this.stage == EnumTreeGrowthStage.Sapling ? "sapling" : "seed";

        public Vec3d Position => this.Pos.ToVec3d().Add(0.5);

        public string Type => "tree";

 
        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            this.stage = byItemStack?.Collectible is ItemTreeSeed ? EnumTreeGrowthStage.Seed : EnumTreeGrowthStage.Sapling;
            this.plantedFromSeed = this.stage == EnumTreeGrowthStage.Seed;
            this.totalHoursTillGrowth = this.Api.World.Calendar.TotalHours + (this.NextStageDaysRnd.nextFloat(1, this.Api.World.Rand) * 24 * this.GrowthRateMod);
        }


        private void CheckGrow(float dt)
        {
            if (this.Api.World.Calendar.TotalHours < this.totalHoursTillGrowth)
            { return; }

            var conds = this.Api.World.BlockAccessor.GetClimateAt(this.Pos, EnumGetClimateMode.NowValues);

            if (BotanyConfig.Loaded.HarshSaplingsEnabled)
            {
                if (conds == null)
                { return; }
                if (conds.Temperature < this.minTemp || conds.Temperature > this.maxTemp)
                {
                    if (conds.Temperature < this.minTemp - BotanyConfig.Loaded.TreeRevertGrowthTempThreshold || conds.Temperature > this.maxTemp + BotanyConfig.Loaded.TreeRevertGrowthTempThreshold)
                    {
                        this.totalHoursTillGrowth = this.Api.World.Calendar.TotalHours + ((float)this.Api.World.Rand.NextDouble() * 72 * this.GrowthRateMod);
                    }

                    return;
                }
            }
            else
            {
                if (conds == null || conds.Temperature < 5)
                {
                    return;
                }

                if (conds.Temperature < 0)
                {
                    this.totalHoursTillGrowth = this.Api.World.Calendar.TotalHours + ((float)this.Api.World.Rand.NextDouble() * 72 * this.GrowthRateMod);
                    return;
                }
            }

            if (this.stage == EnumTreeGrowthStage.Seed)
            {
                this.stage = EnumTreeGrowthStage.Sapling;
                this.totalHoursTillGrowth = this.Api.World.Calendar.TotalHours + (this.NextStageDaysRnd.nextFloat(1, this.Api.World.Rand) * 24 * this.GrowthRateMod);
                this.MarkDirty(true);
                return;
            }

            var chunksize = this.Api.World.BlockAccessor.ChunkSize;
            foreach (var facing in BlockFacing.HORIZONTALS)
            {
                var dir = facing.Normali;
                var x = this.Pos.X + (dir.X * chunksize);
                var z = this.Pos.Z + (dir.Z * chunksize);

                // Not at world edge and chunk is not loaded? We must be at the edge of loaded chunks. Wait until more chunks are generated
                if (this.Api.World.BlockAccessor.IsValidPos(new BlockPos(x, this.Pos.Y, z, 0)) && this.Api.World.BlockAccessor.GetChunkAtBlockPos(new BlockPos(x, this.Pos.Y, z, 0)) == null)
                { return; }
            }

            var block = this.Api.World.BlockAccessor.GetBlock(this.Pos);
            var treeGenCode = block.Attributes?["treeGen"].AsString(null);

            if (treeGenCode == null)
            {
                this.Api.Event.UnregisterGameTickListener(this.growListenerId);
                return;
            }

            var code = new AssetLocation(treeGenCode);
            var sapi = this.Api as ICoreServerAPI;

            if (!sapi.World.TreeGenerators.TryGetValue(code, out var gen))
            {
                this.Api.Event.UnregisterGameTickListener(this.growListenerId);
                return;
            }

            var doubleThick = false;
            var found = true;
            var trunk = this.Api.World.GetBlock(new AssetLocation("wildfarmingrevival:trunk-grown-maple"));

            switch (this.Block.Variant["wood"])
            {
                case null:
                    found = false;
                    break;
                case "greenbamboo":
                    found = false;
                    break;
                case "brownbamboo":
                    found = false;
                    break;
                case "ferntree":
                    found = false;
                    break;
                case "crimsonkingmaple":
                    trunk = this.Api.World.GetBlock(new AssetLocation("wildfarmingrevival:trunk-maple"));
                    break;
                case "greenspirecypress":
                    trunk = this.Api.World.GetBlock(new AssetLocation("wildfarmingrevival:trunk-baldcypress"));
                    break;
                default:
                    trunk = this.Api.World.GetBlock(new AssetLocation("wildfarmingrevival:trunk-" + this.Block.Variant["wood"]));
                    doubleThick = this.Block.Variant["wood"] == "redwood";
                    break;

            }

            float otherChance = 0f;
            if (BotanyConfig.Loaded.ResinGrowthEnabled)
            { otherChance = 1f; }

            if (BotanyConfig.Loaded.LivingTreesEnabled && found)
            {
                
                this.Api.World.BlockAccessor.SetBlock(trunk.BlockId, this.Pos);
                var size = BotanyConfig.Loaded.SaplingToTreeSize;

                var pa = new TreeGenParams()
                {
                    skipForestFloor = true,
                    size = size,
                    otherBlockChance = otherChance, //resin regrowth on living trees UNTESTED
                    vinesGrowthChance = 0,
                    mossGrowthChance = 0
                };
                sapi.World.TreeGenerators[code].GrowTree(this.changer, this.Pos.AddCopy(0, doubleThick ? 1 : 0, 0), pa);
                if (this.Api.World.BlockAccessor.GetBlockEntity(this.Pos) is BlockEntityTrunk growth)
                { growth.SetupTree(this.changer.Commit()); }
                else
                { this.changer.Commit(); }
            }
            else
            {
                this.Api.World.BlockAccessor.SetBlock(0, this.Pos);
                var size = 0.6f + ((float)this.Api.World.Rand.NextDouble() * 0.5f);
                var pa = new TreeGenParams()
                {
                    skipForestFloor = true,
                    size = size,
                    otherBlockChance = otherChance,
                    vinesGrowthChance = 0,
                    mossGrowthChance = 0
                };
                sapi.World.TreeGenerators[code].GrowTree(this.changer, this.Pos.DownCopy(), pa);
                this.changer.Commit();
            }
        }


        public override void OnBlockRemoved()
        {
            this.treeFinder?.RemovePOI(this);
            base.OnBlockRemoved();
        }


        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetDouble("totalHoursTillGrowth", this.totalHoursTillGrowth);
            tree.SetInt("growthStage", (int)this.stage);
            tree.SetBool("plantedFromSeed", this.plantedFromSeed);
        }


        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
            this.totalHoursTillGrowth = tree.GetDouble("totalHoursTillGrowth", 0);
            this.stage = (EnumTreeGrowthStage)tree.GetInt("growthStage", 1);
            this.plantedFromSeed = tree.GetBool("plantedFromSeed");
        }


        public ItemStack[] GetDrops()
        {
            if (this.stage == EnumTreeGrowthStage.Seed)
            {
                var item = this.Api.World.GetItem(AssetLocation.Create("treeseed-" + this.Block.Variant["wood"], this.Block.Code.Domain));
                return new ItemStack[] { new ItemStack(item) };
            }
            else
            {
                return new ItemStack[] { new ItemStack(this.Block) };
            }
        }


        public string GetBlockName()
        {
            if (this.stage == EnumTreeGrowthStage.Seed)
            {
                return Lang.Get("treeseed-planted-" + this.Block.Variant["wood"]);
            }
            else
            {
                return this.Block.OnPickBlock(this.Api.World, this.Pos).GetName();
            }
        }


        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            base.GetBlockInfo(forPlayer, dsc);

            var hoursleft = this.totalHoursTillGrowth - this.Api.World.Calendar.TotalHours;
            var daysleft = hoursleft / this.Api.World.Calendar.HoursPerDay;

            if (this.stage == EnumTreeGrowthStage.Seed)
            {
                if (daysleft <= 1)
                {
                    dsc.AppendLine(Lang.Get("Will sprout in less than a day"));
                }
                else
                {
                    dsc.AppendLine(Lang.Get("Will sprout in about {0} days", (int)daysleft));
                }
            }
            else
            {

                if (daysleft <= 1)
                {
                    dsc.AppendLine(Lang.Get("Will mature in less than a day"));
                }
                else
                {
                    dsc.AppendLine(Lang.Get("Will mature in about {0} days", (int)daysleft));
                }
            }

            if (BotanyConfig.Loaded.HarshSaplingsEnabled)
            {
                var conds = this.Api.World.BlockAccessor.GetClimateAt(this.Pos, EnumGetClimateMode.NowValues);

                if (conds.Temperature < this.minTemp)
                { dsc.AppendLine(Lang.Get("wildfarmingrevival:tree-cold")); }
                else if (conds.Temperature > this.maxTemp)
                { dsc.AppendLine(Lang.Get("wildfarmingrevival:tree-hot")); }
            }
        }


        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
        {
            if (this.plantedFromSeed)
            {
                mesher.AddMeshData(this.DirtMoundMesh);
            }
            if (this.stage == EnumTreeGrowthStage.Seed)
            { return true; }
            return base.OnTesselation(mesher, tessThreadTesselator);
        }


        //public bool IsSuitableFor(Entity entity, string[] diet) //ehm-93
        public bool IsSuitableFor(Entity entity, CreatureDiet diet) //ehm-93
        {
            //diet = entity.Properties.Attributes?["blockDiet"]?.AsArray<string>(); //ehm-93
            if (diet == null)
            { return false; }
            //return diet.Contains("Wood"); //ehm-93
            return diet.Matches(EnumFoodCategory.NoNutrition, "Wood"); //ehm-93
        }


        //public float ConsumeOnePortion() //ehm-93
        public float ConsumeOnePortion(Entity entity) //ehm-93
        {
            if (0.05f > this.Api.World.Rand.NextDouble())
            {
                this.Api.World.BlockAccessor.BreakBlock(this.Pos, null);
                return 1;
            }
            return 0.1f;
        }
    }
}
