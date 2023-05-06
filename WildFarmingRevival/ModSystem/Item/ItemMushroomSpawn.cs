namespace WildFarmingRevival.ModSystem
{
    using Vintagestory.API.Client;
    using Vintagestory.API.Common;
    //using Vintagestory.API.MathTools;

    public class ItemMushroomSpawn : Item
    {
        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            if (blockSel == null || byEntity?.World == null)
            { return; }

            var world = byEntity.World;
            var ground = world.BlockAccessor.GetBlock(blockSel.Position);
            var onPos = blockSel.Position.UpCopy(1);
            var taken = world.BlockAccessor.GetBlock(onPos);
            var mushroom = world.GetBlock(new AssetLocation("game:mushroom-" + slot.Itemstack.Collectible.CodeEndWithoutParts(1) + "-harvested-free"));
            //System.Diagnostics.Debug.WriteLine(plant);
            IPlayer byPlayer = null;
            if (byEntity is EntityPlayer player)
            { byPlayer = byEntity.World.PlayerByUid(player.PlayerUID); }
            // Checking to see if we can place the plant. If not this stops the method
            if (!byEntity.World.Claims.TryAccess(byPlayer, onPos, EnumBlockAccessFlags.BuildOrBreak))
            { return; }
            if (taken.Replaceable <= 9501)
            { return; }
            if (ground.Fertility <= 0)
            { return; }

            // Placing the plant
            world.BlockAccessor.SetBlock(mushroom.BlockId, onPos);
            byEntity.World.PlaySoundAt(new AssetLocation("sounds/block/plant"), onPos.X, onPos.Y, onPos.Z, byPlayer);
            ((byEntity as EntityPlayer)?.Player as IClientPlayer)?.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);

            if (byPlayer?.WorldData?.CurrentGameMode != EnumGameMode.Creative)
            {
                slot.TakeOut(1);
                slot.MarkDirty();
            }
            handling = EnumHandHandling.PreventDefault;
        }
    }
}
