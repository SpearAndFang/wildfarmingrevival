using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.API.Server;
using Vintagestory.API.Config;

//namespace WildFarmingRevival.ModSystem
public class GasHelper : ModSystem
{
    private ICoreAPI api;

    //DO NOT CHANGE, if this number does not match up to one on Gas API github this helper is outdated
    private const double VersionNumber = 1.0;

    public override void Start(ICoreAPI papi)
    {
        base.Start(this.api);
        this.api = papi;
        try
        {
            var asset = this.api.Assets.Get("gasapi:config/gases.json");
            liteGasDict = asset.ToObject<Dictionary<string, GasInfoLite>>();
            if (liteGasDict == null)
            {
                liteGasDict = new Dictionary<string, GasInfoLite>();
            }
        }
        catch
        {
            liteGasDict = new Dictionary<string, GasInfoLite>();
        }
    }


    private static Dictionary<string, GasInfoLite> liteGasDict;
    //Returns the gases for the entire chunk; Does not create gases or chunk data
    public Dictionary<int, Dictionary<string, float>> GetGasesForChunk(BlockPos pos)
    {
        if (!this.api.ModLoader.IsModEnabled("gasapi"))
        { return null; }

        byte[] data;
        var chunk = this.api.World.BlockAccessor.GetChunkAtBlockPos(pos);
        if (chunk == null)
        {
            return null;
        }
        data = chunk.GetModdata("gases");
        Dictionary<int, Dictionary<string, float>> gasesOfChunk = null;

        if (data != null)
        {
            try
            {
                gasesOfChunk = SerializerUtil.Deserialize<Dictionary<int, Dictionary<string, float>>>(data);
            }
            catch (Exception)
            { gasesOfChunk = null; }
        }
        return gasesOfChunk;
    }


    //Returns the gases for the entire chunk; Does not create gases or chunk data
    public Dictionary<int, Dictionary<string, float>> GetGasesForChunk(IWorldChunk chunk)
    {
        if (!this.api.ModLoader.IsModEnabled("gasapi"))
        { return null; }

        byte[] data;
        if (chunk == null)
        { return null; }

        data = chunk.GetModdata("gases");
        Dictionary<int, Dictionary<string, float>> gasesOfChunk = null;
        if (data != null)
        {
            try
            {
                gasesOfChunk = SerializerUtil.Deserialize<Dictionary<int, Dictionary<string, float>>>(data);
            }
            catch (Exception)
            { gasesOfChunk = null; }
        }
        return gasesOfChunk;
    }


    //Returns gases for a particular block position
    public Dictionary<string, float> GetGases(BlockPos pos)
    {
        if (!this.api.ModLoader.IsModEnabled("gasapi"))
        { return null; }

        var gasesOfChunk = this.GetGasesForChunk(pos);
        if (gasesOfChunk == null)
        { return null; }

        var index3d = this.ToLocalIndex(pos);
        if (!gasesOfChunk.ContainsKey(index3d))
        { return null; }

        return gasesOfChunk[index3d];
    }


    //Returns the amount of the specified gas at a position if it is present
    public float GetGas(BlockPos pos, string name)
    {
        if (!this.api.ModLoader.IsModEnabled("gasapi"))
        { return 0; }

        var gasesHere = this.GetGases(pos);
        if (gasesHere == null || !gasesHere.ContainsKey(name))
        { return 0; }
        return gasesHere[name];
    }


    //Serializes and sends a gas spread event on the bus
    public void SendGasSpread(BlockPos pos, Dictionary<string, float> gases = null)
    {
        if (!this.api.ModLoader.IsModEnabled("gasapi") || this.api.Side != EnumAppSide.Server)
        { return; }
        (this.api as ICoreServerAPI)?.Event.PushEvent("spreadGas", this.SerializeGasTreeData(pos, gases));
    }


    //Serializes a gas spreading event
    public TreeAttribute SerializeGasTreeData(BlockPos pos, Dictionary<string, float> gases)
    {
        if (!this.api.ModLoader.IsModEnabled("gasapi"))
        { return null; }

        if (pos == null)
        { return null; }

        var tree = new TreeAttribute();
        tree.SetBlockPos("pos", pos);
        if (gases != null && gases.Count > 0)
        {
            var sGases = new TreeAttribute();
            foreach (var gas in gases)
            {
                sGases.SetFloat(gas.Key, gas.Value);
            }
            tree.SetAttribute("gases", sGases);
        }
        return tree;
    }


