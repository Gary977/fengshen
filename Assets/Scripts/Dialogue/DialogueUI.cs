using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Video;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem.UI;
#endif

public class DialogueUI : MonoBehaviour
{
    [Header("Background Layer")]
    public Image bgImage;
    public CanvasGroup bgCanvasGroup;
    [Tooltip("When set, videos render into this RawImage instead of the sprite BG.")]
    public RawImage bgVideoImage;
    [Tooltip("Optional VideoPlayer used for background videos.")]
    public VideoPlayer bgVideoPlayer;
    [Tooltip("RenderTexture size used when creating a target texture for video output.")]
    public Vector2Int videoTextureSize = new Vector2Int(1920, 1080);

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
    [Tooltip("点击音效，默认尝试载入 Unity 内置 MenuClick 声音")]
    public AudioClip clickSound;
    [Tooltip("按钮悬停音效，默认尝试载入 Unity 内置 MenuHighlight 声音")]
    public AudioClip hoverSound;
    [Range(0f, 1f)] public float clickSoundVolume = 0.6f;
    [Range(0f, 1f)] public float hoverSoundVolume = 0.45f;

    [HideInInspector] public bool isTyping = false;
    private bool skipTyping = false;
    private Coroutine typingCoroutine;
    private Coroutine indicatorCoroutine;
    private bool historyWarningIssued = false;
    private readonly List<string> historyEntries = new List<string>(64);
    private bool isWiringHistory;

    // 向后兼容的旧接口
    public Image background { get => bgImage; set => bgImage = value; }
    public CanvasGroup bgCanvas { get => bgCanvasGroup; set => bgCanvasGroup = value; }
    public Image portrait { get => leftPortrait; set => leftPortrait = value; }
    public CanvasGroup portraitCanvas { get => leftPortraitCanvasGroup; set => leftPortraitCanvasGroup = value; }
    public TextMeshProUGUI speakerName { get => nameTag; set => nameTag = value; }
    public GameObject choiceParent { get => choicePanel; set => choicePanel = value; }

