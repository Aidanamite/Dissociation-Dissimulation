using UnityEngine;
using HarmonyLib;
using System.Collections.Generic;
using System;
using System.Reflection;
using System.Linq;
using HMLLibrary;
using _DissociationDissimulation;

public class DissociationDissimulation : Mod
{
    public static Dictionary<Network_Player, int> playerValues;
    const int SharkHead = 127;
    const int PufferHead = 206;
    const int BoarHead = 253;
    const int MamaBearHead = 287;
    const int PolarBearHead = 381;
    const int BearHead = 285;
    const int StoneBirdHead = 205;
    const int WhiteStoneBirdHead = 306;
    const int HyenaBossHead = 537;
    const int HyenaHead = 536;
    const int AnglerFishHead = 368;
    const int Headlamp = 271;
    const int AdvancedHeadlamp = 617;
    public static List<HeadRule> heads = new List<HeadRule>
    {
        (typeof(AI_StateMachine_Shark), SharkHead),
        (typeof(AI_StateMachine_PufferFish), new[] {new Head(PufferHead), new Head(AnglerFishHead, () => pufferSpecialRule) }),
        (typeof(AI_StateMachine_Boar), BoarHead),
        (typeof(AI_StateMachine_MamaBear), new[] { MamaBearHead, PolarBearHead }),
        (typeof(AI_StateMachine_Bear), new[] { BearHead, MamaBearHead, PolarBearHead }, typeof(AI_StateMachine_MamaBear)),
        (typeof(AI_StateMachine_StoneBird), new[] { StoneBirdHead, WhiteStoneBirdHead }),
        (typeof(AI_StateMachine_HyenaBoss), HyenaBossHead),
        (typeof(AI_StateMachine_Hyena), new[] { HyenaHead, HyenaBossHead }, typeof(AI_StateMachine_HyenaBoss)),
        (typeof(AI_StateMachine_AnglerFish), AnglerFishHead),
        (typeof(AI_StateMachine_Rat), new Head(BoarHead, () => ratSpecialRule)),
        (typeof(AI_StateMachine_ButlerBot), new[] { new Head(Headlamp, () => butlerSpecialRule), new Head(AdvancedHeadlamp, () => butlerSpecialRule) })
    };
    static float detectionRange { set => detectionRangeSqr = value * value; }
    public static float detectionRangeSqr;
    public static bool ratSpecialRule;
    public static bool butlerSpecialRule;
    public static bool pufferSpecialRule;

    public void ExtraSettingsAPI_Unload() => SetSettingDefaults();
    static void SetSettingDefaults()
    {
        detectionRangeSqr = 0;
        ratSpecialRule = false;
        butlerSpecialRule = false;
        pufferSpecialRule = false;
    }

    Harmony harmony;
    public void Awake()
    {
        SetSettingDefaults();
        playerValues = new Dictionary<Network_Player, int>();
        harmony = new Harmony("com.aidanamite.DissociationDissimulation");
        harmony.PatchAll();
        Log("Mod has been loaded!");
    }

    public void OnModUnload()
    {
        harmony.UnpatchAll(harmony.Id);
        Log("Mod has been unloaded!");
    }
}

namespace _DissociationDissimulation
{
    static class ExtentionMethods
    {
        public static bool IsWearing(this PlayerEquipment equipment, Head[] items)
        {
            Equipment[] equips = Traverse.Create(equipment).Field("equipment").GetValue<Equipment[]>();
            for (int i = 0; i < equips.Length; i++)
                if (equips[i].Equipped && items.Any(x => x.Condition() && x.ItemIndex == equips[i].equipableItem.UniqueIndex))
                    return true;
            return false;
        }
    }

