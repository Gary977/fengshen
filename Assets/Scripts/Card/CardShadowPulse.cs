using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Drives a pulsing shadow glow for card UI Images using pre-placed Shadow components.
/// Assign 4~8 Shadows (around the card) in the inspector, then call SetStrong/SetWeak/Clear.
/// </summary>
public class CardShadowPulse : MonoBehaviour
{
    [Header("Assigned Shadows (required)")]
    public Shadow[] shadows;

    [Header("Colors")]
    public Color strongColor = new Color(1f, 0.9f, 0.2f, 1f); // gold
    public Color weakColor = new Color(1f, 0.2f, 0.2f, 1f);   // red

    private bool isStrong;
    private bool isWeak;

    // Pulse settings
    private const float pulseSpeed = 1.5f;   // Hz
    private const float baseAlpha = 0.4f;  // midpoint alpha
    private const float alphaAmp = 0.6f;   // +/- amplitude -> 0.2~0.6
    private const float minAlpha = baseAlpha - alphaAmp; // 0.2f
    private const float maxAlpha = baseAlpha + alphaAmp; // 0.6f

    private Color[] originalColors;
    private bool originalsCaptured;

    void Awake()
    {
        // If not wired in inspector, try to gather shadows on this object and children.
        if (shadows == null || shadows.Length == 0)
        {
            shadows = GetComponentsInChildren<Shadow>(includeInactive: true);
        }

        CaptureOriginals();
    }

    void Update()
    {
        if (!isStrong && !isWeak) return;
        if (shadows == null || shadows.Length == 0) return;

        float t = Time.unscaledTime * pulseSpeed * Mathf.PI * 2f;
        float alpha = Mathf.Clamp01(baseAlpha + Mathf.Sin(t) * alphaAmp);

        Color baseColor = isStrong ? strongColor : weakColor;
        ApplyPulsedColor(baseColor, alpha);
    }

    public void SetStrong()
    {
        isStrong = true;
        isWeak = false;
        CaptureOriginals();
        ApplyPulsedColor(strongColor, baseAlpha);
    }

    public void SetWeak()
    {
        isStrong = false;
        isWeak = true;
        CaptureOriginals();
        ApplyPulsedColor(weakColor, baseAlpha);
    }

    public void Clear()
    {
        isStrong = false;
        isWeak = false;
        RestoreOriginals();
    }

    private void ApplyPulsedColor(Color targetColor, float targetAlpha)
    {
        if (shadows == null) return;
        float lerpFactor = Mathf.InverseLerp(minAlpha, maxAlpha, targetAlpha);
        for (int i = 0; i < shadows.Length; i++)
        {
            var s = shadows[i];
            if (s == null) continue;
            Color orig = (originalColors != null && i < originalColors.Length) ? originalColors[i] : s.effectColor;
            Color tinted = new Color(targetColor.r, targetColor.g, targetColor.b, targetAlpha);
            s.effectColor = Color.Lerp(orig, tinted, lerpFactor);
        }
    }

    private void CaptureOriginals()
    {
        if (originalsCaptured || shadows == null) return;
        originalColors = new Color[shadows.Length];
        for (int i = 0; i < shadows.Length; i++)
        {
            originalColors[i] = shadows[i] != null ? shadows[i].effectColor : Color.clear;
        }
        originalsCaptured = true;
    }

    private void RestoreOriginals()
    {
        if (shadows == null || originalColors == null) return;
        for (int i = 0; i < shadows.Length; i++)
        {
            var s = shadows[i];
            if (s == null) continue;
            s.effectColor = originalColors[i];
        }
    }
}
