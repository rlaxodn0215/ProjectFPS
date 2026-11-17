// Copyright (c) Pixel Crushers. All rights reserved.

#if UNITY_2022_1_OR_NEWER

using System;
using UnityEngine.UIElements;

namespace PixelCrushers.DialogueSystem.DialogueEditor
{

    /// <summary>
    /// UI Toolkit Script wizard. Each row edits a LuaScriptWizardModel.ScriptItem, with
    /// the same choices as the IMGUI LuaScriptWizard. The model generates the Lua.
    /// </summary>
    public class LuaScriptWizardElement : LuaWizardElement
    {

        private readonly LuaScriptWizardModel model;

        public override int ItemCount { get { return model.scriptItems.Count; } }

        public LuaScriptWizardElement(DialogueDatabase database, string luaCode, Action<string, FieldType> addVariable) : base(database)
        {
            model = new LuaScriptWizardModel(database);
            model.addVariableHandler = addVariable;
            model.savedLuaCode = luaCode ?? string.Empty;
            model.append = true;
            model.RefreshWizardResources();
            model.ValidateCustomLuaFunctionInfo();

            AddAppendToggle(model.append, v => model.append = v);

            AddItem();
        }

        public override string BuildLua()
        {
            return model.BuildLua();
        }

        public override void AddNewVariables()
        {
            model.AddNewVariables();
        }

        protected override void AddItem()
        {
            var item = new LuaScriptWizardModel.ScriptItem();
            model.scriptItems.Add(item);
            var row = CreateRow();
            itemsContainer.Add(row);
            BuildRow(row, item);
        }

        private void BuildRow(VisualElement row, LuaScriptWizardModel.ScriptItem item)
        {
            row.Clear();
            Action after = () =>
            {
                model.RememberLastUsed(item);
                NotifyChanged();
            };
            Action rebuild = () =>
            {
                after();
                BuildRow(row, item);
            };

#if USE_UNET
            if (item.resourceType == LuaWizardBase.ScriptWizardResourceType.Quest ||
                item.resourceType == LuaWizardBase.ScriptWizardResourceType.QuestEntry ||
                item.resourceType == LuaWizardBase.ScriptWizardResourceType.Variable)
            {
                row.Add(CreateEnumPopup(item.netSetMode, v => item.netSetMode = v, after));
            }
            else
            {
                item.netSetMode = LuaScriptWizardModel.NetSetMode.Set;
                row.Add(CreateLabel("Set"));
            }
#else
            row.Add(CreateLabel("Set"));
#endif
            row.Add(CreateEnumPopup(item.resourceType, v =>
            {
                item.resourceType = v;
                item.scriptQuestEntryNames = new string[0];
            }, rebuild, 90));

            switch (item.resourceType)
            {
                case LuaWizardBase.ScriptWizardResourceType.Quest:
                    row.Add(CreateIndexPopup(model.questNames, item.questNamesIndex, v => item.questNamesIndex = v, after));
                    row.Add(CreateLabel("to"));
                    row.Add(CreateQuestStatePopup(item.questState, v => item.questState = v, after));
                    break;

                case LuaWizardBase.ScriptWizardResourceType.QuestEntry:
                    row.Add(CreateIndexPopup(model.complexQuestNames, item.questNamesIndex, v =>
                    {
                        item.questNamesIndex = v;
                        item.scriptQuestEntryNames = new string[0];
                    }, rebuild));
                    row.Add(CreateIndexPopup(GetQuestEntryNames(item), item.questEntryIndex, v => item.questEntryIndex = v, after));
                    row.Add(CreateLabel("to"));
                    row.Add(CreateQuestStatePopup(item.questState, v => item.questState = v, after));
                    break;

                case LuaWizardBase.ScriptWizardResourceType.Variable:
                    row.Add(CreateVariablePopup(model, item.variableNamesIndex, v => item.variableNamesIndex = v, rebuild));
                    FieldType variableType;
                    if (item.variableNamesIndex == 0)
                    {
                        AddNewVariableEditors(row, item, rebuild);
                        variableType = item.newVariableType;
                    }
                    else
                    {
                        variableType = model.GetWizardVariableType(item.variableNamesIndex);
                    }
                    AddValue(row, item, variableType, null, after);
                    break;

                case LuaWizardBase.ScriptWizardResourceType.Actor:
                    row.Add(CreateIndexPopup(model.actorNames, item.actorNamesIndex, v => item.actorNamesIndex = v, rebuild));
                    row.Add(CreateIndexPopup(model.actorFieldNames, item.actorFieldIndex, v => item.actorFieldIndex = v, rebuild));
                    AddValue(row, item, model.GetWizardActorFieldType(item.actorFieldIndex),
                        GetCustomFieldType(FindByName(database.actors, GetName(model.actorNames, item.actorNamesIndex)), GetName(model.actorFieldNames, item.actorFieldIndex)),
                        after);
                    break;

                case LuaWizardBase.ScriptWizardResourceType.Item:
                    row.Add(CreateIndexPopup(model.itemNames, item.itemNamesIndex, v => item.itemNamesIndex = v, rebuild));
                    row.Add(CreateIndexPopup(model.itemFieldNames, item.itemFieldIndex, v => item.itemFieldIndex = v, rebuild));
                    AddValue(row, item, model.GetWizardItemFieldType(item.itemFieldIndex),
                        GetCustomFieldType(FindByName(database.items, GetName(model.itemNames, item.itemNamesIndex)), GetName(model.itemFieldNames, item.itemFieldIndex)),
                        after);
                    break;

                case LuaWizardBase.ScriptWizardResourceType.Location:
                    row.Add(CreateIndexPopup(model.locationNames, item.locationNamesIndex, v => item.locationNamesIndex = v, rebuild));
                    row.Add(CreateIndexPopup(model.locationFieldNames, item.locationFieldIndex, v => item.locationFieldIndex = v, rebuild));
                    AddValue(row, item, model.GetWizardLocationFieldType(item.locationFieldIndex),
                        GetCustomFieldType(FindByName(database.locations, GetName(model.locationNames, item.locationNamesIndex)), GetName(model.locationFieldNames, item.locationFieldIndex)),
                        after);
                    break;

                case LuaWizardBase.ScriptWizardResourceType.SimStatus:
                    row.Add(CreateIntegerField(item.simStatusID, v => item.simStatusID = v, after));
                    row.Add(CreateEnumPopup(item.simStatusType, v => item.simStatusType = v, after, 90));
                    break;

                case LuaWizardBase.ScriptWizardResourceType.Alert:
                    row.Add(CreateTextField(item.stringValue, v => item.stringValue = v, after, 120));
                    break;

                case LuaWizardBase.ScriptWizardResourceType.Misc:
                    AddLuaFunction(row, item, model.builtinLuaFuncs, model.builtinLuaFuncNames, after, rebuild);
                    break;

                case LuaWizardBase.ScriptWizardResourceType.Custom:
                    AddLuaFunction(row, item, model.customLuaFuncs, model.customLuaFuncNames, after, rebuild);
                    break;

                case LuaWizardBase.ScriptWizardResourceType.ManualEnter:
                    row.Add(CreateTextField(item.stringValue, v => item.stringValue = v, after, 120));
                    break;
            }

            row.Add(CreateDeleteButton(() =>
            {
                model.scriptItems.Remove(item);
                row.RemoveFromHierarchy();
                NotifyChanged();
            }));
        }