    //Deserializes a gas spreading event
    public static Dictionary<string, float> DeserializeGasTreeData(IAttribute data, out BlockPos pos)
    {
        var tree = data as TreeAttribute;
        pos = tree?.GetBlockPos("pos");
        var gases = tree?.GetTreeAttribute("gases");
        if (pos == null)
        { return null; }

        Dictionary<string, float> dGases = null;
        if (gases != null)
        {
            dGases = new Dictionary<string, float>();
            foreach (var gas in gases)
            {
                var value = gases.TryGetFloat(gas.Key);
                if (value == null)
                { continue; }
                dGases.Add(gas.Key, (float)value);
            }
        }
        return dGases;
    }


    //Cleanly merges a gas into an already existing gas dictionary
    public static void MergeGasIntoDict(string gasName, float gasValue, ref Dictionary<string, float> dest)
    {
        if (gasName == null || gasValue == 0 || dest == null)
        { return; }
        if (!dest.ContainsKey(gasName))
        { dest.Add(gasName, gasValue); }
        else
        { dest[gasName] += gasValue; }
    }


    //Cleanly merges two gas dictionaries together
    public static void MergeGasDicts(Dictionary<string, float> source, ref Dictionary<string, float> dest)
    {
        if (source == null || dest == null)
        { return; }

        foreach (var gas in source)
        {
            if (gas.Key == "RADIUS")
            {
                if (!dest.ContainsKey(gas.Key))
                { dest.Add(gas.Key, gas.Value); }
                else if (dest[gas.Key] < gas.Value)
                { dest[gas.Key] = gas.Value; }
            }
            else
            { MergeGasIntoDict(gas.Key, gas.Value, ref dest); }
        }
    }


    //Returns the air quality for this position, ranging from 1 to -1. Postive values allow breathing, negative values suffocate
    public float GetAirAmount(BlockPos pos)
    {
        if (!this.api.ModLoader.IsModEnabled("gasapi"))
        { return 1; }

        var gasesHere = this.GetGases(pos);
        if (gasesHere == null)
        { return 1; }

        float conc = 0;
        foreach (var gas in gasesHere)
        {
            if (liteGasDict.ContainsKey(gas.Key))
            {
                if (liteGasDict[gas.Key] != null)
                { conc += gas.Value * liteGasDict[gas.Key].QualityMult; }
                else
                { conc += gas.Value; }
            }
        }

        if (conc >= 2)
        { return -1; }
        if (conc < 0)
        { return 1; }
        return 1 - conc;
    }


    //Returns the aciditiy for an area between 0 and 1
    public float GetAcidity(BlockPos pos)
    {
        if (!this.api.ModLoader.IsModEnabled("gasapi"))
        { return 0; }
        var gasesHere = this.GetGases(pos);

        if (gasesHere == null)
        { return 0; }

        float conc = 0;
        foreach (var gas in gasesHere)
        {
            if (liteGasDict.ContainsKey(gas.Key))
            {
                if (liteGasDict[gas.Key] != null && liteGasDict[gas.Key].acidic)
                { conc += gas.Value; }
                if (conc >= 1)
                { return 1; }
            }
        }
        return conc;
    }


    //Returns whether there is a flammable amount of gas at this position
    public bool IsVolatile(BlockPos pos)
    {
        if (!this.api.ModLoader.IsModEnabled("gasapi"))
        { return false; }

        var gasesHere = this.GetGases(pos);
        if (gasesHere == null)
        { return false; }

        foreach (var gas in gasesHere)
        {
            if (liteGasDict.ContainsKey(gas.Key))
            {
                if (liteGasDict[gas.Key].flammableAmount > 0 && gas.Value >= liteGasDict[gas.Key].flammableAmount)
                { return true; }
            }
        }
        return false;
    }


