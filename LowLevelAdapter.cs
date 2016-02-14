﻿using System;
using System.Collections.Generic;
using System.Net.Configuration;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.ComponentModel;

namespace dotSwitcher
{
    public static partial class LowLevelAdapter
    {

        public static IntPtr SetHook(int type, HookProc callback)
        {
            var process = Process.GetCurrentProcess();
            var module = process.MainModule;
            var handle = GetModuleHandle(module.ModuleName);
            return SetWindowsHookEx(type, callback, handle, 0);
        }
        public static void ReleaseHook(IntPtr id)
        {
            UnhookWindowsHookEx(id);
        }
        public static IntPtr NextHook(int nCode, IntPtr wParam, IntPtr lParam)
        {
            return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
        }

        public static bool KeyPressed(Keys keyCode)
        {
            return (GetKeyState((int)keyCode) & 0x8000) == 0x8000;
        }


        //[DllImport("USER32.DLL")]
        //public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        //private static string GetClassName(IntPtr handle)
        //{
        //    StringBuilder className = new StringBuilder(100);
        //    GetClassName(handle, className, className.Capacity);
        //    return className.ToString();
        //}


        private static IntPtr GetFocusedHandle()
        {
            var threadId = GetCurrentThreadId();
            var wndThreadId = GetWindowThreadProcessId(GetForegroundWindow(), IntPtr.Zero);

            if (threadId == wndThreadId)
            {
                return IntPtr.Zero;
            }

            AttachThreadInput(wndThreadId, threadId, true);
            IntPtr focusedHandle = GetFocus();
            AttachThreadInput(wndThreadId, threadId, false);
            return focusedHandle;
        }


        public static void SetNextKeyboardLayout()
        {
            var hWnd = WindowPtr();

            PostMessage(hWnd, WM_INPUTLANGCHANGEREQUEST, INPUTLANGCHANGE_FORWARD, HKL_NEXT);
        }

        private static IntPtr WindowPtr()
        {
            IntPtr hWnd = IntPtr.Zero;
            var threadId = GetWindowThreadProcessId(GetForegroundWindow(), IntPtr.Zero);
            var info = new GUITHREADINFO();
            info.cbSize = Marshal.SizeOf(info);
            var success = GetGUIThreadInfo(threadId, ref info);

            // target = hwndCaret || hwndFocus || (AttachThreadInput + GetFocus) || hwndActive || GetForegroundWindow
            var focusedHandle = GetFocusedHandle();
            if (success)
            {
                if (info.hwndCaret != IntPtr.Zero)
                {
                    hWnd = info.hwndCaret;
                }
                else if (info.hwndFocus != IntPtr.Zero)
                {
                    hWnd = info.hwndFocus;
                }
                else if (focusedHandle != IntPtr.Zero)
                {
                    hWnd = focusedHandle;
                }
                else if (info.hwndActive != IntPtr.Zero)
                {
                    hWnd = info.hwndActive;
                }
            }
            else
            {
                hWnd = focusedHandle;
            }
            if (hWnd == IntPtr.Zero)
            {
                hWnd = GetForegroundWindow();
            }
            return hWnd;
        }

        public static Locale[] GetkeyboardLayouts()
        {
            uint nElements = GetKeyboardLayoutList(0, null);
            IntPtr[] list = new IntPtr[nElements];
            List<Locale> locales = new List<Locale>();
            GetKeyboardLayoutList(3, list);

            foreach (IntPtr ptr in list)
            {
                Locale locale = new Locale { LocaleId = (uint)Math.Abs(ptr.ToInt32()*0xFFFF)};
                locale.Lang = ExecuteGetlocaleInfo(locale.LocaleId, LOCALE_SNATIVELANGNAME).ToLower();
                locales.Add(locale);
            }
            return locales.ToArray();
        }



        public static bool SetLayout(uint locale)
        {
            var hWnd = WindowPtr();
//            return PostMessage(hWnd, WM_INPUTLANGCHANGEREQUEST, INPUTLANGCHANGE_SYSCHARSET, locale * 0xFFFF);
            return PostMessage(hWnd, WM_INPUTLANGCHANGEREQUEST, INPUTLANGCHANGE_SYSCHARSET, locale );
        }

        private static string ExecuteGetlocaleInfo(uint localeId, int localeInfo)
        {
            StringBuilder locale = new StringBuilder();
            int error = GetLocaleInfo(localeId, localeInfo, locale, locale.Capacity);
            return locale.ToString();
        }

        public static void SendKeyPress(Keys vkCode, bool shift = false)
        {
            var down = MakeKeyInput(vkCode, true);
            var up = MakeKeyInput(vkCode, false);

            if (shift)
            {
                var shiftDown = MakeKeyInput(Keys.ShiftKey, true);
                var shiftUp = MakeKeyInput(Keys.ShiftKey, false);
                SendInput(4, new INPUT[4] { shiftDown, down, up, shiftUp }, Marshal.SizeOf(typeof(INPUT)));
            }
            else
            {
                SendInput(2, new INPUT[2] { down, up }, Marshal.SizeOf(typeof(INPUT)));
            }

        }

        public static void ReleasePressedFnKeys()
        {
            // temp solution
            //ReleasePressedKey(Keys.LMenu, true),
            //ReleasePressedKey(Keys.RMenu, true),
            //ReleasePressedKey(Keys.LWin, true),
            //ReleasePressedKey(Keys.RWin, true),
            ReleasePressedKey(Keys.RControlKey, false);
            ReleasePressedKey(Keys.LControlKey, false);
            ReleasePressedKey(Keys.LShiftKey, false);
            ReleasePressedKey(Keys.RShiftKey, false);
        }

        public static bool ReleasePressedKey(Keys keyCode, bool releaseTwice)
        {
            if (!KeyPressed(keyCode)) { return false; }
            //Debug.WriteLine("{0} was down", keyCode);
            var keyUp = MakeKeyInput(keyCode, false);
            if (releaseTwice)
            {
                var secondDown = MakeKeyInput(keyCode, true);
                var secondUp = MakeKeyInput(keyCode, false);
                SendInput(3, new INPUT[3] { keyUp, secondDown, secondUp }, Marshal.SizeOf(typeof(INPUT)));
            }
            else
            {
                SendInput(1, new INPUT[1] { keyUp }, Marshal.SizeOf(typeof(INPUT)));
            }
            return true;
        }
    }

    public struct Locale
    {
        public string Lang { get; set; }
        public uint LocaleId { get; set; }
    }
}