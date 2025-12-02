using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections;
using System.Collections.Generic;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem.UI;
#endif

public class DialogueUI : MonoBehaviour
{
    [Header("Background Layer")]
    public Image bgImage;
    public CanvasGroup bgCanvasGroup;

    [Header("Character Layer")]
    public Image leftPortrait;
    public CanvasGroup leftPortraitCanvasGroup;
    public Image rightPortrait;
    public CanvasGroup rightPortraitCanvasGroup;
    public GameObject fxLayer; // 用于光效、对白气场特效

    [Header("Dialogue Layer - DialogueBox")]
    public GameObject dialogueBox;
    public TextMeshProUGUI nameTag;
    public TextMeshProUGUI dialogueText;
    public GameObject continueIndicator;
    public GameObject autoPlayIcon;

    [Header("Dialogue Layer - ChoicePanel")]
    public GameObject choicePanel;
    public DialogueChoiceButton choicePrefab;

    [Header("Control Layer")]
    public Button skipButton;
    public Button autoButton;
    public Button historyButton;

    [Header("History Window")]
    public GameObject historyWindow;
    public ScrollRect historyScrollRect;
    public TextMeshProUGUI historyText;

    [Header("Settings")]
    public float fadeDuration = 0.25f;
    public float typeSpeed = 0.04f;
    public float portraitBrightnessActive = 1.0f;
    public float portraitBrightnessInactive = 0.5f;

    [Header("Audio")]
    [Tooltip("用于播放打字与点击音效的 AudioSource；为空时会自动创建")]
    public AudioSource uiAudioSource;
    [Tooltip("用于播放打字音效的独立 AudioSource；为空时自动创建")]
    public AudioSource typingAudioSource;
    [Tooltip("打字机音效，默认尝试载入 Unity 内置 MenuHighlight 声音")]
    public AudioClip typeSound;
    [Tooltip("点击音效，默认尝试载入 Unity 内置 MenuClick 声音")]
    public AudioClip clickSound;
    [Tooltip("按钮悬停音效，默认尝试载入 Unity 内置 MenuHighlight 声音")]
    public AudioClip hoverSound;
    public float typeSoundInterval = 3f;
    [Range(0f, 1f)] public float typeSoundVolume = 0.4f;
    [Range(0f, 1f)] public float clickSoundVolume = 0.6f;
    [Range(0f, 1f)] public float hoverSoundVolume = 0.45f;
    [Range(0f, 2f)] public float typeSoundTailDelay = 0.2f;

    [HideInInspector] public bool isTyping = false;
    private bool skipTyping = false;
    private Coroutine typingCoroutine;
    private Coroutine indicatorCoroutine;
    private float nextTypeSoundTime;
    private bool historyWarningIssued = false;
    private readonly List<string> historyEntries = new List<string>(64);
    private bool isWiringHistory;
    private Coroutine typingSoundTailCoroutine;

    // 向后兼容的旧接口
    public Image background { get => bgImage; set => bgImage = value; }
    public CanvasGroup bgCanvas { get => bgCanvasGroup; set => bgCanvasGroup = value; }
    public Image portrait { get => leftPortrait; set => leftPortrait = value; }
    public CanvasGroup portraitCanvas { get => leftPortraitCanvasGroup; set => leftPortraitCanvasGroup = value; }
    public TextMeshProUGUI speakerName { get => nameTag; set => nameTag = value; }
    public GameObject choiceParent { get => choicePanel; set => choicePanel = value; }

    private void Awake()
    {
        historyEntries.Clear();
        AutoWireFields();
        TryRefreshHistoryText(scrollToBottom: false);

        // 初始化UI状态
        if (choicePanel != null) choicePanel.SetActive(false);
        if (historyWindow != null) historyWindow.SetActive(false);
        if (continueIndicator != null) continueIndicator.SetActive(false);
        if (autoPlayIcon != null) autoPlayIcon.SetActive(false);

        EnsureEventSystem();
        EnsureAudioDefaults();

        // 绑定按钮事件
        if (skipButton != null) skipButton.onClick.AddListener(OnSkipButton);
        if (autoButton != null) autoButton.onClick.AddListener(OnAutoButton);
        if (historyButton != null) historyButton.onClick.AddListener(OnHistoryButton);

        RegisterButtonAudio(skipButton, true);
        RegisterButtonAudio(autoButton, true);
        RegisterButtonAudio(historyButton, true);
    }

