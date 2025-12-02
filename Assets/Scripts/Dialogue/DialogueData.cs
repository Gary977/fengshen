using System.Collections.Generic;

[System.Serializable]
public class DialogueLine
{
    public string speaker;   // 角色名
    public string portrait;  // 对应立绘文件名
    public string text;

    // 新增字段
    public string sfx;       // 打字音效 / 特效音
    public string voice;     // 对白语音
    public string bgm;       // 若某句对白要求切 BGM
}

[System.Serializable]
public class DialogueChoice
{
    public string id;        // good / neutral / evil
    public string text;
}

[System.Serializable]
public class DialogueSlide
{
    public string bg;
    public List<DialogueLine> dialogue;
    public List<DialogueChoice> choices;
}

[System.Serializable]
public class DialogueData
{
    public string sceneId;
    public List<DialogueSlide> slides;
}
