// Copyright (c) Pixel Crushers. All rights reserved.

using UnityEngine;
using System;
using System.Collections.Generic;
using System.Text;

namespace PixelCrushers.DialogueSystem
{

    /// <summary>
    /// Model and Lua code generation for the Script wizard, shared by the IMGUI
    /// LuaScriptWizard and the Dialogue Database Editor Window's UI Toolkit wizard.
    /// </summary>
    public class LuaScriptWizardModel : LuaWizardBase
    {

        public enum ValueSetMode
        {
            To,
            Add,
            Subtract
        }

        public enum NetSetMode
        {
            Set,
            NetSet
        }

        public class ScriptItem
        {
            public ScriptWizardResourceType resourceType = ScriptWizardResourceType.Quest;
            public int questNamesIndex = 0;
            public int questEntryIndex = 0;
            public int variableNamesIndex = 0;
            public int actorNamesIndex = 0;
            public int actorFieldIndex = 0;
            public int itemNamesIndex = 0;
            public int itemFieldIndex = 0;
            public int locationNamesIndex = 0;
            public int locationFieldIndex = 0;
            public int simStatusID = 0;
            public int customLuaFuncIndex = 0;
            public SimStatusType simStatusType = SimStatusType.Untouched;
            public QuestState questState = QuestState.Unassigned;
            public string stringValue = string.Empty;
            public BooleanType booleanValue = BooleanType.True;
            public float floatValue = 0;
            public ValueSetMode valueSetMode = ValueSetMode.To;
            public NetSetMode netSetMode = NetSetMode.Set;
            public string[] scriptQuestEntryNames = new string[0];
            public object[] customParamValues = null;
            public string newVariableName = string.Empty;
            public FieldType newVariableType = FieldType.Boolean;

            public ScriptItem()
            {
                resourceType = LuaWizardBase.GetConditionResourceTypeToScriptResourceType(s_lastWizardResourceType);
                questNamesIndex = s_lastQuestNamesIndex;
                questEntryIndex = s_lastQuestEntryIndex;
                actorNamesIndex = s_lastActorNamesIndex;
                actorFieldIndex = s_lastActorFieldIndex;
                itemNamesIndex = s_lastItemNamesIndex;
                itemFieldIndex = s_lastItemFieldIndex;
                locationNamesIndex = s_lastLocationNameIndex;
                locationFieldIndex = s_lastLocationFieldIndex;
                variableNamesIndex = s_lastVariableNameIndex;
            }
        }

        public List<ScriptItem> scriptItems = new List<ScriptItem>();
        public string savedLuaCode = string.Empty;
        public bool append = true;
        public CustomLuaFunctionInfoRecord[] builtinLuaFuncs = null;
        public string[] builtinLuaFuncNames = null;
        public CustomLuaFunctionInfoRecord[] customLuaFuncs = null;
        public string[] customLuaFuncNames = null;

        public LuaScriptWizardModel(DialogueDatabase database) : base(database)
        {
        }

        /// <summary>
        /// Remembers the item's choices so the next new item starts with them.
        /// </summary>
        public void RememberLastUsed(ScriptItem item)
        {
            s_lastWizardResourceType = GetScriptResourceTypeToConditionResourceType(item.resourceType);
            switch (item.resourceType)
            {
                case ScriptWizardResourceType.Quest:
                    s_lastQuestNamesIndex = item.questNamesIndex;
                    break;
                case ScriptWizardResourceType.QuestEntry:
                    s_lastQuestNamesIndex = item.questNamesIndex;
                    s_lastQuestEntryIndex = item.questEntryIndex;
                    break;
                case ScriptWizardResourceType.Variable:
                    s_lastVariableNameIndex = item.variableNamesIndex;
                    break;
                case ScriptWizardResourceType.Actor:
                    s_lastActorNamesIndex = item.actorNamesIndex;
                    s_lastActorFieldIndex = item.actorFieldIndex;
                    break;
                case ScriptWizardResourceType.Item:
                    s_lastItemNamesIndex = item.itemNamesIndex;
                    s_lastItemFieldIndex = item.itemFieldIndex;
                    break;
                case ScriptWizardResourceType.Location:
                    s_lastLocationNameIndex = item.locationNamesIndex;
                    s_lastLocationFieldIndex = item.locationFieldIndex;
                    break;
            }
        }

        public void ValidateCustomLuaFunctionInfo()
        {
            if (customLuaFuncs == null)
            {
                FindAllCustomLuaFuncs(false, 
                    out builtinLuaFuncs, out builtinLuaFuncNames,
                    out customLuaFuncs, out customLuaFuncNames);
            }
        }

