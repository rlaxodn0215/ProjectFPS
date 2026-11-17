// Copyright (c) Pixel Crushers. All rights reserved.

#if UNITY_2022_1_OR_NEWER

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;

namespace PixelCrushers.DialogueSystem.DialogueEditor
{

    /// <summary>
    /// This part of the UI Toolkit dialogue database editor window handles the
    /// category toolbar at the top of the window.
    /// </summary>
    public partial class DialogueDatabaseEditorWindow
    {

        public enum Category { Database, Actors, ItemsQuests, Locations, Variables, Conversations, Template }

        private static readonly string[] CategoryLabels = { "Database", "Actors", "Items/Quests", "Locations", "Variables", "Conversations", "Template" };
        private const string TemplateLabel = "Template";
        private const string WatchesLabel = "Watches";

        [SerializeField]
        private Category currentCategory = Category.Database;
        public Category CurrentCategory
        {
            get { return currentCategory; }
            set { SelectCategory(value); }
        }

        // Per-project user setting that remembers the category after the window is closed.
        private const string CategoryConfigKey = "PixelCrushers.DialogueSystem.DialogueDatabaseEditorWindow.Category";

        private Dictionary<Category, ToolbarToggle> categoryToggles = new Dictionary<Category, ToolbarToggle>();
        private ObjectField databaseField = null;

        private VisualElement CreateToolbar()
        {
            var toolbar = new UnityEditor.UIElements.Toolbar();
            LoadSavedCategory();
            categoryToggles.Clear();
            for (int i = 0; i < CategoryLabels.Length; i++)
            {
                var category = (Category)i;
                var toggle = new ToolbarToggle() { text = CategoryLabels[i] };
                toggle.SetValueWithoutNotify(category == currentCategory);
                toggle.RegisterValueChangedCallback(evt => OnCategoryToggleChanged(category, evt.newValue));
                categoryToggles.Add(category, toggle);
                toolbar.Add(toggle);
            }

            var spacer = new ToolbarSpacer();
            spacer.style.flexGrow = 1;
            toolbar.Add(spacer);

            databaseField = new ObjectField()
            {
                objectType = typeof(DialogueDatabase),
                allowSceneObjects = false,
                tooltip = "Dialogue database to edit."
            };
            databaseField.style.minWidth = 180;
            databaseField.RegisterValueChangedCallback(evt => SetDatabase(evt.newValue as DialogueDatabase));
            toolbar.Add(databaseField);

            return toolbar;
        }

        private void OnCategoryToggleChanged(Category category, bool isOn)
        {
            if (isOn)
            {
                SelectCategory(category);
            }
            else if (category == currentCategory)
            {
                // Clicking the active category keeps it selected, like GUILayout.Toolbar.
                categoryToggles[category].SetValueWithoutNotify(true);
            }
        }

        private void SelectCategory(Category category)
        {
            currentCategory = category;
            SaveCategorySelection(category);
            foreach (var kvp in categoryToggles)
            {
                kvp.Value.SetValueWithoutNotify(kvp.Key == category);
            }
            RefreshVisiblePanel();
        }

        private void LoadSavedCategory()
        {
            if (System.Enum.TryParse(EditorUserSettings.GetConfigValue(CategoryConfigKey), out Category savedCategory))
            {
                currentCategory = savedCategory;
            }
        }

        private static void SaveCategorySelection(Category category)
        {
            var value = category.ToString();
            if (EditorUserSettings.GetConfigValue(CategoryConfigKey) != value)
            {
                EditorUserSettings.SetConfigValue(CategoryConfigKey, value);
            }
        }

        private void RefreshPlayModeState()
        {
            if (categoryToggles.TryGetValue(Category.Template, out var templateToggle))
            {
                templateToggle.text = EditorApplication.isPlaying ? WatchesLabel : TemplateLabel;
            }
            RefreshVisiblePanel();
        }

    }

}

#endif
