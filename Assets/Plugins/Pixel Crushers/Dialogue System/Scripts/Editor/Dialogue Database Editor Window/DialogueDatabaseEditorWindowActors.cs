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
    /// This part of the UI Toolkit dialogue database editor window holds the Actors
    /// panel's state so it survives domain reloads.
    /// </summary>
    public partial class DialogueDatabaseEditorWindow
    {

        [Serializable]
        public class ActorsPanelState : AssetPanelState
        {
            public bool texturesFoldout = false;
            public bool spritesFoldout = false;
        }

        [SerializeField]
        private ActorsPanelState actorsPanelState = new ActorsPanelState();
        public ActorsPanelState ActorsState { get { return actorsPanelState; } }

    }

    /// <summary>
    /// Actors category. Adds display name, portraits, node color, and Is Player to the
    /// common asset list panel.
    /// </summary>
    public class ActorsPanel : AssetListPanel<Actor>
    {

        private const string NodeColorFieldTitle = "NodeColor";

        public ActorsPanel() : base("Actors") { }

        protected override string AssetTypeName { get { return "Actor"; } }
        protected override string ListPropertyName { get { return "actors"; } }
        protected override DialogueDatabaseEditorWindow.AssetPanelState state { get { return window.ActorsState; } }
        private DialogueDatabaseEditorWindow.ActorsPanelState actorsState { get { return window.ActorsState; } }

        protected override List<Actor> GetAssets(DialogueDatabase database) { return database.actors; }
        protected override List<Field> GetTemplateFields(Template template, Actor actor) { return template.actorFields; }
        protected override List<string> GetPrimaryFieldTitles(Template template, Actor actor) { return template.actorPrimaryFieldTitles; }

        protected override bool IsSyncEnabled
        {
            get { return database.syncInfo.syncActors; }
            set { database.syncInfo.syncActors = value; }
        }

        protected override DialogueDatabase SyncDatabase
        {
            get { return database.syncInfo.syncActorsDatabase; }
            set { database.syncInfo.syncActorsDatabase = value; }
        }

        protected override void SyncFromDatabase() { database.SyncActors(); }

        protected override string DisconnectSyncMessage
        {
            get { return "Also delete synced actors from this database?\n(Will not delete ID 1, typically Player.)"; }
        }

        protected override bool ShouldDeleteOnDisconnect(Actor actor) { return actor.id != 1; }

        protected override void ResetNewAssetProperties(SerializedProperty actorProperty)
        {
            actorProperty.FindPropertyRelative("portrait").objectReferenceValue = null;
            actorProperty.FindPropertyRelative("spritePortrait").objectReferenceValue = null;
            actorProperty.FindPropertyRelative("alternatePortraits").ClearArray();
            actorProperty.FindPropertyRelative("spritePortraits").ClearArray();
        }

        protected override AIFieldTarget? GetAIFieldTarget(string fieldTitle)
        {
            return AIFieldTarget.ForActor(SelectedAssetID, fieldTitle);
        }

        protected override void AddAssetDetails(VisualElement parent, SerializedProperty actorProperty, SerializedProperty fieldsProperty)
        {
            AddDisplayNameFields(parent, fieldsProperty);
            var aiVoiceRow = window.CreateAIVoiceRow(SelectedAssetID);
            if (aiVoiceRow != null) parent.Add(aiVoiceRow);
            AddPortraitFoldouts(parent, actorProperty);
            AddNodeColorField(parent, fieldsProperty);
            parent.Add(CreateOptionalToggle("Is Player", "Tick if this actor is a player character.", fieldsProperty, DialogueSystemFields.IsPlayer));
            AddPrimaryFields(parent, fieldsProperty, true);
        }

        private void AddPortraitFoldouts(VisualElement parent, SerializedProperty actorProperty)
        {
            var spritesFoldout = new Foldout() { text = "Portrait Sprites", value = actorsState.spritesFoldout, tooltip = "Portrait images using sprite assets." };
            spritesFoldout.RegisterValueChangedCallback(evt => { if (evt.target == spritesFoldout) actorsState.spritesFoldout = evt.newValue; });
            spritesFoldout.Add(CreateObjectField<Sprite>("Portrait", actorProperty.FindPropertyRelative("spritePortrait"), "This actor's portrait ([pic=1]). Only necessary if your UI uses portraits."));
            spritesFoldout.Add(CreatePropertyField(actorProperty.FindPropertyRelative("spritePortraits"), "Alternate Portraits", "Alternate portraits. The first is [pic=2]."));
            var aiPortraitButton = window.CreateAIPortraitButton(SelectedAssetID);
            if (aiPortraitButton != null)
            {
                aiPortraitButton.style.alignSelf = Align.FlexEnd;
                spritesFoldout.Add(aiPortraitButton);
            }
            parent.Add(spritesFoldout);

            var texturesFoldout = new Foldout() { text = "Portrait Textures", value = actorsState.texturesFoldout, tooltip = "Portrait images using texture assets. (Portrait Sprites are generally preferred for UIs.)" };
            texturesFoldout.RegisterValueChangedCallback(evt => { if (evt.target == texturesFoldout) actorsState.texturesFoldout = evt.newValue; });
            texturesFoldout.Add(CreateObjectField<Texture2D>("Portrait", actorProperty.FindPropertyRelative("portrait"), "This actor's portrait ([pic=1]). Only necessary if your UI uses portraits."));
            texturesFoldout.Add(CreatePropertyField(actorProperty.FindPropertyRelative("alternatePortraits"), "Alternate Portraits", "Alternate portraits. The first is [pic=2]."));
            parent.Add(texturesFoldout);
        }

        private static ObjectField CreateObjectField<T>(string label, SerializedProperty property, string tooltip) where T : UnityEngine.Object
        {
            var objectField = new ObjectField(label) { objectType = typeof(T), allowSceneObjects = false, tooltip = tooltip };
            objectField.BindProperty(property);
            return objectField;
        }

        private static PropertyField CreatePropertyField(SerializedProperty property, string label, string tooltip)
        {
            var propertyField = new PropertyField(property, label) { tooltip = tooltip };
            propertyField.BindProperty(property);
            return propertyField;
        }

        private void AddNodeColorField(VisualElement parent, SerializedProperty fieldsProperty)
        {
            var nodeColorProperty = FieldPropertyUtility.FindField(fieldsProperty, NodeColorFieldTitle);
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            var toggle = new UnityEngine.UIElements.Toggle("Custom Node Color") { value = nodeColorProperty != null, tooltip = "Specify a custom color for this actor's conversation nodes in the editor." };
            toggle.RegisterValueChangedCallback(evt =>
            {
                serializedObject.Update();
                if (evt.newValue)
                {
                    var isPlayer = FieldPropertyUtility.StringToBool(FieldPropertyUtility.LookupValue(fieldsProperty, DialogueSystemFields.IsPlayer));
                    FieldPropertyUtility.SetFieldValue(fieldsProperty, NodeColorFieldTitle, isPlayer ? "Blue" : "Gray", FieldType.Text);
                }
                else
                {
                    FieldPropertyUtility.RemoveField(fieldsProperty, NodeColorFieldTitle);
                }
                DeferRebuildDetails();
            });
            row.Add(toggle);
            if (nodeColorProperty != null)
            {
                var valueProperty = nodeColorProperty.FindPropertyRelative(FieldPropertyUtility.ValuePropertyName);
                var colorField = new ColorField() { showAlpha = false, hdr = false, showEyeDropper = true };
                colorField.style.flexGrow = 1;
                colorField.SetValueWithoutNotify(EditorTools.NodeColorStringToColor(valueProperty.stringValue));
                colorField.RegisterValueChangedCallback(evt =>
                {
                    var color = evt.newValue;
                    color.a = 1; // Force solid alpha.
                    valueProperty.stringValue = Tools.ToWebColor(color);
                    serializedObject.ApplyModifiedProperties();
                });
                colorField.TrackPropertyValue(valueProperty, property => colorField.SetValueWithoutNotify(EditorTools.NodeColorStringToColor(property.stringValue)));
                row.Add(colorField);
            }
            parent.Add(row);
        }

    }

}

#endif
