using UnityEngine;
using UnityEngine.EventSystems;

namespace MiniTank
{
    /// <summary>Yetenek butonu. Dokununca bir kez tetiklenir.</summary>
    public class AbilityButton : MonoBehaviour, IPointerDownHandler
    {
        public static AbilityButton Instance { get; private set; }
        bool pressed;

        void OnEnable() { Instance = this; }
        void OnDisable() { if (Instance == this) Instance = null; pressed = false; }

        public void OnPointerDown(PointerEventData e) { pressed = true; }

        /// <summary>Basıldıysa true döner ve durumu sıfırlar.</summary>
        public bool Consume()
        {
            bool p = pressed;
            pressed = false;
            return p;
        }
    }
}
