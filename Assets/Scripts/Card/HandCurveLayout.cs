using UnityEngine;
using System.Collections.Generic;

public class HandCurveLayout : MonoBehaviour
{
    [Header("核心设置")]
    public float VerticalOffset = -500f; // 无论半径怎么变，中间那张牌就在这个高度

    [Header("动态半径设置 (解决沉底问题)")]
    public float MinRadius = 1000f;      // 卡牌少时的半径 (弯曲)
    public float MaxRadius = 5000f;      // 卡牌多时的半径 (平缓)
    public int MaxCardCountForRadius = 10; // 当达到几张牌时，半径变为最大值

    [Header("角度设置")]
    public float BaseAngleSpread = 5f;
    public float MaxTotalAngle = 100f;

    public void RefreshLayout()
    {
        ApplyLayout();
    }

    void ApplyLayout()
    {
        // ① 收集真实卡牌
        List<Transform> realCards = new List<Transform>();
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform c = transform.GetChild(i);
            if (c.GetComponent<CardUI>() != null)
                realCards.Add(c);
        }

        int count = realCards.Count;
        if (count == 0) return;

        // --- 关键修改 1: 动态计算半径 ---
        // 根据卡牌数量，在 MinRadius 和 MaxRadius 之间插值
        // 牌越多，t 越大，radius 越大（越平）
        float t = Mathf.Clamp01((float)(count - 1) / (MaxCardCountForRadius - 1));
        float currentRadius = Mathf.Lerp(MinRadius, MaxRadius, t);

        // --- 关键修改 2: 动态角度挤压 ---
        float currentSpread = BaseAngleSpread;
        float expectedTotalAngle = BaseAngleSpread * (count - 1);

        if (expectedTotalAngle > MaxTotalAngle)
        {
            // 如果总角度太大，就强制压缩间隔
            currentSpread = MaxTotalAngle / Mathf.Max(1, count - 1);
        }

        float startAngle = -currentSpread * (count - 1) / 2f;

        // ③ 排版循环
        for (int i = 0; i < count; i++)
        {
            Transform card = realCards[i];

            // 角度计算
            float angleDeg = startAngle + currentSpread * i;
            float angleRad = angleDeg * Mathf.Deg2Rad;

            // 位置计算
            // Math: 
            // X = Sin(a) * R
            // Y = Cos(a) * R - R (将圆弧顶点归零) + Offset (整体上下移)

            float x = Mathf.Sin(angleRad) * currentRadius;
            float y = (Mathf.Cos(angleRad) * currentRadius) - currentRadius + VerticalOffset;

            // 深度 (Optional)
            float z = -i * 0.1f;

            card.localPosition = new Vector3(x, y, z);
            card.localRotation = Quaternion.Euler(0, 0, -angleDeg);

            // 确保渲染层级正确
            card.SetSiblingIndex(i);
        }
    }
}