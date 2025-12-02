using UnityEngine;

public class DialogueSceneStarter : MonoBehaviour
{
    public string dialogueId;

    void Start()
    {
        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.LoadDialogue(dialogueId);
            DialogueManager.Instance.StartDialogue();
        }
    }
}


