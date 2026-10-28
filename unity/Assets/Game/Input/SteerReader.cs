using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DesktopDrift.Game
{
    public sealed class SteerReader : MonoBehaviour, ISteerSource
    {
        [SerializeField] InputActionAsset actions;

        InputAction steerLeft, steerRight;
        readonly List<double> pointers = new List<double>(10);

        public InputAction Pause { get; private set; }
        public InputAction Confirm { get; private set; }
        public InputAction Back { get; private set; }

        void OnEnable()
        {
            var map = actions.FindActionMap("Race", true);
            // Two button actions (not one 1DAxis composite): the 2D resolveSteer sums "any left key" and
            // "any right key" as -1/+1, so a cross pair such as leftArrow + D cancels to 0 (input.js:7-9).
            steerLeft = map.FindAction("SteerLeft", true);
            steerRight = map.FindAction("SteerRight", true);
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
            bool keyLeft = steerLeft != null && steerLeft.IsPressed();
            bool keyRight = steerRight != null && steerRight.IsPressed();
            return SteerInput.Resolve(pointers, keyLeft, keyRight, Screen.width);
        }
    }
}
