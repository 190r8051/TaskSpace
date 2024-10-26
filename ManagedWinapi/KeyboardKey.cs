using System;
using System.Text;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace ManagedWinapi {
    /// <summary>
    /// This class contains utility methods related to keys on the keyboard.
    /// </summary>
    public class KeyboardKey {
        readonly Keys _key;
        readonly bool _isExtended;

        /// <summary>
        /// Initializes a new instance of this class for a given key.
        /// </summary>
        /// <param name="key">The key.</param>
        public KeyboardKey(
            Keys key
        ) {
            this._key = key;
            switch(key) {
                case Keys.Insert:
                case Keys.Delete:
                case Keys.PageUp:
                case Keys.PageDown:
                case Keys.Home:
                case Keys.End:
                case Keys.Up:
                case Keys.Down:
                case Keys.Left:
                case Keys.Right:
                    this._isExtended = true;
                    break;
                default:
                    this._isExtended = false;
                    break;
            }
        }

        /// <summary>
        /// The state of this key, as seen by this application.
        /// </summary>
        public short State { get { return GetKeyState((short)_key); } }

        /// <summary>
        /// The global state of this key.
        /// </summary>
        public short AsyncState { get { return GetAsyncKeyState((short)_key); } }

        /// <summary>
        /// Presses this key and releases it.
        /// </summary>
        public void PressAndRelease() {
            Press();
            Release();
        }

        /// <summary>
        /// Presses this key.
        /// </summary>
        public void Press() {
            keybd_event((byte)_key, (byte)MapVirtualKey((int)_key, 0), _isExtended ? (uint)0x1 : 0x0, UIntPtr.Zero);
        }

        /// <summary>
        /// Releases this key.
        /// </summary>
        public void Release() {
            keybd_event((byte)_key, (byte)MapVirtualKey((int)_key, 0), _isExtended ? (uint)0x3 : 0x2, UIntPtr.Zero);
        }

        /// <summary>
        /// Determines the name of a key in the current keyboard layout.
        /// </summary>
        /// <returns>The key's name.</returns>
        public string KeyName {
            get {
                StringBuilder stringBuilder = new StringBuilder(512);
                int scanCode = MapVirtualKey((int)_key, 0);
                if(_isExtended) {
                    scanCode += 0x100;
                }

                GetKeyNameText(scanCode << 16, stringBuilder, stringBuilder.Capacity);
                if(stringBuilder.Length != 0) {
                    return stringBuilder.ToString();
                }

                switch(_key) {
                    case Keys.BrowserBack:
                        stringBuilder.Append("Back");
                        break;
                    case Keys.BrowserForward:
                        stringBuilder.Append("Forward");
                        break;
                    case (Keys)19:
                        stringBuilder.Append("Break");
                        break;
                    case Keys.Apps:
                        stringBuilder.Append("ContextMenu");
                        break;
                    case Keys.LWin:
                    case Keys.RWin:
                        stringBuilder.Append("Windows");
                        break;
                    case Keys.PrintScreen:
                        stringBuilder.Append("PrintScreen");
                        break;
                }
                return stringBuilder.ToString();
            }
        }

        /// <summary>
        /// Injects a keyboard event into the event loop, as if the user performed
        /// it with his keyboard.
        /// </summary>
        public static void InjectKeyboardEvent(Keys key, byte scanCode, uint flags, UIntPtr extraInfo) {
            keybd_event((byte)key, scanCode, flags, extraInfo);
        }

        /// <summary>
        /// Injects a mouse event into the event loop, as if the user performed
        /// it with his mouse.
        /// </summary>
        public static void InjectMouseEvent(uint flags, uint dx, uint dy, uint data, UIntPtr extraInfo) {
            mouse_event(flags, dx, dy, data, extraInfo);
        }

        #region PInvoke Declarations

        [DllImport("user32.dll")]
        private static extern short GetKeyState(short nVirtKey);

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags,
           UIntPtr dwExtraInfo);

        [DllImport("user32.dll")]
        static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData,
           UIntPtr dwExtraInfo);

        [DllImport("user32.dll")]
        static extern int GetKeyNameText(int lParam, [Out] StringBuilder lpString,
           int nSize);

        [DllImport("user32.dll")]
        static extern int MapVirtualKey(int uCode, int uMapType);

        [DllImport("user32.dll")]
        static extern short GetAsyncKeyState(int vKey);
        #endregion
    }
}
