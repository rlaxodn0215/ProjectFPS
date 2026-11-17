// Copyright (c) Pixel Crushers. All rights reserved.

#if UNITY_2022_1_OR_NEWER

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;

namespace PixelCrushers.DialogueSystem.DialogueEditor
{

    /// <summary>
    /// Base for the UI Toolkit Conditions and Script wizards shown by LuaWizardPopup.
    /// Subclasses edit the item list of a LuaConditionWizardModel or LuaScriptWizardModel,
    /// which generates the Lua code. This class provides the row controls they share.
    /// </summary>
    public abstract class LuaWizardElement : VisualElement
    {

        /// <summary>Invoked whenever an item changes, so the popup can refresh its preview.</summary>
        public event Action changed;

        protected readonly DialogueDatabase database;
        protected readonly VisualElement itemsContainer;
        protected readonly VisualElement footer;

        public abstract int ItemCount { get; }
        public abstract string BuildLua();
        public abstract void AddNewVariables();
        protected abstract void AddItem();

        protected LuaWizardElement(DialogueDatabase database)
        {
            this.database = database;
            itemsContainer = new VisualElement();
            Add(itemsContainer);
            footer = CreateRow();
            footer.style.marginTop = 2;
            var addButton = new Button(() =>
            {
                AddItem();
                NotifyChanged();
            }) { text = "+", tooltip = "Add a new row." };
            addButton.style.width = 22;
            footer.Add(addButton);
            Add(footer);
        }

        /// <summary>Adds the Append toggle at the end of the footer, after any subclass controls.</summary>
        protected void AddAppendToggle(bool value, Action<bool> setter)
        {
            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            footer.Add(spacer);
            var toggle = new UnityEngine.UIElements.Toggle("Append") { value = value, tooltip = "Tick to add to the existing Lua code. Untick to replace it." };
            toggle.labelElement.style.minWidth = StyleKeyword.Auto;
            toggle.RegisterValueChangedCallback(evt =>
            {
                setter(evt.newValue);
                NotifyChanged();
            });
            footer.Add(toggle);
        }

        protected void NotifyChanged()
        {
            if (changed != null) changed();
        }

        #region Row Controls

        protected static VisualElement CreateRow()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.Wrap;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 2;
            return row;
        }

        private static void StyleControl(VisualElement element, float minWidth)
        {
            element.style.minWidth = minWidth;
            element.style.flexGrow = 1;
            element.style.flexShrink = 1;
            element.style.marginTop = 1;
            element.style.marginBottom = 1;
        }

        protected static Label CreateLabel(string text)
        {
            var label = new Label(text);
            label.style.unityTextAlign = TextAnchor.MiddleLeft;
            label.style.marginLeft = 3;
            label.style.marginRight = 3;
            return label;
        }

        /// <summary>
        /// Popup that selects an index into names. Out-of-range indices are clamped, and an
        /// empty list shows a disabled "(None)" popup.
        /// </summary>
        /// <param name="menuNames">Optional names shown in the dropdown menu, where "/" makes submenus.
        /// The closed field still shows names. Ignored unless it's the same length as names.</param>
        protected static VisualElement CreateIndexPopup(string[] names, int index, Action<int> setter, Action after, float minWidth = 80, string[] menuNames = null)
        {
            if (names == null || names.Length == 0)
            {
                var empty = new PopupField<string>(new List<string> { "(None)" }, 0);
                empty.SetEnabled(false);
                StyleControl(empty, minWidth);
                return empty;
            }
            var clamped = Mathf.Clamp(index, 0, names.Length - 1);
            if (clamped != index) setter(clamped);
            var indices = new List<int>();
            for (int i = 0; i < names.Length; i++) indices.Add(i);
            Func<int, string> format = i => (0 <= i && i < names.Length) ? names[i] : string.Empty;
            Func<int, string> formatListItem = format;
            if (menuNames != null && menuNames.Length == names.Length)
            {
                formatListItem = i => (0 <= i && i < menuNames.Length) ? menuNames[i] : string.Empty;
            }
            var popup = new PopupField<int>(indices, clamped, format, formatListItem);
            popup.RegisterValueChangedCallback(evt =>
            {
                setter(evt.newValue);
                after();
            });
            StyleControl(popup, minWidth);
            return popup;
        }

