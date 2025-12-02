using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

public class DialogueManager : MonoBehaviour
{
    public DialogueUI ui;
    public float autoPlayDelay = 1.2f;

    [Header("Dialogue Source")]
    [Tooltip("直接指定 JSON TextAsset；为空则按 fallbackDialogueId 从 Resources/Dialogue/ 中加载")]
    public TextAsset dialogueAsset;
    [Tooltip("当没有手动挂 TextAsset 时，依旧可以用旧方式输入 Resources 路径 ID")] 
    public string fallbackDialogueId;
    [Tooltip("是否在 Start 时自动加载并播放上面配置的对话 JSON")]
    public bool playOnStart = false;

    [Header("Resources Lookup")]
    [Tooltip("Resources 文件夹下的背景子目录，可为空代表直接使用 JSON 中的路径")]
    public string backgroundFolder = "Backgrounds";
    [Tooltip("Resources 文件夹下的立绘子目录，可为空代表直接使用 JSON 中的路径")]
    public string portraitFolder = "Portraits";
    [Tooltip("在找不到资源时是否打印警告日志，帮助排查路径问题")]
    public bool logMissingAssets = true;

    private DialogueData data;
    private int slideIdx = 0;
    private int lineIdx = 0;
    private readonly System.Collections.Generic.List<RaycastResult> pointerRaycastCache = new System.Collections.Generic.List<RaycastResult>();

    private bool autoPlay = false;
    private bool paused = false;
    private bool historyBlocking = false;
    public static DialogueManager Instance { get; private set; }
    private void Awake()
    {
        // 单例初始化
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
        // 新输入系统：使用 Input System 的 Mouse
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            OnClickNext();
        }
#else
        // 旧输入系统或 Both 模式
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
            Debug.LogError($"未找到 Resources/Dialogue/{id} 对应的 JSON 文件");
            return;
        }

        fallbackDialogueId = id;
        LoadDialogue(json);
    }

    public void LoadDialogue(TextAsset asset)
    {
        if (asset == null)
        {
            Debug.LogWarning("传入的 dialogue TextAsset 为空");
            data = null;
            return;
        }

        dialogueAsset = asset;
        try
        {
            data = JsonUtility.FromJson<DialogueData>(asset.text);
            if (data == null)
            {
                Debug.LogError("对话 JSON 解析后为空，请检查格式");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"解析对话 JSON 失败: {ex.Message}");
            data = null;
        }
    }

    public void StartDialogue()
    {
        if (data == null)
        {
            Debug.LogWarning("当前还没有加载任何对话 JSON，请先调用 LoadDialogue");
            return;
        }
        if (ui == null)
        {
            Debug.LogError("DialogueManager 未绑定 DialogueUI，无法开始对话");
            return;
        }
        slideIdx = 0;
        lineIdx = 0;
        StartCoroutine(PlaySlide());
    }

    IEnumerator PlaySlide()
    {
        var slide = data.slides[slideIdx];

        // 背景
        Sprite bg = LoadSpriteFromFolder(backgroundFolder, slide.bg);
        yield return StartCoroutine(ui.FadeBackground(bg));

        // 第一条对白
        ShowLine();
    }

    void ShowLine()
    {
        var line = data.slides[slideIdx].dialogue[lineIdx];

        ui.SetSpeakerName(line.speaker);

        // --- 音乐 / 音效 ---
        if (DialogueAudio.Instance != null)
        {
            if (!string.IsNullOrEmpty(line.bgm))
                DialogueAudio.Instance.PlayBGM(line.bgm);

            if (!string.IsNullOrEmpty(line.sfx))
                DialogueAudio.Instance.PlaySFX(line.sfx);

            if (!string.IsNullOrEmpty(line.voice))
                DialogueAudio.Instance.PlayVoice(line.voice);
        }

        // 立绘（简化处理：根据portrait字段判断左右，实际可以根据speaker判断）
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

        // 更新立绘
        if (leftPortrait != null)
            StartCoroutine(ui.FadePortrait(true, leftPortrait, leftIsSpeaking));
        else
            ui.HidePortrait(true);

        if (rightPortrait != null)
            StartCoroutine(ui.FadePortrait(false, rightPortrait, !leftIsSpeaking));
        else
            ui.HidePortrait(false);
        
        // 更新立绘亮度状态
        ui.UpdatePortraitStates(leftIsSpeaking, !leftIsSpeaking);

        // 历史记录
        ui.AddHistory(line.speaker, line.text);

        // 打字机
        StartCoroutine(ui.TypeText(line.text));
    }


    public void OnClickNext()
    {
        if (historyBlocking)
            return;
        if (IsPointerOverBlockingUI())
            return;
        ui?.PlayClickSound();
        if (data == null || data.slides == null || data.slides.Count == 0)
        {
            Debug.LogWarning("当前没有可播放的对话数据，请先在 Inspector 中挂载 JSON 并调用 LoadDialogue/StartDialogue");
            return;
        }

        if (ui.isTyping)
        {
            ui.FastForward();
            return;
        }

        lineIdx++;
        var slide = data.slides[slideIdx];

        if (lineIdx >= slide.dialogue.Count)
        {
            // 进入选项?
            if (slide.choices != null && slide.choices.Count > 0)
            {
                ShowChoices();
                return;
            }

            // 下一张图
            slideIdx++;
            lineIdx = 0;

            if (slideIdx >= data.slides.Count)
            {
                EndDialogue();
                return;
            }

            StartCoroutine(PlaySlide());
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
        slideIdx++;
        lineIdx = 0;

        if (slideIdx >= data.slides.Count)
        {
            EndDialogue();
            return;
        }

        StartCoroutine(PlaySlide());
    }
    IEnumerator AutoPlayRoutine()
    {
        while (autoPlay)
        {
            if (!ui.isTyping)
            {
                yield return new WaitForSeconds(autoPlayDelay);
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
        Debug.Log("Dialogue ended");
        // 当对话结束时切换到战斗场景
        SceneManager.LoadScene("BattleDemo");
    }

    private Sprite LoadSpriteFromFolder(string folder, string resourceKey)
    {
        if (string.IsNullOrEmpty(resourceKey)) return null;

        string trimmedFolder = string.IsNullOrEmpty(folder) ? string.Empty : folder.TrimEnd('/', '\\');
        string finalPath = string.IsNullOrEmpty(trimmedFolder) ? resourceKey : trimmedFolder + "/" + resourceKey;

        Sprite sprite = Resources.Load<Sprite>(finalPath);
        if (sprite == null)
        {
            // 尝试直接使用原始 key，防止 JSON 已经写了完整路径
            sprite = Resources.Load<Sprite>(resourceKey);
        }

        if (sprite == null && logMissingAssets)
        {
            Debug.LogWarning($"未能在 Resources 中找到 Sprite：{finalPath} (或 {resourceKey})");
        }

        return sprite;
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

