// Copyright (c) Pixel Crushers. All rights reserved.

using UnityEngine;
using System;
using System.Collections.Generic;
using System.Text;

namespace PixelCrushers.DialogueSystem
{

    /// <summary>
    /// Model and Lua code generation for the Conditions wizard, shared by the IMGUI
    /// LuaConditionWizard and the Dialogue Database Editor Window's UI Toolkit wizard.
    /// </summary>
    public class LuaConditionWizardModel : LuaWizardBase
    {

        public class ConditionItem
        {
            public ConditionWizardResourceType conditionType = ConditionWizardResourceType.Quest;
            public int questNamesIndex = 0;
            public int questEntryIndex = 0;
            public int variableNamesIndex = 0;
            public int actorNamesIndex = 0;
            public int actorFieldIndex = 0;
            public int itemNamesIndex = 0;
            public int itemFieldIndex = 0;
            public int locationNamesIndex = 0;
            public int locationFieldIndex = 0;
            public bool simStatusThisID = true;
            public int simStatusID = 0;
            public int customLuaFuncIndex = 0;
            public SimStatusType simStatusType = SimStatusType.WasDisplayed;
            public EqualityType equalityType = EqualityType.Is;
            public ComparisonType comparisonType = ComparisonType.Is;
            [QuestState]
            public QuestState questState = QuestState.Unassigned;
            public string stringValue = string.Empty;
            public BooleanType booleanValue = BooleanType.True;
            public float floatValue = 0;
            public float floatValue2 = 0;
            public string[] conditionsQuestEntryNames = new string[0];
            public object[] customParamValues = null;
            public string newVariableName = string.Empty;
            public FieldType newVariableType = FieldType.Boolean;

