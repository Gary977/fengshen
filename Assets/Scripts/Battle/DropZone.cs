using UnityEngine;
using UnityEngine.EventSystems;

public class DropZone : MonoBehaviour, IDropHandler
{
    public BattleManager battle;

    public void OnDrop(PointerEventData eventData)
    {
        if (eventData == null) return;
        if (eventData.pointerDrag == null) return;

        // ★★ 最关键修复：从父级查找 CardUI，而不是 pointerDrag 自身
        var cardUI = eventData.pointerDrag.GetComponentInParent<CardUI>();

        // 如果拖拽到的是 Ghost 或子物体，避免报错
        if (cardUI == null) return;
        if (cardUI.instance == null) return;
        if (battle == null) return;

        battle.PlayerPlayCard(cardUI.instance);
    }
}