    /// <summary>
    /// 如果部分序列化字段在Prefab里没有被手动赋值，尝试通过子对象名称查找并自动赋值。
    /// 方便快速将生成的Prefab直接使用而不必手动在Inspector中连接所有引用。
    /// </summary>
    private void AutoWireFields()
    {
        Assign(ref bgImage, "BackgroundLayer/BGImage");
        Assign(ref bgCanvasGroup, "BackgroundLayer/BGImage");
        Assign(ref leftPortrait, "CharacterLayer/LeftPortrait");
        Assign(ref leftPortraitCanvasGroup, "CharacterLayer/LeftPortrait");
        Assign(ref rightPortrait, "CharacterLayer/RightPortrait");
        Assign(ref rightPortraitCanvasGroup, "CharacterLayer/RightPortrait");
        AssignGO(ref fxLayer, "CharacterLayer/FXLayer");

        AssignGO(ref dialogueBox, "DialogueLayer/DialogueBox");
        AssignTMP(ref nameTag, "DialogueLayer/DialogueBox/NameText", "DialogueLayer/DialogueBox/NameTag/NameText");
        AssignTMP(ref dialogueText, "DialogueLayer/DialogueBox/DialogueText");
        AssignGO(ref continueIndicator, "DialogueLayer/DialogueBox/ContinueIndicator");
        AssignGO(ref autoPlayIcon, "DialogueLayer/DialogueBox/AutoPlayIcon");
        AssignGO(ref choicePanel, "DialogueLayer/ChoicePanel");

        Assign(ref skipButton, "ControlLayer/SkipButton");
        Assign(ref autoButton, "ControlLayer/AutoButton");
        Assign(ref historyButton, "ControlLayer/HistoryButton");

        AutoWireHistory();
    }
    private RectTransform cachedHistoryViewport;
    private RectTransform cachedHistoryContent;

    private void AutoWireHistory()
    {
        if (isWiringHistory)
            return;

        isWiringHistory = true;
        try
        {
            AssignGO(ref historyWindow, "HistoryPanel");
            if (historyWindow == null)
                return;

            EnsureHistoryScrollInfrastructure();
        }
        finally
        {
            isWiringHistory = false;
        }
    }

    private void Assign<T>(ref T target, string path) where T : Component
    {
        if (target != null) return;
        var child = transform.Find(path);
        if (child != null)
        {
            target = child.GetComponent<T>();
        }
    }

    private void AssignTMP(ref TextMeshProUGUI target, params string[] paths)
    {
        if (target != null || paths == null) return;
        foreach (var path in paths)
        {
            var child = transform.Find(path);
            if (child == null) continue;
            var tmp = child.GetComponent<TextMeshProUGUI>();
            if (tmp != null)
            {
                target = tmp;
                return;
            }
        }
    }

    private void AssignGO(ref GameObject target, string path)
    {
        if (target != null) return;
        var child = transform.Find(path);
        if (child != null)
        {
            target = child.gameObject;
        }
    }

    #region Background Methods
    public IEnumerator FadeBackground(Sprite newBG)
    {
        if (bgImage == null)
            yield break;

        if (bgCanvasGroup == null)
        {
            bgCanvasGroup = bgImage.GetComponent<CanvasGroup>() ?? bgImage.gameObject.AddComponent<CanvasGroup>();
            bgCanvasGroup.interactable = false;
            bgCanvasGroup.blocksRaycasts = false;
        }

        bgImage.sprite = newBG;
        bgImage.enabled = newBG != null;
        bgCanvasGroup.alpha = newBG == null ? 0f : 1f;
        yield break;
    }
    #endregion

    #region Portrait Methods
    /// <summary>
    /// 设置左侧立绘
    /// </summary>
    public void SetLeftPortrait(Sprite sprite, bool isSpeaking)
    {
        ApplyPortrait(leftPortrait, leftPortraitCanvasGroup, sprite, isSpeaking);
    }

