// Copyright (c) Pixel Crushers. All rights reserved.

using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.Text;

namespace PixelCrushers.DialogueSystem
{

    /// <summary>
    /// This Lua condition wizard is meant to be called from a custom editor's
    /// OnInspectorGUI() method. It includes an EditorGUILayout version (Draw(...))
    /// and an EditorGUI version (Draw(rect,...)).
    /// </summary>
    public class LuaConditionWizard : LuaConditionWizardModel
    {

        public bool IsOpen { get { return isOpen; } }

        private bool isOpen = false;

        public LuaConditionWizard(DialogueDatabase database) : base(database)
        {
        }

        public float GetHeight(string luaCode, float width, bool flexibleHeight)
        {
            if (database == null) return 0;
            if (!isOpen) return EditorGUIUtility.singleLineHeight;
            var textAreaHeight = flexibleHeight
                ? GUI.skin.textArea.CalcHeight(new GUIContent(luaCode), width)
                : EditorGUIUtility.singleLineHeight;
            var height = (EditorGUIUtility.singleLineHeight + 2) + // label
                (conditionItems.Count + 1) * (EditorGUIUtility.singleLineHeight + 2f) + // wiz
                2 + // separator
                textAreaHeight + 2; // text area
            return height;
        }

        public string Draw(GUIContent guiContent, string luaCode, bool showOpenCloseButton = true)
        {
            if (database == null) isOpen = false;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(guiContent);
            if (showOpenCloseButton)
            {
                EditorGUI.BeginDisabledGroup(database == null);
                if (GUILayout.Button(new GUIContent("...", "Open Lua wizard."), EditorStyles.miniButton, GUILayout.Width(22)))
                {
                    OpenWizard(luaCode);
                }
                EditorGUI.EndDisabledGroup();
            }
            EditorGUILayout.EndHorizontal();

            if (isOpen)
            {
                luaCode = DrawConditionsWizard(luaCode);
            }

            luaCode = EditorGUILayout.TextArea(luaCode);

            return luaCode;
        }

        public void OpenWizard(string luaCode)
        {
            if (isOpen) return;
            ToggleConditionsWizard();
            if (isOpen) savedLuaCode = luaCode;
            append = true;
        }

        public void ResetWizard()
        {
            isOpen = false;
            savedLuaCode = string.Empty;
        }

        private void ToggleConditionsWizard()
        {
            isOpen = !isOpen && (database != null);
            conditionItems.Clear();
            RefreshWizardResources();
        }

        private string DrawConditionsWizard(string luaCode)
        {
            EditorGUILayout.BeginVertical("button");

            EditorGUI.BeginChangeCheck();

            // Condition items:
            ConditionItem itemToDelete = null;
            foreach (ConditionItem item in conditionItems)
            {
                DrawConditionItem(item, ref itemToDelete);
            }
            if (itemToDelete != null) conditionItems.Remove(itemToDelete);

            // Logical operator (Any/All) and add new condition button:
            // Revert and Apply buttons:
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(new GUIContent("+", "Add a new condition."), EditorStyles.miniButton, GUILayout.Width(22)))
            {
                conditionItems.Add(new ConditionItem());
            }
            conditionsLogicalOperator = (LogicalOperatorType)EditorGUILayout.EnumPopup(conditionsLogicalOperator, EditorTools.GUILayoutPopupWidth(conditionsLogicalOperator));
            EditorGUILayout.LabelField("must be true.", EditorTools.GUILayoutLabelWidth("must be true."));

            GUILayout.FlexibleSpace();
            append = EditorGUILayout.ToggleLeft("Append", append, EditorTools.GUILayoutToggleWidth("Append"));

            if (EditorGUI.EndChangeCheck())
            {
                BuildLua();
            }

            if (GUILayout.Button(new GUIContent("Revert", "Cancel these settings."), EditorStyles.miniButton, EditorTools.GUILayoutButtonWidth("Revert")))
            {
                luaCode = CancelConditionsWizard();
            }
            if (GUILayout.Button(new GUIContent("Apply", "Apply these settings"), EditorStyles.miniButton, EditorTools.GUILayoutButtonWidth("Apply")))
            {
                luaCode = AcceptConditionsWizard();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();

            return luaCode;
        }