    //Returns whether there is enough explosive gas here to explode
    public bool ShouldExplode(BlockPos pos)
    {
        if (!this.api.ModLoader.IsModEnabled("gasapi"))
        { return false; }

        var gasesHere = this.GetGases(pos);
        if (gasesHere == null)
        { return false; }

        foreach (var gas in gasesHere)
        {
            if (liteGasDict.ContainsKey(gas.Key))
            {
                if (liteGasDict[gas.Key].explosionAmount <= gas.Value)
                { return true; }
            }
        }
        return false;
    }


    //Determines if there is enough of the gas to be toxic
    public bool IsToxic(string name, float amount)
    {
        if (!this.api.ModLoader.IsModEnabled("gasapi"))
        { return false; }
        if (!liteGasDict.ContainsKey(name))
        { return true; }
        return amount > liteGasDict[name].toxicAt;
    }


    //Collects gases and voids them in the world and returns them as a table
    //Note: Because this happens on the main thread and gas spreading happens on an off thread, it may be somewhat inaccurate
    public Dictionary<string, float> CollectGases(BlockPos pos, int radius, string[] gasFilter)
    {
        if (!this.api.ModLoader.IsModEnabled("gasapi") || this.api.Side != EnumAppSide.Server)
        { return null; }

        var blockAccessor = this.api.World.BlockAccessor;
        if (pos.Y < 1 || pos.Y > blockAccessor.MapSizeY)
        { return null; }

        var result = new Dictionary<string, float>();
        var checkQueue = new Queue<Vec3i>();
        var chunks = new Dictionary<Vec3i, IWorldChunk>();
        var gasChunks = new Dictionary<Vec3i, Dictionary<int, Dictionary<string, float>>>();
        var markedPositions = new HashSet<BlockPos>();
        var blocks = new Dictionary<int, Block>();
        var bounds = new Cuboidi(pos.X - radius, pos.Y - radius, pos.Z - radius, pos.X + radius, pos.Y + radius, pos.Z + radius);
        var curPos = pos.Copy();
        var faces = BlockFacing.ALLFACES;

        for (var x = bounds.MinX / blockAccessor.ChunkSize; x <= bounds.MaxX / blockAccessor.ChunkSize; x++)
        {
            for (var y = bounds.MinY / blockAccessor.ChunkSize; y <= bounds.MaxY / blockAccessor.ChunkSize; y++)
            {
                for (var z = bounds.MinZ / blockAccessor.ChunkSize; z <= bounds.MaxZ / blockAccessor.ChunkSize; z++)
                {
                    var chunk = blockAccessor.GetChunk(x, y, z);
                    var currentChunkPos = new Vec3i(x, y, z);
                    chunks.Add(currentChunkPos, chunk);
                    gasChunks.Add(currentChunkPos, this.GetGasesForChunk(chunk));
                }
            }
        }
        if (chunks.Count < 1)
        { return result; }

        var originChunkVec = new Vec3i(pos.X / blockAccessor.ChunkSize, pos.Y / blockAccessor.ChunkSize, pos.Z / blockAccessor.ChunkSize);
        if (chunks[originChunkVec] == null)
        { return null; }
        checkQueue.Enqueue(pos.ToVec3i());
        markedPositions.Add(pos.Copy());
        var starter = blockAccessor.GetBlock(pos);
        blocks.Add(starter.BlockId, starter);
        if (gasChunks[originChunkVec] != null && gasChunks[originChunkVec].ContainsKey(this.ToLocalIndex(pos)))
        {
            var gasesHere = gasChunks[originChunkVec][this.ToLocalIndex(pos)];
            if (gasFilter == null)
            { MergeGasDicts(gasesHere, ref result); }
            else
            {
                foreach (var gas in gasesHere)
                {
                    if (gasFilter.Contains(gas.Key))
                    { MergeGasIntoDict(gas.Key, gas.Value, ref result); }
                }
            }
        }

        while (checkQueue.Count > 0)
        {
            var bpos = checkQueue.Dequeue();
            var parentChunkVec = new Vec3i(bpos.X / blockAccessor.ChunkSize, bpos.Y / blockAccessor.ChunkSize, bpos.Z / blockAccessor.ChunkSize);

            Block parent = null;
            var parentChunk = chunks[parentChunkVec];
            if (!blocks.ContainsKey(parentChunk.UnpackAndReadBlock(this.ToLocalIndex(bpos.AsBlockPos), BlockLayersAccess.Default)))
            { continue; }

            parent = blocks[parentChunk.UnpackAndReadBlock(this.ToLocalIndex(bpos.AsBlockPos), BlockLayersAccess.Default)];

            foreach (var facing in faces)
            {
                if (this.SolidCheck(parent, facing))
                { continue; }
                curPos.Set(bpos.X + facing.Normali.X, bpos.Y + facing.Normali.Y, bpos.Z + facing.Normali.Z);
                if (!bounds.Contains(curPos) || markedPositions.Contains(curPos))
                { continue; }
                if (curPos.Y < 0 || curPos.Y > blockAccessor.MapSizeY)
                { continue; }

                var curChunkVec = new Vec3i(curPos.X / blockAccessor.ChunkSize, curPos.Y / blockAccessor.ChunkSize, curPos.Z / blockAccessor.ChunkSize);
                var chunkBid = this.ToLocalIndex(curPos);
                var chunk = chunks[curChunkVec];

                if (chunk == null)
                { continue; }

                var blockId = chunk.UnpackAndReadBlock(this.ToLocalIndex(curPos), BlockLayersAccess.Default);

                if (!blocks.TryGetValue(blockId, out var atPos))
                { atPos = blocks[blockId] = blockAccessor.GetBlock(blockId); }

                if (this.SolidCheck(atPos, facing.Opposite))
                { continue; }

                if (gasChunks[curChunkVec] != null && gasChunks[curChunkVec].ContainsKey(chunkBid))
                {
                    var gasesHere = gasChunks[curChunkVec][chunkBid];
                    if (gasFilter == null)
                    { MergeGasDicts(gasesHere, ref result); }
                    else
                    {
                        foreach (var gas in gasesHere)
                        {
                            if (gasFilter.Contains(gas.Key))
                            { MergeGasIntoDict(gas.Key, gas.Value, ref result); }
                        }
                    }
                }
                markedPositions.Add(curPos.Copy());
                checkQueue.Enqueue(curPos.ToVec3i());
            }
        }
        var reverse = new Dictionary<string, float>();
        foreach (var gas in result)
        {
            reverse.Add(gas.Key, -gas.Value);
        }
        reverse.Add("IGNORELIQUIDS", -100);
        reverse.Add("RADIUS", radius);
        this.SendGasSpread(pos, reverse);
        return result;
    }


