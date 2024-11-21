namespace WildFarmingRevival.ModSystem
{
    using System.Text;
    using Vintagestory.API.Common;
    using Vintagestory.API.Client;
    //using Vintagestory.API.MathTools;
    using Vintagestory.API.Util;


    public class WildSeed : Item
    {

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            if (blockSel == null || byEntity?.World == null)
            { return; }

            var world = byEntity.World;
            var ground = world.BlockAccessor.GetBlock(blockSel.Position);
            var onPos = blockSel.Position.UpCopy(1);
            var taken = world.BlockAccessor.GetBlock(onPos);
            var plant = slot.Itemstack.Collectible.CodeEndWithoutParts(1);
            var wildPlant = world.GetBlock(new AssetLocation("wildfarmingrevival:wildplant-" + plant));
            //System.Diagnostics.Debug.WriteLine(plant);
            IPlayer byPlayer = null;
            if (byEntity is EntityPlayer player)
            { byPlayer = byEntity.World.PlayerByUid(player.PlayerUID); }

            // Checking to see if we can place the plant. If not this stops the method
            if (!byEntity.World.Claims.TryAccess(byPlayer, onPos, EnumBlockAccessFlags.BuildOrBreak))
            { return; }
            if (!ground.SideSolid[blockSel.Face.Index])
            { return; }
            if (taken.Replaceable < 9501)
            { return; }
            if (ground.Fertility <= 0)
            { return; }

            // Placing the plant
            world.BlockAccessor.SetBlock(wildPlant.BlockId, onPos);
            byEntity.World.PlaySoundAt(new AssetLocation("sounds/block/plant"), onPos.X, onPos.Y, onPos.Z, byPlayer);

            ((byEntity as EntityPlayer)?.Player as IClientPlayer)?.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);

            if (byPlayer?.WorldData?.CurrentGameMode != EnumGameMode.Creative)
            {
                slot.TakeOut(1);
                slot.MarkDirty();
            }
            handling = EnumHandHandling.PreventDefault;
        }


        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
            var block = world.GetBlock(new AssetLocation("wildfarmingrevival:wildplant-" + this.CodeEndWithoutParts(1)));

            if (block != null)
            {
                dsc.AppendLine("Average Grow Time: " + (block.Attributes["hours"].AsFloat(192f) / 24));
                dsc.AppendLine("Maximum Growing Temperature: " + block.Attributes["maxTemp"].AsFloat(50f));
                dsc.AppendLine("Minimum Growing Temperature: " + block.Attributes["minTemp"].AsFloat(-5f));
            }
        }


        public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
        {
            return new WorldInteraction[] {
                new WorldInteraction()
                {
                    ActionLangCode = "heldhelp-plant",
                    MouseButton = EnumMouseButton.Right,
                }
            }.Append(base.GetHeldInteractionHelp(inSlot));
        }
    }
}
