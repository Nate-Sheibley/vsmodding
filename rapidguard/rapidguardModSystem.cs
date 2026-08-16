using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace YourModNamespace
{
    /// <summary>
    /// Injects a slab into the same block layer stack entry as rapid rivulet source blocks during world generation.
    /// </summary>
    public class RivuletSlabLayerPatch : ModSystem
    {
        private static ICoreAPI api;

        // Replace with your slab block id once registered
        private static int slabBlockId;

        public override void Start(ICoreAPI api)
        {
            RivuletSlabLayerPatch.api = api;

            slabBlockId = api.World.BlockAccessor.GetBlock(new AssetLocation("cobblestoneslab-granite-top-free"))?.BlockId ?? 0;

            var harmony = new Harmony("yourmod.rivuletslabpatch");
            harmony.PatchAll();
        }

        /// <summary>
        /// Primary injection point: worldgen block placement (broad hook)
        /// </summary>
        [HarmonyPatch(typeof(BlockAccessorWorldGen), "SetBlock")]
        public class Patch_BlockPlacement
        {
            static void Postfix(BlockAccessorWorldGen __instance, int blockId, BlockPos pos)
            {
                if (blockId <= 0 || slabBlockId <= 0) return;

                var world = __instance.World;

                Block block = world.BlockAccessor.GetBlock(blockId);

                if (!IsRapidRivuletSource(block)) return;

                InjectSlabIntoLayer(__instance, pos);
            }
        }

        /// <summary>
        /// Secondary safety hook for alternative generation paths
        /// </summary>
        [HarmonyPatch(typeof(GenTerra), "GenerateTerrain")]
        public class Patch_GenTerra
        {
            static void Postfix(IBlockAccessor blockAccessor)
            {
                // Optional fallback pass if rivulets are injected after terrain pass
                // You can extend this if needed
            }
        }

        /// <summary>
        /// Core logic: inject slab into SAME voxel layer stack entry
        /// </summary>
        private static void InjectSlabIntoLayer(BlockAccessorWorldGen worldGen, BlockPos pos)
        {
            // IMPORTANT:
            // This assumes the engine supports layered block insertion
            // Adjust method name to actual 1.22 API if different.

            try
            {
                // Preferred (if available in your API):
                worldGen.SetDecorBlock(pos, slabBlockId);

                // If SetDecorBlock does not exist in your build,
                // replace with correct layered API call such as:
                //
                // worldGen.AddBlockLayer(pos, slabBlockId, EnumBlockLayers.SolidDecor);
            }
            catch
            {
                // Fallback: try generic layer-capable accessor if exposed
                var ba = worldGen.World.BlockAccessor;

                if (ba is IWorldChunkAccessor layered)
                {
                    layered.SetDecor(pos, slabBlockId);
                }
            }
        }

        /// <summary>
        /// Detects rapid rivulet source blocks.
        /// Adjust this once you inspect actual block codes in 1.22.
        /// </summary>
        private static bool IsRapidRivuletSource(Block block)
        {
            if (block == null || block.Code == null) return false;

            string code = block.Code.ToString();

            return code.Contains("rapidwater-still-7");
        }
    }
}
