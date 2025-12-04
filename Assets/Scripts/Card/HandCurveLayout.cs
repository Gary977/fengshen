using UnityEngine;
using System.Collections.Generic;

public class HandCurveLayout : MonoBehaviour
{
    [Header("手牌整体下压偏移")]
    public float VerticalOffset = -500f;

    [Header("手牌半径配置 (随数量插值)")]
    public float MinRadius = 1000f;
    public float MaxRadius = 5000f;
    public int MaxCardCountForRadius = 10;

    [Header("角度配置")]
    public float BaseAngleSpread = 5f;
    public float MaxTotalAngle = 100f;

    public void RefreshLayout()
    {
        if (CardUI.globalDragging) return;
        ApplyLayout();
    }

    void ApplyLayout()
    {
        if (CardUI.globalDragging) return;

        List<Transform> realCards = new List<Transform>();
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform c = transform.GetChild(i);
            if (c.GetComponent<CardUI>() != null)
                realCards.Add(c);
        }

        int count = realCards.Count;
        if (count == 0) return;

        float t = Mathf.Clamp01((float)(count - 1) / (MaxCardCountForRadius - 1));
        float currentRadius = Mathf.Lerp(MinRadius, MaxRadius, t);

        float currentSpread = BaseAngleSpread;
        float expectedTotalAngle = BaseAngleSpread * (count - 1);

        if (expectedTotalAngle > MaxTotalAngle)
        {
            currentSpread = MaxTotalAngle / Mathf.Max(1, count - 1);
        }

        float startAngle = -currentSpread * (count - 1) / 2f;

        for (int i = 0; i < count; i++)
        {
            Transform card = realCards[i];

            float angleDeg = startAngle + currentSpread * i;
            float angleRad = angleDeg * Mathf.Deg2Rad;

            float x = Mathf.Sin(angleRad) * currentRadius;
            float y = (Mathf.Cos(angleRad) * currentRadius) - currentRadius + VerticalOffset;
            float z = -i * 0.1f;

            card.localPosition = new Vector3(x, y, z);
            card.localRotation = Quaternion.Euler(0, 0, -angleDeg);
            card.SetSiblingIndex(i);
        }
    }
}
