using System;
using System.Collections;
using TMPro;
using UnityEngine;

public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance { get; private set; }

    [SerializeField] private GameObject dialoguePanel;
    [SerializeField] private TMP_Text speakerNameText;
    [SerializeField] private TMP_Text dialogueText;
    [SerializeField] private float typeSpeed = 0.02f;
    [SerializeField] private KeyCode advanceKey = KeyCode.E;

    private string[] currentLines;
    private int lineIndex;
    private Action onDialogueComplete;
    private Coroutine typingCoroutine;
    private bool isTyping;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (dialoguePanel != null)
        {
            dialoguePanel.SetActive(false);
        }
    }

    private void Update()
    {
        if (dialoguePanel == null || !dialoguePanel.activeSelf)
        {
            return;
        }

        if (Input.GetKeyDown(advanceKey))
        {
            if (isTyping)
            {
                CompleteLine();
            }
            else
            {
                AdvanceLine();
            }
        }
    }

    public void StartDialogue(DialogueData data, Action onComplete)
    {
        if (data == null || data.lines == null || data.lines.Length == 0)
        {
            onComplete?.Invoke();
            return;
        }

        currentLines = data.lines;
        lineIndex = 0;
        onDialogueComplete = onComplete;

        if (speakerNameText != null)
        {
            speakerNameText.text = data.npcName;
        }

        dialoguePanel.SetActive(true);
        ShowLine();
    }

    private void ShowLine()
    {
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
        }

        typingCoroutine = StartCoroutine(TypeLine(currentLines[lineIndex]));
    }

    private IEnumerator TypeLine(string line)
    {
        isTyping = true;
        dialogueText.text = string.Empty;

        foreach (char c in line)
        {
            dialogueText.text += c;
            yield return new WaitForSeconds(typeSpeed);
        }

        isTyping = false;
    }

    private void CompleteLine()
    {
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
        }

        dialogueText.text = currentLines[lineIndex];
        isTyping = false;
    }

    private void AdvanceLine()
    {
        lineIndex++;

        if (lineIndex >= currentLines.Length)
        {
            EndDialogue();
        }
        else
        {
            ShowLine();
        }
    }

    private void EndDialogue()
    {
        dialoguePanel.SetActive(false);
        var callback = onDialogueComplete;
        onDialogueComplete = null;
        callback?.Invoke();
    }
}
