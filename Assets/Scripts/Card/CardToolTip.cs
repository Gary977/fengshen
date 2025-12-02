using UnityEngine;
using TMPro;

public class CardTooltip : MonoBehaviour
{
    public static CardTooltip Instance;

    [Header("UI References")]
    public GameObject tooltipPanel;
    public TextMeshProUGUI contentText;

    [Header("Settings")]
    public Vector3 offset = new Vector3(0f, 200f, 0f);
    // ↑现在只往上偏移，不再左右漂移

    private Canvas mainCanvas;

    void Awake()
    {
        Instance = this;
        HideTooltip();
        mainCanvas = GetComponentInParent<Canvas>();
    }

    public void ShowTooltip(string description, RectTransform cardRect)
    {
        if (tooltipPanel == null || contentText == null)
            return;

        tooltipPanel.SetActive(true);
        contentText.text = description;

        // 卡牌世界坐标 → 屏幕坐标
        Vector2 cardScreenPos = RectTransformUtility.WorldToScreenPoint(
                                    mainCanvas.worldCamera,
                                    cardRect.position);

        // Tooltip 放在卡牌上方 offset
        Vector2 finalPos = cardScreenPos + new Vector2(offset.x, offset.y);

        // 把屏幕坐标转换为 UI 本地坐标
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            mainCanvas.transform as RectTransform,
            finalPos,
            mainCanvas.worldCamera,
            out Vector2 localPoint);

        // 应用位置
        (transform as RectTransform).localPosition = localPoint;
    }

    public void HideTooltip()
    {
        if (tooltipPanel != null)
            tooltipPanel.SetActive(false);
    }
}
