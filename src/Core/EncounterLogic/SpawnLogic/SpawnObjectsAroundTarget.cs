using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

using BattleTech;
using BattleTech.Designed;

using MissionControl.Rules;
using MissionControl.Utils;

namespace MissionControl.Logic {
  public class SpawnObjectsAroundTarget : SpawnLogic {
    private LogicState state;

    private List<string> objectKeys = new List<string>();
    private List<string> orientationTargetKeys = new List<string>();
    private Dictionary<string, string> keyLookup = new Dictionary<string, string>();
    private string defaultOrientationTargetKey = "";

    private List<GameObject> objectGos = new List<GameObject>();
    private Dictionary<string, GameObject> orientationTargets = new Dictionary<string, GameObject>();
    private Dictionary<GameObject, Vector3> validOrientationTargetPositions = new Dictionary<GameObject, Vector3>();

    private float minDistanceFromTarget = 50f;
    private float maxDistanceFromTarget = 150f;
    private LookDirection lookDirection = LookDirection.TOWARDS_TARGET;

    private int AttemptCountMax { get; set; } = 3;
    private int AttemptCount { get; set; } = 0;
    private int TotalAttemptMax { get; set; } = 3;
    private int TotalAttemptCount { get; set; } = 0;

    private Vector3 validOrientationTargetPosition;

    public SpawnObjectsAroundTarget(EncounterRules encounterRules, string objectKey, string orientationTargetKey, LookDirection lookDirection) : base(encounterRules) {
      this.objectKeys = new List<string> { objectKey };
      this.defaultOrientationTargetKey = orientationTargetKey;
      this.lookDirection = lookDirection;
    }

    public SpawnObjectsAroundTarget(EncounterRules encounterRules, string objectKey, string orientationTargetKey, LookDirection lookDirection, float minDistance, float maxDistance) : base(encounterRules) {
      this.objectKeys = new List<string> { objectKey };
      this.defaultOrientationTargetKey = orientationTargetKey;
      this.minDistanceFromTarget = minDistance;
      this.maxDistanceFromTarget = maxDistance;
      this.lookDirection = lookDirection;
    }

    public SpawnObjectsAroundTarget(EncounterRules encounterRules, List<string> objectKeys, string orientationTargetKey, LookDirection lookDirection) : base(encounterRules) {
      this.objectKeys = objectKeys;
      this.defaultOrientationTargetKey = orientationTargetKey;
      this.lookDirection = lookDirection;
    }

    public SpawnObjectsAroundTarget(EncounterRules encounterRules, List<string> objectKeys, string orientationTargetKey, LookDirection lookDirection, float minDistance, float maxDistance) : base(encounterRules) {
      this.objectKeys = objectKeys;
      this.defaultOrientationTargetKey = orientationTargetKey;
      this.minDistanceFromTarget = minDistance;
      this.maxDistanceFromTarget = maxDistance;
      this.lookDirection = lookDirection;
    }

    public SpawnObjectsAroundTarget(EncounterRules encounterRules, LogicState state, LookDirection lookDirection) : base(encounterRules) {
      this.state = state;
      this.lookDirection = lookDirection;
    }

    public SpawnObjectsAroundTarget(EncounterRules encounterRules, LogicState state, LookDirection lookDirection, float minDistance, float maxDistance) : base(encounterRules) {
      this.state = state;
      this.minDistanceFromTarget = minDistance;
      this.maxDistanceFromTarget = maxDistance;
      this.lookDirection = lookDirection;
    }

