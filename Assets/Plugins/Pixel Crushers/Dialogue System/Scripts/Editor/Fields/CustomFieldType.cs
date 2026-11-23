using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;

namespace PixelCrushers.DialogueSystem
{

    /// <summary>
    /// The abstract base custom field type.
    /// </summary>
    public abstract class CustomFieldType
    {

        public virtual FieldType storeFieldAsType
        {
            get
            {
                return FieldType.Text;
            }
        }

        public abstract string Draw(string currentValue, DialogueDatabase database);

        public virtual string Draw(GUIContent label, string currentValue, DialogueDatabase database)
        {
            return Draw(currentValue, database);
        }

        public virtual string Draw(Rect rect, string currentValue, DialogueDatabase database)
        {
            Debug.LogWarning("Dialogue System: API change notice: Since version 1.6.8, CustomFieldType has added an overload to Draw() that uses a Rect. Please add an implementation of this overload to " + GetType().Name + ".");
            return EditorGUI.TextField(rect, currentValue);
        }

        /// <summary>
        /// Opens the legacy Dialogue Editor window if it isn't open. Kept for custom field types
        /// that use DialogueEditorWindow.instance. Built-in types don't call this; they use
        /// DrawAssetPopup, which doesn't depend on any editor window.
        /// </summary>
        protected void RequireDialogueEditorWindow(DialogueDatabase database)
        {
            if (DialogueEditor.DialogueEditorWindow.instance == null)
            {
                var window = DialogueEditor.DialogueEditorWindow.OpenDialogueEditorWindow();
                if (window != null) window.SelectObject(database);
            }
        }

        private const string NoIDString = "-1";

        /// <summary>
        /// Draws a popup of assets (actors, items, or locations) and returns the selected asset's
        /// ID as a string, or -1 for (None). Doesn't depend on any editor window.
        /// </summary>
        protected static string DrawAssetPopup<T>(string value, List<T> assets) where T : Asset
        {
            if (assets == null)
            {
                EditorGUILayout.LabelField("(no database)");
                return value;
            }
            string[] ids;
            GUIContent[] names;
            int index = MakeAssetPopupList(value, assets, out ids, out names);
            int newIndex = EditorGUILayout.Popup(index, names);
            return (newIndex != index) ? GetAssetPopupID(ids, newIndex) : value;
        }

        /// <summary>
        /// Draws a popup of assets (actors, items, or locations) in a rect and returns the
        /// selected asset's ID as a string, or -1 for (None). Doesn't depend on any editor window.
        /// </summary>
        protected static string DrawAssetPopup<T>(Rect rect, string value, List<T> assets) where T : Asset
        {
            if (assets == null)
            {
                EditorGUI.LabelField(rect, "(no database)");
                return value;
            }
            string[] ids;
            GUIContent[] names;
            int index = MakeAssetPopupList(value, assets, out ids, out names);
            int newIndex = EditorGUI.Popup(rect, index, names);
            return (newIndex != index) ? GetAssetPopupID(ids, newIndex) : value;
        }

        /// <summary>
        /// Builds the popup's IDs and names, with (None) first, and returns the index of value's ID,
        /// or -1 if no asset has that ID.
        /// </summary>
        private static int MakeAssetPopupList<T>(string value, List<T> assets, out string[] ids, out GUIContent[] names) where T : Asset
        {
            ids = new string[assets.Count + 1];
            names = new GUIContent[assets.Count + 1];
            ids[0] = NoIDString;
            names[0] = new GUIContent("(None)");
            int id;
            if (!int.TryParse(value, out id)) id = -1;
            int index = (id == -1) ? 0 : -1;
            for (int i = 0; i < assets.Count; i++)
            {
                var asset = assets[i];
                ids[i + 1] = asset.id.ToString();
                names[i + 1] = new GUIContent(string.Format("{0} [{1}]", asset.Name, asset.id));
                if (index == -1 && asset.id == id) index = i + 1;
            }
            return index;
        }

        private static string GetAssetPopupID(string[] ids, int index)
        {
            return (0 <= index && index < ids.Length) ? ids[index] : NoIDString;
        }
    }

}