    /// <summary>
    /// 设置右侧立绘
    /// </summary>
    public void SetRightPortrait(Sprite sprite, bool isSpeaking)
    {
        ApplyPortrait(rightPortrait, rightPortraitCanvasGroup, sprite, isSpeaking);
    }

    /// <summary>
    /// 淡入淡出切换立绘（向后兼容）
    /// </summary>
    public IEnumerator FadePortrait(Sprite newPortrait)
    {
        return FadePortrait(true, newPortrait, true);
    }

    /// <summary>
    /// 淡入淡出切换立绘
    /// </summary>
    public IEnumerator FadePortrait(bool isLeft, Sprite newPortrait, bool isSpeaking)
    {
        CanvasGroup targetCanvas = isLeft ? leftPortraitCanvasGroup : rightPortraitCanvasGroup;
        Image targetImage = isLeft ? leftPortrait : rightPortrait;

        if (targetCanvas == null || targetImage == null) yield break;

        // Fade out
        for (float t = 0; t < fadeDuration; t += Time.deltaTime)
        {
            targetCanvas.alpha = 1 - (t / fadeDuration);
            yield return null;
        }

        if (newPortrait == null)
        {
            targetImage.sprite = null;
            targetImage.enabled = false;
            targetCanvas.alpha = 0f;
            yield break;
        }

        targetImage.enabled = true;
        targetImage.sprite = newPortrait;
        SetPortraitBrightness(targetCanvas, isSpeaking, targetImage);

        // Fade in
        for (float t = 0; t < fadeDuration; t += Time.deltaTime)
        {
            targetCanvas.alpha = (t / fadeDuration);
            yield return null;
        }

        targetCanvas.alpha = 1;
    }

    /// <summary>
    /// 设置立绘亮度（说话者1.0，非说话者0.5）
    /// </summary>
    private void ApplyPortrait(Image image, CanvasGroup canvasGroup, Sprite sprite, bool isSpeaking)
    {
        if (image == null || canvasGroup == null) return;

        if (sprite == null)
        {
            image.sprite = null;
            image.enabled = false;
            canvasGroup.alpha = 0f;
            return;
        }

        image.enabled = true;
        image.sprite = sprite;
        SetPortraitBrightness(canvasGroup, isSpeaking, image);
    }

    private void SetPortraitBrightness(CanvasGroup canvasGroup, bool isSpeaking, Image image = null)
    {
        if (canvasGroup == null) return;
        if (image != null && !image.enabled)
        {
            canvasGroup.alpha = 0f;
            return;
        }

        float targetAlpha = isSpeaking ? portraitBrightnessActive : portraitBrightnessInactive;
        canvasGroup.alpha = targetAlpha;
    }

    /// <summary>
    /// 更新立绘状态（根据说话者自动调整左右立绘亮度）
    /// </summary>
    public void UpdatePortraitStates(bool leftIsSpeaking, bool rightIsSpeaking)
    {
        SetPortraitBrightness(leftPortraitCanvasGroup, leftIsSpeaking, leftPortrait);
        SetPortraitBrightness(rightPortraitCanvasGroup, rightIsSpeaking, rightPortrait);
    }

    public void HidePortrait(bool isLeft)
    {
        var img = isLeft ? leftPortrait : rightPortrait;
        var cg = isLeft ? leftPortraitCanvasGroup : rightPortraitCanvasGroup;
        if (img == null || cg == null) return;
        img.sprite = null;
        img.enabled = false;
        cg.alpha = 0f;
    }
    #endregion

    #region Dialogue Text Methods
    public IEnumerator TypeText(string content)
    {
        if (dialogueText == null) yield break;

        isTyping = true;
        skipTyping = false;
        CancelTypingSoundTailCountdown();
        dialogueText.text = "";

        if (continueIndicator != null) continueIndicator.SetActive(false);

        foreach (char c in content)
        {
            if (skipTyping)
            {
                dialogueText.text = content;
                break;
            }

            dialogueText.text += c;
            if (!char.IsWhiteSpace(c))
            {
                TryPlayTypeSound();
            }
            yield return new WaitForSeconds(typeSpeed);
        }

        isTyping = false;
        BeginTypingSoundTailCountdown();
        ShowContinueIndicator();
    }

