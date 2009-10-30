﻿using System;
using System.Runtime.InteropServices;
using System.Windows;

namespace WpfHint
{
// ReSharper disable InconsistentNaming
    public static class Win32
    {
        public delegate int Callback(IntPtr wHwnd, int msg, int wParam, int lParam);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public POINT(double x, double y)
            {
                X = (int) x;
                Y = (int) y;
            }
            public int X;
            public int Y;
        }

        public const int GA_PARENT = 1;
        public const int GA_ROOT = 2;
        public const int GA_ROOTOWNER = 3;

        public const int WM_ACTIVATE = 0x0006;
        public const int WM_ACTIVATEAPP = 0x001C;

        public const int WM_KEYDOWN = 0x0100;
        public const int WM_MOUSEWHEEL = 0x020A;
        public const int WM_RBUTTONDOWN = 0x204;
        public const int WM_LBUTTONDOWN = 0x201;
        public const int WM_LBUTTONUP = 0x202;
        public const int WM_MOUSEMOVE = 0x0200;
        public const int WM_MOUSELEAVE = 0x02A3;
        public const int WM_MOUSEHOVER = 0x02A1;
        public const int GWL_WNDPROC = -4;

        [DllImport("user32.dll")]
        public static extern bool ClientToScreen(IntPtr hWnd, POINT[] lpPoint);
        [DllImport("user32.dll")]
        public static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

        [DllImport("user32.dll", CallingConvention = CallingConvention.StdCall, EntryPoint = "SetWindowLong", CharSet = CharSet.Auto)]
        public static extern Callback SetWindowLong(IntPtr hwnd, int nIndex, Callback dwNewLong);

        [DllImport("user32.dll", CallingConvention = CallingConvention.StdCall, EntryPoint = "CallWindowProc", CharSet = CharSet.Auto)]
        public static extern int CallWindowProc(Callback lpPrevWndFunc, IntPtr hwnd, int msg, int wParam, int lParam);

        [DllImport("user32.dll")]
        public static extern IntPtr WindowFromPoint(POINT Point);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll", ExactSpelling = true)]
        public static extern IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);

        public static Point GetCursorPos()
        {
            POINT pt;
            GetCursorPos(out pt);
            return new Point(pt.X, pt.Y);
        }
    }
}