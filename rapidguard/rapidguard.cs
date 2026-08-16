using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.ServerMods;

namespace RapidGuard;

public static class GenRivuletsPatch
{
    private static int slabBlockId;

    private static FieldInfo? gcfgField;
    private static FieldInfo? rapidWaterField;

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

    private static int GetSlabBlockId(IServerChunk[] chunks, BlockPos pos)
    {
        IMapChunk mapChunk = chunks[0].MapChunk;

        const int chunkSize = 32;

        int localX = pos.X % chunkSize;
        int localZ = pos.Z % chunkSize;

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

        string rockType = rockBlock.Code.LastCodePart();

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

    public static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> codes = new(instructions);

        bool patched = false;

        for (int i = 0; i < codes.Count; i++)
        {
            CodeInstruction instruction = codes[i];

            if (instruction.opcode != OpCodes.Callvirt &&
                instruction.opcode != OpCodes.Call)
            {
                continue;
            }

            if (instruction.operand is not MethodInfo method)
            {
                continue;
            }

            // We specifically want:
            //
            // blockAccessor.SetBlock(int, BlockPos)
            //
            // NOT:
            //
            // blockAccessor.SetBlock(int, BlockPos, int)
            //
            ParameterInfo[] parameters = method.GetParameters();

            if (method.Name != "SetBlock" ||
                parameters.Length != 2 ||
                parameters[0].ParameterType != typeof(int) ||
                parameters[1].ParameterType != typeof(BlockPos))
            {
                continue;
            }

            // Expected IL around the call:
            //
            // ld... blockAccessor
            // ldc.i4.0
            // ldloc... pos
            // callvirt IBlockAccessor.SetBlock
            //
            // We replace ldc.i4.0 with:
            //
            // ldarg.0              // GenRivulets instance
            // ldarg.3              // liquidBlockID
            // call GetTerrainBlockId
            //
            // NOTE: instance = argument 0
            //       chunks   = argument 1
            //       chunkX   = argument 2
            //       chunkZ   = argument 3
            //       liquid   = argument 4

            if (i < 2 || !codes[i - 2].LoadsConstant(0))
            {
                continue;
            }

            MethodInfo helper = AccessTools.Method(
                typeof(GenRivuletsPatch),
                nameof(GetTerrainBlockId)
            )!;

            // Preserve labels/blocks from the original ldc.i4.0.
            CodeInstruction originalZero = codes[i - 2];

            codes[i - 2] = CodeInstruction.LoadArgument(0);
            codes[i - 2].labels.AddRange(originalZero.labels);
            codes[i - 2].blocks.AddRange(originalZero.blocks);

            codes.Insert(
                i - 1,
                CodeInstruction.LoadArgument(4)
            );

            codes.Insert(
                i,
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
                "[RapidGuard] Could not find the terrain SetBlock(0, pos) instruction."
            );
        }

        return codes;
    }

    private static int GetTerrainBlockId(
    GenRivulets instance,
    IServerChunk[] chunks,
    int liquidBlockID,
    BlockPos pos
    )
    {
        if (gcfgField == null || rapidWaterField == null)
        {
            return 0;
        }

        object? gcfg = gcfgField.GetValue(instance);

        if (gcfg == null)
        {
            return 0;
        }

        int rapidWaterBlockId =
            (int)rapidWaterField.GetValue(gcfg)!;

        if (liquidBlockID != rapidWaterBlockId)
        {
            return 0;
        }

        return GetSlabBlockId(chunks, pos);
    }