    /// <summary>
    /// 快速跳过打字机效果
    /// </summary>
    public void FastForward()
    {
        if (isTyping)
        {
            skipTyping = true;
        }
    }

    /// <summary>
    /// 设置角色名字
    /// </summary>
    public void SetSpeakerName(string name)
    {
        if (nameTag != null)
        {
            nameTag.text = name;
        }
    }

    /// <summary>
    /// 显示继续指示器（闪动箭头）
    /// </summary>
    private void ShowContinueIndicator()
    {
        if (continueIndicator != null)
        {
            continueIndicator.SetActive(true);
            if (indicatorCoroutine != null)
                StopCoroutine(indicatorCoroutine);
            indicatorCoroutine = StartCoroutine(BlinkIndicator());
        }
    }

    /// <summary>
    /// 闪烁指示器动画
    /// </summary>
    private IEnumerator BlinkIndicator()
    {
        if (continueIndicator == null) yield break;

        CanvasGroup indicatorCG = continueIndicator.GetComponent<CanvasGroup>();
        if (indicatorCG == null)
        {
            indicatorCG = continueIndicator.AddComponent<CanvasGroup>();
        }

        while (continueIndicator.activeSelf)
        {
            // Fade out
            for (float t = 0; t < 0.5f; t += Time.deltaTime)
            {
                indicatorCG.alpha = 1 - (t / 0.5f);
                yield return null;
            }

            // Fade in
            for (float t = 0; t < 0.5f; t += Time.deltaTime)
            {
                indicatorCG.alpha = t / 0.5f;
                yield return null;
            }
        }
    }
    #endregion

    #region Choice Panel Methods
    /// <summary>
    /// 显示选项面板
    /// </summary>
    public void ShowChoices(System.Collections.Generic.List<DialogueChoice> choices, System.Action<string> onChoiceSelected)
    {
        if (choicePanel == null || choicePrefab == null || choices == null) return;

        choicePanel.SetActive(true);

        // 清除旧选项
        foreach (Transform child in choicePanel.transform)
        {
            Destroy(child.gameObject);
        }

        // 创建新选项按钮
        foreach (var choice in choices)
        {
            DialogueChoiceButton btn = Instantiate(choicePrefab, choicePanel.transform);
            btn.Setup(choice.text, choice.id, id =>
            {
                onChoiceSelected?.Invoke(id);
            });

            var buttonComponent = btn.GetComponent<Button>();
            RegisterButtonAudio(buttonComponent, true);
        }
    }

    /// <summary>
    /// 隐藏选项面板
    /// </summary>
    public void HideChoices()
    {
        if (choicePanel != null)
        {
            choicePanel.SetActive(false);
        }
    }
    #endregion

    #region Control Buttons
    public void OnPauseButton()
    {
        DialogueManager.Instance?.TogglePause();
    }
    public void OnSkipButton()
    {
        DialogueManager.Instance?.SkipAll();
    }
    public void OnAutoButton()
    {
        DialogueManager.Instance?.ToggleAutoPlay();
    }
    public void OnHistoryButton()
    {
        ToggleHistory();
    }

    /// <summary>
    /// 设置自动播放图标显示状态
    /// </summary>
    public void SetAutoPlayIcon(bool active)
    {
        if (autoPlayIcon != null)
        {
            autoPlayIcon.SetActive(active);
        }
    }
    #endregion

    #region History Methods
    public void AddHistory(string speaker, string text)
    {
        var line = string.IsNullOrWhiteSpace(speaker)
            ? (text ?? string.Empty)
            : $"<b>{speaker}</b>: {text}";

        historyEntries.Add(line);

        var shouldScroll = historyWindow != null && historyWindow.activeInHierarchy;

        if (!TryRefreshHistoryText(shouldScroll))
        {
            if (!historyWarningIssued)
            {
                Debug.LogWarning("DialogueUI: historyText 未绑定，已缓存历史条目，等你重新绑定 HistoryPanel/Viewport/Content/HistoryText 后会自动恢复。");
                historyWarningIssued = true;
            }
        }
    }

