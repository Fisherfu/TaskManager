using UnityEngine;

[CreateAssetMenu(fileName = "NewDialogue", menuName = "NPC Game/Dialogue Data")]
public class DialogueData : ScriptableObject
{
    public string npcName = "NPC";

    [TextArea(2, 5)]
    public string[] lines;
}