        protected static EnumField CreateEnumPopup<T>(T value, Action<T> setter, Action after, float minWidth = 60) where T : Enum
        {
            var field = new EnumField(value);
            field.RegisterValueChangedCallback(evt =>
            {
                setter((T)(object)evt.newValue);
                after();
            });
            StyleControl(field, minWidth);
            field.style.flexGrow = 0;
            return field;
        }

        /// <summary>
        /// Quest state popup with Lua's lowercase state names.
        /// </summary>
        protected static VisualElement CreateQuestStatePopup(QuestState value, Action<QuestState> setter, Action after)
        {
            var names = QuestStateDrawer.stateNames;
            var index = Mathf.Max(0, Array.IndexOf(names, QuestLog.StateToString(value)));
            return CreateIndexPopup(names, index, i => setter(QuestLog.StringToState(names[i])), after, 80);
        }

        protected static TextField CreateTextField(string value, Action<string> setter, Action after, float minWidth = 80)
        {
            var field = new TextField() { value = value ?? string.Empty };
            field.RegisterValueChangedCallback(evt =>
            {
                setter(evt.newValue);
                after();
            });
            StyleControl(field, minWidth);
            return field;
        }

        protected static FloatField CreateFloatField(float value, Action<float> setter, Action after)
        {
            var field = new FloatField() { value = value };
            field.RegisterValueChangedCallback(evt =>
            {
                setter(evt.newValue);
                after();
            });
            StyleControl(field, 50);
            return field;
        }

        protected static IntegerField CreateIntegerField(int value, Action<int> setter, Action after)
        {
            var field = new IntegerField() { value = value };
            field.RegisterValueChangedCallback(evt =>
            {
                setter(evt.newValue);
                after();
            });
            StyleControl(field, 40);
            field.style.flexGrow = 0;
            return field;
        }

        protected static UnityEngine.UIElements.Toggle CreateToggle(bool value, string tooltip, Action<bool> setter, Action after)
        {
            var toggle = new UnityEngine.UIElements.Toggle() { value = value, tooltip = tooltip };
            toggle.RegisterValueChangedCallback(evt =>
            {
                setter(evt.newValue);
                after();
            });
            return toggle;
        }

        /// <summary>
        /// Custom field types only provide IMGUI drawers, so host the drawer in an IMGUIContainer.
        /// </summary>
        protected VisualElement CreateCustomFieldTypeCell(CustomFieldType customFieldType, string value, Action<string> setter, Action after)
        {
            var current = value ?? string.Empty;
            var container = new IMGUIContainer(() =>
            {
                var newValue = customFieldType.Draw(current, database);
                if (newValue != current)
                {
                    current = newValue;
                    setter(newValue);
                    after();
                }
            });
            StyleControl(container, 80);
            return container;
        }

        protected static Button CreateDeleteButton(Action onDelete)
        {
            var button = new Button(onDelete) { text = "-", tooltip = "Delete this row." };
            button.style.width = 22;
            return button;
        }

        /// <summary>
        /// Display names for a variable popup. Index 0 is "(New)", which adds a variable.
        /// </summary>
        protected static string[] GetVariableDisplayNames(LuaWizardBase model)
        {
            var names = new string[model.variableNames.Length];
            for (int i = 0; i < names.Length; i++)
            {
                names[i] = (i == 0) ? "(New)" : model.variableNames[i];
            }
            return names;
        }

        /// <summary>
        /// Variable popup whose dropdown treats "." in variable names as "/" for submenus.
        /// Index 0 is "(New)", which adds a variable.
        /// </summary>
        protected static VisualElement CreateVariablePopup(LuaWizardBase model, int index, Action<int> setter, Action after)
        {
            return CreateIndexPopup(GetVariableDisplayNames(model), index, setter, after, 80, model.variablePopupNames);
        }

