using UnityEngine;
using UnityEngine.UI;

namespace DanglePals
{
    /// <summary>
    /// Keeps 1 canvas unit = 1 design pixel: phones match the 390-px width, wide screens (tablets, foldables)
    /// match the 760-px height so nothing gets oversized.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(CanvasScaler))]
    public sealed class CanvasAspect : MonoBehaviour
    {
        CanvasScaler _scaler;
        void Update()
        {
            if (_scaler == null) _scaler = GetComponent<CanvasScaler>();
            if (Screen.height <= 0) return;
            float m = (float)Screen.width / Screen.height > 390f / 760f ? 1f : 0f;
            if (!Mathf.Approximately(_scaler.matchWidthOrHeight, m)) _scaler.matchWidthOrHeight = m;
        }
    }
}
