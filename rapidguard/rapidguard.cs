using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Linq;
using HarmonyLib;
using Vintagestory.API.MathTools;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.ServerMods;

namespace RapidGuard;

public static class GenRivuletsPatch
{
    private static FieldInfo? gcfgField;
    private static FieldInfo? rapidWaterField;
    private static FieldInfo? blockAccessorField;

    private static readonly Dictionary<int, int> slabBlockIds = new();

    private static ICoreServerAPI api = null!;

    public static void Initialize(ICoreServerAPI api)
    {
        GenRivuletsPatch.api = api;

        gcfgField = AccessTools.Field(
            typeof(GenRivulets),
            "gcfg"
        );

        if (gcfgField == null)
        {
            api.Logger.Error(
                "[RapidGuard] Could not find GenRivulets.gcfg."
            );
            return;
        }

        rapidWaterField = AccessTools.Field(
            gcfgField.FieldType,
            "rivuletRapidWaterBlockId"
        );
        
        blockAccessorField = AccessTools.Field(
            typeof(GenRivulets),
            "blockAccessor"
        );

        if (blockAccessorField == null)
        {
            api.Logger.Error(
                "[RapidGuard] Could not find GenRivulets.blockAccessor."
            );
            return;
        }


        if (rapidWaterField == null)
        {
            api.Logger.Error(
                "[RapidGuard] Could not find gcfg.rivuletRapidWaterBlockId."
            );
            return;
        }

        MethodInfo? target = AccessTools.Method(
            typeof(GenRivulets),
            "tryGenMountainSideRivers",
            new[]
            {
                typeof(IServerChunk[]),
                typeof(int),
                typeof(int),
                typeof(int)
            }
        );

        if (target == null)
        {
            api.Logger.Error(
                "[RapidGuard] Could not find GenRivulets.tryGenMountainSideRivers."
            );
            return;
        }

        MethodInfo? transpiler = AccessTools.Method(
            typeof(GenRivuletsPatch),
            nameof(Transpiler)
        );

        if (transpiler == null)
        {
            api.Logger.Error(
                "[RapidGuard] Could not find RapidGuard transpiler."
            );
            return;
        }

        Harmony harmony = new Harmony("rapidguard");

        harmony.Patch(
            target,
            transpiler: new HarmonyMethod(transpiler)
        );

        api.Logger.Notification(
            "[RapidGuard] Patched GenRivulets.tryGenMountainSideRivers."
        );
    }

    public static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> codes = new(instructions);

        MethodInfo helper = AccessTools.Method(
            typeof(GenRivuletsPatch),
            nameof(PlaceRapidSourceSlab)
        )!;

        bool patched = false;

        for (int i = 0; i < codes.Count; i++)
        {
            CodeInstruction instruction = codes[i];

            if (instruction.opcode != OpCodes.Call &&
                instruction.opcode != OpCodes.Callvirt)
            {
                continue;
            }

            if (instruction.operand is not MethodInfo method)
            {
                continue;
            }

            if (method.Name != "ScheduleBlockUpdate")
            {
                continue;
            }

            ParameterInfo[] parameters = method.GetParameters();

            if (parameters.Length != 1 ||
                parameters[0].ParameterType != typeof(BlockPos))
            {
                continue;
            }

            /*
            * We found:
            *
            *     blockAccessor.ScheduleBlockUpdate(pos);
            *
            * This is inside the m == 0 branch.
            *
            * IMPORTANT:
            *
            * We inject AFTER this call so the evaluation stack
            * is empty.
            */

            if (i < 1)
            {
                continue;
            }

            /*
            * The instruction immediately before ScheduleBlockUpdate
            * loads the BlockPos argument.
            *
            * We need another copy of that local load for our helper.
            *
            * Do NOT use Clone(), because Clone() can copy labels and
            * exception blocks.
            */
            CodeInstruction originalPosLoad = codes[i - 1];

            CodeInstruction posLoad = new CodeInstruction(
                originalPosLoad.opcode,
                originalPosLoad.operand
            );

            /*
            * Insert AFTER ScheduleBlockUpdate.
            *
            * The original call has consumed:
            *
            *     blockAccessor
            *     pos
            *
            * so the evaluation stack is empty here.
            *
            * We now push:
            *
            *     this
            *     chunks
            *     liquidBlockID
            *     pos
            *
            * and call:
            *
            *     PlaceRapidSourceSlab(...)
            */

            int insertIndex = i + 1;

            codes.Insert(
                insertIndex++,
                CodeInstruction.LoadArgument(0)
            );

            codes.Insert(
                insertIndex++,
                CodeInstruction.LoadArgument(1)
            );

            codes.Insert(
                insertIndex++,
                CodeInstruction.LoadArgument(4)
            );

            codes.Insert(
                insertIndex++,
                posLoad
            );

            codes.Insert(
                insertIndex,
                new CodeInstruction(
                    OpCodes.Call,
                    helper
                )
            );

            patched = true;
            break;
        }

        if (!patched)
        {
            throw new InvalidOperationException(
                "[RapidGuard] Could not find ScheduleBlockUpdate(BlockPos)."
            );
        }

        return codes;
    }


    private static int GetSlabBlockId(IServerChunk[] chunks, BlockPos pos)
    {
        IMapChunk mapChunk = chunks[0].MapChunk;

        const int chunkSize = 32;

        int localX = pos.X & 31;
        int localZ = pos.Z & 31;

        int rockBlockId = mapChunk.TopRockIdMap[
            localZ * chunkSize + localX
        ];

        if (rockBlockId <= 0)
        {
            return 0;
        }

        if (slabBlockIds.TryGetValue(rockBlockId, out int cachedSlabId))
        {
            return cachedSlabId;
        }

        Block rockBlock = api.World.GetBlock(rockBlockId);

        if (rockBlock == null)
        {
            slabBlockIds[rockBlockId] = 0;
            return 0;
        }

        string rockType = rockBlock.Code.Path.Split('-').Last();

        AssetLocation slabCode = new AssetLocation(
            "game",
            $"cobblestoneslab-{rockType}-up-free"
        );

        Block slabBlock = api.World.GetBlock(slabCode);

        if (slabBlock == null)
        {
            api.Logger.Warning(
                "[RapidGuard] No slab found for top rock {0}: {1}",
                rockBlock.Code,
                slabCode
            );

            slabBlockIds[rockBlockId] = 0;
            return 0;
        }

        slabBlockIds[rockBlockId] = slabBlock.Id;

        api.Logger.Debug(
            "[RapidGuard] Top rock {0} -> slab {1}",
            rockBlock.Code,
            slabBlock.Code
        );

        return slabBlock.Id;
    }

    private static void PlaceRapidSourceSlab(
        GenRivulets instance,
        IServerChunk[] chunks,
        int liquidBlockID,
        BlockPos pos)
    {
        if (gcfgField == null ||
            rapidWaterField == null ||
            blockAccessorField == null)
        {
            return;
        }

        object? gcfg = gcfgField.GetValue(instance);

        if (gcfg == null)
        {
            return;
        }

        int rapidWaterBlockId =
            (int)rapidWaterField.GetValue(gcfg)!;

        // Only rapid water gets the slab.
        if (liquidBlockID != rapidWaterBlockId)
        {
            return;
        }

        int slabId = GetSlabBlockId(chunks, pos);

        if (slabId <= 0)
        {
            return;
        }

        IWorldGenBlockAccessor? blockAccessor =
            blockAccessorField.GetValue(instance)
            as IWorldGenBlockAccessor;

        if (blockAccessor == null)
        {
            return;
        }

        blockAccessor.SetBlock(slabId, pos);
    }


}