using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Video;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

public class DialogueManager : MonoBehaviour
{
    public DialogueUI ui;
    public float autoPlayDelay = 1.2f;

    [Header("Dialogue Source")]
    [Tooltip("ç›´æŽ¥æŒ‡å®š JSON TextAssetï¼›ä¸ºç©ºåˆ™æŒ‰ fallbackDialogueId ä»Ž Resources/Dialogue/ ä¸­åŠ è½½")]
    public TextAsset dialogueAsset;
    [Tooltip("å½“æ²¡æœ‰æ‰‹åŠ¨æŒ‚ TextAsset æ—¶ï¼Œä¾æ—§å¯ä»¥ç”¨æ—§æ–¹å¼è¾“å…¥ Resources è·¯å¾„ ID")] 
    public string fallbackDialogueId;
    [Tooltip("æ˜¯å¦åœ¨ Start æ—¶è‡ªåŠ¨åŠ è½½å¹¶æ’­æ”¾ä¸Šé¢é…ç½®çš„å¯¹è¯ JSON")]
    public bool playOnStart = false;

    [Header("Resources Lookup")]
    [Tooltip("Resources æ–‡ä»¶å¤¹ä¸‹çš„èƒŒæ™¯å­ç›®å½•ï¼Œå¯ä¸ºç©ºä»£è¡¨ç›´æŽ¥ä½¿ç”¨ JSON ä¸­çš„è·¯å¾„")]
    public string backgroundFolder = "Backgrounds";
    [Tooltip("Resources æ–‡ä»¶å¤¹ä¸‹çš„ç«‹ç»˜å­ç›®å½•ï¼Œå¯ä¸ºç©ºä»£è¡¨ç›´æŽ¥ä½¿ç”¨ JSON ä¸­çš„è·¯å¾„")]
    public string portraitFolder = "Portraits";
    [Tooltip("åœ¨æ‰¾ä¸åˆ°èµ„æºæ—¶æ˜¯å¦æ‰“å°è­¦å‘Šæ—¥å¿—ï¼Œå¸®åŠ©æŽ’æŸ¥è·¯å¾„é—®é¢˜")]
    public bool logMissingAssets = true;

    private DialogueData data;
    private int slideIdx = 0;
    private int lineIdx = 0;
    private readonly System.Collections.Generic.List<RaycastResult> pointerRaycastCache = new System.Collections.Generic.List<RaycastResult>();

    private bool autoPlay = false;
    private bool paused = false;
    private bool historyBlocking = false;
    private bool dialogueEnded = false;
    private bool transitionBlocking = false;
    public static DialogueManager Instance { get; private set; }
    private void Awake()
    {
        // å•ä¾‹åˆå§‹åŒ–
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        if (!playOnStart)
            return;

        if (dialogueAsset != null)
        {
            LoadDialogue(dialogueAsset);
        }
        else if (!string.IsNullOrEmpty(fallbackDialogueId))
        {
            LoadDialogue(fallbackDialogueId);
        }

        if (data != null)
        {
            StartDialogue();
        }
    }