        private void DrawConditionItem(ConditionItem item, ref ConditionItem itemToDelete)
        {
            EditorGUILayout.BeginHorizontal();

            ConditionWizardResourceType newConditionType = (ConditionWizardResourceType)EditorGUILayout.EnumPopup(item.conditionType, EditorTools.GUILayoutPopupWidth(item.conditionType));
            if (newConditionType != item.conditionType)
            {
                item.conditionType = newConditionType;
                item.conditionsQuestEntryNames = new string[0];

                s_lastWizardResourceType = newConditionType;
            }

            if (item.conditionType == ConditionWizardResourceType.Quest)
            {
                // Quest:
                item.questNamesIndex = EditorGUILayout.Popup(item.questNamesIndex, questNames);
                item.equalityType = (EqualityType)EditorGUILayout.EnumPopup(item.equalityType, EditorTools.GUILayoutPopupWidth(item.equalityType));
                item.questState = QuestStateDrawer.LayoutQuestStatePopup(item.questState, 96);

                s_lastQuestNamesIndex = item.questNamesIndex;
            }
            else if (item.conditionType == ConditionWizardResourceType.QuestEntry)
            {
                // Quest Entry:
                int newQuestNamesIndex = EditorGUILayout.Popup(item.questNamesIndex, complexQuestNames);
                if (newQuestNamesIndex != item.questNamesIndex)
                {
                    item.questNamesIndex = newQuestNamesIndex;
                    item.conditionsQuestEntryNames = new string[0];
                }
                if ((item.conditionsQuestEntryNames.Length == 0) && (item.questNamesIndex < complexQuestNames.Length))
                {
                    item.conditionsQuestEntryNames = GetQuestEntryNames(complexQuestNames[item.questNamesIndex]);
                }
                item.questEntryIndex = EditorGUILayout.Popup(item.questEntryIndex, item.conditionsQuestEntryNames);
                item.equalityType = (EqualityType)EditorGUILayout.EnumPopup(item.equalityType, EditorTools.GUILayoutPopupWidth(item.equalityType));
                item.questState = QuestStateDrawer.LayoutQuestStatePopup(item.questState, 96);

                s_lastQuestNamesIndex = item.questNamesIndex;
                s_lastQuestEntryIndex = item.questEntryIndex;

            }
            else if (item.conditionType == ConditionWizardResourceType.Variable)
            {
                // Variable:
                item.variableNamesIndex = EditorGUILayout.Popup(item.variableNamesIndex, variablePopupNames);
                FieldType variableType;
                if (item.variableNamesIndex == 0)
                {
                    // New variable:
                    item.newVariableName = EditorGUILayout.TextField(item.newVariableName);
                    item.newVariableType = (FieldType)EditorGUILayout.EnumPopup(item.newVariableType);
                    variableType = item.newVariableType;
                }
                else
                {
                    // Existing variable:
                    variableType = GetWizardVariableType(item.variableNamesIndex);
                }
                DrawRightHand(item, variableType);

                s_lastVariableNameIndex = item.variableNamesIndex;
            }
            else if (item.conditionType == ConditionWizardResourceType.Actor)
            {
                // Actor:
                item.actorNamesIndex = EditorGUILayout.Popup(item.actorNamesIndex, actorNames);
                item.actorFieldIndex = EditorGUILayout.Popup(item.actorFieldIndex, actorFieldNames);

                CustomFieldType customFieldType = GetCustomFieldType(database.actors, item.actorNamesIndex, item.actorFieldIndex);
                DrawRightHand(item, GetWizardActorFieldType(item.actorFieldIndex), customFieldType);

                s_lastActorNamesIndex = item.actorNamesIndex;
                s_lastActorFieldIndex = item.actorFieldIndex;
            }
            else if (item.conditionType == ConditionWizardResourceType.Item)
            {
                // Item:
                item.itemNamesIndex = EditorGUILayout.Popup(item.itemNamesIndex, itemNames);
                item.itemFieldIndex = EditorGUILayout.Popup(item.itemFieldIndex, itemFieldNames);
                CustomFieldType customFieldType = GetCustomFieldType(database.items, item.itemNamesIndex, item.itemFieldIndex);
                DrawRightHand(item, GetWizardItemFieldType(item.itemFieldIndex), customFieldType);

                s_lastItemNamesIndex = item.itemNamesIndex;
                s_lastItemFieldIndex = item.itemFieldIndex;
            }
            else if (item.conditionType == ConditionWizardResourceType.Location)
            {
                // Location:
                item.locationNamesIndex = EditorGUILayout.Popup(item.locationNamesIndex, locationNames);
                item.locationFieldIndex = EditorGUILayout.Popup(item.locationFieldIndex, locationFieldNames);
                CustomFieldType customFieldType = GetCustomFieldType(database.locations, item.locationNamesIndex, item.locationFieldIndex);
                DrawRightHand(item, GetWizardLocationFieldType(item.locationFieldIndex), customFieldType);

                s_lastLocationNameIndex = item.locationNamesIndex;
                s_lastLocationFieldIndex = item.locationFieldIndex;
            }

            else if (item.conditionType == ConditionWizardResourceType.SimStatus)
            {
                // SimStatus:
                item.simStatusThisID = EditorGUILayout.Toggle(GUIContent.none, item.simStatusThisID, GUILayout.Width(14));
                if (item.simStatusThisID)
                {
                    EditorGUILayout.LabelField("thisID", EditorTools.GUILayoutLabelWidth("thisID"));
                }
                else
                {
                    item.simStatusID = EditorGUILayout.IntField(item.simStatusID, GUILayout.Width(38));
                }
                item.equalityType = (EqualityType)EditorGUILayout.EnumPopup(item.equalityType, EditorTools.GUILayoutPopupWidth(item.equalityType));
                item.simStatusType = (SimStatusType)EditorGUILayout.EnumPopup(item.simStatusType);
            }

            else if (item.conditionType == ConditionWizardResourceType.Misc)
            {
                ValidateCustomLuaFunctionInfo();

                int newCustomLuaFuncIndex = EditorGUILayout.Popup(item.customLuaFuncIndex, builtinLuaFuncNames);
                if (newCustomLuaFuncIndex != item.customLuaFuncIndex)
                {
                    item.customLuaFuncIndex = newCustomLuaFuncIndex;
                    item.customParamValues = null;
                }
                if (0 <= item.customLuaFuncIndex && item.customLuaFuncIndex < builtinLuaFuncNames.Length)
                {
                    var luaFuncRecord = builtinLuaFuncs[item.customLuaFuncIndex];
                    if (item.customParamValues == null) InitCustomParamValues(luaFuncRecord, out item.customParamValues);
                    for (int i = 0; i < luaFuncRecord.parameters.Length; i++)
                    {
                        switch (luaFuncRecord.parameters[i])
                        {
                            case CustomLuaParameterType.Bool:
                                item.customParamValues[i] = (BooleanType)EditorGUILayout.EnumPopup((BooleanType)item.customParamValues[i]);
                                break;
                            case CustomLuaParameterType.Double:
                                item.customParamValues[i] = EditorGUILayout.FloatField((float)item.customParamValues[i]);
                                break;
                            case CustomLuaParameterType.String:
                                item.customParamValues[i] = EditorGUILayout.TextField((string)item.customParamValues[i]);
                                break;
                            case CustomLuaParameterType.Actor:
                                item.customParamValues[i] = EditorGUILayout.Popup((int)item.customParamValues[i], actorNames);
                                break;
                            case CustomLuaParameterType.Quest:
                                item.customParamValues[i] = EditorGUILayout.Popup((int)item.customParamValues[i], questNames);
                                item.questNamesIndex = (int)item.customParamValues[i];
                                break;
                            case CustomLuaParameterType.QuestEntry:
                                if ((item.conditionsQuestEntryNames.Length == 0) && (item.questNamesIndex < complexQuestNames.Length))
                                {
                                    item.conditionsQuestEntryNames = GetQuestEntryNames(complexQuestNames[item.questNamesIndex]);
                                }
                                item.customParamValues[i] = EditorGUILayout.Popup((int)item.customParamValues[i], item.conditionsQuestEntryNames);
                                break;
                            case CustomLuaParameterType.Variable:
                            case CustomLuaParameterType.VariableName:
                                item.customParamValues[i] = EditorGUILayout.Popup((int)item.customParamValues[i], variablePopupNames);
                                if ((int)item.customParamValues[i] == 0)
                                {
                                    // New variable:
                                    item.newVariableName = EditorGUILayout.TextField(item.newVariableName);
                                    item.newVariableType = (FieldType)EditorGUILayout.EnumPopup(item.newVariableType);
                                }
                                break;
                            case CustomLuaParameterType.Item:
                                item.customParamValues[i] = EditorGUILayout.Popup((int)item.customParamValues[i], itemNames);
                                break;
                            case CustomLuaParameterType.QuestState:
                                if (item.customParamValues[i] == null || item.customParamValues[i].GetType() != typeof(QuestState))
                                {
                                    item.customParamValues[i] = QuestState.Unassigned;
                                }
                                item.customParamValues[i] = (QuestState)EditorGUILayout.EnumPopup((QuestState)item.customParamValues[i]);
                                break;
                        }
                    }
                    switch (luaFuncRecord.returnValue)
                    {
                        case CustomLuaReturnType.Bool:
                            DrawRightHand(item, FieldType.Boolean);
                            break;
                        case CustomLuaReturnType.Double:
                            DrawRightHand(item, FieldType.Number);
                            break;
                        case CustomLuaReturnType.String:
                            DrawRightHand(item, FieldType.Text);
                            break;
                    }
                }
            }

            else if (item.conditionType == ConditionWizardResourceType.Custom)
            {
                ValidateCustomLuaFunctionInfo();

                int newCustomLuaFuncIndex = EditorGUILayout.Popup(item.customLuaFuncIndex, customLuaFuncNames);
                if (newCustomLuaFuncIndex != item.customLuaFuncIndex)
                {
                    item.customLuaFuncIndex = newCustomLuaFuncIndex;
                    item.customParamValues = null;
                }
                if (0 <= item.customLuaFuncIndex && item.customLuaFuncIndex < customLuaFuncs.Length)
                {
                    var luaFuncRecord = customLuaFuncs[item.customLuaFuncIndex];
                    if (item.customParamValues == null) InitCustomParamValues(luaFuncRecord, out item.customParamValues);
                    for (int i = 0; i < luaFuncRecord.parameters.Length; i++)
                    {
                        switch (luaFuncRecord.parameters[i])
                        {
                            case CustomLuaParameterType.Bool:
                                item.customParamValues[i] = (BooleanType)EditorGUILayout.EnumPopup((BooleanType)item.customParamValues[i]);
                                break;
                            case CustomLuaParameterType.Double:
                                item.customParamValues[i] = EditorGUILayout.FloatField((float)item.customParamValues[i]);
                                break;
                            case CustomLuaParameterType.String:
                                item.customParamValues[i] = EditorGUILayout.TextField((string)item.customParamValues[i]);
                                break;
                            case CustomLuaParameterType.Actor:
                                item.customParamValues[i] = EditorGUILayout.Popup((int)item.customParamValues[i], actorNames);
                                break;
                            case CustomLuaParameterType.Quest:
                                item.customParamValues[i] = EditorGUILayout.Popup((int)item.customParamValues[i], questNames);
                                item.questNamesIndex = (int)item.customParamValues[i];
                                break;
                            case CustomLuaParameterType.QuestEntry:
                                if ((item.conditionsQuestEntryNames.Length == 0) && (item.questNamesIndex < complexQuestNames.Length))
                                {
                                    item.conditionsQuestEntryNames = GetQuestEntryNames(complexQuestNames[item.questNamesIndex]);
                                }
                                item.customParamValues[i] = EditorGUILayout.Popup((int)item.customParamValues[i], item.conditionsQuestEntryNames);
                                break;
                            case CustomLuaParameterType.Variable:
                            case CustomLuaParameterType.VariableName:
                                item.customParamValues[i] = EditorGUILayout.Popup((int)item.customParamValues[i], variablePopupNames);
                                if ((int)item.customParamValues[i] == 0)
                                {
                                    // New variable:
                                    item.newVariableName = EditorGUILayout.TextField(item.newVariableName);
                                    item.newVariableType = (FieldType)EditorGUILayout.EnumPopup(item.newVariableType);
                                }
                                break;
                            case CustomLuaParameterType.Item:
                                item.customParamValues[i] = EditorGUILayout.Popup((int)item.customParamValues[i], itemNames);
                                break;
                            case CustomLuaParameterType.QuestState:
                                if (item.customParamValues[i] == null || item.customParamValues[i].GetType() != typeof(QuestState))
                                {
                                    item.customParamValues[i] = QuestState.Unassigned;
                                }
                                item.customParamValues[i] = (QuestState)EditorGUILayout.EnumPopup((QuestState)item.customParamValues[i]);
                                break;
                        }
                    }
                    switch (luaFuncRecord.returnValue)
                    {
                        case CustomLuaReturnType.Bool:
                            DrawRightHand(item, FieldType.Boolean);
                            break;
                        case CustomLuaReturnType.Double:
                            DrawRightHand(item, FieldType.Number);
                            break;
                        case CustomLuaReturnType.String:
                            DrawRightHand(item, FieldType.Text);
                            break;
                    }
                }
            }

            else if (item.conditionType == ConditionWizardResourceType.ManualEnter)
            {
                // Custom:
                item.stringValue = EditorGUILayout.TextField(item.stringValue);
            }

            if (GUILayout.Button(new GUIContent("-", "Delete this condition."), EditorStyles.miniButton, GUILayout.Width(22)))
            {
                itemToDelete = item;
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawRightHand(ConditionItem item, FieldType fieldType, CustomFieldType customFieldType = null)
        {
            switch (fieldType)
            {
                case FieldType.Boolean:
                    item.equalityType = (EqualityType)EditorGUILayout.EnumPopup(item.equalityType, GUILayout.Width(60));
                    item.booleanValue = (BooleanType)EditorGUILayout.EnumPopup(item.booleanValue);
                    break;
                case FieldType.Number:
                    item.comparisonType = (ComparisonType)EditorGUILayout.EnumPopup(item.comparisonType, GUILayout.Width(96));
                    if (item.comparisonType == ComparisonType.Between)
                    {
                        item.floatValue = EditorGUILayout.FloatField(item.floatValue);
                        EditorGUILayout.LabelField("and", GUILayout.Width(28));
                        item.floatValue2 = EditorGUILayout.FloatField(item.floatValue2);
                    }
                    else
                    {
                        item.floatValue = EditorGUILayout.FloatField(item.floatValue);
                    }
                    break;
                default:
                    item.equalityType = (EqualityType)EditorGUILayout.EnumPopup(item.equalityType, GUILayout.Width(60));

                    if (customFieldType != null)
                    {
                        item.stringValue = customFieldType.Draw(item.stringValue, database);
                    }
                    else
                    {
                        item.stringValue = EditorGUILayout.TextField(item.stringValue);
                    }
                    break;
            }
        }

        public string CancelConditionsWizard()
        {
            isOpen = false;
            return savedLuaCode;
        }

        public string AcceptConditionsWizard()
        {
            isOpen = false;
            AddNewVariables();
            return BuildLua();
        }


        //===================================================================================


        public string Draw(Rect position, GUIContent guiContent, string luaCode, bool flexibleHeight = false)
        {
            if (database == null) isOpen = false;

            // Title label:
            var rect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.LabelField(rect, guiContent);

            var textAreaHeight = flexibleHeight
                ? EditorGUIUtility.singleLineHeight + 2
                : Mathf.Max(EditorGUIUtility.singleLineHeight,
                    GUI.skin.textArea.CalcHeight(new GUIContent(luaCode), position.width) + 2);

            var boxRect = new Rect(0, 0, 0, 0);
            float separator = 0;
            if (isOpen)
            {
                // Lua wizard content:
                boxRect = new Rect(position.x + 16, position.y + EditorGUIUtility.singleLineHeight,
                    position.width - 16, position.height - textAreaHeight - EditorGUIUtility.singleLineHeight);
                boxRect.height = (conditionItems.Count + 1) * (EditorGUIUtility.singleLineHeight + 2f);
                separator = 2;

                EditorGUI.BeginDisabledGroup(true);
                GUI.Button(boxRect, GUIContent.none);
                EditorGUI.EndDisabledGroup();

                luaCode = DrawConditionsWizard(boxRect, luaCode);
            }

            var textAreaRect = new Rect(position.x,
                position.y + EditorGUIUtility.singleLineHeight + boxRect.height + separator,
                position.width,
                textAreaHeight);
            if (flexibleHeight)
            {
                luaCode = EditorGUI.TextArea(textAreaRect, luaCode);
            }
            else
            {
                luaCode = EditorGUI.TextField(textAreaRect, luaCode);
            }

            return luaCode;
        }

        private string DrawConditionsWizard(Rect position, string luaCode)
        {
            int originalIndentLevel = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            var rect = position;
            var x = position.x;
            var y = position.y;

            EditorGUI.BeginChangeCheck();

            // Condition items:
            ConditionItem itemToDelete = null;
            foreach (ConditionItem item in conditionItems)
            {
                var innerHeight = EditorGUIUtility.singleLineHeight + 2;
                //var innerRect = new Rect(x, y, position.width, innerHeight);
                DrawConditionItem(new Rect(x, y, position.width, innerHeight), item, ref itemToDelete);
                y += EditorGUIUtility.singleLineHeight + 2;
            }
            if (itemToDelete != null) conditionItems.Remove(itemToDelete);

            // Bottom row (add condition item, logical operator, Revert & Apply buttons):
            x = position.x;
            y = position.y + position.height - EditorGUIUtility.singleLineHeight;
            rect = new Rect(x, y, 22, EditorGUIUtility.singleLineHeight);
            if (GUI.Button(rect, new GUIContent("+", "Add a new condition."), EditorStyles.miniButton))
            {
                conditionItems.Add(new ConditionItem());
            }
            x += rect.width + 2;

            rect = new Rect(x, y, 64, EditorGUIUtility.singleLineHeight);
            conditionsLogicalOperator = (LogicalOperatorType)EditorGUI.EnumPopup(rect, GUIContent.none, conditionsLogicalOperator);
            x += rect.width + 2;

            rect = new Rect(x, y, 84, EditorGUIUtility.singleLineHeight);
            EditorGUI.LabelField(rect, "must be true.");
            x += rect.width + 2;

            rect = new Rect(x, y, 72, EditorGUIUtility.singleLineHeight);
            append = EditorGUI.ToggleLeft(rect, "Append", append);

            if (EditorGUI.EndChangeCheck())
            {
                BuildLua();
            }

            EditorGUI.BeginDisabledGroup(conditionItems.Count <= 0);
            rect = new Rect(position.x + position.width - 52 - 4 - 56, y, 56, EditorGUIUtility.singleLineHeight);
            if (GUI.Button(rect, new GUIContent("Revert", "Cancel these settings."), EditorStyles.miniButton))
            {
                luaCode = CancelConditionsWizard();
            }
            rect = new Rect(position.x + position.width - 52, y, 52, EditorGUIUtility.singleLineHeight);
            GUI.Box(rect, GUIContent.none);
            if (GUI.Button(rect, new GUIContent("Apply", "Apply these settings"), EditorStyles.miniButton))
            {
                luaCode = AcceptConditionsWizard();
            }
            EditorGUI.EndDisabledGroup();

            EditorGUI.indentLevel = originalIndentLevel;

            return luaCode;
        }

        private void DrawConditionItem(Rect position, ConditionItem item, ref ConditionItem itemToDelete)
        {
            const float typeWidth = 96;
            const float equalityWidth = 64;
            const float questStateWidth = 96;
            const float deleteButtonWidth = 22;

            int originalIndentLevel = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            var x = position.x;
            var y = position.y;
            var rect = new Rect(x, y, 96, EditorGUIUtility.singleLineHeight);
            x += rect.width + 2;
            ConditionWizardResourceType newConditionType = (ConditionWizardResourceType)EditorGUI.EnumPopup(rect, GUIContent.none, item.conditionType);
            if (newConditionType != item.conditionType)
            {
                item.conditionType = newConditionType;
                item.conditionsQuestEntryNames = new string[0];
            }

            if (item.conditionType == ConditionWizardResourceType.Quest)
            {

                // Quest:
                var questNameWidth = position.width - (typeWidth + equalityWidth + questStateWidth + deleteButtonWidth + 8);
                rect = new Rect(x, y, questNameWidth, EditorGUIUtility.singleLineHeight);
                x += rect.width + 2;
                item.questNamesIndex = EditorGUI.Popup(rect, item.questNamesIndex, questNames);
                rect = new Rect(x, y, equalityWidth, EditorGUIUtility.singleLineHeight);
                item.equalityType = (EqualityType)EditorGUI.EnumPopup(rect, item.equalityType);
                x += rect.width + 2;
                rect = new Rect(x, y, questStateWidth, EditorGUIUtility.singleLineHeight);
                item.questState = QuestStateDrawer.RectQuestStatePopup(rect, item.questState);
                x += rect.width + 2;

            }
            else if (item.conditionType == ConditionWizardResourceType.QuestEntry)
            {

                // Quest Entry:
                var freeWidth = position.width - (typeWidth + equalityWidth + questStateWidth + deleteButtonWidth + 10);
                rect = new Rect(x, y, freeWidth / 2, EditorGUIUtility.singleLineHeight);
                int newQuestNamesIndex = EditorGUI.Popup(rect, item.questNamesIndex, complexQuestNames);
                if (newQuestNamesIndex != item.questNamesIndex)
                {
                    item.questNamesIndex = newQuestNamesIndex;
                    item.conditionsQuestEntryNames = new string[0];
                }
                if ((item.conditionsQuestEntryNames.Length == 0) && (item.questNamesIndex < complexQuestNames.Length))
                {
                    item.conditionsQuestEntryNames = GetQuestEntryNames(complexQuestNames[item.questNamesIndex]);
                }
                x += rect.width + 2;
                rect = new Rect(x, y, freeWidth / 2, EditorGUIUtility.singleLineHeight);
                item.questEntryIndex = EditorGUI.Popup(rect, item.questEntryIndex, item.conditionsQuestEntryNames);
                x += rect.width + 2;
                rect = new Rect(x, y, equalityWidth, EditorGUIUtility.singleLineHeight);
                item.equalityType = (EqualityType)EditorGUI.EnumPopup(rect, item.equalityType);
                x += rect.width + 2;
                rect = new Rect(x, y, questStateWidth, EditorGUIUtility.singleLineHeight);
                item.questState = QuestStateDrawer.RectQuestStatePopup(rect, item.questState);
                x += rect.width + 2;

            }
            else if (item.conditionType == ConditionWizardResourceType.Variable)
            {

                // Variable:
                var freeWidth = position.width - (typeWidth + equalityWidth + deleteButtonWidth + 8);
                rect = new Rect(x, y, freeWidth / 2, EditorGUIUtility.singleLineHeight);
                FieldType variableType = FieldType.Text;
                if (item.variableNamesIndex == 0)
                {
                    // New variable:
                    var thirdWidth = rect.width / 3;
                    item.variableNamesIndex = EditorGUI.Popup(new Rect(rect.x, rect.y, thirdWidth, rect.height), item.variableNamesIndex, variablePopupNames);
                    item.newVariableName = EditorGUI.TextField(new Rect(rect.x + thirdWidth, rect.y, thirdWidth, rect.height), item.newVariableName);
                    item.newVariableType = (FieldType)EditorGUI.EnumPopup(new Rect(rect.x + 2 * thirdWidth, rect.y, thirdWidth, rect.height), item.newVariableType);
                    variableType = item.newVariableType;
                }
                else
                {
                    // Existing variable:
                    item.variableNamesIndex = EditorGUI.Popup(rect, item.variableNamesIndex, variablePopupNames);
                    variableType = GetWizardVariableType(item.variableNamesIndex);
                }
                x += rect.width + 2;
                rect = new Rect(x, y, equalityWidth + 2 + (freeWidth / 2), EditorGUIUtility.singleLineHeight);
                DrawRightHand(rect, item, variableType);
                x += rect.width + 2;

            }
            else if (item.conditionType == ConditionWizardResourceType.Actor)
            {

                // Actor:
                var freeWidth = position.width - (typeWidth + equalityWidth + deleteButtonWidth + 10);
                rect = new Rect(x, y, freeWidth / 3, EditorGUIUtility.singleLineHeight);
                item.actorNamesIndex = EditorGUI.Popup(rect, item.actorNamesIndex, actorNames);
                x += rect.width + 2;
                rect = new Rect(x, y, freeWidth / 3, EditorGUIUtility.singleLineHeight);
                item.actorFieldIndex = EditorGUI.Popup(rect, item.actorFieldIndex, actorFieldNames);
                x += rect.width + 2;
                rect = new Rect(x, y, equalityWidth + 2 + (freeWidth / 3), EditorGUIUtility.singleLineHeight);
                DrawRightHand(rect, item, GetWizardActorFieldType(item.actorFieldIndex));
                x += rect.width + 2;

            }
            else if (item.conditionType == ConditionWizardResourceType.Item)
            {

                // Item:
                var freeWidth = position.width - (typeWidth + equalityWidth + deleteButtonWidth + 10);
                rect = new Rect(x, y, freeWidth / 3, EditorGUIUtility.singleLineHeight);
                item.itemNamesIndex = EditorGUI.Popup(rect, item.itemNamesIndex, itemNames);
                x += rect.width + 2;
                rect = new Rect(x, y, freeWidth / 3, EditorGUIUtility.singleLineHeight);
                item.itemFieldIndex = EditorGUI.Popup(rect, item.itemFieldIndex, itemFieldNames);
                x += rect.width + 2;
                rect = new Rect(x, y, equalityWidth + 2 + (freeWidth / 3), EditorGUIUtility.singleLineHeight);
                DrawRightHand(rect, item, GetWizardItemFieldType(item.itemFieldIndex));
                x += rect.width + 2;

            }
            else if (item.conditionType == ConditionWizardResourceType.Location)
            {

                // Location:
                var freeWidth = position.width - (typeWidth + equalityWidth + deleteButtonWidth + 10);
                rect = new Rect(x, y, freeWidth / 3, EditorGUIUtility.singleLineHeight);
                item.locationNamesIndex = EditorGUI.Popup(rect, item.locationNamesIndex, locationNames);
                x += rect.width + 2;
                rect = new Rect(x, y, freeWidth / 3, EditorGUIUtility.singleLineHeight);
                item.locationFieldIndex = EditorGUI.Popup(rect, item.locationFieldIndex, locationFieldNames);
                x += rect.width + 2;
                rect = new Rect(x, y, equalityWidth + 2 + (freeWidth / 3), EditorGUIUtility.singleLineHeight);
                DrawRightHand(rect, item, GetWizardLocationFieldType(item.locationFieldIndex));
                x += rect.width + 2;

            }
            else if (item.conditionType == ConditionWizardResourceType.SimStatus)
            {

                // SimStatus:
                var freeWidth = position.width - (88 + 56 + equalityWidth + deleteButtonWidth + 10);
                rect = new Rect(x, y, 14, EditorGUIUtility.singleLineHeight);
                item.simStatusThisID = EditorGUI.Toggle(rect, GUIContent.none, item.simStatusThisID);
                rect = new Rect(x + 14, y, 38, EditorGUIUtility.singleLineHeight);
                if (item.simStatusThisID)
                {
                    EditorGUI.LabelField(rect, "thisID");
                }
                else
                {
                    item.simStatusID = EditorGUI.IntField(rect, item.simStatusID);
                }
                x += rect.width + 14;
                rect = new Rect(x, y, 56, EditorGUIUtility.singleLineHeight);
                item.equalityType = (EqualityType)EditorGUI.EnumPopup(rect, item.equalityType);
                x += rect.width + 2;
                rect = new Rect(x, y, freeWidth, EditorGUIUtility.singleLineHeight);
                item.simStatusType = (SimStatusType)EditorGUI.EnumPopup(rect, item.simStatusType);
                x += rect.width + 2;

            }
            else if (item.conditionType == ConditionWizardResourceType.Misc)
            {
                ValidateCustomLuaFunctionInfo();

                var customPopupWidth = 60f;
                var rightHandWidth = 80f;
                rect = new Rect(x, y, customPopupWidth, EditorGUIUtility.singleLineHeight);
                int newCustomLuaFuncIndex = EditorGUI.Popup(rect, item.customLuaFuncIndex, builtinLuaFuncNames);
                if (newCustomLuaFuncIndex != item.customLuaFuncIndex)
                {
                    item.customLuaFuncIndex = newCustomLuaFuncIndex;
                    item.customParamValues = null;
                }
                if (0 <= item.customLuaFuncIndex && item.customLuaFuncIndex < builtinLuaFuncs.Length)
                {
                    var luaFuncRecord = builtinLuaFuncs[item.customLuaFuncIndex];
                    var numParams = luaFuncRecord.parameters.Length;
                    var freeWidth = position.width - (typeWidth + equalityWidth + deleteButtonWidth + 10) - customPopupWidth - rightHandWidth;
                    if (item.customParamValues == null) InitCustomParamValues(luaFuncRecord, out item.customParamValues);
                    for (int i = 0; i < luaFuncRecord.parameters.Length; i++)
                    {
                        x += rect.width + 2;
                        rect = new Rect(x, y, freeWidth / numParams, EditorGUIUtility.singleLineHeight);
                        switch (luaFuncRecord.parameters[i])
                        {
                            case CustomLuaParameterType.Bool:
                                item.customParamValues[i] = (BooleanType)EditorGUI.EnumPopup(rect, (BooleanType)item.customParamValues[i]);
                                break;
                            case CustomLuaParameterType.Double:
                                item.customParamValues[i] = EditorGUI.FloatField(rect, (float)item.customParamValues[i]);
                                break;
                            case CustomLuaParameterType.String:
                                item.customParamValues[i] = EditorGUI.TextField(rect, (string)item.customParamValues[i]);
                                break;
                            case CustomLuaParameterType.Actor:
                                item.customParamValues[i] = EditorGUI.Popup(rect, (int)item.customParamValues[i], actorNames);
                                break;
                            case CustomLuaParameterType.Quest:
                                item.customParamValues[i] = EditorGUI.Popup(rect, (int)item.customParamValues[i], questNames);
                                item.questNamesIndex = (int)item.customParamValues[i];
                                break;
                            case CustomLuaParameterType.QuestEntry:
                                if ((item.conditionsQuestEntryNames.Length == 0) && (item.questNamesIndex < complexQuestNames.Length))
                                {
                                    item.conditionsQuestEntryNames = GetQuestEntryNames(complexQuestNames[item.questNamesIndex]);
                                }
                                item.customParamValues[i] = EditorGUI.Popup(rect, (int)item.customParamValues[i], item.conditionsQuestEntryNames);
                                break;
                            case CustomLuaParameterType.Variable:
                            case CustomLuaParameterType.VariableName:
                                item.customParamValues[i] = EditorGUI.Popup(rect, (int)item.customParamValues[i], variablePopupNames);
                                break;
                            case CustomLuaParameterType.Item:
                                item.customParamValues[i] = EditorGUI.Popup(rect, (int)item.customParamValues[i], itemNames);
                                break;
                            case CustomLuaParameterType.QuestState:
                                if (item.customParamValues[i] == null || item.customParamValues[i].GetType() != typeof(QuestState))
                                {
                                    item.customParamValues[i] = QuestState.Unassigned;
                                }
                                item.customParamValues[i] = (QuestState)EditorGUI.EnumPopup(rect, (QuestState)item.customParamValues[i]);
                                break;
                        }
                    }
                    x += rect.width + 2;
                    rect = new Rect(x, y, position.width - x, EditorGUIUtility.singleLineHeight);
                    switch (luaFuncRecord.returnValue)
                    {
                        case CustomLuaReturnType.Bool:
                            DrawRightHand(rect, item, FieldType.Boolean);
                            break;
                        case CustomLuaReturnType.Double:
                            DrawRightHand(rect, item, FieldType.Number);
                            break;
                        case CustomLuaReturnType.String:
                            DrawRightHand(rect, item, FieldType.Text);
                            break;
                    }
                }
            }
            else if (item.conditionType == ConditionWizardResourceType.Custom)
            {
                ValidateCustomLuaFunctionInfo();

                var customPopupWidth = 60f;
                var rightHandWidth = 80f;
                rect = new Rect(x, y, customPopupWidth, EditorGUIUtility.singleLineHeight);
                int newCustomLuaFuncIndex = EditorGUI.Popup(rect, item.customLuaFuncIndex, customLuaFuncNames);
                if (newCustomLuaFuncIndex != item.customLuaFuncIndex)
                {
                    item.customLuaFuncIndex = newCustomLuaFuncIndex;
                    item.customParamValues = null;
                }
                if (0 <= item.customLuaFuncIndex && item.customLuaFuncIndex < customLuaFuncs.Length)
                {
                    var luaFuncRecord = customLuaFuncs[item.customLuaFuncIndex];
                    var numParams = luaFuncRecord.parameters.Length;
                    var freeWidth = position.width - (typeWidth + equalityWidth + deleteButtonWidth + 10) - customPopupWidth - rightHandWidth;
                    if (item.customParamValues == null) InitCustomParamValues(luaFuncRecord, out item.customParamValues);
                    for (int i = 0; i < luaFuncRecord.parameters.Length; i++)
                    {
                        x += rect.width + 2;
                        rect = new Rect(x, y, freeWidth / numParams, EditorGUIUtility.singleLineHeight);
                        switch (luaFuncRecord.parameters[i])
                        {
                            case CustomLuaParameterType.Bool:
                                item.customParamValues[i] = (BooleanType)EditorGUI.EnumPopup(rect, (BooleanType)item.customParamValues[i]);
                                break;
                            case CustomLuaParameterType.Double:
                                item.customParamValues[i] = EditorGUI.FloatField(rect, (float)item.customParamValues[i]);
                                break;
                            case CustomLuaParameterType.String:
                                item.customParamValues[i] = EditorGUI.TextField(rect, (string)item.customParamValues[i]);
                                break;
                            case CustomLuaParameterType.Actor:
                                item.customParamValues[i] = EditorGUI.Popup(rect, (int)item.customParamValues[i], actorNames);
                                break;
                            case CustomLuaParameterType.Quest:
                                item.customParamValues[i] = EditorGUI.Popup(rect, (int)item.customParamValues[i], questNames);
                                item.questNamesIndex = (int)item.customParamValues[i];
                                break;
                            case CustomLuaParameterType.QuestEntry:
                                if ((item.conditionsQuestEntryNames.Length == 0) && (item.questNamesIndex < complexQuestNames.Length))
                                {
                                    item.conditionsQuestEntryNames = GetQuestEntryNames(complexQuestNames[item.questNamesIndex]);
                                }
                                item.customParamValues[i] = EditorGUI.Popup(rect, (int)item.customParamValues[i], item.conditionsQuestEntryNames);
                                break;
                            case CustomLuaParameterType.Variable:
                            case CustomLuaParameterType.VariableName:
                                item.customParamValues[i] = EditorGUI.Popup(rect, (int)item.customParamValues[i], variablePopupNames);
                                break;
                            case CustomLuaParameterType.Item:
                                item.customParamValues[i] = EditorGUI.Popup(rect, (int)item.customParamValues[i], itemNames);
                                break;
                            case CustomLuaParameterType.QuestState:
                                if (item.customParamValues[i] == null || item.customParamValues[i].GetType() != typeof(QuestState))
                                {
                                    item.customParamValues[i] = QuestState.Unassigned;
                                }
                                item.customParamValues[i] = (QuestState)EditorGUI.EnumPopup(rect, (QuestState)item.customParamValues[i]);
                                break;
                        }
                    }
                    x += rect.width + 2;
                    rect = new Rect(x, y, position.width - x, EditorGUIUtility.singleLineHeight);
                    switch (luaFuncRecord.returnValue)
                    {
                        case CustomLuaReturnType.Bool:
                            DrawRightHand(rect, item, FieldType.Boolean);
                            break;
                        case CustomLuaReturnType.Double:
                            DrawRightHand(rect, item, FieldType.Number);
                            break;
                        case CustomLuaReturnType.String:
                            DrawRightHand(rect, item, FieldType.Text);
                            break;
                    }
                }
            }
            else if (item.conditionType == ConditionWizardResourceType.ManualEnter)
            {

                // Custom:
                rect = new Rect(x, y, position.width - (88 + deleteButtonWidth + 20), EditorGUIUtility.singleLineHeight);
                item.stringValue = EditorGUI.TextField(rect, item.stringValue);

            }

            // Delete button:
            rect = new Rect(position.x + position.width - deleteButtonWidth, y, deleteButtonWidth, EditorGUIUtility.singleLineHeight);
            if (GUI.Button(rect, new GUIContent("-", "Delete this condition."), EditorStyles.miniButton))
            {
                itemToDelete = item;
            }

            EditorGUI.indentLevel = originalIndentLevel;
        }

        private void DrawRightHand(Rect position, ConditionItem item, FieldType fieldType)
        {
            const float equalityWidth = 64;

            var rect1 = new Rect(position.x, position.y, equalityWidth, EditorGUIUtility.singleLineHeight);
            var rect2 = new Rect(position.x + 2 + equalityWidth, position.y, position.width - (equalityWidth + 2), EditorGUIUtility.singleLineHeight);
            switch (fieldType)
            {
                case FieldType.Boolean:
                    item.equalityType = (EqualityType)EditorGUI.EnumPopup(rect1, item.equalityType);
                    item.booleanValue = (BooleanType)EditorGUI.EnumPopup(rect2, item.booleanValue);
                    break;
                case FieldType.Number:
                    item.comparisonType = (ComparisonType)EditorGUI.EnumPopup(rect1, item.comparisonType);
                    item.floatValue = EditorGUI.FloatField(rect2, item.floatValue);
                    break;
                default:
                    item.equalityType = (EqualityType)EditorGUI.EnumPopup(rect1, item.equalityType);
                    item.stringValue = EditorGUI.TextField(rect2, item.stringValue);
                    break;
            }
        }

    }

}