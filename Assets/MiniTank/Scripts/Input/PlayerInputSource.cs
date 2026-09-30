using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace MiniTank
{
    /// <summary>
    /// Oyuncu girdisi: telefonda joystick + kaydırma + ateş butonu,
    /// editörde test için WASD + sağ tık basılı fare ile bakış + sol tık/Space ile ateş.
    /// </summary>
    public class PlayerInputSource : MonoBehaviour, ITankInputSource
    {
        public VirtualJoystick moveJoystick;
        public TouchLookArea lookArea;
        public FireButton fireButton;
        public ThirdPersonCamera cameraRig;
        public float mouseSensitivity = 1f;

        TankController tank;
        bool abilityQueued;

        public void Bind(TankController target) { tank = target; }

        /// <summary>Şu an nişan alınan dünya noktası (girdiyi tüketmeden).</summary>
        public Vector3 CurrentAimPoint()
        {
            if (cameraRig != null) return cameraRig.GetAimPoint(tank);
            return tank != null ? tank.transform.position + tank.transform.forward * 50f : Vector3.zero;
        }

        void Update()
        {
            if (cameraRig == null) return;
            Vector2 look = lookArea != null ? lookArea.ConsumeDelta() : Vector2.zero;

#if UNITY_EDITOR || UNITY_STANDALONE
#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            if (mouse != null && mouse.rightButton.isPressed)
                look += mouse.delta.ReadValue() * mouseSensitivity;
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetMouseButton(1))
                look += new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * 10f * mouseSensitivity;
#endif
#endif
            cameraRig.AddLook(look * GameSettings.Sensitivity);

            // Yetenek tuşu Update'te yakalanır, bir sonraki fizik adımında kullanılır
            if (AbilityButton.Instance != null && AbilityButton.Instance.Consume()) abilityQueued = true;
#if (UNITY_EDITOR || UNITY_STANDALONE) && ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.eKey.wasPressedThisFrame || keyboard.qKey.wasPressedThisFrame)) abilityQueued = true;
#elif (UNITY_EDITOR || UNITY_STANDALONE) && ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Q)) abilityQueued = true;
#endif
        }

        public TankInputData GetInput()
        {
            Vector2 move = moveJoystick != null ? moveJoystick.Value : Vector2.zero;
            bool fire = fireButton != null && fireButton.IsPressed;

#if UNITY_EDITOR || UNITY_STANDALONE
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) move.y += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) move.y -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) move.x += 1f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) move.x -= 1f;
                fire |= kb.spaceKey.isPressed;
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            move += new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            fire |= Input.GetKey(KeyCode.Space);
#endif
#endif

            var data = new TankInputData
            {
                Move = Vector2.ClampMagnitude(move, 1f),
                Fire = fire,
                Ability = abilityQueued,
                AimPoint = cameraRig != null
                    ? cameraRig.GetAimPoint(tank)
                    : (tank != null ? tank.transform.position + tank.transform.forward * 50f : Vector3.zero)
            };
            abilityQueued = false;
            return data;
        }
    }
}