    void Update()
    {
        if (paused) return;

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        // æ–°è¾“å…¥ç³»ç»Ÿï¼šä½¿ç”¨ Input System çš„ Mouse
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            OnClickNext();
        }
#else
        // æ—§è¾“å…¥ç³»ç»Ÿæˆ– Both æ¨¡å¼
        if (Input.GetMouseButtonDown(0))
        {
            OnClickNext();
        }
#endif

    }
    public void LoadDialogue(string id)
    {
        TextAsset json = Resources.Load<TextAsset>("Dialogue/" + id);
        if (json == null)
        {
            Debug.LogError($"æœªæ‰¾åˆ° Resources/Dialogue/{id} å¯¹åº”çš„ JSON æ–‡ä»¶");
            return;
        }

        fallbackDialogueId = id;
        LoadDialogue(json);
    }

    public void LoadDialogue(TextAsset asset)
    {
        if (asset == null)
        {
            Debug.LogWarning("ä¼ å…¥çš„ dialogue TextAsset ä¸ºç©º");
            data = null;
            return;
        }

        dialogueAsset = asset;
        try
        {
            data = JsonUtility.FromJson<DialogueData>(asset.text);
            if (data == null)
            {
                Debug.LogError("å¯¹è¯ JSON è§£æžåŽä¸ºç©ºï¼Œè¯·æ£€æŸ¥æ ¼å¼");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"è§£æžå¯¹è¯ JSON å¤±è´¥: {ex.Message}");
            data = null;
        }
    }

    public void StartDialogue()
    {
        if (data == null)
        {
            Debug.LogWarning("å½“å‰è¿˜æ²¡æœ‰åŠ è½½ä»»ä½•å¯¹è¯ JSONï¼Œè¯·å…ˆè°ƒç”¨ LoadDialogue");
            return;
        }
        if (ui == null)
        {
            Debug.LogError("DialogueManager æœªç»‘å®š DialogueUIï¼Œæ— æ³•å¼€å§‹å¯¹è¯");
            return;
        }
        slideIdx = 0;
        lineIdx = 0;
        dialogueEnded = false;
        transitionBlocking = false;
        StartCoroutine(PlaySlide(skipVideoTransition: true));
    }

    IEnumerator PlaySlide(bool skipVideoTransition = false)
    {
        // load and play background video if present
        var slide = data.slides[slideIdx];
        VideoClip bgVideo = LoadVideoFromFolder(backgroundFolder, slide.bg);
        if (bgVideo != null)
        {
            ui.PlayVideoBackground(bgVideo, skipVideoTransition);
        }

        transitionBlocking = !skipVideoTransition;
        if (!skipVideoTransition)
        {
            while (ui != null && ui.IsVideoTransitioning)
                yield return null;
        }

        transitionBlocking = false;
        ui?.ShowDialoguePanel(true, clearContent: true);
        ShowLine();
    }

    void ShowLine()
    {
        var slide = data.slides[slideIdx];
        var line = slide.dialogue[lineIdx];
        var autoBgmName = slide.bg; // BGM follows background, no line suffix
        var autoLineAudioName = $"{slide.bg}_{lineIdx + 1}"; // SFX/Voice keep underscore + line index

        ui.SetSpeakerName(line.speaker);

        // --- Audio playback ---
        var audio = DialogueAudio.Instance ?? EnsureDialogueAudio();
        if (audio != null)
        {
            var bgmName = string.IsNullOrEmpty(line.bgm) ? autoBgmName : line.bgm;
            var sfxName = string.IsNullOrEmpty(line.sfx) ? autoLineAudioName : line.sfx;
            var voiceName = string.IsNullOrEmpty(line.voice) ? autoLineAudioName : line.voice;

            if (!string.IsNullOrEmpty(bgmName))
                audio.PlayBGM(bgmName);

            if (!string.IsNullOrEmpty(sfxName))
                audio.PlaySFX(sfxName);

            if (!string.IsNullOrEmpty(voiceName))
                audio.PlayVoice(voiceName);
        }
        else
        {
            Debug.LogWarning("[DialogueManager] DialogueAudio singleton missing; audio will not play.");
        }

        // ç«‹ç»˜ï¼ˆç®€åŒ–å¤„ç†ï¼šæ ¹æ®portraitå­—æ®µåˆ¤æ–­å·¦å³ï¼Œå®žé™…å¯ä»¥æ ¹æ®speakeråˆ¤æ–­ï¼‰
        Sprite leftPortrait = null;
        Sprite rightPortrait = null;
        bool leftIsSpeaking = true;

        if (!string.IsNullOrEmpty(line.portrait))
        {
            if (line.portrait.StartsWith("left_") || line.portrait.StartsWith("right_"))
            {
                if (line.portrait.StartsWith("left_"))
                {
                    leftPortrait = LoadSpriteFromFolder(portraitFolder, line.portrait);
                    leftIsSpeaking = true;
                }
                else
                {
                    rightPortrait = LoadSpriteFromFolder(portraitFolder, line.portrait);
                    leftIsSpeaking = false;
                }
            }
            else
            {
                leftPortrait = LoadSpriteFromFolder(portraitFolder, line.portrait);
                leftIsSpeaking = true;
            }
        }

        // æ›´æ–°ç«‹ç»˜
        if (leftPortrait != null)
            StartCoroutine(ui.FadePortrait(true, leftPortrait, leftIsSpeaking));
        else
            ui.HidePortrait(true);

        if (rightPortrait != null)
            StartCoroutine(ui.FadePortrait(false, rightPortrait, !leftIsSpeaking));
        else
            ui.HidePortrait(false);
        
        // æ›´æ–°ç«‹ç»˜äº®åº¦çŠ¶æ€
        ui.UpdatePortraitStates(leftIsSpeaking, !leftIsSpeaking);

        // åŽ†å²è®°å½•
        ui.AddHistory(line.speaker, line.text);

        // æ‰“å­—æœº
        StartCoroutine(ui.TypeText(line.text));
    }


    public void OnClickNext()
    {
        if (historyBlocking) return;
        if (ui != null && ui.IsVideoTransitioning) return;
        if (transitionBlocking) return;
        if (dialogueEnded || data == null || data.slides == null || data.slides.Count == 0) return;
        if (slideIdx >= data.slides.Count) return;
        if (IsPointerOverBlockingUI()) return;

        ui?.PlayClickSound();

        if (ui.isTyping)
        {
            ui.FastForward();
            return;
        }

        lineIdx++;
        var slide = data.slides[slideIdx];

        if (lineIdx >= slide.dialogue.Count)
        {
            // è¿›å…¥é€‰é¡¹?
            if (slide.choices != null && slide.choices.Count > 0)
            {
                ShowChoices();
                return;
            }

            // ä¸‹ä¸€å¼ å›¾
            StartCoroutine(TransitionToNextSlide());
            return;
        }

        ShowLine();
    }

    void ShowChoices()
    {
        var slide = data.slides[slideIdx];
        if (slide.choices == null || slide.choices.Count == 0) return;

        ui.ShowChoices(slide.choices, OnChoiceSelected);
    }

    void OnChoiceSelected(string choiceId)
    {
        Debug.Log("Player selected: " + choiceId);

        ui.HideChoices();

        // 下一张图
        StartCoroutine(TransitionToNextSlide());
    }

    private IEnumerator TransitionToNextSlide()
    {
        ui?.ShowDialoguePanel(false, clearContent: true);
        transitionBlocking = true;

        var audio = DialogueAudio.Instance;
        if (audio != null)
        {
            yield return StartCoroutine(audio.FadeOutAllCoroutine());
        }

        slideIdx++;
        lineIdx = 0;

        if (slideIdx >= data.slides.Count || dialogueEnded)
        {
            EndDialogue();
            yield break;
        }

        yield return StartCoroutine(PlaySlide(skipVideoTransition: false));
        transitionBlocking = false;
    }
    IEnumerator AutoPlayRoutine()
    {
        var wait = new WaitForSeconds(autoPlayDelay);
        while (autoPlay)
        {
            var audio = DialogueAudio.Instance ?? EnsureDialogueAudio();
            bool voicePlaying = audio != null && audio.IsVoicePlaying();

            if (!ui.isTyping && !voicePlaying)
            {
                yield return wait;
                if (!autoPlay) yield break;

                audio = DialogueAudio.Instance ?? EnsureDialogueAudio();
                voicePlaying = audio != null && audio.IsVoicePlaying();

                if (!ui.isTyping && !voicePlaying)
                    OnClickNext();
            }
            yield return null;
        }
    }

    public void ToggleAutoPlay()
    {
        autoPlay = !autoPlay;
        ui.SetAutoPlayIcon(autoPlay);
        if (autoPlay) StartCoroutine(AutoPlayRoutine());
    }


    public void TogglePause()
    {
        paused = !paused;
    }

    public void SkipAll()
    {
        slideIdx = data.slides.Count;
        lineIdx = 0;
        EndDialogue();
    }


        void EndDialogue()
    {
        if (dialogueEnded) return;
        dialogueEnded = true;
        Debug.Log("Dialogue ended");
        // ??"?_1?_???"??Y?-??^?????^??^~?--?o??T_
        SceneManager.LoadScene("BattleDemo");
    }

