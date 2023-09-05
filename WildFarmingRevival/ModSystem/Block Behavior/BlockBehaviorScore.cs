namespace WildFarmingRevival.ModSystem
{
    using System.Collections.Generic;
    //using Vintagestory.API;
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
            var type = this.block.FirstCodePart(2);
            this.scoredBlockCode = new AssetLocation("log-resinharvested-" + type + "-ud");
            this.scoredBlock = api.World.GetBlock(this.scoredBlockCode);
            if (this.scoredBlock == null)
            {
                api.World.Logger.Warning("Unable to resolve scored block code '{0}' for block {1}. Will ignore.", this.scoredBlockCode, this.block.Code);
            }

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
                    knife.Collectible.DamageItem(world, byPlayer.Entity, byPlayer.InventoryManager.ActiveHotbarSlot, 15);
                    if (this.scoredBlock != null)
                    {
                        world.BlockAccessor.SetBlock(this.scoredBlock.BlockId, blockSel.Position);
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
