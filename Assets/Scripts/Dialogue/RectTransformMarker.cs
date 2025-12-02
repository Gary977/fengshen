using UnityEngine;

/// <summary>
/// Simple tag component used to signal DialogueUI that a RectTransform has
/// custom layout settings that should not be overwritten at runtime.
/// </summary>
[DisallowMultipleComponent]
public sealed class RectTransformMarker : MonoBehaviour
{
    [Tooltip("Optional label to describe what customization this marker protects.")]
    public string note;
}
