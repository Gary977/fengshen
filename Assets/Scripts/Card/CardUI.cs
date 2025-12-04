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
    private Combatant owner;
    public Transform handArea;
    private Transform originalParent;
    private int originalIndex;
    private RectTransform outer;

    public RectTransform visual;

    public CanvasGroup canvasGroup;

    // Ghost
    public GameObject ghostPrefab;
    private GameObject ghost;
    private bool indexCaptured = false;
    private bool hoverRaised = false;

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
    public static Transform hoverCanvas;

    [Header("Visual Feedback")]
    [SerializeField] private CardShadowPulse shadowPulse;

    void Awake()
    {
        outer = GetComponent<RectTransform>();
        visualBasePos = Vector3.zero;
        visualTargetPos = visualBasePos;
        handArea = GameObject.Find("HandArea").transform;

        EnsureShadowPulse();

        visualBaseScale = visual.localScale;
        visualTargetScale = visualBaseScale;
    }


    void Update()
    {
        visual.localScale =
            Vector3.Lerp(visual.localScale, visualTargetScale, Time.deltaTime * smooth);

        visual.localPosition =
            Vector3.Lerp(visual.localPosition, visualTargetPos, Time.deltaTime * smooth);

        UpdateElementalGlow();
    }


    // ---------------- HOVER ----------------
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (isDrag || globalDragging) return;

        isHover = true;

        if (!indexCaptured)
        {
            originalIndex = transform.GetSiblingIndex();
            indexCaptured = true;
        }
        transform.SetAsLastSibling();
        hoverRaised = true;

        visualTargetScale = visualBaseScale * hoverScale;
        visualTargetPos = new Vector3(0, hoverLift, 0);
        string desc = instance.definition.description;

        if (CardTooltip.Instance != null)
        {
            CardTooltip.Instance.ShowTooltip(desc, GetComponent<RectTransform>());

        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (CardTooltip.Instance != null)
        {
            CardTooltip.Instance.HideTooltip();
        }
        if (!isDrag && !globalDragging)
        {
            if (hoverRaised && indexCaptured)
            {
                transform.SetSiblingIndex(originalIndex);
                hoverRaised = false;
            }
            visualTargetScale = visualBaseScale;
            visualTargetPos = Vector3.zero;
        }
        isHover = false;
    }


    // ---------------- DRAG ----------------
    public void OnPointerDown(PointerEventData eventData)
    {
        if (CardTooltip.Instance != null)
        {
            CardTooltip.Instance.HideTooltip();
        }
        isDrag = true;
        globalDragging = true;

        originalParent = transform.parent;
        if (!indexCaptured)
        {
            originalIndex = transform.GetSiblingIndex();
            indexCaptured = true;
        }
        // Bring to front visually while dragging
        transform.SetAsLastSibling();

        ghost = Instantiate(ghostPrefab, handArea);
        UpdateGhostPosition(eventData);
        ghost.GetComponent<GhostCard>().Setup(instance.definition);

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
        canvasGroup.alpha = 0f;
    }


    public void OnDrag(PointerEventData eventData)
    {
        UpdateGhostPosition(eventData);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isDrag = false;
        globalDragging = false;

        if (ghost != null)
            Destroy(ghost);

        canvasGroup.alpha = 1f;

        // Always reset hover visuals on release
        visualTargetScale = visualBaseScale;
        visualTargetPos = Vector3.zero;

        transform.SetParent(originalParent);
        if (indexCaptured)
        {
            transform.SetSiblingIndex(originalIndex);
        }
        indexCaptured = false;
        hoverRaised = false;
        isHover = false;

        if (handArea != null)
            handArea.GetComponent<HandCurveLayout>().RefreshLayout();
    }

    private void UpdateElementalGlow()
    {
        if (shadowPulse == null || instance == null || instance.definition == null) return;

        var bm = BattleManager.Instance;
        if (bm == null) return;

        // Only offensive cards get glow (Attack / AttackDebuff); others keep their original shadow.
        var type = instance.definition.type;
        bool isOffensive = (type == CardType.Attack || type == CardType.AttackDebuff);
        if (!isOffensive)
        {
            shadowPulse.Clear();
            return;
        }

        Combatant target = null;
        if (owner == bm.Player) target = bm.Enemy;
        else if (owner == bm.Enemy) target = bm.Player;
        else target = bm.Enemy;

        if (target == null) return;

        bool advantaged = WuxingHelper.IsKe(instance.definition.element, target.currentElement);
        bool disadvantaged = WuxingHelper.IsKe(target.currentElement, instance.definition.element);

        if (advantaged && !disadvantaged) shadowPulse.SetStrong();
        else if (disadvantaged && !advantaged) shadowPulse.SetWeak();
        else shadowPulse.Clear();
    }

    private void EnsureShadowPulse()
    {
        if (shadowPulse != null) return;
        shadowPulse = GetComponent<CardShadowPulse>() ?? GetComponentInChildren<CardShadowPulse>(true);
        if (shadowPulse == null && visual != null)
        {
            shadowPulse = visual.GetComponent<CardShadowPulse>() ?? visual.gameObject.AddComponent<CardShadowPulse>();
        }
        if (shadowPulse == null && cardArt != null)
        {
            shadowPulse = cardArt.GetComponent<CardShadowPulse>() ?? cardArt.gameObject.AddComponent<CardShadowPulse>();
        }
    }

    private void UpdateGhostPosition(PointerEventData eventData)
    {
        if (ghost == null || handArea == null) return;
        var handRect = handArea as RectTransform;
        var ghostRect = ghost.GetComponent<RectTransform>();
        if (handRect == null || ghostRect == null) return;

        Vector2 localPoint;
        var cam = eventData.pressEventCamera;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(handRect, eventData.position, cam, out localPoint))
        {
            ghostRect.anchoredPosition = localPoint;
        }
        else
        {
            // Fallback to world position so it stays visible even if conversion fails
            ghost.transform.position = eventData.position;
        }
    }

    public void Init(CardInstance inst, Combatant ownerCombatant = null)
    {
        instance = inst;
        owner = ownerCombatant;

        cardArt.sprite = inst.definition.cardSprite;


    }
}
