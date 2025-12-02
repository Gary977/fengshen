using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// 负责在 UI Button 上播放悬停与点击音效。
/// 需要被 DialogueUI 在运行时初始化以获得音源引用。
/// </summary>
[RequireComponent(typeof(Selectable))]
public class UIButtonSoundBinder : MonoBehaviour, IPointerEnterHandler
{
    private DialogueUI owner;
    private bool includeClickSound;
    private Button cachedButton;

    public void Initialize(DialogueUI dialogueUI, bool playClickSound)
    {
        owner = dialogueUI;
        includeClickSound = playClickSound;

        if (includeClickSound)
        {
            if (cachedButton == null)
            {
                cachedButton = GetComponent<Button>();
            }

            if (cachedButton != null)
            {
                cachedButton.onClick.RemoveListener(HandleClickSound);
                cachedButton.onClick.AddListener(HandleClickSound);
            }
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        owner?.PlayHoverSound();
    }

    private void HandleClickSound()
    {
        if (includeClickSound)
        {
            owner?.PlayClickSound();
        }
    }

    private void OnDestroy()
    {
        if (cachedButton != null)
        {
            cachedButton.onClick.RemoveListener(HandleClickSound);
        }
    }
}
