namespace WildFarmingRevival.ModSystem
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Vintagestory.API.Common;
    using Vintagestory.API.Common.Entities;
    using Vintagestory.API.Config;
    using Vintagestory.API.Datastructures;
    using Vintagestory.API.MathTools;
    using Vintagestory.API.Server;
    using Vintagestory.API.Util;
    using Vintagestory.GameContent;


    public class BlockEntityTrunk : BlockEntity, ITreePoi
    {
        //Set at runtime/from saved attributes
        private Vec4i[] tree;
        private Vec4i[] leaves;
        private string[] codes;
        private float regenPerc;
        private float logRecovery;
        private double lastChecked;
        private int growthStage;
        private int currentGrowthTime;
        private int currentRepopTime;
        private POIRegistry treeFinder;
        private TreeFriend[] repopBuddies;
        private float friendsWeight;
        private readonly BlockFacing[] rndFaces = { BlockFacing.NORTH, BlockFacing.SOUTH, BlockFacing.EAST, BlockFacing.WEST };
        private string treeFamily;
        private GasHelper gasPlug;

        //Leaves for health calculation
        private int brokenLeaves;
        private int diseasedParts;

        private int CurrentHealthyParts
        {
            get
            {
                if (this.tree == null)
                { return 0; }
                var trueCount = this.tree.Length - this.diseasedParts;
                return trueCount < 0 ? 0 : trueCount;
            }
        }


        private int CurrentLeaves
        {
            get
            {
                if (this.leaves == null)
                { return 0; }
                var trueCount = this.leaves.Length - this.brokenLeaves;
                return trueCount < 0 ? 0 : trueCount;
            }
        }


        private float CurrentHealth
        {
            get
            {
                var leavesHealth = this.CurrentLeaves / (float)this.leaves.Length;
                var treeHealth = this.CurrentHealthyParts / (float)this.tree.Length;
                return (0.5f * leavesHealth) + (0.5f * treeHealth);
            }
        }


        //Set from json attributes
        private Block deathBlock;
        private int timeForNextStage;
        private float maxTemp;
        private float minTemp;

        //For chunk checking
        private int mincx, mincy, mincz, maxcx, maxcy, maxcz;

        //For block setting
        public IBulkBlockAccessor changer;

        public string Stage => this.growthStage >= BotanyConfig.Loaded.MaxTreeGrowthStages ? "mature" : "young-" + this.growthStage;

        public Vec3d Position => this.Pos.ToVec3d().Add(0.5);

        public string Type => "tree";

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            this.RegisterGameTickListener(this.Regenerate, 3000);
            this.gasPlug = api.ModLoader.GetModSystem<GasHelper>();

            this.deathBlock = api.World.GetBlock(new AssetLocation(this.Block.Attributes["deathState"].AsString("game:air")));
            this.timeForNextStage = this.Block.Attributes["growthTime"].AsInt(96);

            this.minTemp = this.Block.Attributes["minTemp"].AsFloat(0f);
            this.maxTemp = this.Block.Attributes["maxTemp"].AsFloat(60f);

            var jsonFriends = this.Block.Attributes["treeFriends"].AsObject<TreeFriend[]>();

            if (jsonFriends != null && jsonFriends.Length > 0)
            {
                var checkFriends = new List<TreeFriend>();
                for (var i = 0; i < jsonFriends.Length; i++)
                {
                    var resolved = jsonFriends[i].Resolve(api);
                    if (resolved)
                    {
                        checkFriends.Add(jsonFriends[i]);
                        this.friendsWeight += jsonFriends[i].weight;
                    }
                }
                this.repopBuddies = checkFriends.ToArray();
            }
            else
            {
                this.repopBuddies = new TreeFriend[0];
            }
            this.treeFamily = WildFarmingRevivalSystem.GetTreeFamily(this.Block.Attributes?["treeGen"].AsString(null));

            if (api.Side == EnumAppSide.Server)
            {
                this.treeFinder = api.ModLoader.GetModSystem<POIRegistry>();
                this.treeFinder.AddPOI(this);
            }
            this.changer = this.Api.World.GetBlockAccessorBulkUpdate(true, true);
            this.changer.ReadFromStagedByDefault = true;
        }


        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            base.OnBlockPlaced(byItemStack);
            this.lastChecked = this.Api.World.Calendar.TotalHours;
        }


        public void Regenerate(float dt)
        {
            if (!BotanyConfig.Loaded.LivingTreesEnabled)
            {
                this.Api.World.BlockAccessor.SetBlock(this.deathBlock.BlockId, this.Pos);
                this.treeFinder.RemovePOI(this);
                return;
            }

            if (this.Api.Side != EnumAppSide.Server || this.tree == null || this.tree.Length < 0 || this.leaves == null || this.leaves.Length < 0)
            { return; }
            var sapi = this.Api as ICoreServerAPI;

            //Check if all chunks are loaded
            for (var cx = this.mincx; cx <= this.maxcx; cx++)
            {
                for (var cy = this.mincy; cy <= this.maxcy; cy++)
                {
                    for (var cz = this.mincz; cz <= this.maxcz; cz++)
                    {
                        if (sapi.WorldManager.GetChunk(cx, cy, cz) == null)
                        { return; }
                    }
                }
            }

            double hoursPerDay = this.Api.World.Calendar.HoursPerDay;
            var sinceLastChecked = this.Api.World.Calendar.TotalHours - this.lastChecked;
            if (sinceLastChecked < hoursPerDay)
            { return; }
            var daysPassed = 0;
            var growNow = false;
            var dailyConds = new List<ClimateCondition>();

            //Find out how many days have passed and get climate for those days
            while (sinceLastChecked >= hoursPerDay)
            {
                sinceLastChecked -= hoursPerDay;
                this.lastChecked += hoursPerDay;
                daysPassed++;
                dailyConds.Add(this.Api.World.BlockAccessor.GetClimateAt(this.Pos, EnumGetClimateMode.ForSuppliedDate_TemperatureOnly, this.lastChecked));
            }

            var tmpPos = this.Pos.Copy();
            Block desiredBlock;
            Block blockThere;

            //Checking for damage to the trees
            foreach (var log in this.tree)
            {
                tmpPos.Set(log.X, log.Y, log.Z);
                desiredBlock = this.Api.World.GetBlock(new AssetLocation(this.codes[log.W]));
                blockThere = this.Api.World.BlockAccessor.GetBlock(tmpPos);

                if (blockThere.FirstCodePart() != desiredBlock.FirstCodePart() || blockThere.Variant["wood"] != desiredBlock.Variant["wood"] || blockThere.Variant["type"] == "placed")
                {
                    this.Api.World.BlockAccessor.SetBlock(this.deathBlock.BlockId, this.Pos);
                    this.treeFinder.RemovePOI(this);
                    return;
                }
            }

            //Check for missing leaves
            var missingLeaves = new Queue<Vec4i>();
            var regenedLeaves = new Queue<Vec4i>();
            var blockedLeaves = new HashSet<Vec4i>();

            foreach (var leaf in this.leaves)
            {
                tmpPos.Set(leaf.X, leaf.Y, leaf.Z);
                desiredBlock = this.Api.World.GetBlock(new AssetLocation(this.codes[leaf.W]));
                blockThere = this.Api.World.BlockAccessor.GetBlock(tmpPos);

                if (blockThere.BlockId != desiredBlock.BlockId)
                {
                    if (desiredBlock.Replaceable >= blockThere.Replaceable)
                    { blockedLeaves.Add(leaf); }
                    else
                    { missingLeaves.Enqueue(leaf); }
                }
            }

            this.brokenLeaves = missingLeaves.Count + blockedLeaves.Count;

            //Mark for regeneration
            for (var d = 0; d < daysPassed; d++)
            {
                if (dailyConds[d].Temperature < this.minTemp || dailyConds[d].Temperature > this.maxTemp)
                {
                    //Do not do anything if too or too hot
                    if (dailyConds[d].Temperature < this.minTemp - BotanyConfig.Loaded.TreeRevertGrowthTempThreshold || dailyConds[d].Temperature > this.maxTemp + BotanyConfig.Loaded.TreeRevertGrowthTempThreshold)
                    {
                        this.regenPerc = 0f;
                        if (this.currentGrowthTime > this.timeForNextStage / 2)
                        { this.currentGrowthTime -= (int)hoursPerDay; }
                    }
                }
                else
                {
                    for (var h = 0; h < 24; h++)
                    {
                        if (missingLeaves.Count > 0 || this.diseasedParts > 0)
                        {
                            this.regenPerc += Math.Max(0.01f, this.CurrentHealth) * BotanyConfig.Loaded.TreeRegenMultiplier;
                            if (this.regenPerc > 1f)
                            {
                                this.regenPerc -= 1f;
                                if (this.diseasedParts > 0)
                                {
                                    this.logRecovery++;
                                    if (this.logRecovery >= 5)
                                    {
                                        this.diseasedParts--;
                                        this.logRecovery = 0;
                                    }
                                }
                                else if (this.CurrentLeaves < this.leaves.Length)
                                {
                                    regenedLeaves.Enqueue(missingLeaves.Dequeue());
                                    this.brokenLeaves--;
                                }
                            }
                        }

                        if (this.CurrentHealth > 0.85)
                        {
                            if (this.growthStage < BotanyConfig.Loaded.MaxTreeGrowthStages)
                            { this.currentGrowthTime++; }
                            else
                            { this.currentRepopTime++; }

                            while (this.growthStage <= BotanyConfig.Loaded.MaxTreeGrowthStages && this.currentGrowthTime >= this.timeForNextStage)
                            {
                                this.currentGrowthTime -= this.timeForNextStage;
                                this.growthStage++;
                                growNow = true;
                            }
                        }
                    }
                }
            }

            //If  the tree is going to grow into another stage, then we do not really need to set the regenerated leaf blocks
            if (growNow)
            {
                foreach (var leaf in this.leaves)
                {
                    tmpPos.Set(leaf.X, leaf.Y, leaf.Z);
                    this.changer.SetBlock(0, tmpPos);
                }

                foreach (var log in this.tree)
                {
                    tmpPos.Set(log.X, log.Y, log.Z);
                    this.changer.SetBlock(0, tmpPos);
                }

                this.changer.Commit();

                var treeGenCode = this.Block.Attributes?["treeGen"].AsString(null);
                if (treeGenCode == null)
                { return; }
                var code = new AssetLocation(treeGenCode);

                if (!sapi.World.TreeGenerators.TryGetValue(code, out var gen))
                { return; }

                var size = 0.6f + (0.125f * this.growthStage);
                var pa = new TreeGenParams()
                {
                    skipForestFloor = true,
                    size = size,
                    otherBlockChance = 0,
                    vinesGrowthChance = 0,
                    mossGrowthChance = 0
                };
                sapi.World.TreeGenerators[code].GrowTree(this.changer, this.Pos.AddCopy(0, this.Block.Variant["wood"] == "redwood" ? 1 : 0, 0), pa);
                this.SetupTree(this.changer.Commit());
            }
            else if (regenedLeaves.Count > 0)
            {
                //We did not grow up, so let's regen
                while (regenedLeaves.Count > 0)
                {
                    var leaf = regenedLeaves.Dequeue();
                    desiredBlock = this.Api.World.GetBlock(new AssetLocation(this.codes[leaf.W]));
                    tmpPos.Set(leaf.X, leaf.Y, leaf.Z);
                    this.changer.SetBlock(desiredBlock.BlockId, tmpPos);
                }
                this.changer.Commit();
            }

            if (this.currentRepopTime >= 24)
            {
                var sapped = false;
                var matureDay = dailyConds.Count;

                while (this.currentRepopTime >= 24)
                {
                    this.currentRepopTime -= 24;
                    matureDay--;
                    var foilTriesPerDay = BotanyConfig.Loaded.TreeFoilageTriesPerDay;
                    //Plant foilage
                    if (this.repopBuddies.Length > 0 && BotanyConfig.Loaded.TreeFoilageChance >= this.Api.World.Rand.NextDouble())
                    {
                        var pop = this.GetRandomFriend(this.Api.World.Rand, dailyConds[matureDay]);
                        if (pop != null)
                        {
                            while (foilTriesPerDay > 0)
                            {
                                var foilPlanted = false;
                                if (pop.onGround)
                                {
                                    var foilX = this.Api.World.Rand.Next(-BotanyConfig.Loaded.GrownTreeRepopMinimum + 1, BotanyConfig.Loaded.GrownTreeRepopMinimum);
                                    var foilZ = this.Api.World.Rand.Next(-BotanyConfig.Loaded.GrownTreeRepopMinimum + 1, BotanyConfig.Loaded.GrownTreeRepopMinimum);
                                    tmpPos.Set(this.Pos);
                                    tmpPos.Add(foilX, -BotanyConfig.Loaded.GrownTreeRepopVertSearch, foilZ);

                                    for (var f = tmpPos.Y; f < BotanyConfig.Loaded.GrownTreeRepopVertSearch; f++)
                                    {
                                        tmpPos.Y += 1;
                                        if (pop.TryToPlant(tmpPos, this.changer, null))
                                        {
                                            var floor = this.changer.GetBlock(new AssetLocation("game:forestfloor-" + this.Api.World.Rand.Next(8)));
                                            this.changer.SetBlock(floor.BlockId, tmpPos);
                                            foilPlanted = true;
                                            break;
                                        }
                                    }
                                }
                                else
                                {
                                    var randomLog = this.tree[this.Api.World.Rand.Next(this.tree.Length)];
                                    tmpPos.Set(randomLog.X, randomLog.Y, randomLog.Z);

                                    if (pop.onUnderneath)
                                    { pop.TryToPlant(tmpPos, this.changer, null); }
                                    else
                                    {
                                        this.rndFaces.Shuffle(this.Api.World.Rand);
                                        foreach (var side in this.rndFaces)
                                        {
                                            if (pop.TryToPlant(tmpPos, this.changer, side))
                                            {
                                                foilPlanted = true;
                                                break;
                                            }
                                        }
                                    }
                                }

                                if (foilPlanted)
                                { foilTriesPerDay = 0; }
                                else
                                { foilTriesPerDay--; }
                            }
                        }
                    }

                    //Plant a sapling
                    if (!sapped && this.Api.World.Rand.NextDouble() <= BotanyConfig.Loaded.TreeRepopChance)
                    {
                        //Plant sapling
                        var plant = this.Api.World.GetBlock(new AssetLocation("sapling-" + this.Block.Variant["wood"] + "-free"));

                        var whichside = this.Api.World.Rand.NextDouble() > 0.5;
                        var side = this.Api.World.Rand.NextDouble() > 0.5 ? -BotanyConfig.Loaded.GrownTreeRepopMinimum : BotanyConfig.Loaded.GrownTreeRepopMinimum;
                        var sidepos = this.Api.World.Rand.Next(-BotanyConfig.Loaded.GrownTreeRepopMinimum, BotanyConfig.Loaded.GrownTreeRepopMinimum + 1);
                        tmpPos.Set(this.Pos);
                        tmpPos.Add(whichside ? side : sidepos, -BotanyConfig.Loaded.GrownTreeRepopVertSearch, !whichside ? side : sidepos);

                        var groundCheck = false;

                        var found = this.treeFinder.GetNearestPoi(tmpPos.ToVec3d().Add(0.5, BotanyConfig.Loaded.GrownTreeRepopVertSearch, 0.5), BotanyConfig.Loaded.GrownTreeRepopMinimum, (poi) =>
                        {
                            if (poi == this || !(poi is ITreePoi))
                            { return false; }
                            return true;
                        });

                        if (found == null)
                        {
                            for (var f = tmpPos.Y; f < BotanyConfig.Loaded.GrownTreeRepopVertSearch; f++)
                            {
                                tmpPos.Y += 1;
                                var foilSearch = this.Api.World.BlockAccessor.GetBlock(tmpPos);
                                if (foilSearch == null)
                                { continue; }

                                if (groundCheck)
                                {
                                    if (foilSearch.IsReplacableBy(plant))
                                    {

                                        this.Api.World.BlockAccessor.SetBlock(plant.BlockId, tmpPos);
                                        sapped = true;
                                        break;
                                    }
                                    else
                                    { groundCheck = foilSearch.Fertility > 0 && foilSearch.SideSolid[BlockFacing.UP.Index]; }
                                }
                                else
                                {
                                    groundCheck = foilSearch.Fertility > 0 && foilSearch.SideSolid[BlockFacing.UP.Index];
                                }
                            }
                        }
                    }
                }

                this.changer.Commit();
            }

            this.gasPlug?.CollectGases(this.Pos, (this.growthStage + 1) * 10, new string[] { "silicadust", "coaldust", "carbondioxide", "carbonmonoxide", "sulfurdioxide", "nitrogendioxide" });

            this.MarkDirty();
        }


        public void SetupTree(List<BlockUpdate> commited)
        {
            var tmpLogs = new List<Vec4i>();
            var tmpLeaves = new List<Vec4i>();
            var codes = new List<string>();

            this.mincx = this.Pos.X;
            this.mincy = this.Pos.Y;
            this.mincz = this.Pos.Z;
            this.maxcx = this.Pos.X;
            this.maxcy = this.Pos.Y;
            this.maxcz = this.Pos.Z;
            this.brokenLeaves = 0;
            this.diseasedParts = 0;

            for (var i = 0; i < commited.Count; i++)
            {
                if (commited[i].Pos.X > this.maxcx)
                { this.maxcx = commited[i].Pos.X; }

                if (commited[i].Pos.X < this.mincx)
                { this.mincx = commited[i].Pos.X; }

                if (commited[i].Pos.Y > this.maxcy)
                { this.maxcy = commited[i].Pos.Y; }

                if (commited[i].Pos.Y < this.mincy)
                { this.mincy = commited[i].Pos.Y; }

                if (commited[i].Pos.Z > this.maxcz)
                { this.maxcz = commited[i].Pos.Z; }

                if (commited[i].Pos.Z < this.mincz)
                { this.mincz = commited[i].Pos.Z; }

                var treeBlock = this.Api.World.GetBlock(commited[i].NewSolidBlockId);
                var localId = 0;
                var dAp = treeBlock.Code.Domain + ":" + treeBlock.Code.Path;

                if ((localId = codes.IndexOf(dAp)) == -1)
                {
                    codes.Add(dAp);
                    localId = codes.IndexOf(dAp);
                }
                if (treeBlock is BlockLeaves || treeBlock.Attributes?.IsTrue("isLeaf") == true)
                {
                    tmpLeaves.Add(new Vec4i(commited[i].Pos.X, commited[i].Pos.Y, commited[i].Pos.Z, localId));
                }
                else
                {
                    tmpLogs.Add(new Vec4i(commited[i].Pos.X, commited[i].Pos.Y, commited[i].Pos.Z, localId));
                }
            }


            if (this.Api is ICoreServerAPI sapi)
            {
                var chunksize = this.Api.World.BlockAccessor.ChunkSize;
                var sizeX = sapi.WorldManager.MapSizeX / chunksize;
                var sizeY = sapi.WorldManager.MapSizeY / chunksize;
                var sizeZ = sapi.WorldManager.MapSizeZ / chunksize;

                this.mincx = GameMath.Clamp(this.mincx / chunksize, 0, sizeX - 1);
                this.maxcx = GameMath.Clamp(this.maxcx / chunksize, 0, sizeX - 1);
                this.mincy = GameMath.Clamp(this.mincy / chunksize, 0, sizeY - 1);
                this.maxcy = GameMath.Clamp(this.maxcy / chunksize, 0, sizeY - 1);
                this.mincz = GameMath.Clamp(this.mincz / chunksize, 0, sizeZ - 1);
                this.maxcz = GameMath.Clamp(this.maxcz / chunksize, 0, sizeZ - 1);
            }

            this.tree = tmpLogs.ToArray();
            this.leaves = tmpLeaves.ToArray();
            this.codes = codes.ToArray();

            Array.Sort(this.tree, (a, b) =>
            {
                var ahortDiff = Math.Max(Math.Abs(a.X - this.Pos.X), Math.Abs(a.Z - this.Pos.Z));
                var bhortDiff = Math.Max(Math.Abs(b.X - this.Pos.X), Math.Abs(b.Z - this.Pos.Z));
                if (ahortDiff - bhortDiff != 0)
                {
                    return ahortDiff - bhortDiff;
                }

                return a.Y - this.Pos.Y - (b.Y - this.Pos.Y);
            });

            Array.Sort(this.leaves, (a, b) =>
            {
                var ahortDiff = Math.Max(Math.Abs(a.X - this.Pos.X), Math.Abs(a.Z - this.Pos.Z));
                var bhortDiff = Math.Max(Math.Abs(b.X - this.Pos.X), Math.Abs(b.Z - this.Pos.Z));

                if (ahortDiff - bhortDiff != 0)
                {
                    return ahortDiff - bhortDiff;
                }
                return a.Y - this.Pos.Y - (b.Y - this.Pos.Y);
            });
            this.MarkDirty();
        }


        private TreeFriend GetRandomFriend(Random rand, ClimateCondition conds)
        {
            TreeFriend result = null;
            var tries = 20;
            while (result == null && tries > 0)
            {
                var rndTarget = rand.NextDouble() * this.friendsWeight;
                this.repopBuddies.Shuffle(rand);
                tries--;
                foreach (var friend in this.repopBuddies)
                {
                    rndTarget -= friend.weight;
                    if (rndTarget <= 0)
                    {
                        if (friend.CanPlant(conds, this.treeFamily))
                        { result = friend; }
                        break;
                    }
                }
            }
            return result;
        }


        public void DestroyTree(int amount)
        {
            if (this.tree == null)
            { return; }
            this.diseasedParts = GameMath.Clamp(this.diseasedParts + amount, 0, this.tree.Length);
            this.MarkDirty();
        }


        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetDouble("lastChecked", this.lastChecked);
            tree.SetFloat("regenPerc", this.regenPerc);
            tree.SetFloat("logRecovery", this.logRecovery);
            tree.SetInt("currentGrowthTime", this.currentGrowthTime);
            tree.SetInt("currentRepopTime", this.currentRepopTime);
            tree.SetInt("growthStage", this.growthStage);
            tree.SetInt("BrokenLeaves", this.brokenLeaves);
            tree.SetInt("DestroyedLeaves", this.diseasedParts);

            tree.SetInt("mincx", this.mincx);
            tree.SetInt("mincy", this.mincy);
            tree.SetInt("mincz", this.mincz);
            tree.SetInt("maxcx", this.maxcx);
            tree.SetInt("maxcy", this.maxcy);
            tree.SetInt("maxcz", this.maxcz);

            tree["blockCodes"] = new StringArrayAttribute(this.codes);

            if (this.tree != null && this.tree.Length > 0)
            {
                var logStorage = tree.GetOrAddTreeAttribute("logStorage");
                var logX = new int[this.tree.Length];
                var logY = new int[this.tree.Length];
                var logZ = new int[this.tree.Length];
                var logW = new int[this.tree.Length];

                for (var i = 0; i < this.tree.Length; i++)
                {
                    logX[i] = this.tree[i].X;
                    logY[i] = this.tree[i].Y;
                    logZ[i] = this.tree[i].Z;
                    logW[i] = this.tree[i].W;
                }
                logStorage["logX"] = new IntArrayAttribute(logX);
                logStorage["logY"] = new IntArrayAttribute(logY);
                logStorage["logZ"] = new IntArrayAttribute(logZ);
                logStorage["logW"] = new IntArrayAttribute(logW);
            }

            if (this.leaves != null && this.leaves.Length > 0)
            {
                var leavesStorage = tree.GetOrAddTreeAttribute("leavesStorage");
                var leavesX = new int[this.leaves.Length];
                var leavesY = new int[this.leaves.Length];
                var leavesZ = new int[this.leaves.Length];
                var leavesW = new int[this.leaves.Length];

                for (var i = 0; i < this.leaves.Length; i++)
                {
                    leavesX[i] = this.leaves[i].X;
                    leavesY[i] = this.leaves[i].Y;
                    leavesZ[i] = this.leaves[i].Z;
                    leavesW[i] = this.leaves[i].W;
                }

                leavesStorage["leavesX"] = new IntArrayAttribute(leavesX);
                leavesStorage["leavesY"] = new IntArrayAttribute(leavesY);
                leavesStorage["leavesZ"] = new IntArrayAttribute(leavesZ);
                leavesStorage["leavesW"] = new IntArrayAttribute(leavesW);
            }
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);
            this.lastChecked = tree.GetDouble("lastChecked", worldAccessForResolve.Calendar.TotalHours);
            this.regenPerc = tree.GetFloat("regenPerc");
            this.logRecovery = tree.GetFloat("logRecovery");
            this.currentGrowthTime = tree.GetInt("currentGrowthTime");
            this.currentRepopTime = tree.GetInt("currentRepopTime");
            this.growthStage = tree.GetInt("growthStage");
            this.brokenLeaves = tree.GetInt("BrokenLeaves");
            this.diseasedParts = tree.GetInt("DestroyedLeaves");

            this.mincx = tree.GetInt("mincx");
            this.maxcx = tree.GetInt("maxcx");
            this.mincy = tree.GetInt("mincy");
            this.maxcy = tree.GetInt("maxcy");
            this.mincz = tree.GetInt("mincz");
            this.maxcz = tree.GetInt("maxcz");

            var logsBack = new List<Vec4i>();
            var leavesBack = new List<Vec4i>();
            int[] xValues;
            int[] yValues;
            int[] zValues;
            int[] wValues;

            var logs = tree.GetTreeAttribute("logStorage");
            var leaves = tree.GetTreeAttribute("leavesStorage");
            this.codes = (tree["blockCodes"] as StringArrayAttribute).value;

            if (logs != null)
            {
                xValues = (logs["logX"] as IntArrayAttribute)?.value;
                yValues = (logs["logY"] as IntArrayAttribute)?.value;
                zValues = (logs["logZ"] as IntArrayAttribute)?.value;
                wValues = (logs["logW"] as IntArrayAttribute)?.value;

                if (xValues != null)
                {
                    for (var i = 0; i < xValues.Length; i++)
                    {
                        logsBack.Add(new Vec4i(xValues[i], yValues[i], zValues[i], wValues[i]));
                    }
                    this.tree = logsBack.ToArray();
                }
            }

            if (leaves != null)
            {
                xValues = (leaves["leavesX"] as IntArrayAttribute)?.value;
                yValues = (leaves["leavesY"] as IntArrayAttribute)?.value;
                zValues = (leaves["leavesZ"] as IntArrayAttribute)?.value;
                wValues = (leaves["leavesW"] as IntArrayAttribute)?.value;

                if (xValues != null)
                {
                    for (var i = 0; i < xValues.Length; i++)
                    {
                        leavesBack.Add(new Vec4i(xValues[i], yValues[i], zValues[i], wValues[i]));
                    }
                    this.leaves = leavesBack.ToArray();
                }
            }
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            base.GetBlockInfo(forPlayer, dsc);

            if (this.leaves == null)
            { return; }

            dsc.AppendLine(Lang.Get("wildfarmingrevival:tree-health", (this.CurrentHealth * 100).ToString("#.#"), 100));

            if (this.growthStage < BotanyConfig.Loaded.MaxTreeGrowthStages)
            {
                dsc.AppendLine(Lang.Get("wildfarmingrevival:tree-growthstage", this.growthStage + 1, BotanyConfig.Loaded.MaxTreeGrowthStages + 1));
            }
            else
            {
                dsc.AppendLine(Lang.Get("wildfarmingrevival:tree-mature"));
            }

            var conds = this.Api.World.BlockAccessor.GetClimateAt(this.Pos, EnumGetClimateMode.NowValues);

            if (conds.Temperature < this.minTemp)
            {
                dsc.AppendLine(Lang.Get("wildfarmingrevival:tree-cold"));
            }
            else if (conds.Temperature > this.maxTemp)
            {
                dsc.AppendLine(Lang.Get("wildfarmingrevival:tree-hot"));
            }
        }


        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();
            if (this.Api.Side == EnumAppSide.Server)
            {
                this.treeFinder?.RemovePOI(this);
            }
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();

            if (this.Api.Side == EnumAppSide.Server)
            {
                this.treeFinder?.RemovePOI(this);
            }
        }


        public bool IsSuitableFor(Entity entity, string[] diet)
        {
            if (this.CurrentHealthyParts < 1)
            { return false; }

            diet = entity.Properties.Attributes?["blockDiet"]?.AsArray<string>();
            if (diet == null)
            { return false; }

            return diet.Contains("Wood");
        }


        public float ConsumeOnePortion()
        {
            this.DestroyTree(1);
            return 1;
        }
    }
}
