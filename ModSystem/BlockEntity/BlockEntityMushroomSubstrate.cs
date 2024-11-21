namespace WildFarmingRevival.ModSystem
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Text;
    using Vintagestory.API.Common;
    using Vintagestory.API.Config;
    using Vintagestory.API.Datastructures;
    using Vintagestory.API.MathTools;
    using Vintagestory.API.Util;
    using Vintagestory.GameContent;
    using Vintagestory.API.Client;

    public class BlockEntityMushroomSubstrate : BlockEntityDisplayCase, ITexPositionSource
    {
        //public float DieWhenTempBelow = 0;
        //public bool DieAfterFruiting = true;

        private Vec3i[] grownMushroomOffsets = new Vec3i[0];

        private double mushroomsGrownTotalDays = 0;
        private double mushroomsDiedTotalDays = -999999;
        private double mushroomsGrowingDays = 0;
        private double lastUpdateTotalDays = 0;

        private string mushroomBlockCode = "";

        //is 20 in BEMycelium so I changed this
        private double fruitingDays = 20; //30
        private double growingDays = 20;
        private readonly int growRange = 7;
        private static readonly Random Rnd = new Random();
        //private MushroomProps props;


        public bool IsGrowing { get; private set; }



        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            //if this still crashes in MP, remove this if statement
            if (api.Side == EnumAppSide.Server)
            {
                var interval = 10000;
                this.RegisterGameTickListener(this.OnBlockTick, interval);
            }
        }


        private void OnBlockTick(float dt)
        {
            if (!this.IsGrowing)
            { return; }

            MushroomProps tempProps = null;
            var thisAssetLoc = new AssetLocation(this.mushroomBlockCode);
            if (thisAssetLoc != null)
            {
                var thisAsset = this.Api.World.GetBlock(thisAssetLoc);
                if (thisAsset == null)
                { return; }

                var isFruiting = this.grownMushroomOffsets.Length > 0;
                tempProps = thisAsset.Attributes["mushroomProps"].AsObject<MushroomProps>();
                if (tempProps != null)
                {
                    //Prevent growing when temperatures are out of range
                    if (isFruiting && tempProps.DieWhenTempBelow > -99)
                    {
                        var conds = this.Api.World.BlockAccessor.GetClimateAt(this.Pos, EnumGetClimateMode.ForSuppliedDate_TemperatureOnly, this.Api.World.Calendar.TotalDays);
                        if (conds == null)
                        { return; }
                        if (tempProps.DieWhenTempBelow > conds.Temperature)
                        {
                            this.DestroyGrownMushrooms();
                            return;
                        }
                    }

                    if (tempProps.DieAfterFruiting && isFruiting && this.mushroomsGrownTotalDays + this.fruitingDays < this.Api.World.Calendar.TotalDays)
                    {
                        this.DestroyGrownMushrooms();
                        return;
                    }

                    if (!isFruiting)
                    {
                        this.lastUpdateTotalDays = Math.Max(this.lastUpdateTotalDays, this.Api.World.Calendar.TotalDays - 50); // Don't check more than 50 days into the past

                        while (this.Api.World.Calendar.TotalDays - this.lastUpdateTotalDays > 1)
                        {
                            var conds = this.Api.World.BlockAccessor.GetClimateAt(this.Pos, EnumGetClimateMode.ForSuppliedDate_TemperatureOnly, this.lastUpdateTotalDays + 0.5);
                            if (conds == null)
                            { return; }

                            if (conds.Temperature > 5)
                            {
                                this.mushroomsGrowingDays += this.Api.World.Calendar.TotalDays - this.lastUpdateTotalDays;
                            }
                            this.lastUpdateTotalDays++;
                        }

                        if (this.mushroomsGrowingDays > this.growingDays)
                        {

                            this.GrowMushrooms(this.Api.World.BlockAccessor, thisAsset);
                            this.mushroomsGrowingDays = 0;
                        }
                    }
                    else
                    {
                        if (this.Api.World.Calendar.TotalDays - this.lastUpdateTotalDays > 0.1)
                        {
                            this.lastUpdateTotalDays = this.Api.World.Calendar.TotalDays;

                            for (var i = 0; i < this.grownMushroomOffsets.Length; i++)
                            {
                                var offset = this.grownMushroomOffsets[i];
                                var pos = this.Pos.AddCopy(offset);
                                var chunk = this.Api.World.BlockAccessor.GetChunkAtBlockPos(pos);
                                if (chunk == null)
                                { return; }

                                if (!this.Api.World.BlockAccessor.GetBlock(pos).Code.Equals(this.mushroomBlockCode))
                                {
                                    this.grownMushroomOffsets = this.grownMushroomOffsets.RemoveEntry(i);
                                    i--;
                                }
                            }
                        }
                    }
                    this.MarkDirty();
                }
            }
        }


        private void DestroyGrownMushrooms()
        {
            var thisAssetLoc = new AssetLocation(this.mushroomBlockCode);
            if (thisAssetLoc != null)
            {
                var thisAsset = this.Api.World.GetBlock(thisAssetLoc);
                if (thisAsset == null)
                { return; }

                this.mushroomsDiedTotalDays = this.Api.World.Calendar.TotalDays;
                foreach (var offset in this.grownMushroomOffsets)
                {
                    var pos = this.Pos.AddCopy(offset);
                    var block = this.Api.World.BlockAccessor.GetBlock(pos);
                    if (block.Variant["mushroom"] == thisAsset.Variant["mushroom"])
                    {
                        this.Api.World.BlockAccessor.SetBlock(0, pos);
                    }
                }
                this.grownMushroomOffsets = new Vec3i[0];
                this.MarkDirty();
            }
        }


        public bool SetMushroomBlock(Block block)
        {
            if (block == null)
            { return false; }
            if (!block.Code.Path.StartsWith("mushroom-"))
            { return false; }
            if (block?.Attributes?["mushroomProps"].Exists != true)
            { return false; }

            this.mushroomBlockCode = block.Code.Path;
            this.mushroomsGrownTotalDays = 0;
            this.mushroomsDiedTotalDays = -999999;
            this.mushroomsGrowingDays = 0;
            this.lastUpdateTotalDays = this.Api.World.Calendar.TotalDays;
            this.fruitingDays = 20 + Rnd.Next(21);
            this.growingDays = 10 + Rnd.Next(11);
            this.mushroomsGrownTotalDays = 0.0;
            this.IsGrowing = true;
            this.MarkDirty();
            return true;
        }


        private void GrowMushrooms(IBlockAccessor blockAccessor, Block mushroomBlock)
        {
            double rnd = Rnd.Next(101) / 100; //0-1
            var sidegrowing = mushroomBlock.Variant.ContainsKey("side");
            if (sidegrowing)
            {
                this.GenerateSideGrowingMushrooms(blockAccessor, mushroomBlock);
            }
            else
            {
                this.GenerateUpGrowingMushrooms(blockAccessor, mushroomBlock);
            }

            this.mushroomsGrownTotalDays = (mushroomBlock as BlockMushroom).Api.World.Calendar.TotalDays - (Rnd.NextDouble() * this.fruitingDays);
            this.MarkDirty();
        }


        private void GenerateUpGrowingMushrooms(IBlockAccessor blockAccessor, Block mushroomBlock)
        {
            var cnt = 2 + Rnd.Next(11);
            var pos = new BlockPos(0,0,0,0);
            var chunkSize = blockAccessor.ChunkSize;
            var offsets = new List<Vec3i>();

            if (!this.IsChunkAreaLoaded(blockAccessor, this.growRange))
            { return; }

            while (cnt-- > 0)
            {
                var dx = this.growRange - Rnd.Next((2 * this.growRange) + 1);
                var dz = this.growRange - Rnd.Next((2 * this.growRange) + 1);
                pos.Set(this.Pos.X + dx, 0, this.Pos.Z + dz);
                var mapChunk = blockAccessor.GetMapChunkAtBlockPos(pos);
                var lx = GameMath.Mod(pos.X, chunkSize);
                var lz = GameMath.Mod(pos.Z, chunkSize);
                pos.Y = this.Pos.Y + 1;
                var hereBlock = blockAccessor.GetBlock(pos);
                var belowBlock = blockAccessor.GetBlock(new BlockPos(pos.X, pos.Y - 1, pos.Z, 0));

                if (belowBlock.Fertility < 10 || hereBlock.LiquidCode != null)
                { continue; }

                if ((this.mushroomsGrownTotalDays == 0 && hereBlock.Replaceable >= 6000) || hereBlock.Id == 0)
                {
                    blockAccessor.SetBlock(mushroomBlock.Id, pos);
                    offsets.Add(new Vec3i(dx, pos.Y - this.Pos.Y, dz));
                }
            }
            this.grownMushroomOffsets = offsets.ToArray();
        }


        private bool IsChunkAreaLoaded(IBlockAccessor blockAccessor, int growRange)
        {
            var chunksize = blockAccessor.ChunkSize;
            var mincx = (this.Pos.X - growRange) / chunksize;
            var maxcx = (this.Pos.X + growRange) / chunksize;
            var mincz = (this.Pos.Z - growRange) / chunksize;
            var maxcz = (this.Pos.Z + growRange) / chunksize;

            for (var cx = mincx; cx <= maxcx; cx++)
            {
                for (var cz = mincz; cz <= maxcz; cz++)
                {
                    if (blockAccessor.GetChunk(cx, this.Pos.Y / chunksize, cz) == null)
                    { return false; }
                }
            }
            return true;
        }


        private void GenerateSideGrowingMushrooms(IBlockAccessor blockAccessor, Block mushroomBlock)
        {
            var cnt = 1 + Rnd.Next(5);
            var mpos = new BlockPos(0,0,0,0);
            var offsets = new List<Vec3i>();

            while (cnt-- > 0)
            {
                var dx = 0;
                var dy = 1 + Rnd.Next(5);
                var dz = 0;
                mpos.Set(this.Pos.X + dx, this.Pos.Y + dy, this.Pos.Z + dz);
                var block = blockAccessor.GetBlock(mpos);

                if (!(block is BlockLog) || !this.RightWood(block.Variant["wood"], mushroomBlock) || block.Variant["type"] == "resin")
                { continue; }

                BlockFacing facing = null;
                var rndside = Rnd.Next(4);

                for (var j = 0; j < 4; j++)
                {
                    var f = BlockFacing.HORIZONTALS[(j + rndside) % 4];
                    mpos.Set(this.Pos.X + dx, this.Pos.Y + dy, this.Pos.Z + dz).Add(f);
                    var nblock = blockAccessor.GetBlock(mpos);
                    if (nblock.Id != 0)
                    { continue; }

                    facing = f.Opposite;
                    break;

                }

                if (facing == null)
                { continue; }

                var mblock = blockAccessor.GetBlock(mushroomBlock.CodeWithVariant("side", facing.Code));
                blockAccessor.SetBlock(mblock.Id, mpos);
                offsets.Add(new Vec3i(mpos.X - this.Pos.X, mpos.Y - this.Pos.Y, mpos.Z - this.Pos.Z));
            }
            this.grownMushroomOffsets = offsets.ToArray();
        }


        private bool RightWood(string type, Block mushroomBlock)
        {
            var treeType = mushroomBlock.Attributes?["needsTree"].AsString(null);
            if (treeType == null)
            { return true; }
            return treeType == WildFarmingRevivalSystem.GetTreeFamily(type);
        }


        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);
            this.mushroomBlockCode = tree.GetString("mushroomBlockCode");
            this.grownMushroomOffsets = tree.GetVec3is("grownMushroomOffsets");

            this.mushroomsGrownTotalDays = tree.GetDouble("mushromsGrownTotalDays");
            this.mushroomsDiedTotalDays = tree.GetDouble("mushroomsDiedTotalDays");
            this.lastUpdateTotalDays = tree.GetDouble("lastUpdateTotalDays");
            this.mushroomsGrowingDays = tree.GetDouble("mushroomsGrowingDays");

            this.IsGrowing = tree.GetInt("growing") > 0;
            this.growingDays = tree.GetDouble("growingDays", 20);
            this.fruitingDays = tree.GetDouble("fruitingDays", 30);
            if (this.Api != null)
            {
                if (this.Api.Side == EnumAppSide.Client)
                { this.Api.World.BlockAccessor.MarkBlockDirty(this.Pos); }
            }
        }


        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetString("mushroomBlockCode", this.mushroomBlockCode);
            tree.SetVec3is("grownMushroomOffsets", this.grownMushroomOffsets);

            tree.SetDouble("mushromsGrownTotalDays", this.mushroomsGrownTotalDays);
            tree.SetDouble("mushroomsDiedTotalDays", this.mushroomsDiedTotalDays);
            tree.SetDouble("lastUpdateTotalDays", this.lastUpdateTotalDays);
            tree.SetDouble("mushroomsGrowingDays", this.mushroomsGrowingDays);

            tree.SetInt("growing", this.IsGrowing ? 1 : 0);
            tree.SetDouble("fruitingDays", this.fruitingDays);
            tree.SetDouble("growingDays", this.growingDays);
        }


        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            MushroomProps tempProps = null;
            dsc.AppendLine();
            if (this.mushroomBlockCode == "")
            {
                dsc.AppendLine(Lang.Get("wildfarmingrevival:msubstrate-barren"));
                return;
            }
            dsc.AppendLine(Lang.Get("wildfarmingrevival:msubstrate-incubating", Lang.GetMatching("block-" + this.mushroomBlockCode)));

            var thisAssetLoc = new AssetLocation(this.mushroomBlockCode);
            if (thisAssetLoc != null)
            {
                var thisAsset = this.Api.World.GetBlock(thisAssetLoc);
                if (thisAsset == null)
                { return; }

                tempProps = thisAsset.Attributes["mushroomProps"].AsObject<MushroomProps>();

                if (thisAsset.Variant.ContainsKey("side"))
                {
                    var treeType = thisAsset.Attributes?["needsTree"].AsString(null);
                    if (treeType != null)
                    {
                        dsc.AppendLine(Lang.Get("wildfarmingrevival:mushroom-" + treeType));
                    }
                }
            }

            if (this.grownMushroomOffsets.Length > 0)
            {
                var fruitTime = this.mushroomsGrowingDays + this.fruitingDays; // - this.Api.World.Calendar.TotalDays;
                if (fruitTime >= 0)
                { dsc.AppendLine(Lang.Get("wildfarmingrevival:msubstrate-fruiting", fruitTime.ToString("#.#"))); }
            }
            else
            {
                var growTime = this.growingDays - this.mushroomsGrowingDays;
                if (this.mushroomsGrowingDays >= 0)
                { dsc.AppendLine(Lang.Get("wildfarmingrevival:msubstrate-growing", growTime.ToString("#.#"))); }
            }

            var debug = false;

            if (debug)
            {
                dsc.AppendLine();
                dsc.AppendLine("DEBUG");
                dsc.AppendLine("Growing: " + this.IsGrowing.ToString());

                dsc.AppendLine("mushromsGrownTotalDays: " + Math.Round(this.mushroomsGrownTotalDays, 0).ToString());
                dsc.AppendLine("mushroomsDiedTotalDays: " + Math.Round(this.mushroomsDiedTotalDays, 1).ToString());
                dsc.AppendLine("lastUpdateTotalDays: " + Math.Round(this.lastUpdateTotalDays, 1).ToString());
                dsc.AppendLine("mushroomsGrowingDays: " + Math.Round(this.mushroomsGrowingDays, 1).ToString());

                dsc.AppendLine("fruitingDays: " + this.fruitingDays.ToString());
                dsc.AppendLine("growingDays: " + this.growingDays.ToString());

                if (tempProps != null)
                {
                    dsc.AppendLine("DieAfterFruiting: " + tempProps.DieAfterFruiting.ToString());
                    dsc.AppendLine("DieWhenTempBelow: " + tempProps.DieWhenTempBelow.ToString());
                }
            }
        }


        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            this.IsGrowing = false;
            this.mushroomBlockCode = "";
            base.OnBlockPlaced(byItemStack);
        }


        /*
        public void SetItemstackAttributes(ItemStack stack)
        {
            if (stack == null || this.mushroomBlock == null)
            { return; }
            stack.Attributes.SetString("mushroomBlockCode", this.mushroomBlockCode?.ToShortString());
        }
        */


        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            MeshData mesh;
            var block = this.Api.World.BlockAccessor.GetBlock(this.Pos, BlockLayersAccess.Default) as BlockMushroomSubstrate;
            if (this.mushroomBlockCode == "")
            {
                mesh = this.capi.TesselatorManager.GetDefaultBlockMesh(block); //add normal block
                mesher.AddMeshData(mesh);
            }
            else //render spores
            {
                var block2 = this.Api.World.BlockAccessor.GetBlock(new AssetLocation("wildfarmingrevival:mushroomsubstrateincubated"));
                var shapePath = "game:shapes/block/basic/cube";
                var texture = tesselator.GetTextureSource(block2);
                mesh = block.GenMesh(this.Api as ICoreClientAPI, shapePath, texture);
                if (mesh != null)
                { mesher.AddMeshData(mesh); }
            }
            return true;
        }
    }
}
