using UnityEngine;

[RequireComponent(typeof(NPCController))]
public class NPCDialogueTrigger : MonoBehaviour, IInteractable
{
    [SerializeField] private DialogueData dialogueData;

    private NPCController npcController;

    public string InteractionPrompt => dialogueData != null
        ? $"Talk to {dialogueData.npcName}"
        : "Talk";

    private void Awake()
    {
        npcController = GetComponent<NPCController>();
    }

    public void Interact(GameObject interactor)
    {
        if (dialogueData == null || DialogueManager.Instance == null)
        {
            return;
        }

        npcController.SetTalking(true);

        var playerController = interactor.GetComponent<PlayerController>();
        if (playerController != null)
        {
            playerController.InputLocked = true;
        }

        DialogueManager.Instance.StartDialogue(dialogueData, () =>
        {
            npcController.SetTalking(false);
            if (playerController != null)
            {
                playerController.InputLocked = false;
            }
        });
    }
}