    private RenderTexture bgVideoTexture;
    private Coroutine bgVideoCoroutine;
    private Coroutine bgTransitionCoroutine;
    public bool IsVideoTransitioning { get; private set; }

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
        EnsureButtonHoverAndClickEffects(skipButton);
        EnsureButtonHoverAndClickEffects(autoButton);
        EnsureButtonHoverAndClickEffects(historyButton);
    }

    /// <summary>
    /// 如果部分序列化字段在Prefab里没有被手动赋值，尝试通过子对象名称查找并自动赋值。
    /// 方便快速将生成的Prefab直接使用而不必手动在Inspector中连接所有引用。
    /// </summary>
    private void AutoWireFields()
    {
        Assign(ref bgImage, "BackgroundLayer/BGImage");
        Assign(ref bgCanvasGroup, "BackgroundLayer/BGImage");
        Assign(ref bgVideoImage, "BackgroundLayer/BGVideo");
        Assign(ref bgVideoPlayer, "BackgroundLayer/BGVideo");
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
    public void PlayVideoBackground(VideoClip clip, bool skipTransition = false)
    {
        if (bgVideoPlayer == null || bgVideoImage == null)
            return;

        if (bgTransitionCoroutine != null)
        {
            StopCoroutine(bgTransitionCoroutine);
        }

        if (clip == null)
        {
            StopBackgroundVideo();
            IsVideoTransitioning = false;
            return;
        }

        if (skipTransition)
        {
            ConfigureVideoTarget(clip);
            IsVideoTransitioning = false;
            if (bgVideoCoroutine != null)
            {
                StopCoroutine(bgVideoCoroutine);
                bgVideoCoroutine = null;
            }
            bgVideoCoroutine = StartCoroutine(PrepareAndPlayVideo());
            // ensure visible color
            bgVideoImage.color = Color.white;
            return;
        }

        IsVideoTransitioning = true;
        bgTransitionCoroutine = StartCoroutine(VideoTransitionRoutine(clip));
    }

    public void StopBackgroundVideo()
    {
        if (bgVideoCoroutine != null)
        {
            StopCoroutine(bgVideoCoroutine);
            bgVideoCoroutine = null;
        }

        if (bgVideoPlayer != null)
        {
            bgVideoPlayer.Stop();
            bgVideoPlayer.clip = null;
            bgVideoPlayer.targetTexture = null;
        }

        if (bgVideoImage != null)
        {
            bgVideoImage.enabled = false;
            bgVideoImage.texture = null;
        }
    }

    private IEnumerator PrepareAndPlayVideo()
    {
        if (bgVideoPlayer == null || bgVideoPlayer.clip == null)
            yield break;

        bgVideoPlayer.Prepare();
        while (!bgVideoPlayer.isPrepared)
        {
            yield return null;
        }

        bgVideoPlayer.Play();

        if (bgVideoImage != null)
            bgVideoImage.enabled = true;
    }
    #endregion
    private IEnumerator VideoTransitionRoutine(VideoClip clip)
    {
        if (bgVideoImage == null)
        {
            IsVideoTransitioning = false;
            yield break;
        }

        var originalColor = bgVideoImage.color;
        // Fallback to visible color if original was clear/black
        var fadeInColor = (originalColor.a < 0.99f && originalColor.r <= 0.01f && originalColor.g <= 0.01f && originalColor.b <= 0.01f)
            ? Color.white
            : new Color(originalColor.r, originalColor.g, originalColor.b, 1f);

        // Fade to black on RawImage color
        yield return FadeRawImageColor(bgVideoImage, Color.black, fadeDuration);
        yield return new WaitForSeconds(2f);

        if (clip == null)
        {
            StopBackgroundVideo();
            IsVideoTransitioning = false;
            yield break;
        }

        ConfigureVideoTarget(clip);
        if (bgVideoImage != null)
        {
            // keep hidden while preparing
            bgVideoImage.color = Color.black;
        }

        if (bgVideoCoroutine != null)
        {
            StopCoroutine(bgVideoCoroutine);
            bgVideoCoroutine = null;
        }
        // Wait for prepare/play to finish before fading back in to avoid showing previous frame
        yield return StartCoroutine(PrepareAndPlayVideo());

        // Fade back from black to original color (alpha 1)
        yield return FadeRawImageColor(bgVideoImage, fadeInColor, fadeDuration);
        IsVideoTransitioning = false;
    }


    private IEnumerator FadeRawImageColor(RawImage img, Color target, float duration)
    {
        if (img == null) yield break;
        var startColor = img.color;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float lerp = duration > 0f ? t / duration : 1f;
            img.color = Color.Lerp(startColor, target, lerp);
            yield return null;
        }
        img.color = target;
    }

    private void ConfigureVideoTarget(VideoClip clip)
    {
        if (bgVideoTexture == null || bgVideoTexture.width != videoTextureSize.x || bgVideoTexture.height != videoTextureSize.y)
        {
            int w = videoTextureSize.x > 0 ? videoTextureSize.x : 1920;
            int h = videoTextureSize.y > 0 ? videoTextureSize.y : 1080;
            bgVideoTexture = new RenderTexture(w, h, 0);
        }

        bgVideoPlayer.renderMode = VideoRenderMode.RenderTexture;
        bgVideoPlayer.targetTexture = bgVideoTexture;
        bgVideoPlayer.clip = clip;
        bgVideoPlayer.isLooping = true;
        bgVideoPlayer.playOnAwake = false;
        bgVideoPlayer.skipOnDrop = false;
        bgVideoPlayer.audioOutputMode = VideoAudioOutputMode.None;
        bgVideoPlayer.EnableAudioTrack(0, false);
        bgVideoPlayer.controlledAudioTrackCount = 0;

        bgVideoImage.texture = bgVideoTexture;
        bgVideoImage.enabled = true;
    }

private IEnumerator FadeCanvasAlpha(CanvasGroup cg, float target, float duration)
    {
        if (cg == null)
            yield break;

        float start = cg.alpha;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            cg.alpha = Mathf.Lerp(start, target, duration > 0f ? t / duration : 1f);
            yield return null;
        }
        cg.alpha = target;
    }

    private IEnumerator FadeImageAlpha(Image img, float target, float duration)
    {
        if (img == null) yield break;
        var color = img.color;
        float start = color.a;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float a = Mathf.Lerp(start, target, duration > 0f ? t / duration : 1f);
            img.color = new Color(color.r, color.g, color.b, a);
            yield return null;
        }
        img.color = new Color(color.r, color.g, color.b, target);
    }

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
    public void ShowDialoguePanel(bool visible, bool clearContent = true)
    {
        if (dialogueBox != null)
            dialogueBox.SetActive(visible);
        if (!visible && clearContent)
        {
            if (dialogueText != null) dialogueText.text = string.Empty;
            if (continueIndicator != null) continueIndicator.SetActive(false);
            if (nameTag != null) nameTag.text = string.Empty;
        }
    }

    public IEnumerator TypeText(string content)
    {
        if (dialogueText == null) yield break;

        isTyping = true;
        skipTyping = false;
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
            yield return new WaitForSeconds(typeSpeed);
        }

        isTyping = false;
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
            // 1) If we already have one, just ensure the right input module.
            var existing = EventSystem.current
