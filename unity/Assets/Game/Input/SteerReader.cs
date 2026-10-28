using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DesktopDrift.Game
{
    public sealed class SteerReader : MonoBehaviour, ISteerSource
    {
        [SerializeField] InputActionAsset actions;

        InputAction steer;
        readonly List<double> pointers = new List<double>(10);

        public InputAction Pause { get; private set; }
        public InputAction Confirm { get; private set; }
        public InputAction Back { get; private set; }

        void OnEnable()
        {
            var map = actions.FindActionMap("Race", true);
            steer = map.FindAction("Steer", true);
            Pause = map.FindAction("Pause", true);
            Confirm = map.FindAction("Confirm", true);
            Back = map.FindAction("Back", true);
            map.Enable();
        }

        void OnDisable()
        {
            if (actions != null) actions.FindActionMap("Race")?.Disable();
        }

        // Stateless: every call re-reads the devices, so nothing needs releasing on pause
        // (the 2D game clears its pointer map on pause for the same reason).
        public int Resolve()
        {
            pointers.Clear();
            var ts = Touchscreen.current;
            if (ts != null)
            {
                var touches = ts.touches;
                for (int i = 0; i < touches.Count; i++)
                    if (touches[i].isInProgress) pointers.Add(touches[i].position.ReadValue().x);
            }
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.isPressed) pointers.Add(mouse.position.ReadValue().x);
            float v = steer != null ? steer.ReadValue<float>() : 0f;
            return SteerInput.Resolve(pointers, v < 0, v > 0, Screen.width);
        }
    }
}