    [HarmonyPatch]
    static class Patch_ValidTarget
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(AI_StateMachine), "Update");
            yield return AccessTools.Method(typeof(AI_Component), "Update");
        }
        static void Prefix(AI_StateMachine __instance, out List<Player> __state)
        {
            __state = new List<Player>();
            foreach (Network_Player player in ComponentManager<Raft_Network>.Value.remoteUsers.Values)
                if (!player.PlayerScript.IsDead && (DissociationDissimulation.detectionRangeSqr == 0 || (player.FeetPosition - __instance.transform.position).sqrMagnitude > DissociationDissimulation.detectionRangeSqr))
                    foreach (var connection in DissociationDissimulation.heads)
                        if (connection.ValidFor(__instance, player))
                        {
                            player.PlayerScript.IsDead = true;
                            __state.Add(player.PlayerScript);
                            break;
                        }
        }
        static void Finalizer(List<Player> __state)
        {
            foreach (Player player in __state)
                player.IsDead = false;
        }
    }

    /*[HarmonyPatch(typeof(AI_Component), "Update")]
    public class Patch_ValidTarget2
    {
        static void Prefix(AI_Component __instance, ref List<Player> __state)
        {
            __state = new List<Player>();
            if (__instance.connectedState?.stateMachine)
                foreach (Network_Player player in ComponentManager<Raft_Network>.Value.remoteUsers.Values)
                    if (!player.PlayerScript.IsDead && (player.FeetPosition - __instance.connectedState.stateMachine.transform.position).sqrMagnitude > DissociationDissimulation.DetectionRange)
                        foreach (var connection in DissociationDissimulation.heads)
                            if (connection.Item1.IsInstanceOfType(__instance.connectedState.stateMachine) && player.IsWearing(connection.Item2))
                            {
                                player.PlayerScript.IsDead = true;
                                __state.Add(player.PlayerScript);
                            }
        }
        static void Postfix(List<Player> __state)
        {
            foreach (Player player in __state)
                player.IsDead = false;
        }
    }*/

    public class Head
    {
        public readonly int ItemIndex;
        public readonly Func<bool> Condition;
        public Head(int ItemIndex, Func<bool> Condition = null)
        {
            this.ItemIndex = ItemIndex;
            if (Condition == null)
                Condition = () => true;
            this.Condition = Condition;
        }
        public static implicit operator Head(int v) => new Head(v);
        public static implicit operator int(Head v) => v.ItemIndex;
    }
    public class HeadRule
    {
        public readonly Type stateMachine;
        public readonly Head[] heads;
        public readonly Type[] excludeStateMachienes;
        public HeadRule(Type StateMachine, Head[] Heads, Type[] ExcludedStateMachienes)
        {
            stateMachine = StateMachine;
            heads = Heads;
            excludeStateMachienes = ExcludedStateMachienes;
        }
        public HeadRule(Type StateMachine, int[] Heads, Type[] ExcludedStateMachienes)
        {
            stateMachine = StateMachine;
            heads = new Head[Heads.Length];
            for (int i = 0; i < heads.Length; i++)
                heads[i] = Heads[i];
            excludeStateMachienes = ExcludedStateMachienes;
        }
        public static implicit operator HeadRule((Type, Head) v) => new HeadRule(v.Item1, new[] { v.Item2 }, new Type[0]);
        public static implicit operator HeadRule((Type, Head, Type) v) => new HeadRule(v.Item1, new[] { v.Item2 }, new[] { v.Item3 });
        public static implicit operator HeadRule((Type, Head, Type[]) v) => new HeadRule(v.Item1, new[] { v.Item2 }, v.Item3);
        public static implicit operator HeadRule((Type, Head[]) v) => new HeadRule(v.Item1, v.Item2, new Type[0]);
        public static implicit operator HeadRule((Type, Head[], Type) v) => new HeadRule(v.Item1, v.Item2, new[] { v.Item3 });
        public static implicit operator HeadRule((Type, Head[], Type[]) v) => new HeadRule(v.Item1, v.Item2, v.Item3);
        public static implicit operator HeadRule((Type, int[]) v) => new HeadRule(v.Item1, v.Item2, new Type[0]);
        public static implicit operator HeadRule((Type, int[], Type) v) => new HeadRule(v.Item1, v.Item2, new[] { v.Item3 });
        public static implicit operator HeadRule((Type, int[], Type[]) v) => new HeadRule(v.Item1, v.Item2, v.Item3);

        public bool ValidFor(AI_StateMachine ai, Network_Player player) => stateMachine.IsInstanceOfType(ai) && player.PlayerEquipment.IsWearing(heads) && (excludeStateMachienes == null || !excludeStateMachienes.Any(x => x.IsInstanceOfType(ai)));
    }
}