#if UNITY_2023_1_OR_NEWER
                            ?? Object.FindFirstObjectByType<EventSystem>();
#else
                            ?? Object.FindObjectOfType<EventSystem>();
#endif
            if (existing != null)
            {
                EnsureInputModule(existing);
                CleanupExtraEventSystems(existing);
                return;
            }

            // 2) Create a scene-scoped EventSystem (do NOT DontDestroyOnLoad to avoid duplicates in gameplay scenes).
            var es = new GameObject("EventSystem", typeof(EventSystem));
            EnsureInputModule(es.GetComponent<EventSystem>());
        }

        private void EnsureInputModule(EventSystem es)
        {
            if (es == null) return;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            var standalone = es.GetComponent<StandaloneInputModule>();
            if (standalone != null)
            {
                Destroy(standalone);
            }
            if (es.GetComponent<InputSystemUIInputModule>() == null)
            {
                es.gameObject.AddComponent<InputSystemUIInputModule>();
            }
#else
            var inputSystemModule = es.GetComponent<InputSystemUIInputModule>();
            if (inputSystemModule != null)
            {
                Destroy(inputSystemModule);
            }
            if (es.GetComponent<StandaloneInputModule>() == null)
            {
                es.gameObject.AddComponent<StandaloneInputModule>();
            }
#endif
        }

        private void CleanupExtraEventSystems(EventSystem keep)
        {
            var all = FindObjectsOfType<EventSystem>();
            foreach (var es in all)
            {
                if (es == null || es == keep) continue;
                // If multiple found (e.g., dialogue carried over, plus gameplay scene), remove extras.
                Destroy(es.gameObject);
            }
        }

    private void EnsureAudioDefaults()
    {
        if (uiAudioSource == null)
        {
            uiAudioSource = gameObject.AddComponent<AudioSource>();
            uiAudioSource.playOnAwake = false;
            uiAudioSource.loop = false;
        }

        if (clickSound == null)
        {
            clickSound = TryLoadBuiltinClip(new [] { "UI/MenuClick.wav", "UI/Sounds/MenuClick.wav", "Sounds/MenuClick.wav" });
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

    private void EnsureButtonHoverAndClickEffects(Button button)
    {
        if (button == null) return;
        var trigger = button.GetComponent<EventTrigger>() ?? button.gameObject.AddComponent<EventTrigger>();
        AddTrigger(trigger, EventTriggerType.PointerEnter, _ =>
        {
            button.transform.localScale = Vector3.one * 1.05f;
            PlayHoverSound();
        });
        AddTrigger(trigger, EventTriggerType.PointerExit, _ =>
        {
            button.transform.localScale = Vector3.one;
        });
        AddTrigger(trigger, EventTriggerType.PointerClick, _ =>
        {
            PlayClickSound();
            StartCoroutine(ButtonClickPulse(button.transform));
        });
    }

    private void AddTrigger(EventTrigger trigger, EventTriggerType type, System.Action<BaseEventData> action)
    {
        var entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(new UnityEngine.Events.UnityAction<BaseEventData>(action));
        trigger.triggers.Add(entry);
    }

    private IEnumerator ButtonClickPulse(Transform target)
    {
        if (target == null) yield break;
        Vector3 start = target.localScale;
        Vector3 down = start * 0.92f;
        float t = 0f;
        const float downDuration = 0.08f;
        while (t < downDuration)
        {
            t += Time.unscaledDeltaTime;
            target.localScale = Vector3.Lerp(start, down, t / downDuration);
            yield return null;
        }
        target.localScale = down;

        t = 0f;
        const float upDuration = 0.12f;
        while (t < upDuration)
        {
            t += Time.unscaledDeltaTime;
            target.localScale = Vector3.Lerp(down, Vector3.one, t / upDuration);
            yield return null;
        }
        target.localScale = Vector3.one;
    }
}
