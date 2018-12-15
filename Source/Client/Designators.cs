﻿using Harmony;
using Multiplayer.Common;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Verse;

namespace Multiplayer.Client
{
    [HarmonyPatch(typeof(Designator))]
    [HarmonyPatch(nameof(Designator.Finalize))]
    [HarmonyPatch(new[] { typeof(bool) })]
    public static class DesignatorFinalizePatch
    {
        static bool Prefix(bool somethingSucceeded)
        {
            if (Multiplayer.Client == null) return true;
            return !somethingSucceeded || Multiplayer.ExecutingCmds;
        }
    }

    public static class DesignatorPatches
    {
        public static bool DesignateSingleCell(Designator __instance, [HarmonyArgument(0)] IntVec3 cell)
        {
            if (!Multiplayer.ShouldSync) return true;

            Designator designator = __instance;

            Map map = Find.CurrentMap;
            LoggingByteWriter writer = new LoggingByteWriter();
            writer.LogNode("Designate single cell: " + designator.GetType());

            WriteData(writer, DesignatorMode.SingleCell, designator);
            Sync.WriteSync(writer, cell);

            Multiplayer.Client.SendCommand(CommandType.Designator, map.uniqueID, writer.GetArray());
            Multiplayer.PacketLog.nodes.Add(writer.current);

            return false;
        }

        public static bool DesignateMultiCell(Designator __instance, [HarmonyArgument(0)] IEnumerable<IntVec3> cells)
        {
            if (!Multiplayer.ShouldSync) return true;

            // No cells implies Finalize(false), which currently doesn't cause side effects
            if (cells.Count() == 0) return true;

            Designator designator = __instance;

            Map map = Find.CurrentMap;
            LoggingByteWriter writer = new LoggingByteWriter();
            writer.LogNode("Designate multi cell: " + designator.GetType());
            IntVec3[] cellArray = cells.ToArray();

            WriteData(writer, DesignatorMode.MultiCell, designator);
            Sync.WriteSync(writer, cellArray);

            Multiplayer.Client.SendCommand(CommandType.Designator, map.uniqueID, writer.GetArray());
            Multiplayer.PacketLog.nodes.Add(writer.current);

            return false;
        }

        public static bool DesignateThing(Designator __instance, [HarmonyArgument(0)] Thing thing)
        {
            if (!Multiplayer.ShouldSync) return true;

            Designator designator = __instance;

            Map map = Find.CurrentMap;
            LoggingByteWriter writer = new LoggingByteWriter();
            writer.LogNode("Designate thing: " + thing + " " + designator.GetType());

            WriteData(writer, DesignatorMode.Thing, designator);
            Sync.WriteSync(writer, thing);

            Multiplayer.Client.SendCommand(CommandType.Designator, map.uniqueID, writer.GetArray());
            Multiplayer.PacketLog.nodes.Add(writer.current);

            MoteMaker.ThrowMetaPuffs(thing);

            return false;
        }

        private static void WriteData(ByteWriter data, DesignatorMode mode, Designator designator)
        {
            Sync.WriteSync(data, mode);
            Sync.WriteSync(data, designator);

            if (designator is Designator_AreaAllowed)
                Sync.WriteSync(data, Designator_AreaAllowed.SelectedArea);

            if (designator is Designator_Place place)
                Sync.WriteSync(data, place.placingRot);

            if (designator is Designator_Build build && build.PlacingDef.MadeFromStuff)
                Sync.WriteSync(data, build.stuffDef);

            if (designator is Designator_Install)
                Sync.WriteSync(data, ThingToInstall());
        }

        private static Thing ThingToInstall()
        {
            Thing singleSelectedThing = Find.Selector.SingleSelectedThing;
            if (singleSelectedThing is MinifiedThing)
                return singleSelectedThing;

            Building building = singleSelectedThing as Building;
            if (building != null && building.def.Minifiable)
                return singleSelectedThing;

            return null;
        }
    }

    [HarmonyPatch(typeof(Designator_Install))]
    [HarmonyPatch(nameof(Designator_Install.MiniToInstallOrBuildingToReinstall), MethodType.Getter)]
    public static class DesignatorInstallPatch
    {
        public static Thing thingToInstall;

        static void Postfix(ref Thing __result)
        {
            if (thingToInstall != null)
                __result = thingToInstall;
        }
    }

    [HarmonyPatch(typeof(Game))]
    [HarmonyPatch(nameof(Game.CurrentMap), MethodType.Getter)]
    public static class CurrentMapGetPatch
    {
        public static Map currentMap;

        static void Postfix(ref Map __result)
        {
            if (currentMap != null)
                __result = currentMap;
        }
    }

    [HarmonyPatch(typeof(Game))]
    [HarmonyPatch(nameof(Game.CurrentMap), MethodType.Setter)]
    [HarmonyPriority(Priority.First)]
    public static class CurrentMapSetPatch
    {
        public static bool ignore;

        static bool Prefix()
        {
            return !ignore;
        }
    }
}