    public override void Run(RunPayload payload) {
      if (!GetObjectReferences()) return;

      for (int i = 0; i < objectGos.Count; i++) {
        GameObject objectGo = objectGos[i];
        string objectKey = objectKeys[i];
        string orientationTargetKey = orientationTargetKeys[i];
        Main.Logger.LogDebug($"[SpawnObjectsAroundTarget] Attempting for '{objectGo.name}' with key '{objectKey}'");
        GameObject orientationTarget = orientationTargets[orientationTargetKey];
        Main.Logger.LogDebug($"[SpawnObjectsAroundTarget] Using orientation target key '{orientationTargetKey}' and Go name '{orientationTarget.transform.name}'");

        SaveSpawnPosition(objectGo);

        CombatGameState combatState = UnityGameInstance.BattleTechGame.Combat;
        MissionControl encounterManager = MissionControl.Instance;

        if (validOrientationTargetPositions.ContainsKey(orientationTarget)) {
          validOrientationTargetPosition = validOrientationTargetPositions[orientationTarget];
          Main.LogDebug($"[SpawnObjectsAroundTarget] Reusing cached orientation target of '{orientationTarget.name}' at '{validOrientationTargetPosition}'.");
        } else {
          Main.LogDebug($"[SpawnObjectsAroundTarget] Orientation target of '{orientationTarget.name}' at '{orientationTarget.transform.position}'. Attempting to get closest valid path finding hex.");
          validOrientationTargetPosition = GetClosestValidPathFindingHex(orientationTarget, orientationTarget.transform.position, $"OrientationTarget.{orientationTarget.name}");
        }

        if (TotalAttemptCount >= TotalAttemptMax) {
          RestoreSpawnPosition(objectGo);
          return;
        }

        Vector3 newSpawnPosition = GetRandomPositionFromTarget(validOrientationTargetPosition, minDistanceFromTarget, maxDistanceFromTarget);
        newSpawnPosition = GetClosestValidPathFindingHex(objectGo, newSpawnPosition, $"NewRandomSpawnPositionFromOrientationTarget.{orientationTarget.name}", 2);

        if (encounterManager.EncounterLayerData.IsInEncounterBounds(newSpawnPosition)) {
          objectGo.transform.position = newSpawnPosition;

          if (lookDirection == LookDirection.TOWARDS_TARGET) {
            RotateToTarget(objectGo, orientationTarget);
          } else {
            RotateAwayFromTarget(objectGo, orientationTarget);
          }

          if (IsSpawnValid(objectGo, validOrientationTargetPosition)) {
            Main.Logger.Log("[SpawnObjectsAroundTarget] Object spawn complete");
            AttemptCount = 0;
            TotalAttemptCount = 0;
          } else {
            CheckAttempts();
            Run(payload);
          }
        } else {
          Main.LogDebugWarning("[SpawnObjectsAroundTarget] Selected object spawn point is outside of the boundary. Select a new object spawn point.");
          CheckAttempts();
          Run(payload);
        }
      }
    }

    private void CheckAttempts() {
      AttemptCount++;
      TotalAttemptCount++;

      if (AttemptCount > AttemptCountMax) {
        AttemptCount = 0;
        Main.LogDebug($"[SpawnObjectsAroundTarget] Cannot find a suitable object spawn within the boundaries of {minDistanceFromTarget} and {maxDistanceFromTarget}. Widening search");
        minDistanceFromTarget -= 10;
        if (minDistanceFromTarget <= 10) minDistanceFromTarget = 10;
        maxDistanceFromTarget += 25;
      }
    }

    // TODO: Check the object references properly for nulls like the other spawners
    protected override bool GetObjectReferences() {
      if (state != null) {
        List<string[]> extraLanceKeys = (List<string[]>)state.GetObject("ExtraLanceSpawnKeys");
        for (int i = 0; i < extraLanceKeys.Count; i++) {
          string[] keys = extraLanceKeys[i];
          string objectKey = keys[0];
          string orientationObjectKey = keys[1];

          objectKeys.Add(objectKey);
          orientationTargetKeys.Add(orientationObjectKey);
          keyLookup[objectKey] = orientationObjectKey;
        }
      }

      for (int i = 0; i < objectKeys.Count; i++) {
        GameObject objectGoShell;
        this.EncounterRules.ObjectLookup.TryGetValue(objectKeys[i], out objectGoShell);
        objectGos.Add(objectGoShell);
      }

      for (int i = 0; i < orientationTargetKeys.Count; i++) {
        GameObject orientationTargetGoShell;
        this.EncounterRules.ObjectLookup.TryGetValue(orientationTargetKeys[i], out orientationTargetGoShell);
        orientationTargets[orientationTargetKeys[i]] = orientationTargetGoShell;
      }

      if (defaultOrientationTargetKey != "") {
        GameObject defaultOrientationTargetGoShell;
        this.EncounterRules.ObjectLookup.TryGetValue(defaultOrientationTargetKey, out defaultOrientationTargetGoShell);
        orientationTargets[defaultOrientationTargetKey] = defaultOrientationTargetGoShell;
      }

      return true;
    }
  }
}