            public ConditionItem()
            {
                conditionType = s_lastWizardResourceType;
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

        public List<ConditionItem> conditionItems = new List<ConditionItem>();
        public LogicalOperatorType conditionsLogicalOperator = LogicalOperatorType.All;
        public string savedLuaCode = string.Empty;
        public bool append = true;
        public CustomLuaFunctionInfoRecord[] customLuaFuncs = null;
        public CustomLuaFunctionInfoRecord[] builtinLuaFuncs = null;
        public string[] customLuaFuncNames = null;
        public string[] builtinLuaFuncNames = null;

        public LuaConditionWizardModel(DialogueDatabase database) : base(database)
        {
        }

        /// <summary>
        /// Remembers the item's choices so the next new item starts with them.
        /// </summary>
        public void RememberLastUsed(ConditionItem item)
        {
            s_lastWizardResourceType = item.conditionType;
            switch (item.conditionType)
            {
                case ConditionWizardResourceType.Quest:
                    s_lastQuestNamesIndex = item.questNamesIndex;
                    break;
                case ConditionWizardResourceType.QuestEntry:
                    s_lastQuestNamesIndex = item.questNamesIndex;
                    s_lastQuestEntryIndex = item.questEntryIndex;
                    break;
                case ConditionWizardResourceType.Variable:
                    s_lastVariableNameIndex = item.variableNamesIndex;
                    break;
                case ConditionWizardResourceType.Actor:
                    s_lastActorNamesIndex = item.actorNamesIndex;
                    s_lastActorFieldIndex = item.actorFieldIndex;
                    break;
                case ConditionWizardResourceType.Item:
                    s_lastItemNamesIndex = item.itemNamesIndex;
                    s_lastItemFieldIndex = item.itemFieldIndex;
                    break;
                case ConditionWizardResourceType.Location:
                    s_lastLocationNameIndex = item.locationNamesIndex;
                    s_lastLocationFieldIndex = item.locationFieldIndex;
                    break;
            }
        }

        public void AddNewVariables()
        {
            foreach (var item in conditionItems)
            {
                if (item.conditionType == ConditionWizardResourceType.Variable)
                {
                    AddNewVariable(item.newVariableName, item.newVariableType);
                }
                else if (item.conditionType == ConditionWizardResourceType.Custom)
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

        public void ValidateCustomLuaFunctionInfo()
        {
            if (customLuaFuncs == null)
            {
                FindAllCustomLuaFuncs(true,
                    out builtinLuaFuncs, out builtinLuaFuncNames,
                    out customLuaFuncs, out customLuaFuncNames);
            }
        }

        private string openParen = string.Empty;
        private string closeParen = string.Empty;

        public string BuildLua()
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                string logicalOperator = GetLogicalOperatorText(conditionsLogicalOperator);
                var needParens = (conditionItems.Count > 1) || (append && !string.IsNullOrEmpty(savedLuaCode));
                openParen = needParens ? "(" : string.Empty;
                closeParen = needParens ? ")" : string.Empty;
                if (append && !string.IsNullOrEmpty(savedLuaCode)) sb.AppendFormat("{0} {1} ", savedLuaCode, logicalOperator);
                bool first = true;
                foreach (ConditionItem item in conditionItems)
                {
                    if (!first) sb.AppendFormat(" {0} ", logicalOperator);
                    first = false;
                    if (item.conditionType == ConditionWizardResourceType.Quest)
                    {

                        // Quest:
                        string questName = GetWizardQuestName(questNames, item.questNamesIndex);
                        sb.AppendFormat("{0}CurrentQuestState(\"{1}\") {2} \"{3}\"{4}",
                                        openParen,
                                        questName,
                                        GetWizardEqualityText(item.equalityType),
                                        QuestLog.StateToString(item.questState),
                                        closeParen);

                    }
                    else if (item.conditionType == ConditionWizardResourceType.QuestEntry)
                    {

                        // Quest Entry:
                        string questName = GetWizardQuestName(complexQuestNames, item.questNamesIndex);
                        sb.AppendFormat("{0}CurrentQuestEntryState(\"{1}\", {2}) {3} \"{4}\"{5}",
                                        openParen,
                                        questName,
                                        item.questEntryIndex + 1,
                                        GetWizardEqualityText(item.equalityType),
                                        QuestLog.StateToString(item.questState),
                                        closeParen);

                    }
                    else if (item.conditionType == ConditionWizardResourceType.Variable)
                    {

                        // Variable:
                        item.variableNamesIndex = Mathf.Clamp(item.variableNamesIndex, 0, variableNames.Length - 1);
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
                                sb.AppendFormat("{0}Variable[\"{1}\"] {2} {3}{4}",
                                                openParen,
                                                DialogueLua.StringToTableIndex(variableName),
                                                GetWizardEqualityText(item.equalityType),
                                                (item.booleanValue == BooleanType.True) ? "true" : "false",
                                                closeParen);
                                break;
                            case FieldType.Number:
                                if (item.comparisonType == ComparisonType.Between)
                                {
                                    sb.AppendFormat("{0}{3} <= Variable[\"{1}\"] and Variable[\"{1}\"] <= {4}{5}",
                                                    openParen,
                                                    DialogueLua.StringToTableIndex(variableName),
                                                    GetWizardComparisonText(item.comparisonType),
                                                    item.floatValue.ToString(System.Globalization.CultureInfo.InvariantCulture),
                                                    item.floatValue2.ToString(System.Globalization.CultureInfo.InvariantCulture),
                                                    closeParen);
                                }
                                else
                                {
                                    sb.AppendFormat("{0}Variable[\"{1}\"] {2} {3}{4}",
                                                    openParen,
                                                    DialogueLua.StringToTableIndex(variableName),
                                                    GetWizardComparisonText(item.comparisonType),
                                                    item.floatValue.ToString(System.Globalization.CultureInfo.InvariantCulture),
                                                    closeParen);
                                }
                                break;
                            default:
                                sb.AppendFormat("{0}Variable[\"{1}\"] {2} \"{3}\"{4}",
                                                openParen,
                                                DialogueLua.StringToTableIndex(variableName),
                                                GetWizardEqualityText(item.equalityType),
                                                item.stringValue,
                                                closeParen);
                                break;
                        }

                    }
                    else if (item.conditionType == ConditionWizardResourceType.Actor)
                    {

                        // Actor:
                        if (item.actorNamesIndex < actorNames.Length)
                        {
                            item.actorNamesIndex = Mathf.Clamp(item.actorNamesIndex, 0, actorNames.Length - 1);
                            item.actorFieldIndex = Mathf.Clamp(item.actorFieldIndex, 0, actorFieldNames.Length - 1);
                            var actorName = actorNames[item.actorNamesIndex];
                            var actorField = actorFieldNames[item.actorFieldIndex];
                            var actorFieldType = GetWizardActorFieldType(item.actorFieldIndex);
                            AppendFormat(sb, "Actor", actorName, actorField, actorFieldType, item);
                        }
                        else
                        {
                            sb.Append("(true)");
                        }

                    }
                    else if (item.conditionType == ConditionWizardResourceType.Item)
                    {

                        // Item:
                        if (item.itemNamesIndex < itemNames.Length)
                        {
                            item.itemNamesIndex = Mathf.Clamp(item.itemNamesIndex, 0, itemNames.Length - 1);
                            item.itemFieldIndex = Mathf.Clamp(item.itemFieldIndex, 0, itemFieldNames.Length - 1);
                            var itemName = itemNames[item.itemNamesIndex];
                            var itemField = itemFieldNames[item.itemFieldIndex];
                            var itemFieldType = GetWizardItemFieldType(item.itemFieldIndex);
                            AppendFormat(sb, "Item", itemName, itemField, itemFieldType, item);
                        }
                        else
                        {
                            sb.Append("(true)");
                        }

                    }
                    else if (item.conditionType == ConditionWizardResourceType.Location)
                    {

                        // Location:
                        if (item.locationNamesIndex < locationNames.Length)
                        {
                            item.locationNamesIndex = Mathf.Clamp(item.locationNamesIndex, 0, locationNames.Length - 1);
                            item.locationFieldIndex = Mathf.Clamp(item.locationFieldIndex, 0, locationFieldNames.Length - 1);
                            var locationName = locationNames[item.locationNamesIndex];
                            var locationField = locationFieldNames[item.locationFieldIndex];
                            var locationFieldType = GetWizardLocationFieldType(item.locationFieldIndex);
                            AppendFormat(sb, "Location", locationName, locationField, locationFieldType, item);
                        }
                        else
                        {
                            sb.Append("(true)");
                        }

                    }
                    else if (item.conditionType == ConditionWizardResourceType.SimStatus)
                    {

                        // SimStatus:
                        string simStatusID = item.simStatusThisID ? "thisID" : item.simStatusID.ToString();
                        sb.AppendFormat("{0}Dialog[{1}].SimStatus {2} \"{3}\"{4}",
                                        openParen,
                                        simStatusID,
                                        GetWizardEqualityText(item.equalityType),
                                        item.simStatusType,
                                        closeParen);

                    }
                    else if (item.conditionType == ConditionWizardResourceType.Misc)
                    {

                        // Custom:
                        ValidateCustomLuaFunctionInfo();

                        if (0 <= item.customLuaFuncIndex && item.customLuaFuncIndex < builtinLuaFuncs.Length)
                        {
                            sb.Append(openParen);
                            var luaFuncRecord = builtinLuaFuncs[item.customLuaFuncIndex];
                            sb.Append(Tools.GetAllAfterSlashes(luaFuncRecord.functionName) + "(");
                            if (item.customParamValues == null) InitCustomParamValues(luaFuncRecord, out item.customParamValues);
                            for (int p = 0; p < luaFuncRecord.parameters.Length; p++)
                            {
                                if (p > 0) sb.Append(", ");
                                switch (luaFuncRecord.parameters[p])
                                {
                                    case CustomLuaParameterType.Bool:
                                        if (item.customParamValues[p] == null) item.customParamValues[p] = BooleanType.True;
                                        sb.Append(((BooleanType)item.customParamValues[p] == BooleanType.True) ? "true" : "false");
                                        break;
                                    case CustomLuaParameterType.Double:
                                        if (item.customParamValues[p] == null) item.customParamValues[p] = (float)0;
                                        sb.Append(((float)item.customParamValues[p]).ToString(System.Globalization.CultureInfo.InvariantCulture));
                                        break;
                                    case CustomLuaParameterType.String:
                                        if (item.customParamValues[p] == null) item.customParamValues[p] = string.Empty;
                                        sb.Append("\"" + (string)item.customParamValues[p] + "\"");
                                        break;
                                    case CustomLuaParameterType.Actor:
                                        if (item.customParamValues[p] == null) item.customParamValues[p] = (int)0;
                                        var actorIndex = (int)item.customParamValues[p];
                                        sb.Append((0 <= actorIndex && actorIndex < actorNames.Length) ? ("\"" + actorNames[actorIndex] + "\"") : "\"\"");
                                        break;
                                    case CustomLuaParameterType.Quest:
                                        if (item.customParamValues[p] == null) item.customParamValues[p] = (int)0;
                                        var questIndex = (int)item.customParamValues[p];
                                        sb.Append((0 <= questIndex && questIndex < questNames.Length) ? ("\"" + questNames[questIndex] + "\"") : "\"\"");
                                        break;
                                    case CustomLuaParameterType.QuestEntry:
                                        if (item.customParamValues[p] == null) item.customParamValues[p] = (int)0;
                                        sb.Append(((int)item.customParamValues[p] + 1).ToString());
                                        break;
                                    case CustomLuaParameterType.Variable:
                                        if (item.customParamValues[p] == null) item.customParamValues[p] = (int)0;
                                        var variableIndex = (int)item.customParamValues[p];
                                        var value = (0 <= variableIndex && variableIndex < variableNames.Length) ? ("Variable[\"" + DialogueLua.StringToTableIndex(variableNames[variableIndex]) + "\"]") : "\"\"";
                                        if (variableIndex == 0)
                                        {
                                            value = "Variable[\"" + DialogueLua.StringToTableIndex(item.newVariableName) + "\"]";
                                        }
                                        sb.Append(value);
                                        break;
                                    case CustomLuaParameterType.VariableName:
                                        if (item.customParamValues[p] == null) item.customParamValues[p] = (int)0;
                                        var variableNameIndex = (int)item.customParamValues[p];
                                        var variableNameValue = (0 <= variableNameIndex && variableNameIndex < variableNames.Length) ? ("\"" + variableNames[variableNameIndex] + "\"") : "\"\"";
                                        if (variableNameIndex == 0)
                                        {
                                            variableNameValue = "\"" + item.newVariableName + "\"";
                                        }
                                        sb.Append(variableNameValue);
                                        break;
                                    case CustomLuaParameterType.Item:
                                        if (item.customParamValues[p] == null) item.customParamValues[p] = (int)0;
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
                            sb.Append(") ");
                            switch (luaFuncRecord.returnValue)
                            {
                                case CustomLuaReturnType.Bool:
                                    sb.Append(((item.equalityType == EqualityType.Is) ? " == " : " ~= ") +
                                        ((item.booleanValue == BooleanType.True) ? "true" : "false"));
                                    break;
                                case CustomLuaReturnType.Double:
                                    sb.Append(GetWizardComparisonText(item.comparisonType) + " " + item.floatValue.ToString(System.Globalization.CultureInfo.InvariantCulture));
                                    break;
                                case CustomLuaReturnType.String:
                                    sb.Append(((item.equalityType == EqualityType.Is) ? " == " : " ~= ") +
                                        "\"" + item.stringValue + "\"");
                                    break;
                            }
                            sb.Append(closeParen);
                        }
                    }
                    else if (item.conditionType == ConditionWizardResourceType.Custom)
                    {

                        // Custom:
                        ValidateCustomLuaFunctionInfo();

                        if (0 <= item.customLuaFuncIndex && item.customLuaFuncIndex < customLuaFuncs.Length)
                        {
                            sb.Append(openParen);
                            var luaFuncRecord = customLuaFuncs[item.customLuaFuncIndex];
                            sb.Append(Tools.GetAllAfterSlashes(luaFuncRecord.functionName) + "(");
                            if (item.customParamValues == null) InitCustomParamValues(luaFuncRecord, out item.customParamValues);
                            for (int p = 0; p < luaFuncRecord.parameters.Length; p++)
                            {
                                if (p > 0) sb.Append(", ");
                                switch (luaFuncRecord.parameters[p])
                                {
                                    case CustomLuaParameterType.Bool:
                                        if (item.customParamValues[p] == null) item.customParamValues[p] = BooleanType.True;
                                        sb.Append(((BooleanType)item.customParamValues[p] == BooleanType.True) ? "true" : "false");
                                        break;
                                    case CustomLuaParameterType.Double:
                                        if (item.customParamValues[p] == null) item.customParamValues[p] = (float)0;
                                        sb.Append(((float)item.customParamValues[p]).ToString(System.Globalization.CultureInfo.InvariantCulture));
                                        break;
                                    case CustomLuaParameterType.String:
                                        if (item.customParamValues[p] == null) item.customParamValues[p] = string.Empty;
                                        sb.Append("\"" + (string)item.customParamValues[p] + "\"");
                                        break;
                                    case CustomLuaParameterType.Actor:
                                        if (item.customParamValues[p] == null) item.customParamValues[p] = (int)0;
                                        var actorIndex = (int)item.customParamValues[p];
                                        sb.Append((0 <= actorIndex && actorIndex < actorNames.Length) ? ("\"" + actorNames[actorIndex] + "\"") : "\"\"");
                                        break;
                                    case CustomLuaParameterType.Quest:
                                        if (item.customParamValues[p] == null) item.customParamValues[p] = (int)0;
                                        var questIndex = (int)item.customParamValues[p];
                                        sb.Append((0 <= questIndex && questIndex < questNames.Length) ? ("\"" + questNames[questIndex] + "\"") : "\"\"");
                                        break;
                                    case CustomLuaParameterType.QuestEntry:
                                        if (item.customParamValues[p] == null) item.customParamValues[p] = (int)0;
                                        sb.Append(((int)item.customParamValues[p] + 1).ToString());
                                        break;
                                    case CustomLuaParameterType.Variable:
                                        if (item.customParamValues[p] == null) item.customParamValues[p] = (int)0;
                                        var variableIndex = (int)item.customParamValues[p];
                                        sb.Append((0 <= variableIndex && variableIndex < variableNames.Length) ? ("Variable[\"" + DialogueLua.StringToTableIndex(variableNames[variableIndex]) + "\"]") : "\"\"");
                                        break;
                                    case CustomLuaParameterType.VariableName:
                                        if (item.customParamValues[p] == null) item.customParamValues[p] = (int)0;
                                        var variableNameIndex = (int)item.customParamValues[p];
                                        sb.Append((0 <= variableNameIndex && variableNameIndex < variableNames.Length) ? ("\"" + variableNames[variableNameIndex] + "\"") : "\"\"");
                                        break;
                                    case CustomLuaParameterType.Item:
                                        if (item.customParamValues[p] == null) item.customParamValues[p] = (int)0;
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
                            sb.Append(") ");
                            switch (luaFuncRecord.returnValue)
                            {
                                case CustomLuaReturnType.Bool:
                                    sb.Append(((item.equalityType == EqualityType.Is) ? " == " : " ~= ") +
                                        ((item.booleanValue == BooleanType.True) ? "true" : "false"));
                                    break;
                                case CustomLuaReturnType.Double:
                                    sb.Append(GetWizardComparisonText(item.comparisonType) + " " + item.floatValue.ToString(System.Globalization.CultureInfo.InvariantCulture));
                                    break;
                                case CustomLuaReturnType.String:
                                    sb.Append(((item.equalityType == EqualityType.Is) ? " == " : " ~= ") +
                                        "\"" + item.stringValue + "\"");
                                    break;
                            }
                            sb.Append(closeParen);
                        }
                    }
                    else if (item.conditionType == ConditionWizardResourceType.ManualEnter)
                    {

                        // Manual Enter:
                        sb.AppendFormat("{0}{1}{2}", openParen, item.stringValue, closeParen);
                    }
                }
                return sb.ToString();
            }
            catch (Exception e)
            {
                Debug.LogError(string.Format("{0}: Internal error building condition: {1}", DialogueDebug.Prefix, e.Message));
                return savedLuaCode;
            }
        }

        private void AppendFormat(StringBuilder sb, string tableName, string elementName, string fieldName, FieldType fieldType, ConditionItem item)
        {
            switch (fieldType)
            {
                case FieldType.Boolean:
                    sb.AppendFormat("{0}{1}[\"{2}\"].{3} {4} {5}{6}",
                                    openParen,
                                    tableName,
                                    DialogueLua.StringToTableIndex(elementName),
                                    DialogueLua.StringToFieldName(fieldName),
                                    GetWizardEqualityText(item.equalityType),
                                    (item.booleanValue == BooleanType.True) ? "true" : "false",
                                    closeParen);
                    break;
                case FieldType.Number:
                    if (item.comparisonType == ComparisonType.Between)
                    {
                        sb.AppendFormat("{0}{5} <= {1}[\"{2}\"].{3} and {1}[\"{2}\"].{3} <= {6}{7}",
                                        openParen,
                                        tableName,
                                        DialogueLua.StringToTableIndex(elementName),
                                        DialogueLua.StringToFieldName(fieldName),
                                        GetWizardComparisonText(item.comparisonType),
                                        item.floatValue.ToString(System.Globalization.CultureInfo.InvariantCulture),
                                        item.floatValue2.ToString(System.Globalization.CultureInfo.InvariantCulture),
                                        closeParen);
                    }
                    else
                    {
                        sb.AppendFormat("{0}{1}[\"{2}\"].{3} {4} {5}{6}",
                                        openParen,
                                        tableName,
                                        DialogueLua.StringToTableIndex(elementName),
                                        DialogueLua.StringToFieldName(fieldName),
                                        GetWizardComparisonText(item.comparisonType),
                                        item.floatValue.ToString(System.Globalization.CultureInfo.InvariantCulture),
                                        closeParen);
                    }
                    break;
                default:
                    sb.AppendFormat("{0}{1}[\"{2}\"].{3} {4} \"{5}\"{6}",
                                    openParen,
                                    tableName,
                                    DialogueLua.StringToTableIndex(elementName),
                                    DialogueLua.StringToFieldName(fieldName),
                                    GetWizardEqualityText(item.equalityType),
                                    item.stringValue,
                                    closeParen);
                    break;
            }
        }

    }

}
