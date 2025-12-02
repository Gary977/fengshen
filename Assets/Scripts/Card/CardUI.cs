using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CardUI : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler,
    IPointerDownHandler, IPointerUpHandler,
    IDragHandler
{
    public CardInstance instance;
    public Transform handArea;
    private Transform originalParent;
    private int originalIndex;
    // 外层由 HandCurveLayout 控制位置
    private RectTransform outer;

    // 内层是我们真正动的部分（缩放、抬高、旋转）
    public RectTransform visual;

    public CanvasGroup canvasGroup;

    // Ghost
    public GameObject ghostPrefab;
    private GameObject ghost;

    // Hover animation
    public float hoverScale = 1.12f;
    public float hoverLift = 40f;
    public float smooth = 12f;

    private bool isHover = false;
    private bool isDrag = false;

    private Vector3 visualBasePos;
    private Vector3 visualTargetPos;
    private Vector3 visualBaseScale;
    private Vector3 visualTargetScale;

    public static bool globalDragging = false;

    private DropZone dropZone;

    [Header("UI References")]
    public Image cardArt;
    public TextMeshProUGUI cardName;
    public TextMeshProUGUI cardCost;
    public TextMeshProUGUI cardPolarity;
    public static Transform hoverCanvas;

    void Awake()
    {
        outer = GetComponent<RectTransform>();
        visualBasePos = Vector3.zero;
        visualTargetPos = visualBasePos;
        handArea = GameObject.Find("HandArea").transform;

        visualBaseScale = visual.localScale;
        visualTargetScale = visualBaseScale;
    }


    void Update()
    {
        visual.localScale =
            Vector3.Lerp(visual.localScale, visualTargetScale, Time.deltaTime * smooth);

        visual.localPosition =
            Vector3.Lerp(visual.localPosition, visualTargetPos, Time.deltaTime * smooth);
    }


    // ---------------- HOVER ----------------
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (isDrag || globalDragging) return;

        isHover = true;

        visualTargetScale = visualBaseScale * hoverScale;
        visualTargetPos = new Vector3(0, hoverLift, 0);
        transform.SetSiblingIndex(999);
        // [新增] 呼叫 Tooltip 显示
        // 假设你的 definition 里有一个叫 description 的 string 字段
        string desc = instance.definition.description;

        // 传入当前卡牌的位置 (transform.position)
        if (CardTooltip.Instance != null)
        {
            CardTooltip.Instance.ShowTooltip(desc, GetComponent<RectTransform>());

        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        // [新增] 隐藏
        if (CardTooltip.Instance != null)
        {
            CardTooltip.Instance.HideTooltip();
        }
        if (!isDrag && !globalDragging)

            if (!isDrag)
        {
            visualTargetScale = visualBaseScale;
            visualTargetPos = Vector3.zero;
        }
    }


    // ---------------- DRAG ----------------
    public void OnPointerDown(PointerEventData eventData)
    {
        // [新增] 开始拖拽时，强制关闭描述框（否则拖着牌还有个框跟着很奇怪）
        if (CardTooltip.Instance != null)
        {
            CardTooltip.Instance.HideTooltip();
        }
        isDrag = true;
        globalDragging = true;

        originalParent = transform.parent;
        originalIndex = transform.GetSiblingIndex();

        // ★ Ghost 放在 HandArea，而不是 Canvas
        ghost = Instantiate(ghostPrefab, handArea);
        ghost.transform.position = eventData.position;
        ghost.GetComponent<GhostCard>().Setup(instance.definition);

        // ★ 同步 RectTransform
        var ghostRect = ghost.GetComponent<RectTransform>();
        var cardRect = GetComponent<RectTransform>();

        if (ghostRect != null && cardRect != null)
        {
            ghostRect.sizeDelta = cardRect.sizeDelta;
            ghostRect.localScale = cardRect.localScale;
            ghostRect.pivot = cardRect.pivot;
            ghostRect.anchorMin = cardRect.anchorMin;
            ghostRect.anchorMax = cardRect.anchorMax;
        }
        Debug.Log("HAND AREA = " + handArea);
        // 隐藏真实卡牌
        canvasGroup.alpha = 0f;
    }


    public void OnDrag(PointerEventData eventData)
    {
        if (ghost != null)
            ghost.transform.position = eventData.position;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isDrag = false;
        globalDragging = false;

        if (ghost != null)
            Destroy(ghost);

        canvasGroup.alpha = 1f;

        // 不再手动判断 DropZone
        // DropZone 会在 OnDrop() 自动处理

        // 恢复 hover 或原位置
        if (isHover)
        {
            visualTargetScale = visualBaseScale * hoverScale;
            visualTargetPos = new Vector3(0, hoverLift, 0);
        }
        else
        {
            visualTargetScale = visualBaseScale;
            visualTargetPos = Vector3.zero;
        }

        transform.SetParent(originalParent);
        transform.SetSiblingIndex(originalIndex);

        if (handArea != null)
            handArea.GetComponent<HandCurveLayout>().RefreshLayout();
    }

    public void Init(CardInstance inst)
    {
        instance = inst;

        cardArt.sprite = inst.definition.cardSprite;
        cardName.text = inst.definition.cardName;
        cardCost.text = inst.definition.cost.ToString();
        cardPolarity.text = inst.definition.polarity.ToString(); // Yin / Yang
    }
}