    /// <summary>
    /// 切换历史窗口显示
    /// </summary>
    public void ToggleHistory()
    {
        if (historyWindow == null) return;

        var show = !historyWindow.activeSelf;
        historyWindow.SetActive(show);

        if (!show)
        {
            DialogueManager.Instance?.SetHistoryBlocking(false);
            return;
        }

        // 确保历史面板渲染在所有对话层之上
        historyWindow.transform.SetAsLastSibling();

        var cg = historyWindow.GetComponent<CanvasGroup>();
        if (cg != null)
        {
            cg.alpha = 1f;
            cg.blocksRaycasts = true;
            cg.interactable = true;
        }

        // 保证历史文字立即更新并滚动到底部
        TryRefreshHistoryText();

        DialogueManager.Instance?.SetHistoryBlocking(true);
    }
    #endregion

    private bool TryRefreshHistoryText(bool scrollToBottom = true)
    {
        if (historyText == null && !isWiringHistory)
        {
            AutoWireHistory();
        }

        if (historyText == null)
        {
            return false;
        }

        if (historyEntries.Count == 0)
        {
            historyText.text = string.Empty;
        }
        else if (historyEntries.Count == 1)
        {
            historyText.text = historyEntries[0];
        }
        else
        {
            historyText.text = string.Join("\n", historyEntries);
        }

        historyWarningIssued = false;

        EnsureHistoryScrollInfrastructure();

        if (historyScrollRect != null && historyScrollRect.content != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(historyScrollRect.content);
        }

        if (scrollToBottom)
        {
            ScrollHistoryToBottom();
        }
        return true;
    }

    private void OnDestroy()
    {
        if (typingCoroutine != null)
            StopCoroutine(typingCoroutine);
        if (indicatorCoroutine != null)
            StopCoroutine(indicatorCoroutine);
        if (typingSoundTailCoroutine != null)
            StopCoroutine(typingSoundTailCoroutine);
        if (typingAudioSource != null)
            typingAudioSource.Stop();
    }

    private void ScrollHistoryToBottom()
    {
        if (historyScrollRect == null) return;
        Canvas.ForceUpdateCanvases();
        historyScrollRect.verticalNormalizedPosition = 0f;
    }

    private void EnsureHistoryScrollInfrastructure()
    {
        if (historyWindow == null)
            return;

        if (historyScrollRect == null)
        {
            historyScrollRect = historyWindow.GetComponentInChildren<ScrollRect>(true);
            if (historyScrollRect == null)
            {
                historyScrollRect = historyWindow.AddComponent<ScrollRect>();
            }
            historyScrollRect.horizontal = false;
            historyScrollRect.vertical = true;
            historyScrollRect.movementType = ScrollRect.MovementType.Clamped;
            historyScrollRect.scrollSensitivity = 45f;
        }

        if (historyScrollRect.viewport == null)
        {
            cachedHistoryViewport = historyWindow.transform.Find("Viewport") as RectTransform;
            if (cachedHistoryViewport == null)
            {
                cachedHistoryViewport = historyWindow.GetComponent<RectTransform>();
            }
            historyScrollRect.viewport = cachedHistoryViewport;
        }
        else
        {
            cachedHistoryViewport = historyScrollRect.viewport;
        }

        if (cachedHistoryViewport != null && cachedHistoryViewport.GetComponent<Mask>() == null && cachedHistoryViewport.GetComponent<RectMask2D>() == null)
        {
            cachedHistoryViewport.gameObject.AddComponent<RectMask2D>();
        }

        if (historyText == null)
        {
            historyText = historyWindow.GetComponentInChildren<TextMeshProUGUI>(true);
        }

        if (historyScrollRect.content == null && historyText != null)
        {
            historyScrollRect.content = historyText.rectTransform;
        }

        cachedHistoryContent = historyScrollRect.content;
        if (cachedHistoryContent == null)
            return;

        if (cachedHistoryContent.GetComponent<ContentSizeFitter>() == null && cachedHistoryContent.GetComponent<TextMeshProUGUI>() != null)
        {
            var fitter = cachedHistoryContent.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        }
    }

