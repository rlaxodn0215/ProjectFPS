// Copyright (c) Pixel Crushers. All rights reserved.

#if UNITY_2022_1_OR_NEWER

using System;
using UnityEngine.UIElements;

namespace PixelCrushers.DialogueSystem.DialogueEditor
{

    /// <summary>
    /// UI Toolkit Conditions wizard. Each row edits a LuaConditionWizardModel.ConditionItem,
    /// with the same choices as the IMGUI LuaConditionWizard. The model generates the Lua.
    /// </summary>
    public class LuaConditionWizardElement : LuaWizardElement
    {

        private readonly LuaConditionWizardModel model;

        public override int ItemCount { get { return model.conditionItems.Count; } }

        public LuaConditionWizardElement(DialogueDatabase database, string luaCode, Action<string, FieldType> addVariable) : base(database)
        {
            model = new LuaConditionWizardModel(database);
            model.addVariableHandler = addVariable;
            model.savedLuaCode = luaCode ?? string.Empty;
            model.append = true;
            model.RefreshWizardResources();
            model.ValidateCustomLuaFunctionInfo();

            footer.Add(CreateEnumPopup(model.conditionsLogicalOperator, v => model.conditionsLogicalOperator = v, NotifyChanged));
            footer.Add(CreateLabel("must be true."));
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
            var item = new LuaConditionWizardModel.ConditionItem();
            model.conditionItems.Add(item);
            var row = CreateRow();
            itemsContainer.Add(row);
            BuildRow(row, item);
        }

        private void BuildRow(VisualElement row, LuaConditionWizardModel.ConditionItem item)
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

            row.Add(CreateEnumPopup(item.conditionType, v =>
            {
                item.conditionType = v;
                item.conditionsQuestEntryNames = new string[0];
            }, rebuild, 90));

            switch (item.conditionType)
            {
                case LuaWizardBase.ConditionWizardResourceType.Quest:
                    row.Add(CreateIndexPopup(model.questNames, item.questNamesIndex, v => item.questNamesIndex = v, after));
                    row.Add(CreateEnumPopup(item.equalityType, v => item.equalityType = v, after));
                    row.Add(CreateQuestStatePopup(item.questState, v => item.questState = v, after));
                    break;

                case LuaWizardBase.ConditionWizardResourceType.QuestEntry:
                    row.Add(CreateIndexPopup(model.complexQuestNames, item.questNamesIndex, v =>
                    {
                        item.questNamesIndex = v;
                        item.conditionsQuestEntryNames = new string[0];
                    }, rebuild));
                    row.Add(CreateIndexPopup(GetQuestEntryNames(item), item.questEntryIndex, v => item.questEntryIndex = v, after));
                    row.Add(CreateEnumPopup(item.equalityType, v => item.equalityType = v, after));
                    row.Add(CreateQuestStatePopup(item.questState, v => item.questState = v, after));
                    break;

                case LuaWizardBase.ConditionWizardResourceType.Variable:
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
                    AddRightHand(row, item, variableType, null, after, rebuild);
                    break;

                case LuaWizardBase.ConditionWizardResourceType.Actor:
                    row.Add(CreateIndexPopup(model.actorNames, item.actorNamesIndex, v => item.actorNamesIndex = v, rebuild));
                    row.Add(CreateIndexPopup(model.actorFieldNames, item.actorFieldIndex, v => item.actorFieldIndex = v, rebuild));
                    AddRightHand(row, item, model.GetWizardActorFieldType(item.actorFieldIndex),
                        GetCustomFieldType(FindByName(database.actors, GetName(model.actorNames, item.actorNamesIndex)), GetName(model.actorFieldNames, item.actorFieldIndex)),
                        after, rebuild);
                    break;

                case LuaWizardBase.ConditionWizardResourceType.Item:
                    row.Add(CreateIndexPopup(model.itemNames, item.itemNamesIndex, v => item.itemNamesIndex = v, rebuild));
                    row.Add(CreateIndexPopup(model.itemFieldNames, item.itemFieldIndex, v => item.itemFieldIndex = v, rebuild));
                    AddRightHand(row, item, model.GetWizardItemFieldType(item.itemFieldIndex),
                        GetCustomFieldType(FindByName(database.items, GetName(model.itemNames, item.itemNamesIndex)), GetName(model.itemFieldNames, item.itemFieldIndex)),
                        after, rebuild);
                    break;

                case LuaWizardBase.ConditionWizardResourceType.Location:
                    row.Add(CreateIndexPopup(model.locationNames, item.locationNamesIndex, v => item.locationNamesIndex = v, rebuild));
                    row.Add(CreateIndexPopup(model.locationFieldNames, item.locationFieldIndex, v => item.locationFieldIndex = v, rebuild));
                    AddRightHand(row, item, model.GetWizardLocationFieldType(item.locationFieldIndex),
                        GetCustomFieldType(FindByName(database.locations, GetName(model.locationNames, item.locationNamesIndex)), GetName(model.locationFieldNames, item.locationFieldIndex)),
                        after, rebuild);
                    break;

                case LuaWizardBase.ConditionWizardResourceType.SimStatus:
                    row.Add(CreateToggle(item.simStatusThisID, "Tick to check this entry. Untick to check an entry by ID.", v => item.simStatusThisID = v, rebuild));
                    if (item.simStatusThisID) row.Add(CreateLabel("thisID"));
                    else row.Add(CreateIntegerField(item.simStatusID, v => item.simStatusID = v, after));
                    row.Add(CreateEnumPopup(item.equalityType, v => item.equalityType = v, after));
                    row.Add(CreateEnumPopup(item.simStatusType, v => item.simStatusType = v, after, 90));
                    break;

                case LuaWizardBase.ConditionWizardResourceType.Misc:
                    AddLuaFunction(row, item, model.builtinLuaFuncs, model.builtinLuaFuncNames, after, rebuild);
                    break;

                case LuaWizardBase.ConditionWizardResourceType.Custom:
                    AddLuaFunction(row, item, model.customLuaFuncs, model.customLuaFuncNames, after, rebuild);
                    break;

                case LuaWizardBase.ConditionWizardResourceType.ManualEnter:
                    row.Add(CreateTextField(item.stringValue, v => item.stringValue = v, after, 120));
                    break;
            }

