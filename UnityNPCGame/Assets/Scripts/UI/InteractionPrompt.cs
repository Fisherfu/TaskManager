using TMPro;
using UnityEngine;

public class InteractionPrompt : MonoBehaviour
{
    [SerializeField] private GameObject promptRoot;
    [SerializeField] private TMP_Text promptText;
    [SerializeField] private Vector3 offset = new Vector3(0f, 1.2f, 0f);

    private void Awake()
    {
        if (promptRoot != null)
        {
            promptRoot.SetActive(false);
        }
    }

    public void Show(string message, Vector3 worldPosition)
    {
        if (promptRoot == null)
        {
            return;
        }

        promptRoot.SetActive(true);

        if (promptText != null)
        {
            promptText.text = message;
        }

        transform.position = worldPosition + offset;
    }

    public void Hide()
    {
        if (promptRoot != null)
        {
            promptRoot.SetActive(false);
        }
    }
}
