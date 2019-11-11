using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

using BattleTech;

using MissionControl.Trigger;
using MissionControl.Logic;

namespace MissionControl.Rules {
  public class CaptureEscortAdditionalBlockersEncounterRules : EncounterRules {
    public CaptureEscortAdditionalBlockersEncounterRules() : base() { }

    public override void Build() {
      Main.Logger.Log("[CaptureEscortAdditionalBlockersEncounterRules] Setting up rule object references");
      BuildAi();
      BuildRandomSpawns();
      BuildAdditionalLances("EnemyBlockingLance", SpawnLogic.LookDirection.AWAY_FROM_TARGET, "SpawnerPlayerLance", SpawnLogic.LookDirection.AWAY_FROM_TARGET, 25f, 100f);
    }

    public void BuildAi() {
      EncounterLogic.Add(new IssueFollowLanceOrderTrigger(new List<string>(){ Tags.EMPLOYER_TEAM }, IssueAIOrderTo.ToLance, new List<string>() { Tags.PLAYER_1_TEAM }));
    }

    public void BuildRandomSpawns() {
      if (!Main.Settings.RandomSpawns) return;
      
      Main.Logger.Log("[CaptureEscortAdditionalBlockersEncounterRules] Building spawns rules");
      EncounterLogic.Add(new SpawnLanceAtEdgeOfBoundary(this, "SpawnerPlayerLance", "EscortRegion"));
      EncounterLogic.Add(new SpawnLanceAtEdgeOfBoundary(this, "HunterLance", "EscortExtractionRegion", 200, true));
    }

    public override void LinkObjectReferences(string mapName) {
      ObjectLookup["EnemyBlockingLance"] = EncounterLayerData.gameObject.FindRecursive("Lance_Enemy_BlockingForce");
      ObjectLookup["EscortRegion"] = EncounterLayerData.gameObject.FindRecursive("Region_Occupy");
      ObjectLookup["HunterLance"] = EncounterLayerData.gameObject.FindRecursive("Lance_Enemy_Hunter");
      ObjectLookup["EscortExtractionRegion"] = EncounterLayerData.gameObject.FindRecursive("Region_Extraction");
    }
  }
}