        /// <summary>
        /// Returns the custom field type of the asset's field, or null if it uses a built-in type.
        /// Looks up by name and title because the wizard's popups list names, not database indices.
        /// </summary>
        protected static CustomFieldType GetCustomFieldType(Asset asset, string fieldTitle)
        {
            if (asset == null || asset.fields == null || string.IsNullOrEmpty(fieldTitle)) return null;
            var field = asset.fields.Find(f => f.title == fieldTitle);
            return (field != null) ? CustomFieldTypeService.GetFieldCustomType(field.typeString) : null;
        }

        protected static string GetName(string[] names, int index)
        {
            return (names != null && 0 <= index && index < names.Length) ? names[index] : null;
        }

        protected static T FindByName<T>(List<T> assets, string name) where T : Asset
        {
            return (assets != null && name != null) ? assets.Find(asset => asset.Name == name) : null;
        }

        /// <summary>
        /// Adds an editor for each parameter of a Lua function record (Misc and Custom rows).
        /// </summary>
        /// <param name="model">Provides the actor, quest, variable, and item name lists.</param>
        /// <param name="values">The item's customParamValues, already initialized for the record.</param>
        /// <param name="questEntryNames">Entry names of the item's selected quest.</param>
        /// <param name="questChanged">Called with the new quest index when a Quest parameter changes.</param>
        /// <param name="createNewVariableEditors">Adds the name and type editors for a "(New)" variable parameter.</param>
        /// <param name="after">Called after a value changes.</param>
        /// <param name="rebuild">Called after a change that alters which editors appear.</param>
        protected static void AddLuaFunctionParameters(VisualElement row, LuaWizardBase model, CustomLuaFunctionInfoRecord record,
            object[] values, string[] questEntryNames, Action<int> questChanged, Action<VisualElement> createNewVariableEditors,
            Action after, Action rebuild)
        {
            for (int i = 0; i < record.parameters.Length; i++)
            {
                var p = i;
                switch (record.parameters[p])
                {
                    case CustomLuaParameterType.Bool:
                        row.Add(CreateEnumPopup(values[p] is BooleanType ? (BooleanType)values[p] : BooleanType.True, v => values[p] = v, after));
                        break;
                    case CustomLuaParameterType.Double:
                        row.Add(CreateFloatField(values[p] is float ? (float)values[p] : 0, v => values[p] = v, after));
                        break;
                    case CustomLuaParameterType.String:
                        row.Add(CreateTextField(values[p] as string, v => values[p] = v, after));
                        break;
                    case CustomLuaParameterType.Actor:
                        row.Add(CreateIndexPopup(model.actorNames, values[p] is int ? (int)values[p] : 0, v => values[p] = v, after));
                        break;
                    case CustomLuaParameterType.Quest:
                        row.Add(CreateIndexPopup(model.questNames, values[p] is int ? (int)values[p] : 0, v =>
                        {
                            values[p] = v;
                            questChanged(v);
                        }, rebuild));
                        break;
                    case CustomLuaParameterType.QuestEntry:
                        row.Add(CreateIndexPopup(questEntryNames, values[p] is int ? (int)values[p] : 0, v => values[p] = v, after));
                        break;
                    case CustomLuaParameterType.Variable:
                    case CustomLuaParameterType.VariableName:
                        var variableIndex = values[p] is int ? (int)values[p] : 0;
                        row.Add(CreateVariablePopup(model, variableIndex, v => values[p] = v, rebuild));
                        if (variableIndex == 0) createNewVariableEditors(row);
                        break;
                    case CustomLuaParameterType.Item:
                        row.Add(CreateIndexPopup(model.itemNames, values[p] is int ? (int)values[p] : 0, v => values[p] = v, after));
                        break;
                    case CustomLuaParameterType.QuestState:
                        if (!(values[p] is QuestState)) values[p] = QuestState.Unassigned;
                        row.Add(CreateQuestStatePopup((QuestState)values[p], v => values[p] = v, after));
                        break;
                }
            }
        }

        #endregion

    }

}

#endif
