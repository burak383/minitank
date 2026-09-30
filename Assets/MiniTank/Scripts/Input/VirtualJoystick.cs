using UnityEngine;
using UnityEngine.EventSystems;

namespace MiniTank
{
    /// <summary>Sol alttaki hareket joystick'i. Script arka plan görselinin üzerine eklenir.</summary>
    public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public RectTransform handle;
        public float radius = 110f;

        public Vector2 Value { get; private set; }

        RectTransform background;

        void Awake() { background = (RectTransform)transform; }

        public void OnPointerDown(PointerEventData e) { OnDrag(e); }

        public void OnDrag(PointerEventData e)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(background, e.position, e.pressEventCamera, out Vector2 local))
                return;
            Vector2 clamped = Vector2.ClampMagnitude(local, radius);
            if (handle != null) handle.anchoredPosition = clamped;
            Vector2 v = clamped / radius;
            Value = v.magnitude < 0.12f ? Vector2.zero : v; // küçük ölü bölge
        }

        public void OnPointerUp(PointerEventData e)
        {
            Value = Vector2.zero;
            if (handle != null) handle.anchoredPosition = Vector2.zero;
        }

        void OnDisable() { Value = Vector2.zero; }
    }
}
