using UnityEngine;
using HarmonyLib;
using System.Collections.Generic;
using System;
using System.Reflection;
using System.Linq;
using HMLLibrary;

public class DissociationDissimulation : Mod
{
    public static Dictionary<Network_Player, int> playerValues;
    public static List<HeadRule> heads = new List<HeadRule>
    {
        (typeof(AI_StateMachine_Shark), 127),
        (typeof(AI_StateMachine_PufferFish), 206),
        (typeof(AI_StateMachine_Boar), 253),
        (typeof(AI_StateMachine_MamaBear), new[] { 287, 381 }),
        (typeof(AI_StateMachine_Bear), new[] { 285, 287, 381 }, typeof(AI_StateMachine_MamaBear)),
        (typeof(AI_StateMachine_StoneBird), new[] { 205, 306 }),
        (typeof(AI_StateMachine_HyenaBoss), 537),
        (typeof(AI_StateMachine_Hyena), new[] { 536, 537 }, typeof(AI_StateMachine_HyenaBoss)),
        (typeof(AI_StateMachine_AnglerFish), 368),
        (typeof(AI_StateMachine_Rat), new Head(253, () => ratSpecialRule)),
        (typeof(AI_StateMachine_ButlerBot), new[] { new Head(271, () => butlerSpecialRule), new Head(617, () => butlerSpecialRule) })
    };
    static float detectionRange;
    public static float DetectionRange => ExtraSettingsAPI_Loaded ? detectionRange : 0;
    public static bool ratSpecialRule = false;
    public static bool butlerSpecialRule = false;
    Harmony harmony;
    public void Start()
    {
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
    static bool ExtraSettingsAPI_Loaded = false;

    public void ExtraSettingsAPI_Load() => ExtraSettingsAPI_SettingsClose();
    public void ExtraSettingsAPI_SettingsClose()
    {
        detectionRange = Mathf.Pow(ExtraSettingsAPI_GetSliderValue("detectionRange"), 2);
        ratSpecialRule = ExtraSettingsAPI_GetCheckboxState("ratSpecialRule");
        butlerSpecialRule = ExtraSettingsAPI_GetCheckboxState("butlerSpecialRule");
    }

    public float ExtraSettingsAPI_GetSliderValue(string SettingName) => 0;
    public bool ExtraSettingsAPI_GetCheckboxState(string SettingName) => false;
}
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

[HarmonyPatch(typeof(AI_StateMachine), "UpdateStateMachine")]
static class Patch_ValidTarget
{
    static void Prefix(AI_StateMachine __instance, ref List<Player> __state)
    {
        __state = new List<Player>();
        foreach (Network_Player player in ComponentManager<Raft_Network>.Value.remoteUsers.Values)
            if (!player.PlayerScript.IsDead && (player.FeetPosition - __instance.transform.position).sqrMagnitude > DissociationDissimulation.DetectionRange)
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
    public static implicit operator HeadRule((Type, int) v) => new HeadRule(v.Item1, new[] { v.Item2 }, new Type[0]);
    public static implicit operator HeadRule((Type, int, Type) v) => new HeadRule(v.Item1, new[] { v.Item2 }, new[] { v.Item3 });
    public static implicit operator HeadRule((Type, int, Type[]) v) => new HeadRule(v.Item1, new[] { v.Item2 }, v.Item3);
    public static implicit operator HeadRule((Type, int[]) v) => new HeadRule(v.Item1, v.Item2, new Type[0]);
    public static implicit operator HeadRule((Type, int[], Type) v) => new HeadRule(v.Item1, v.Item2, new[] { v.Item3 });
    public static implicit operator HeadRule((Type, int[], Type[]) v) => new HeadRule(v.Item1, v.Item2, v.Item3);

    public bool ValidFor(AI_StateMachine ai, Network_Player player) => stateMachine.IsInstanceOfType(ai) && player.PlayerEquipment.IsWearing(heads) && (excludeStateMachienes == null || !excludeStateMachienes.Any(x => x.IsInstanceOfType(ai)));
}
