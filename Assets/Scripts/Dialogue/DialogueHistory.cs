using UnityEngine;
using TMPro;

public class DialogueHistory : MonoBehaviour
{
    public GameObject historyWindow;
    public TextMeshProUGUI historyText;

    public void AddHistory(string speaker, string text)
    {
        if (historyText != null)
        {
            historyText.text += $"{speaker}：{text}\n";
        }
    }

    public void ToggleHistory()
    {
        if (historyWindow != null)
        {
            historyWindow.SetActive(!historyWindow.activeSelf);
        }
    }
}