        private void EnsureEventSystem()
        {
            var existing = EventSystem.current ?? FindObjectOfType<EventSystem>();
            if (existing != null)
            {
    #if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
                var standalone = existing.GetComponent<StandaloneInputModule>();
                if (standalone != null)
                {
                    Destroy(standalone);
                }
                if (existing.GetComponent<InputSystemUIInputModule>() == null)
                {
                    existing.gameObject.AddComponent<InputSystemUIInputModule>();
                }
    #else
                var inputSystemModule = existing.GetComponent<InputSystemUIInputModule>();
                if (inputSystemModule != null)
                {
                    Destroy(inputSystemModule);
                }
                if (existing.GetComponent<StandaloneInputModule>() == null)
                {
                    existing.gameObject.AddComponent<StandaloneInputModule>();
                }
    #endif
                return;
            }

            var es = new GameObject("EventSystem", typeof(EventSystem));
    #if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        es.AddComponent<InputSystemUIInputModule>();
    #else
        es.AddComponent<StandaloneInputModule>();
    #endif
        DontDestroyOnLoad(es);
        }

    private void EnsureAudioDefaults()
    {
        if (uiAudioSource == null)
        {
            uiAudioSource = gameObject.AddComponent<AudioSource>();
            uiAudioSource.playOnAwake = false;
            uiAudioSource.loop = false;
        }

        if (typingAudioSource == null)
        {
            typingAudioSource = gameObject.AddComponent<AudioSource>();
            typingAudioSource.playOnAwake = false;
            typingAudioSource.loop = false;
        }

        if (clickSound == null)
        {
            clickSound = TryLoadBuiltinClip(new [] { "UI/MenuClick.wav", "UI/Sounds/MenuClick.wav", "Sounds/MenuClick.wav" });
        }

        if (typeSound == null)
        {
            typeSound = TryLoadBuiltinClip(new [] { "UI/MenuHighlight.wav", "UI/Sounds/MenuHighlight.wav", "Sounds/MenuHighlight.wav" });
        }

        if (hoverSound == null)
        {
            hoverSound = TryLoadBuiltinClip(new [] { "UI/MenuHighlight.wav", "UI/Sounds/MenuHighlight.wav", "Sounds/MenuHighlight.wav" });
        }
    }

    private AudioClip TryLoadBuiltinClip(string[] candidatePaths)
    {
        foreach (var path in candidatePaths)
        {
            try
            {
                var clip = Resources.GetBuiltinResource<AudioClip>(path);
                if (clip != null) return clip;
            }
            catch
            {
                // ignore missing
            }
        }
        return null;
    }

    private void RegisterButtonAudio(Button button, bool includeClickSound)
    {
        if (button == null) return;
        var binder = button.GetComponent<UIButtonSoundBinder>();
        if (binder == null)
        {
            binder = button.gameObject.AddComponent<UIButtonSoundBinder>();
        }
        binder.Initialize(this, includeClickSound);
    }

    public void PlayClickSound()
    {
        if (uiAudioSource != null && clickSound != null)
        {
            uiAudioSource.PlayOneShot(clickSound, clickSoundVolume);
        }
    }

    public void PlayHoverSound()
    {
        if (uiAudioSource != null && hoverSound != null)
        {
            uiAudioSource.PlayOneShot(hoverSound, hoverSoundVolume);
        }
    }

    private void TryPlayTypeSound()
    {
        if (typingAudioSource == null || typeSound == null) return;
        if (Time.time < nextTypeSoundTime) return;
        nextTypeSoundTime = Time.time + typeSoundInterval;
        typingAudioSource.PlayOneShot(typeSound, typeSoundVolume);
    }

    private void BeginTypingSoundTailCountdown()
    {
        if (typingAudioSource == null || !typingAudioSource.isPlaying)
            return;

        CancelTypingSoundTailCountdown();

        if (typeSoundTailDelay <= 0f)
        {
            typingAudioSource.Stop();
            return;
        }

        typingSoundTailCoroutine = StartCoroutine(StopTypingSoundAfterDelay());
    }

    private void CancelTypingSoundTailCountdown()
    {
        if (typingSoundTailCoroutine != null)
        {
            StopCoroutine(typingSoundTailCoroutine);
            typingSoundTailCoroutine = null;
        }
    }

    private IEnumerator StopTypingSoundAfterDelay()
    {
        yield return new WaitForSeconds(typeSoundTailDelay);
        if (typingAudioSource != null)
        {
            typingAudioSource.Stop();
        }
        typingSoundTailCoroutine = null;
    }
}
