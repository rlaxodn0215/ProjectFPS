#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
namespace GSPAWN
{
    internal static class IMGUIViewUtility
    {
        public static Color selectedColor { get { return new Color(0.235f, 0.470f, 0.843f, 0.35f); } }
        public static Color unfocusedSelectedColor { get { return EditorGUIUtility.isProSkin ? new Color32(77,77,77,255) : new Color32(174,174,174,255); } }
        public static Color borderColor { get { return EditorGUIUtility.isProSkin ? new Color32(31,31,31,255) : new Color32(153,153,153,255); } }
        public static void DrawBorder(Rect r, Color color)
        {
            EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, 1f), color);
            EditorGUI.DrawRect(new Rect(r.x, r.yMax-1f, r.width, 1f), color);
            EditorGUI.DrawRect(new Rect(r.x, r.y, 1f, r.height), color);
            EditorGUI.DrawRect(new Rect(r.xMax-1f, r.y, 1f, r.height), color);
        }
        public static Vector2 ScrollRectIntoView(Rect item, Vector2 scroll, float viewportWidth, float viewportHeight)
        {
            if (item.yMin < scroll.y) scroll.y = item.yMin;
            else if (item.yMax > scroll.y + viewportHeight) scroll.y = item.yMax - viewportHeight;
            scroll.y = Mathf.Max(0, scroll.y);
            return scroll;
        }
        public static bool HandleCopyPasteShortcut(Event e, Action copy, Action cut, Action paste)
        {
            if (e == null || e.type != EventType.KeyDown || !(e.control || e.command) || e.shift || e.alt) return false;
            Action action = null;
            if (e.keyCode == KeyCode.C) action = copy;
            else if (e.keyCode == KeyCode.X) action = cut;
            else if (e.keyCode == KeyCode.V) action = paste;
            if (action == null) return false;
            action();
            e.Use();
            return true;
        }
    }
}
#endif
