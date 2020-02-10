using UnityEngine;

using BattleTech;
using BattleTech.Framework;

using HBS.Util;

using MissionControl.Data;

namespace MissionControl.LogicComponents.Dialogue {
  public class DialogueActivator : EncounterObjectGameLogic, ExecutableGameLogic {

    [SerializeField]
    public string dialogueGuid { get; set; }

    public override TaggedObjectType Type {
      get {
        return (TaggedObjectType)MCTaggedObjectType.ActivateDialogue;
      }
    }

    private void ActivateDialogue() {
      Main.LogDebug($"[DialogueActivator.ActivateDialogue]) Activating dialogue...");
      EncounterObjectGameLogic dialogue = MissionControl.Instance.EncounterLayerData.gameObject.GetEncounterObjectGameLogic(dialogueGuid);

      if (dialogue is DialogueGameLogic) {
        Main.LogDebug($"[DialogueActivator.ActivateDialogue]) Activating dialogue for '{dialogueGuid}:{dialogue.gameObject.name}'");
        ((DialogueGameLogic)dialogue).TriggerDialogue(true);
      }
    }

    public override void FromJSON(string json) {
      JSONSerializationUtility.FromJSON<DialogueActivator>(this, json);
    }

    public override string GenerateJSONTemplate() {
      return JSONSerializationUtility.ToJSON<DialogueActivator>(new DialogueActivator());
    }

    public override string ToJSON() {
      return JSONSerializationUtility.ToJSON<DialogueActivator>(this);
    }

    public void Execute() {
      ActivateDialogue();
    }
  }
}