            row.Add(CreateDeleteButton(() =>
            {
                model.conditionItems.Remove(item);
                row.RemoveFromHierarchy();
                NotifyChanged();
            }));
        }

        private string[] GetQuestEntryNames(LuaConditionWizardModel.ConditionItem item)
        {
            if ((item.conditionsQuestEntryNames.Length == 0) && (0 <= item.questNamesIndex && item.questNamesIndex < model.complexQuestNames.Length))
            {
                item.conditionsQuestEntryNames = model.GetQuestEntryNames(model.complexQuestNames[item.questNamesIndex]);
            }
            return item.conditionsQuestEntryNames;
        }

        private void AddNewVariableEditors(VisualElement row, LuaConditionWizardModel.ConditionItem item, Action rebuild)
        {
            var nameField = CreateTextField(item.newVariableName, v => item.newVariableName = v, NotifyChanged);
            nameField.tooltip = "Name of the new variable to add.";
            row.Add(nameField);
            row.Add(CreateEnumPopup(item.newVariableType, v => item.newVariableType = v, rebuild));
        }

        /// <summary>
        /// The comparison and value, like LuaConditionWizard.DrawRightHand.
        /// </summary>
        private void AddRightHand(VisualElement row, LuaConditionWizardModel.ConditionItem item, FieldType fieldType,
            CustomFieldType customFieldType, Action after, Action rebuild)
        {
            switch (fieldType)
            {
                case FieldType.Boolean:
                    row.Add(CreateEnumPopup(item.equalityType, v => item.equalityType = v, after));
                    row.Add(CreateEnumPopup(item.booleanValue, v => item.booleanValue = v, after));
                    break;
                case FieldType.Number:
                    row.Add(CreateEnumPopup(item.comparisonType, v => item.comparisonType = v, rebuild, 90));
                    row.Add(CreateFloatField(item.floatValue, v => item.floatValue = v, after));
                    if (item.comparisonType == ComparisonType.Between)
                    {
                        row.Add(CreateLabel("and"));
                        row.Add(CreateFloatField(item.floatValue2, v => item.floatValue2 = v, after));
                    }
                    break;
                default:
                    row.Add(CreateEnumPopup(item.equalityType, v => item.equalityType = v, after));
                    if (customFieldType != null) row.Add(CreateCustomFieldTypeCell(customFieldType, item.stringValue, v => item.stringValue = v, after));
                    else row.Add(CreateTextField(item.stringValue, v => item.stringValue = v, after));
                    break;
            }
        }

        private void AddLuaFunction(VisualElement row, LuaConditionWizardModel.ConditionItem item,
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
                    item.conditionsQuestEntryNames = new string[0];
                },
                parent => AddNewVariableEditors(parent, item, rebuild),
                after, rebuild);
            switch (record.returnValue)
            {
                case CustomLuaReturnType.Bool:
                    AddRightHand(row, item, FieldType.Boolean, null, after, rebuild);
                    break;
                case CustomLuaReturnType.Double:
                    AddRightHand(row, item, FieldType.Number, null, after, rebuild);
                    break;
                case CustomLuaReturnType.String:
                    AddRightHand(row, item, FieldType.Text, null, after, rebuild);
                    break;
            }
        }

    }

}

#endif
