/*
 * @author Valentin Frolov
 * @author Valentin Simonov / http://va.lent.in/
 */

using TouchScript.Utils.Attributes;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace TouchScript.InputSources
{
    /// <summary>
    /// Processes Windows 7 touch events.
    /// Known issues:
    /// <list type="bullet">
    ///     <item>DOES NOT WORK IN EDITOR.</item>
    /// </list>
    /// </summary>
    [AddComponentMenu("TouchScript/Input Sources/Windows 7 Touch Input")]
    public sealed class Win7TouchInput : InputSource
    {
        #region Constants

        private const string PRESS_AND_HOLD_ATOM = "MicrosoftTabletPenServiceProperty";

        private enum TouchEvent : int
        {
            TOUCHEVENTF_MOVE = 0x0001,
            TOUCHEVENTF_DOWN = 0x0002,
            TOUCHEVENTF_UP = 0x0004,
            TOUCHEVENTF_INRANGE = 0x0008,
            TOUCHEVENTF_PRIMARY = 0x0010,
            TOUCHEVENTF_NOCOALESCE = 0x0020,
            TOUCHEVENTF_PEN = 0x0040
        }

        private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        #endregion

        #region Public properties

        /// <summary>
        /// Indicates if this input source should disable <see cref="MouseInput"/> in scene.
        /// </summary>
        /// <remarks>
        /// Operation Systems which support touch input send first touches as mouse clicks which may result in duplicated touch points in exactly the same coordinates. This affects clusters and multitouch gestures.
        /// </remarks>
        [ToggleLeft]
        public bool DisableMouseInputInBuilds = true;

        /// <summary>
        /// Tags added to touches coming from this input.
        /// </summary>
        public Tags Tags = new Tags(Tags.INPUT_TOUCH);

        #endregion

        #region Private variables

        private IntPtr hMainWindow;
        private IntPtr oldWndProcPtr;
        private IntPtr newWndProcPtr;

        private WndProcDelegate newWndProc;
        private ushort pressAndHoldAtomID;

        private Dictionary<int, int> winToInternalId = new Dictionary<int, int>();
        private bool isInitialized = false;

        #endregion

        #region Unity

        /// <inheritdoc />
        protected override void OnEnable()
        {
            if (Application.platform != RuntimePlatform.WindowsPlayer)
            {
                enabled = false;
                return;
            }

            if (DisableMouseInputInBuilds)
            {
                var inputs = FindObjectsOfType<MouseInput>();
                var count = inputs.Length;
                for (var i = 0; i < count; i++)
                {
                    inputs[i].enabled = false;
                }
            }

            base.OnEnable();
            init();
        }

        /// <inheritdoc />
        protected override void OnDisable()
        {
            if (isInitialized)
            {
                if (pressAndHoldAtomID != 0)
                {
                    RemoveProp(hMainWindow, PRESS_AND_HOLD_ATOM);
                    GlobalDeleteAtom(pressAndHoldAtomID);
                }

                SetWindowLongPtr(hMainWindow, -4, oldWndProcPtr);
                UnregisterTouchWindow(hMainWindow);

                hMainWindow = IntPtr.Zero;
                oldWndProcPtr = IntPtr.Zero;
                newWndProcPtr = IntPtr.Zero;

                newWndProc = null;
            }

            foreach (var i in winToInternalId)
            {
                cancelTouch(i.Value);
            }

            base.OnDisable();
        }

        #endregion

        #region Private functions

        private void init()
        {
            touchInputSize = Marshal.SizeOf(typeof(TOUCHINPUT));

            hMainWindow = GetForegroundWindow();
            RegisterTouchWindow(hMainWindow, 0);

            newWndProc = wndProc;
            newWndProcPtr = Marshal.GetFunctionPointerForDelegate(newWndProc);
            oldWndProcPtr = SetWindowLongPtr(hMainWindow, -4, newWndProcPtr);

            pressAndHoldAtomID = GlobalAddAtom(PRESS_AND_HOLD_ATOM);
            SetProp(hMainWindow, PRESS_AND_HOLD_ATOM, 1);

            isInitialized = true;
        }

        private IntPtr wndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            switch (msg)
            {
                case WM_TOUCH:
                    decodeTouches(wParam, lParam);
                    return IntPtr.Zero;
                default:
                    return CallWindowProc(oldWndProcPtr, hWnd, msg, wParam, lParam);
            }
        }

        private void decodeTouches(IntPtr wParam, IntPtr lParam)
        {
            int inputCount = LOWORD(wParam.ToInt32());
            TOUCHINPUT[] inputs = new TOUCHINPUT[inputCount];

            if (!GetTouchInputInfo(lParam, inputCount, inputs, touchInputSize))
            {
                return;
            }

            for (int i = 0; i < inputCount; i++)
            {
                TOUCHINPUT touch = inputs[i];

                if ((touch.dwFlags & (int)TouchEvent.TOUCHEVENTF_DOWN) != 0)
                {
                    POINT p = new POINT();
                    p.X = touch.x / 100;
                    p.Y = touch.y / 100;
                    ScreenToClient(hMainWindow, ref p);

                    winToInternalId.Add(touch.dwID, beginTouch(new Vector2(p.X, Screen.height - p.Y), new Tags(Tags)).Id);
                }
                else if ((touch.dwFlags & (int)TouchEvent.TOUCHEVENTF_UP) != 0)
                {
                    int existingId;
                    if (winToInternalId.TryGetValue(touch.dwID, out existingId))
                    {
                        winToInternalId.Remove(touch.dwID);
                        endTouch(existingId);
                    }
                }
                else if ((touch.dwFlags & (int)TouchEvent.TOUCHEVENTF_MOVE) != 0)
                {
                    int existingId;
                    if (winToInternalId.TryGetValue(touch.dwID, out existingId))
                    {
                        POINT p = new POINT();
                        p.X = touch.x / 100;
                        p.Y = touch.y / 100;
                        ScreenToClient(hMainWindow, ref p);

                        moveTouch(existingId, new Vector2(p.X, Screen.height - p.Y));
                    }
                }
            }

            CloseTouchInputHandle(lParam);
        }

        #endregion

        #region p/invoke

        // Touch event window message constants [winuser.h]
        private const int WM_TOUCH = 0x0240;

        // Touch API defined structures [winuser.h]
        [StructLayout(LayoutKind.Sequential)]
        private struct TOUCHINPUT
        {
            public int x;
            public int y;
            public IntPtr hSource;
            public int dwID;
            public int dwFlags;
            public int dwMask;
            public int dwTime;
            public IntPtr dwExtraInfo;
            public int cxContact;
            public int cyContact;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        private IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
        {
            if (IntPtr.Size == 8) return SetWindowLongPtr64(hWnd, nIndex, dwNewLong);
            return new IntPtr(SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32()));
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
        private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll")]
        private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool RegisterTouchWindow(IntPtr hWnd, uint ulFlags);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnregisterTouchWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetTouchInputInfo(IntPtr hTouchInput, int cInputs, [Out] TOUCHINPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern void CloseTouchInputHandle(IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);

        [DllImport("Kernel32.dll")]
        private static extern ushort GlobalAddAtom(string lpString);

        [DllImport("Kernel32.dll")]
        private static extern ushort GlobalDeleteAtom(ushort nAtom);

        [DllImport("user32.dll")]
        private static extern int SetProp(IntPtr hWnd, string lpString, int hData);

        [DllImport("user32.dll")]
        private static extern int RemoveProp(IntPtr hWnd, string lpString);

        private int touchInputSize;

        private int HIWORD(int value)
        {
            return (int)(value >> 0xf);
        }

        private int LOWORD(int value)
        {
            return (int)(value & 0xffff);
        }

        #endregion
    }
}
