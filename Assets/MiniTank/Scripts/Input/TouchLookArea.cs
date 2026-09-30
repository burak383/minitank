using UnityEngine;
using UnityEngine.EventSystems;

namespace MiniTank
{
    /// <summary>Ekranın boş kısmında parmak kaydırarak kamerayı döndürme alanı.</summary>
    public class TouchLookArea : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public float multiplier = 1f;
        Vector2 accumulated;
        int pointerId = int.MinValue;

        public void OnPointerDown(PointerEventData e)
        {
            if (pointerId == int.MinValue) pointerId = e.pointerId;
        }

        public void OnDrag(PointerEventData e)
        {
            if (e.pointerId != pointerId) return;
            // Farklı ekran çözünürlüklerinde aynı hissi vermek için 1080p'ye göre ölçekle
            float scale = 1080f / Mathf.Max(1, Screen.height);
            accumulated += e.delta * scale * multiplier;
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (e.pointerId == pointerId) pointerId = int.MinValue;
        }

        public Vector2 ConsumeDelta()
        {
            Vector2 d = accumulated;
            accumulated = Vector2.zero;
            return d;
        }
    }
}
