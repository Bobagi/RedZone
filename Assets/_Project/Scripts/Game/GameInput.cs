using RedZone.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RedZone.Game
{
    /// <summary>
    /// Gameplay input with the Input System: arrows, WASD, d-pad and left stick give one instruction per press and
    /// repeat while held; a swipe on the screen gives one too (touch and mouse drag). Menus use the UI module.
    /// </summary>
    public static class GameInput
    {
        const float RepeatDelay = 0.24f, RepeatRate = 0.11f, SwipePx = 45f;
        static Dir? held;
        static float heldFor, nextRepeat;
        static Vector2? swipeStart;
        static bool stickWasOut;

        /// <summary>The instruction of this frame, if any. Call once per frame.</summary>
        public static Dir? ReadMove()
        {
            Dir? pressed = null, down = null;
            var k = Keyboard.current;
            if (k != null)
            {
                if (k.upArrowKey.wasPressedThisFrame || k.wKey.wasPressedThisFrame) pressed = Dir.Up;
                else if (k.downArrowKey.wasPressedThisFrame || k.sKey.wasPressedThisFrame) pressed = Dir.Down;
                else if (k.leftArrowKey.wasPressedThisFrame || k.aKey.wasPressedThisFrame) pressed = Dir.Left;
                else if (k.rightArrowKey.wasPressedThisFrame || k.dKey.wasPressedThisFrame) pressed = Dir.Right;

                if (k.upArrowKey.isPressed || k.wKey.isPressed) down = Dir.Up;
                else if (k.downArrowKey.isPressed || k.sKey.isPressed) down = Dir.Down;
                else if (k.leftArrowKey.isPressed || k.aKey.isPressed) down = Dir.Left;
                else if (k.rightArrowKey.isPressed || k.dKey.isPressed) down = Dir.Right;
            }
            var g = Gamepad.current;
            if (g != null)
            {
                if (g.dpad.up.wasPressedThisFrame) pressed = Dir.Up;
                else if (g.dpad.down.wasPressedThisFrame) pressed = Dir.Down;
                else if (g.dpad.left.wasPressedThisFrame) pressed = Dir.Left;
                else if (g.dpad.right.wasPressedThisFrame) pressed = Dir.Right;
                if (down == null)
                {
                    if (g.dpad.up.isPressed) down = Dir.Up;
                    else if (g.dpad.down.isPressed) down = Dir.Down;
                    else if (g.dpad.left.isPressed) down = Dir.Left;
                    else if (g.dpad.right.isPressed) down = Dir.Right;
                }
                var s = g.leftStick.ReadValue();
                bool outNow = s.magnitude > 0.6f;
                if (outNow)
                {
                    var d = Mathf.Abs(s.x) > Mathf.Abs(s.y) ? (s.x > 0 ? Dir.Right : Dir.Left) : (s.y > 0 ? Dir.Up : Dir.Down);
                    if (!stickWasOut) pressed = d;
                    if (down == null) down = d;
                }
                stickWasOut = outNow;
            }

            if (pressed != null)
            {
                held = pressed; heldFor = 0f; nextRepeat = RepeatDelay;
                return pressed;
            }
            if (down != null && down == held)
            {
                heldFor += Time.unscaledDeltaTime;
                if (heldFor >= nextRepeat) { nextRepeat += RepeatRate; return down; }
                return null;
            }
            held = null;
            return Swipe();
        }

        static Dir? Swipe()
        {
            var p = Pointer.current;
            if (p == null) return null;
            if (p.press.wasPressedThisFrame) swipeStart = p.position.ReadValue();
            if (swipeStart == null) return null;
            var delta = p.position.ReadValue() - swipeStart.Value;
            if (!p.press.isPressed) { swipeStart = null; return null; }
            if (delta.magnitude < SwipePx * Mathf.Max(1f, Screen.height / 1080f)) return null;
            swipeStart = p.position.ReadValue();     // keep dragging for more steps
            return Mathf.Abs(delta.x) > Mathf.Abs(delta.y) ? (delta.x > 0 ? Dir.Right : Dir.Left) : (delta.y > 0 ? Dir.Up : Dir.Down);
        }

        public static bool Pause()
        {
            var k = Keyboard.current; var g = Gamepad.current;
            return (k != null && (k.escapeKey.wasPressedThisFrame || k.pKey.wasPressedThisFrame)) || (g != null && g.startButton.wasPressedThisFrame);
        }

        public static bool Restart()
        {
            var k = Keyboard.current; var g = Gamepad.current;
            return (k != null && k.rKey.wasPressedThisFrame) || (g != null && g.selectButton.wasPressedThisFrame);
        }

        public static bool Any()
        {
            var k = Keyboard.current; var g = Gamepad.current; var p = Pointer.current;
            return (k != null && k.anyKey.wasPressedThisFrame) || (g != null && (g.buttonSouth.wasPressedThisFrame || g.startButton.wasPressedThisFrame))
                || (p != null && p.press.wasPressedThisFrame);
        }

        public static void ResetHeld() { held = null; swipeStart = null; }
    }
}
