namespace WildFarmingRevival.ModSystem
{
    using System;
    //using System.Diagnostics;
    using System.Collections.Generic;
    using Vintagestory.API.Client;
    using Vintagestory.API.Common;
    using Vintagestory.API.Datastructures;
    using Vintagestory.API.Util;

    public class BlockBehaviorScore : BlockBehavior
    {
        private float scoreTime;
        public AssetLocation scoringSound;
        private AssetLocation scoredBlockCode;
        private Block scoredBlock;
        private WorldInteraction[] interactions;

        public BlockBehaviorScore(Block block) : base(block)
        { }


        public override void Initialize(JsonObject properties)
        {
            base.Initialize(properties);
            if (!this.block.Code.Path.Contains("log-grown-pine-") && !this.block.Code.Path.Contains("log-grown-acacia-"))
            { return; }

            this.scoreTime = properties["scoreTime"].AsFloat(0);
            var code = properties["scoringSound"].AsString("game:sounds/block/chop3");
            if (code != null)
            {
                this.scoringSound = AssetLocation.Create(code, this.block.Code.Domain);
            }
        }


        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            if (!this.block.Code.Path.Contains("log-grown-pine-") && !this.block.Code.Path.Contains("log-grown-acacia-"))
            { return; }

            this.interactions = ObjectCacheUtil.GetOrCreate(api, "resinHarvest", () =>
            {
                var knifeStacklist = new List<ItemStack>();
                foreach (var item in api.World.Items)
                {
                    if (item.Code == null)
                    { continue; }
                    if (item.Tool == EnumTool.Knife)
                    { knifeStacklist.Add(new ItemStack(item)); }
                }

                return new WorldInteraction[] {
                    new WorldInteraction()
                    {
                        ActionLangCode = "wildfarmingrevival:blockhelp-score",
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = knifeStacklist.ToArray()
                    }
                };
            });
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref EnumHandling handling)
        {
            if (!world.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.Use))
            { return false; }

            if ((!this.block.Code.Path.Contains("log-grown-pine-") && !this.block.Code.Path.Contains("log-grown-acacia-")) || byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack?.Collectible?.Tool != EnumTool.Knife)
            { return false; }

            handling = EnumHandling.PreventDefault;
            world.PlaySoundAt(this.scoringSound, blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z, byPlayer);
            return true;
        }


        public override bool OnBlockInteractStep(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref EnumHandling handled)
        {
            if (blockSel == null)
            { return false; }

            handled = EnumHandling.PreventDefault;
            (byPlayer as IClientPlayer)?.TriggerFpAnimation(EnumHandInteract.HeldItemAttack);

            if (world.Rand.NextDouble() < 0.1)
            {
                world.PlaySoundAt(this.scoringSound, blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z, byPlayer);
            }
            return world.Side == EnumAppSide.Client || secondsUsed < this.scoreTime;
        }


        public override void OnBlockInteractStop(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref EnumHandling handled)
        {
            handled = EnumHandling.PreventDefault;
            if (secondsUsed > this.scoreTime - 0.05f && world.Side == EnumAppSide.Server)
            {
                var knife = byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack;
                if (knife != null && knife.Collectible.Tool == EnumTool.Knife)
                {

                    var type = this.block.FirstCodePart(2);
                    var facing = blockSel.Face.Opposite.ToString();
                    if (facing == "up" || facing == "down")
                    {
                        var targetPos = blockSel.DidOffset ? blockSel.Position.AddCopy(blockSel.Face.Opposite) : blockSel.Position;
                        var dx = byPlayer.Entity.Pos.X - (targetPos.X + blockSel.HitPosition.X);
                        var dz = byPlayer.Entity.Pos.Z - (targetPos.Z + blockSel.HitPosition.Z);
                        var angle = Math.Atan2(dx, dz);
                        angle += Math.PI;
                        angle /= Math.PI / 2;
                        var halfQuarter = Convert.ToInt32(angle);
                        halfQuarter %= 4;
                        if (halfQuarter == 3)
                        { facing = "west"; }
                        else if (halfQuarter == 2)
                        { facing = "north"; }
                        else if (halfQuarter == 1)
                        { facing = "east"; }
                        else
                        { facing = "south"; }
                        //Debug.WriteLine(halfQuarter.ToString() + " - " + facing);

                    }
                    this.scoredBlockCode = new AssetLocation("wildfarmingrevival:wflog-resinharvested-" + type + "-" + facing);
                    this.scoredBlock = world.GetBlock(this.scoredBlockCode);
                    if (this.scoredBlock == null)
                    {
                        world.Logger.Warning("Unable to resolve scored block code '{0}' for block {1}. Will ignore.", this.scoredBlockCode, this.block.Code);
                    }
                    else
                    {
                        world.BlockAccessor.SetBlock(this.scoredBlock.BlockId, blockSel.Position);
                        knife.Collectible.DamageItem(world, byPlayer.Entity, byPlayer.InventoryManager.ActiveHotbarSlot, 15);
                    }
                    world.PlaySoundAt(this.scoringSound, blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z, byPlayer);
                }
            }
        }


        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer, ref EnumHandling handled)
        {
            if (!this.block.Code.Path.Contains("log-grown-pine-") && !this.block.Code.Path.Contains("log-grown-acacia-"))
            { return null; }
            return this.interactions;
        }
    }
}
