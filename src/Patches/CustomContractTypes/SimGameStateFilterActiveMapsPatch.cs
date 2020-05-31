using Harmony;

using BattleTech;
using BattleTech.Data;
using BattleTech.Framework;

using System.Linq;
using System.Collections.Generic;

namespace MissionControl.Patches {
  [HarmonyPatch(typeof(SimGameState), "FilterActiveMaps")]
  public class SimGameStateFilterActiveMapsPatch {
    static void Postfix(SimGameState __instance, ref WeightedList<MapAndEncounters> activeMaps, List<Contract> currentContracts) {
      // Main.LogDebug($"[SimGameStateFilterActiveMapsPatch.Postfix] Running SimGameStateFilterActiveMapsPatch");
      FixActiveMapWeights(activeMaps);
      FilterOnMapsWithEncountersWithValidContractRequirements(__instance, activeMaps, currentContracts);
    }

    private static void FixActiveMapWeights(WeightedList<MapAndEncounters> activeMaps) {
      foreach (MapAndEncounters level in activeMaps) {
        int indexOfLevelInActiveList = -1;
        int weight = level.Map.Weight;

        // Get index1 of element in the rootList
        List<MapAndEncounters> rootList = (List<MapAndEncounters>)Traverse.Create(activeMaps).Field("rootList").GetValue();
        int indexOfLevelInRootList = rootList.IndexOf(level);

        // Get index2 of element in activeList (if it contains it)
        List<MapAndEncounters> activeList = (List<MapAndEncounters>)Traverse.Create(activeMaps).Field("activeList").GetValue();
        if (activeList.Contains(level)) {
          indexOfLevelInActiveList = activeList.IndexOf(level);
        }

        // Set the weight in the rootWeights in index1 position
        List<int> rootWeights = (List<int>)Traverse.Create(activeMaps).Property("rootWeights").GetValue();
        rootWeights[indexOfLevelInRootList] = weight;

        // if index2 is >= 0, remove from index2 of activeWeights
        if (indexOfLevelInActiveList >= 0) {
          List<int> activeWeights = (List<int>)Traverse.Create(activeMaps).Property("activeWeights").GetValue();
          activeWeights[indexOfLevelInActiveList] = weight;
        }
      }
    }

    private static void FilterOnMapsWithEncountersWithValidContractRequirements(SimGameState simGameState, WeightedList<MapAndEncounters> activeMaps, List<Contract> currentContracts) {
      List<int> indexesToRemove = new List<int>();
      StarSystem system = MissionControl.Instance.System;

      for (int i = 0; i < activeMaps.Count; i++) {
        MapAndEncounters level = activeMaps[i];
        bool removeMap = true;

        foreach (EncounterLayer_MDD encounterLayerMDD in level.Encounters) {
          int contractTypeId = (int)encounterLayerMDD.ContractTypeRow.ContractTypeID;

          // If the encounter ContractTypeID exists in the potential contracts list, continue
          if (MissionControl.Instance.PotentialContracts.ContainsKey(contractTypeId)) {
            // If the contract overrides in the potential contracts by ContractTypeID has a `DoesContractMeetRequirements` sucess, mark remove = false
            List<ContractOverride> contractOverrides = MissionControl.Instance.PotentialContracts[contractTypeId];
            foreach (ContractOverride contractOverride in contractOverrides) {
              bool doesContractMeetReqs = (bool)AccessTools.Method(typeof(SimGameState), "DoesContractMeetRequirements").Invoke(simGameState, new object[] { system, level, contractOverride });
              if (doesContractMeetReqs) {
                // At least one contract override meets the requirements to prevent the infinite spinner so ignore this logic now and continue to the next map/encounter combo
                // Main.LogDebug($"[FilterOnMapsWithEncountersWithValidContractRequirements] Level '{level.Map.MapName}.{encounterLayerMDD.Name}' has at least one valid contract override");
                removeMap = false;
                break;
              }
            }
          }
        }

        if (removeMap) {
          // Main.LogDebug($"[FilterOnMapsWithEncountersWithValidContractRequirements] Level '{level.Map.MapName}' had no encounters with anyvalid contract overrides. Removing map.");
          indexesToRemove.Add(i);
        }
      }

      // Remove maps that have no valid contracts due to failing requirements
      foreach (int indexToRemove in indexesToRemove) {
        activeMaps.RemoveAt(indexToRemove);
      }

      // If there are no more active maps, reset the biomes/maps list
      if (activeMaps.Count <= 0) {
        // Main.LogDebug($"[FilterOnMapsWithEncountersWithValidContractRequirements] No valid map/encounter combinations. Clearing map discard pile.");
        List<string> mapDiscardPile = (List<string>)AccessTools.Field(typeof(SimGameState), "mapDiscardPile").GetValue(simGameState);
        mapDiscardPile.Clear();

        WeightedList<MapAndEncounters> playableMaps = (WeightedList<MapAndEncounters>)AccessTools.Method(typeof(SimGameState), "GetSinglePlayerProceduralPlayableMaps").Invoke(null, new object[] { system });
        IEnumerable<int> source = from map in playableMaps select map.Map.Weight;
        WeightedList<MapAndEncounters> weightedList = new WeightedList<MapAndEncounters>(WeightedListType.WeightedRandom, playableMaps.ToList(), source.ToList<int>(), 0);

        activeMaps.AddRange(weightedList);
      }
    }
  }
}