        public void AddNewVariables()
        {
            foreach (var item in scriptItems)
            {
                if (item.resourceType == ScriptWizardResourceType.Variable && item.variableNamesIndex == 0)
                {
                    AddNewVariable(item.newVariableName, item.newVariableType);
                }
                else if (item.resourceType == ScriptWizardResourceType.Custom)
                {
                    if (0 <= item.customLuaFuncIndex && item.customLuaFuncIndex < customLuaFuncs.Length)
                    {
                        var luaFuncRecord = customLuaFuncs[item.customLuaFuncIndex];
                        for (int p = 0; p < luaFuncRecord.parameters.Length; p++)
                        {
                            if (luaFuncRecord.parameters[p] == CustomLuaParameterType.Variable ||
                                luaFuncRecord.parameters[p] == CustomLuaParameterType.VariableName)
                            {
                                AddNewVariable(item.newVariableName, item.newVariableType);
                            }
                        }
                    }
                }
            }
        }

        public string BuildLua()
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                if (append && !string.IsNullOrEmpty(savedLuaCode))
                {
                    if (savedLuaCode.TrimEnd().EndsWith(";"))
                    {
                        sb.AppendFormat("{0}\n", savedLuaCode);
                    }
                    else
                    {
                        sb.AppendFormat("{0};\n", savedLuaCode);
                    }
                }
                string endText = (scriptItems.Count > 1) ? ";\n" : string.Empty;
                for (int i = 0; i < scriptItems.Count; i++)
                {
                    var item = scriptItems[i];
                    if (item.resourceType == ScriptWizardResourceType.Quest)
                    {

                        // Quest:
                        string questName = GetWizardQuestName(questNames, item.questNamesIndex);
                        if (item.netSetMode == NetSetMode.NetSet) sb.Append("Net");
                        sb.AppendFormat("SetQuestState(\"{0}\", \"{1}\")",
                                        questName,
                                        QuestLog.StateToString(item.questState));

                    }
                    else if (item.resourceType == ScriptWizardResourceType.QuestEntry)
                    {

                        // Quest Entry:
                        string questName = GetWizardQuestName(complexQuestNames, item.questNamesIndex);
                        if (item.netSetMode == NetSetMode.NetSet) sb.Append("Net");
                        sb.AppendFormat("SetQuestEntryState(\"{0}\", {1}, \"{2}\")",
                                        questName,
                                        item.questEntryIndex + 1,
                                        QuestLog.StateToString(item.questState));

                    }
                    else if (item.resourceType == ScriptWizardResourceType.Variable)
                    {

                        // Variable:
                        string variableName;
                        FieldType variableType;
                        if (item.variableNamesIndex == 0)
                        {
                            variableName = item.newVariableName;
                            variableType = item.newVariableType;
                        }
                        else
                        {
                            variableName = (0 <= item.variableNamesIndex && item.variableNamesIndex < variableNames.Length) ? variableNames[item.variableNamesIndex] : "Alert";
                            variableType = GetWizardVariableType(item.variableNamesIndex);
                        }
                        switch (variableType)
                        {
                            case FieldType.Boolean:
                                if (item.netSetMode == NetSetMode.NetSet)
                                {
                                    sb.AppendFormat("NetSetBool(\"{0}\", {1})",
                                                    DialogueLua.StringToTableIndex(variableName),
                                                    (item.booleanValue == BooleanType.True) ? "true" : "false");
                                }
                                else
                                {
                                    sb.AppendFormat("Variable[\"{0}\"] = {1}",
                                                    DialogueLua.StringToTableIndex(variableName),
                                                    (item.booleanValue == BooleanType.True) ? "true" : "false");
                                }
                                break;
                            case FieldType.Number:
                                if (item.netSetMode == NetSetMode.NetSet)
                                {
                                    switch (item.valueSetMode)
                                    {
                                        case ValueSetMode.To:
                                            sb.AppendFormat("NetSetNumber(\"{0}\", {1})",
                                                            DialogueLua.StringToTableIndex(variableName),
                                                            item.floatValue.ToString(System.Globalization.CultureInfo.InvariantCulture));
                                            break;
                                        case ValueSetMode.Add:
                                            sb.AppendFormat("NetSetNumber(\"{0}\", Variable[\"{0}\"] + {1})",
                                                            DialogueLua.StringToTableIndex(variableName),
                                                            item.floatValue.ToString(System.Globalization.CultureInfo.InvariantCulture));
                                            break;
                                        case ValueSetMode.Subtract:
                                            sb.AppendFormat("NetSetNumber(\"{0}\", Variable[\"{0}\"] - {1})",
                                                            DialogueLua.StringToTableIndex(variableName),
                                                            item.floatValue.ToString(System.Globalization.CultureInfo.InvariantCulture));
                                            break;
                                    }
                                }
                                else
                                {
                                    switch (item.valueSetMode)
                                    {
                                        case ValueSetMode.To:
                                            sb.AppendFormat("Variable[\"{0}\"] = {1}",
                                                            DialogueLua.StringToTableIndex(variableName),
                                                            item.floatValue.ToString(System.Globalization.CultureInfo.InvariantCulture));
                                            break;
                                        case ValueSetMode.Add:
                                            sb.AppendFormat("Variable[\"{0}\"] = Variable[\"{0}\"] + {1}",
                                                            DialogueLua.StringToTableIndex(variableName),
                                                            item.floatValue.ToString(System.Globalization.CultureInfo.InvariantCulture));
                                            break;
                                        case ValueSetMode.Subtract:
                                            sb.AppendFormat("Variable[\"{0}\"] = Variable[\"{0}\"] - {1}",
                                                            DialogueLua.StringToTableIndex(variableName),
                                                            item.floatValue.ToString(System.Globalization.CultureInfo.InvariantCulture));
                                            break;
                                    }
                                }
                                break;
                            default:
                                if (item.netSetMode == NetSetMode.NetSet)
                                {
                                    sb.AppendFormat("NetSetString(\"{0}\", \"{1}\")",
                                                    DialogueLua.StringToTableIndex(variableName),
                                                    item.stringValue);
                                }
                                else
                                {
                                    sb.AppendFormat("Variable[\"{0}\"] = \"{1}\"",
                                                    DialogueLua.StringToTableIndex(variableName),
                                                    item.stringValue);
                                }
                                break;
                        }

                    }
                    else if (item.resourceType == ScriptWizardResourceType.Actor)
                    {

                        // Actor:
                        if (item.actorNamesIndex < actorNames.Length)
                        {
                            item.actorFieldIndex = Mathf.Clamp(item.actorFieldIndex, 0, actorFieldNames.Length - 1);
                            var actorName = actorNames[item.actorNamesIndex];
                            var actorFieldName = actorFieldNames[item.actorFieldIndex];
                            var fieldType = GetWizardActorFieldType(item.actorFieldIndex);
                            AppendFormat(sb, "Actor", actorName, actorFieldName, fieldType, item);
                        }

                    }
                    else if (item.resourceType == ScriptWizardResourceType.Item)
                    {

                        // Item:
                        if (item.itemNamesIndex < itemNames.Length)
                        {
                            item.itemFieldIndex = Mathf.Clamp(item.itemFieldIndex, 0, itemFieldNames.Length - 1);
                            var itemName = itemNames[item.itemNamesIndex];
                            var itemFieldName = itemFieldNames[item.itemFieldIndex];
                            var fieldType = GetWizardItemFieldType(item.itemFieldIndex);
                            AppendFormat(sb, "Item", itemName, itemFieldName, fieldType, item);
                        }

                    }
                    else if (item.resourceType == ScriptWizardResourceType.Location)
                    {

                        // Location:
                        if (item.locationNamesIndex < locationNames.Length)
                        {
                            item.locationFieldIndex = Mathf.Clamp(item.locationFieldIndex, 0, locationFieldNames.Length - 1);
                            var locationName = locationNames[item.locationNamesIndex];
                            var locationFieldName = locationFieldNames[item.locationFieldIndex];
                            var fieldType = GetWizardLocationFieldType(item.locationFieldIndex);
                            AppendFormat(sb, "Location", locationName, locationFieldName, fieldType, item);
                        }
                    }
                    else if (item.resourceType == ScriptWizardResourceType.SimStatus)
                    {

                        // SimStatus:
                        sb.AppendFormat("Dialog[{0}].SimStatus = \"{1}\"", item.simStatusID, item.simStatusType);
                    }
                    else if (item.resourceType == ScriptWizardResourceType.Alert)
                    {

                        // Custom:
                        sb.Append("ShowAlert(\"" + item.stringValue.Replace("\"", "\\\"") + "\")");
                    }

                    else if (item.resourceType == ScriptWizardResourceType.Misc)
                    {

                        // Custom:
                        ValidateCustomLuaFunctionInfo();
                        if (0 <= item.customLuaFuncIndex && item.customLuaFuncIndex < builtinLuaFuncs.Length)
                        {
                            var luaFuncRecord = builtinLuaFuncs[item.customLuaFuncIndex];
                            sb.Append(Tools.GetAllAfterSlashes(luaFuncRecord.functionName) + "(");
                            if (item.customParamValues == null) InitCustomParamValues(luaFuncRecord, out item.customParamValues);
                            for (int p = 0; p < luaFuncRecord.parameters.Length; p++)
                            {
                                if (p > 0) sb.Append(", ");
                                switch (luaFuncRecord.parameters[p])
                                {
                                    case CustomLuaParameterType.Bool:
                                        sb.Append(((BooleanType)item.customParamValues[p] == BooleanType.True) ? "true" : "false");
                                        break;
                                    case CustomLuaParameterType.Double:
                                        sb.Append(((float)item.customParamValues[p]).ToString(System.Globalization.CultureInfo.InvariantCulture));
                                        break;
                                    case CustomLuaParameterType.String:
                                        sb.Append("\"" + (string)item.customParamValues[p] + "\"");
                                        break;
                                    case CustomLuaParameterType.Actor:
                                        var actorIndex = (int)item.customParamValues[p];
                                        sb.Append((0 <= actorIndex && actorIndex < actorNames.Length) ? ("\"" + actorNames[actorIndex] + "\"") : "\"\"");
                                        break;
                                    case CustomLuaParameterType.Quest:
                                        var questIndex = (int)item.customParamValues[p];
                                        sb.Append((0 <= questIndex && questIndex < questNames.Length) ? ("\"" + questNames[questIndex] + "\"") : "\"\"");
                                        break;
                                    case CustomLuaParameterType.QuestEntry:
                                        sb.Append(((int)item.customParamValues[p] + 1).ToString());
                                        break;
                                    case CustomLuaParameterType.Variable:
                                        var variableIndex = (int)item.customParamValues[p];
                                        sb.Append((0 <= variableIndex && variableIndex < variableNames.Length) ? ("Variable[\"" + DialogueLua.StringToTableIndex(variableNames[variableIndex]) + "\"]") : "\"\"");
                                        break;
                                    case CustomLuaParameterType.VariableName:
                                        var variableNameIndex = (int)item.customParamValues[p];
                                        sb.Append((0 <= variableNameIndex && variableNameIndex < variableNames.Length) ? ("\"" + variableNames[variableNameIndex] + "\"") : "\"\"");
                                        break;
                                    case CustomLuaParameterType.Item:
                                        var itemIndex = (int)item.customParamValues[p];
                                        sb.Append((0 <= itemIndex && itemIndex < itemNames.Length) ? ("\"" + itemNames[itemIndex] + "\"") : "\"\"");
                                        break;
                                    case CustomLuaParameterType.QuestState:
                                        var tempQuestState = (item.customParamValues[p] == null || item.customParamValues[p].GetType() != typeof(QuestState))
                                            ? QuestState.Unassigned : ((QuestState)(item.customParamValues[p]));
                                        sb.Append("\"" + QuestLog.StateToString(tempQuestState) + "\"");
                                        break;
                                }
                            }
                            sb.Append(")");
                        }
                    }

                    else if (item.resourceType == ScriptWizardResourceType.Custom)
                    {

                        // Custom:
                        ValidateCustomLuaFunctionInfo();
                        if (0 <= item.customLuaFuncIndex && item.customLuaFuncIndex < customLuaFuncs.Length)
                        {
                            var luaFuncRecord = customLuaFuncs[item.customLuaFuncIndex];
                            sb.Append(Tools.GetAllAfterSlashes(luaFuncRecord.functionName) + "(");
                            if (item.customParamValues == null) InitCustomParamValues(luaFuncRecord, out item.customParamValues);
                            for (int p = 0; p < luaFuncRecord.parameters.Length; p++)
                            {
                                if (p > 0) sb.Append(", ");
                                switch (luaFuncRecord.parameters[p])
                                {
                                    case CustomLuaParameterType.Bool:
                                        sb.Append(((BooleanType)item.customParamValues[p] == BooleanType.True) ? "true" : "false");
                                        break;
                                    case CustomLuaParameterType.Double:
                                        sb.Append(((float)item.customParamValues[p]).ToString(System.Globalization.CultureInfo.InvariantCulture));
                                        break;
                                    case CustomLuaParameterType.String:
                                        sb.Append("\"" + (string)item.customParamValues[p] + "\"");
                                        break;
                                    case CustomLuaParameterType.Actor:
                                        var actorIndex = (int)item.customParamValues[p];
                                        sb.Append((0 <= actorIndex && actorIndex < actorNames.Length) ? ("\"" + actorNames[actorIndex] + "\"") : "\"\"");
                                        break;
                                    case CustomLuaParameterType.Quest:
                                        var questIndex = (int)item.customParamValues[p];
                                        sb.Append((0 <= questIndex && questIndex < questNames.Length) ? ("\"" + questNames[questIndex] + "\"") : "\"\"");
                                        break;
                                    case CustomLuaParameterType.QuestEntry:
                                        sb.Append(((int)item.customParamValues[p] + 1).ToString());
                                        break;
                                    case CustomLuaParameterType.Variable:
                                        var variableIndex = (int)item.customParamValues[p];
                                        var value = (0 <= variableIndex && variableIndex < variableNames.Length) ? ("Variable[\"" + DialogueLua.StringToTableIndex(variableNames[variableIndex]) + "\"]") : "\"\"";
                                        if (variableIndex == 0)
                                        {
                                            value = "Variable[\"" + DialogueLua.StringToTableIndex(item.newVariableName) + "\"]";
                                        }
                                        sb.Append(value);
                                        break;
                                    case CustomLuaParameterType.VariableName:
                                        var variableNameIndex = (int)item.customParamValues[p];
                                        var variableNameValue = (0 <= variableNameIndex && variableNameIndex < variableNames.Length) ? ("\"" + variableNames[variableNameIndex] + "\"") : "\"\"";
                                        if (variableNameIndex == 0)
                                        {
                                            variableNameValue = "\"" + item.newVariableName + "\"";
                                        }
                                        sb.Append(variableNameValue);
                                        break;
                                    case CustomLuaParameterType.Item:
                                        var itemIndex = (int)item.customParamValues[p];
                                        sb.Append((0 <= itemIndex && itemIndex < itemNames.Length) ? ("\"" + itemNames[itemIndex] + "\"") : "\"\"");
                                        break;
                                    case CustomLuaParameterType.QuestState:
                                        var tempQuestState = (item.customParamValues[p] == null || item.customParamValues[p].GetType() != typeof(QuestState))
                                            ? QuestState.Unassigned : ((QuestState)(item.customParamValues[p]));
                                        sb.Append("\"" + QuestLog.StateToString(tempQuestState) + "\"");
                                        break;
                                }
                            }
                            sb.Append(")");
                        }
                    }

                    else if (item.resourceType == ScriptWizardResourceType.ManualEnter)
                    {

                        // Manual enter:
                        sb.Append(item.stringValue);
                    }

                    if (i < (scriptItems.Count - 1)) sb.AppendFormat(endText);
                }
                return sb.ToString();
            }
            catch (Exception e)
            {
                Debug.LogError(string.Format("{0}: Internal error building script: {1}", DialogueDebug.Prefix, e.Message));
                return savedLuaCode;
            }
        }

        private void AppendFormat(StringBuilder sb, string tableName, string elementName, string fieldName, FieldType fieldType, ScriptItem item)
        {
            switch (fieldType)
            {
                case FieldType.Boolean:
                    sb.AppendFormat("{0}[\"{1}\"].{2} = {3}",
                                    tableName,
                                    DialogueLua.StringToTableIndex(elementName),
                                    DialogueLua.StringToFieldName(fieldName),
                                    (item.booleanValue == BooleanType.True) ? "true" : "false");
                    break;
                case FieldType.Number:
                    switch (item.valueSetMode)
                    {
                        case ValueSetMode.To:
                            sb.AppendFormat("{0}[\"{1}\"].{2} = {3}",
                                            tableName,
                                            DialogueLua.StringToTableIndex(elementName),
                                            DialogueLua.StringToFieldName(fieldName),
                                            item.floatValue.ToString(System.Globalization.CultureInfo.InvariantCulture));
                            break;
                        case ValueSetMode.Add:
                            sb.AppendFormat("{0}[\"{1}\"].{2} = {0}[\"{1}\"].{2} + {3}",
                                            tableName,
                                            DialogueLua.StringToTableIndex(elementName),
                                            DialogueLua.StringToFieldName(fieldName),
                                            item.floatValue.ToString(System.Globalization.CultureInfo.InvariantCulture));
                            break;
                        case ValueSetMode.Subtract:
                            sb.AppendFormat("{0}[\"{1}\"].{2} = {0}[\"{1}\"].{2} - {3}",
                                            tableName,
                                            DialogueLua.StringToTableIndex(elementName),
                                            DialogueLua.StringToFieldName(fieldName),
                                            item.floatValue.ToString(System.Globalization.CultureInfo.InvariantCulture));
                            break;
                    }
                    break;
                default:
                    sb.AppendFormat("{0}[\"{1}\"].{2} = \"{3}\"",
                                    tableName,
                                    DialogueLua.StringToTableIndex(elementName),
                                    DialogueLua.StringToFieldName(fieldName),
                                    item.stringValue.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    break;
            }
        }

    }

}
