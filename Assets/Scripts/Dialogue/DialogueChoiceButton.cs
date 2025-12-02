using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using UnityEngine.EventSystems;

/// <summary>
/// 选项按钮 - 支持Hover效果和点击缩放动画
/// </summary>
public class DialogueChoiceButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI Components")]
    public TextMeshProUGUI textBox;
    public Image backgroundImage;
    public Image outlineImage; // 用于Hover时的描边效果

    [Header("Animation Settings")]
    public float hoverScale = 1.05f;
    public float clickScale = 0.95f;
    public float animationDuration = 0.1f;

    private string choiceId;
    private System.Action<string> callback;
    private Vector3 originalScale;
    private Button button;

    private void Awake()
    {
        if (button == null)
            button = GetComponent<Button>();
        if (button == null)
            button = gameObject.AddComponent<Button>();
        
        if (textBox == null)
            textBox = GetComponentInChildren<TextMeshProUGUI>();
        if (backgroundImage == null)
            backgroundImage = GetComponent<Image>();

        originalScale = transform.localScale;

        // 添加点击事件
        if (button != null)
        {
            button.onClick.AddListener(OnClick);
        }
    }

    /// <summary>
    /// 设置按钮内容
    /// </summary>
    public void Setup(string text, string id, System.Action<string> action)
    {
        if (textBox != null)
            textBox.text = text;
        
        choiceId = id;
        callback = action;
    }

    /// <summary>
    /// 鼠标进入
    /// </summary>
    public void OnPointerEnter(PointerEventData eventData)
    {
        StartCoroutine(ScaleTo(originalScale * hoverScale));
        
        // 显示描边
        if (outlineImage != null)
        {
            outlineImage.enabled = true;
            Color c = outlineImage.color;
            c.a = 1f;
            outlineImage.color = c;
        }
    }

    /// <summary>
    /// 鼠标离开
    /// </summary>
    public void OnPointerExit(PointerEventData eventData)
    {
        StartCoroutine(ScaleTo(originalScale));
        
        // 隐藏描边
        if (outlineImage != null)
        {
            outlineImage.enabled = false;
        }
    }

    /// <summary>
    /// 点击事件
    /// </summary>
    private void OnClick()
    {
        StartCoroutine(ClickAnimation());
        callback?.Invoke(choiceId);
    }

    /// <summary>
    /// 点击动画：缩放 0.95 → 1.0
    /// </summary>
    private IEnumerator ClickAnimation()
    {
        // 缩小
        yield return StartCoroutine(ScaleTo(originalScale * clickScale));
        // 恢复
        yield return StartCoroutine(ScaleTo(originalScale));
    }

    /// <summary>
    /// 缩放动画协程
    /// </summary>
    private IEnumerator ScaleTo(Vector3 targetScale)
    {
        Vector3 startScale = transform.localScale;
        float elapsed = 0f;

        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / animationDuration;
            transform.localScale = Vector3.Lerp(startScale, targetScale, t);
            yield return null;
        }

        transform.localScale = targetScale;
    }
}

