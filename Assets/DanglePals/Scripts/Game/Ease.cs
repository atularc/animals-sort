using UnityEngine;

namespace DanglePals
{
    public static class Ease
    {
        public static float OutBack(float t) { const float c1 = 1.70158f, c3 = c1 + 1f; return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f); }
        public static float InOut(float t) => t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;
        public static float OutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);
        /// <summary>Frame-rate independent smoothing factor.</summary>
        public static float Damp(float rate, float dt) => 1f - Mathf.Exp(-rate * dt);
    }
}
