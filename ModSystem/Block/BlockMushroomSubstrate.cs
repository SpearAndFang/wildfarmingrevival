namespace WildFarmingRevival.ModSystem
{
    using System.Text;
    using Vintagestory.API.Client;
    using Vintagestory.API.Common;
    using Vintagestory.API.Config;
    using Vintagestory.API.MathTools;
    using Vintagestory.GameContent;

    public class BlockMushroomSubstrate : Block
    {
        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (!(world.BlockAccessor.GetBlockEntity(blockSel.Position) is BlockEntityMushroomSubstrate mse))
            { return base.OnBlockInteractStart(world, byPlayer, blockSel); }

            if (mse?.IsGrowing != false || !(byPlayer?.InventoryManager.ActiveHotbarSlot.Itemstack?.Block is BlockMushroom))
            { return base.OnBlockInteractStart(world, byPlayer, blockSel); }

            else
            {
                var result = mse.SetMushroomBlock(byPlayer?.InventoryManager.ActiveHotbarSlot.Itemstack.Block);
                if (result)
                {
                    byPlayer.InventoryManager.ActiveHotbarSlot.TakeOut(1);
                    byPlayer.InventoryManager.ActiveHotbarSlot.MarkDirty();
                    return true;
                }
            }
            return false;
        }


        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            var result = base.OnPickBlock(world, pos);
            //if (world.BlockAccessor.GetBlockEntity(pos) is BlockEntityMushroomSubstrate mse)
            //{ mse.SetItemstackAttributes(result); }
            return result;
        }


        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            string mushroom;
            if ((mushroom = inSlot?.Itemstack?.Attributes.GetString("mushroomBlockCode")) != null)
            {
                var mushroomBlock = world.GetBlock(new AssetLocation(mushroom));
                dsc.AppendLine(Lang.Get("wildfarmingrevival:msubstrate-incubating", Lang.GetMatching("block-" + mushroomBlock.Code.Path)));
            }
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
        }

        public MeshData GenMesh(ICoreClientAPI capi, string shapePath, ITexPositionSource texture)
        {
            var tesselator = capi.Tesselator;
            Shape shape = null;
            shape = capi.Assets.TryGet(shapePath + ".json").ToObject<Shape>();
            if (shape != null && texture != null)
            {
                tesselator.TesselateShape(shapePath, shape, out var mesh, texture, new Vec3f(this.Shape.rotateX, this.Shape.rotateY, this.Shape.rotateZ));
                return mesh;
            }
            return null;
        }

    }
}