    //Returns the display name of the gas if it has one
    public static string GetGasDisplayName(string gas)
    {
        var results = Lang.GetIfExists("gasapi:gas-" + gas);
        return results ?? gas;
    }


    //Returns whether the block face is solid
    public bool SolidCheck(Block block, BlockFacing face)
    {
        if (block.Attributes?.KeyExists("gassysSolidSides") == true)
        {
            return block.Attributes["gassysSolidSides"].IsTrue(face.Code);
        }
        return block.SideSolid[face.Index];
    }


    //Gives the local index for a block in its chunk
    private int ToLocalIndex(BlockPos pos)
    {
        return MapUtil.Index3d(pos.X % this.api.World.BlockAccessor.ChunkSize, pos.Y % this.api.World.BlockAccessor.ChunkSize, pos.Z % this.api.World.BlockAccessor.ChunkSize, this.api.World.BlockAccessor.ChunkSize, this.api.World.BlockAccessor.ChunkSize);
    }


    //Internal class used to get the gas information from the config.
    [JsonObject(MemberSerialization.OptIn)]
    public class GasInfoLite
    {
        [JsonProperty]
        public bool light;
        [JsonProperty]
        public float ventilateSpeed = 0;
        [JsonProperty]
        public bool pollutant;
        [JsonProperty]
        public bool distribute;
        [JsonProperty]
        public float explosionAmount = 2;
        [JsonProperty]
        public float suffocateAmount = 1;
        [JsonProperty]
        public float flammableAmount = 2;
        [JsonProperty]
        public bool plantAbsorb;
        [JsonProperty]
        public bool acidic;
        [JsonProperty]
        public Dictionary<string, float> effects;
        [JsonProperty]
        public string burnInto;
        [JsonProperty]
        public float toxicAt = 0f;

        public float QualityMult
        {
            get
            {
                if (this.suffocateAmount == 0)
                { return 1; }
                return 1 / this.suffocateAmount;
            }
        }
    }
}

