using UnityEngine;
using UnityEngine.EventSystems;

namespace MiniTank
{
    /// <summary>Ateş butonu. Basılı tutulduğu sürece, dolum bittikçe ateş eder.</summary>
    public class FireButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public bool IsPressed { get; private set; }
        public void OnPointerDown(PointerEventData e) { IsPressed = true; }
        public void OnPointerUp(PointerEventData e) { IsPressed = false; }
        void OnDisable() { IsPressed = false; }
    }
}