private Sprite LoadSpriteFromFolder(string folder, string resourceKey, bool warn = true)
{
    if (string.IsNullOrEmpty(resourceKey)) return null;

    string trimmedFolder = string.IsNullOrEmpty(folder) ? string.Empty : folder.TrimEnd('/', '\\');
    string finalPath = string.IsNullOrEmpty(trimmedFolder) ? resourceKey : trimmedFolder + "/" + resourceKey;

    Sprite sprite = Resources.Load<Sprite>(finalPath);
    if (sprite == null)
        sprite = Resources.Load<Sprite>(resourceKey);

    if (sprite == null && warn && logMissingAssets)
        Debug.LogWarning($"æœªèƒ½åœ¨ Resources ä¸­æ‰¾åˆ° Spriteï¼š{finalPath} (æˆ– {resourceKey})");

    return sprite;
}


    private VideoClip LoadVideoFromFolder(string folder, string resourceKey)
    {
        if (string.IsNullOrEmpty(resourceKey)) return null;

        string trimmedFolder = string.IsNullOrEmpty(folder) ? string.Empty : folder.TrimEnd('/', '\\');
        string finalPath = string.IsNullOrEmpty(trimmedFolder) ? resourceKey : trimmedFolder + "/" + resourceKey;

        VideoClip clip = Resources.Load<VideoClip>(finalPath);
        if (clip == null)
        {
            clip = Resources.Load<VideoClip>(resourceKey);
        }

        if (clip == null && logMissingAssets)
        {
            Debug.LogWarning($"â€˜oÂ¦Å Å¸Â«â€ o\" Resources â€ž,-â€˜%_â€ ^Ã¸ VideoClipâ€¹Â¬s{finalPath} (â€˜^- {resourceKey})");
        }

        return clip;
    }

    private DialogueAudio EnsureDialogueAudio()
    {
        if (DialogueAudio.Instance != null)
            return DialogueAudio.Instance;

#if UNITY_2023_1_OR_NEWER
        var existing = Object.FindFirstObjectByType<DialogueAudio>();
#else
        var existing = Object.FindObjectOfType<DialogueAudio>();
#endif
        if (existing != null)
            return existing;

        var go = new GameObject("DialogueAudio_Auto");
        var created = go.AddComponent<DialogueAudio>();
        Debug.Log("[DialogueManager] Auto-created DialogueAudio in scene.");
        return created;
    }

    private bool IsPointerOverBlockingUI()
    {
        if (EventSystem.current == null)
            return false;

        var pointerPosition = GetPointerPosition();
        var eventData = new PointerEventData(EventSystem.current)
        {
            position = pointerPosition
        };

        pointerRaycastCache.Clear();
        EventSystem.current.RaycastAll(eventData, pointerRaycastCache);

        foreach (var hit in pointerRaycastCache)
        {
            if (hit.gameObject == null) continue;
            if (hit.gameObject.GetComponentInParent<Button>() != null)
                return true;
            if (hit.gameObject.GetComponentInParent<DialogueChoiceButton>() != null)
                return true;
            if (hit.gameObject.GetComponentInParent<ScrollRect>() != null)
                return true;
        }

        return false;
    }

    private Vector2 GetPointerPosition()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        return Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
#else
        return Input.mousePosition;
#endif
    }

    public void SetHistoryBlocking(bool shouldBlock)
    {
        historyBlocking = shouldBlock;
    }
}