        private string[] GetQuestEntryNames(LuaScriptWizardModel.ScriptItem item)
        {
            if ((item.scriptQuestEntryNames.Length == 0) && (0 <= item.questNamesIndex && item.questNamesIndex < model.complexQuestNames.Length))
            {
                item.scriptQuestEntryNames = model.GetQuestEntryNames(model.complexQuestNames[item.questNamesIndex]);
            }
            return item.scriptQuestEntryNames;
        }

        private void AddNewVariableEditors(VisualElement row, LuaScriptWizardModel.ScriptItem item, Action rebuild)
        {
            var nameField = CreateTextField(item.newVariableName, v => item.newVariableName = v, NotifyChanged);
            nameField.tooltip = "Name of the new variable to add.";
            row.Add(nameField);
            row.Add(CreateEnumPopup(item.newVariableType, v => item.newVariableType = v, rebuild));
        }

        /// <summary>
        /// "to" (or To/Add/Subtract for numbers) and the value, like LuaScriptWizard.DrawValueSetMode.
        /// </summary>
        private void AddValue(VisualElement row, LuaScriptWizardModel.ScriptItem item, FieldType fieldType,
            CustomFieldType customFieldType, Action after)
        {
            if (fieldType == FieldType.Number) row.Add(CreateEnumPopup(item.valueSetMode, v => item.valueSetMode = v, after));
            else row.Add(CreateLabel("to"));
            switch (fieldType)
            {
                case FieldType.Boolean:
                    row.Add(CreateEnumPopup(item.booleanValue, v => item.booleanValue = v, after));
                    break;
                case FieldType.Number:
                    row.Add(CreateFloatField(item.floatValue, v => item.floatValue = v, after));
                    break;
                default:
                    if (customFieldType != null) row.Add(CreateCustomFieldTypeCell(customFieldType, item.stringValue, v => item.stringValue = v, after));
                    else row.Add(CreateTextField(item.stringValue, v => item.stringValue = v, after));
                    break;
            }
        }

        private void AddLuaFunction(VisualElement row, LuaScriptWizardModel.ScriptItem item,
            CustomLuaFunctionInfoRecord[] records, string[] names, Action after, Action rebuild)
        {
            row.Add(CreateIndexPopup(names, item.customLuaFuncIndex, v =>
            {
                item.customLuaFuncIndex = v;
                item.customParamValues = null;
            }, rebuild, 120));
            if (records == null || !(0 <= item.customLuaFuncIndex && item.customLuaFuncIndex < records.Length)) return;
            var record = records[item.customLuaFuncIndex];
            if (item.customParamValues == null) model.InitCustomParamValues(record, out item.customParamValues);
            AddLuaFunctionParameters(row, model, record, item.customParamValues, GetQuestEntryNames(item),
                questIndex =>
                {
                    item.questNamesIndex = questIndex;
                    item.scriptQuestEntryNames = new string[0];
                },
                parent => AddNewVariableEditors(parent, item, rebuild),
                after, rebuild);
        }

    }

}

#endif
