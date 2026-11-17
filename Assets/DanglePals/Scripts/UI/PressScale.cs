using UnityEngine;
using UnityEngine.EventSystems;

namespace DanglePals
{
    /// <summary>Squish on press, spring back on release. Gives every button a tactile feel.</summary>
    public sealed class PressScale : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public float Pressed = 0.92f;
        float _target = 1f, _v;
        Vector3 _base = Vector3.one;

        void Awake() { _base = transform.localScale; }
        public void OnPointerDown(PointerEventData e) { _target = Pressed; }
        public void OnPointerUp(PointerEventData e) { _target = 1f; }
        public void OnPointerExit(PointerEventData e) { _target = 1f; }

        void Update()
        {
            float s = transform.localScale.x / Mathf.Max(0.0001f, _base.x);
            _v += ((_target - s) * 400f - _v * 22f) * Time.unscaledDeltaTime;
            s += _v * Time.unscaledDeltaTime;
            transform.localScale = _base * s;
        }
    }
}
