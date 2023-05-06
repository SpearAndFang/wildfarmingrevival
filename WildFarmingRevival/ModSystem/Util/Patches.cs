namespace WildFarmingRevival.ModSystem
{
    using HarmonyLib;
    using System.Reflection;
    using Vintagestory.ServerMods;


    [HarmonyPatch(typeof(TreeGen))]
    public class TreeGenModifications
    {
        /*
        [HarmonyPrepare]
        private static bool Prepare(MethodBase original, Harmony harmony)
        {
            //From Melchoir
            if (original != null)
            {
                foreach (var patched in harmony.GetPatchedMethods())
                {
                    if (patched.Name == original.Name)
                    { return false; }
                }
            }

            return true;
        }
        */
        /*
         
        [HarmonyPatch("getPlaceResumeState")]
        [HarmonyPrefix]
        static void StopGrowingInto(BlockPos targetPos, ref int desiredblockId, IBlockAccessor ___api)
        {
            Block check = ___api.GetBlock(targetPos);
            Block desired = ___api.GetBlock(desiredblockId);

            if (check.Replaceable == desired.Replaceable)
            {
                //System.Diagnostics.Debug.WriteLine("Stopped it");
                desiredblockId = 0;
            }
        }
        */
    }
}
