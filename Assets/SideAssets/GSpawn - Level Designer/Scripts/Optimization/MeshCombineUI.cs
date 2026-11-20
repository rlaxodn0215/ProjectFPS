#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

namespace GSPAWN
{
    public class MeshCombineUI : PluginUI
    {
        public static MeshCombineUI instance { get { return MeshCombineSettings.instance.ui; } }

        protected override void onBuild()
        {
            contentContainer.style.marginLeft       = UIValues.settingsMarginLeft;
            contentContainer.style.marginTop        = UIValues.settingsMarginTop;
            contentContainer.style.marginRight      = UIValues.settingsMarginRight;
            contentContainer.style.marginBottom     = UIValues.settingsMarginBottom;
            contentContainer.style.flexGrow         = 1.0f;
            var scrollView                          = new ScrollView(ScrollViewMode.Vertical);
            contentContainer.Add(scrollView);

            var parent = scrollView;

            MeshCombineSettings.instance.buildUI(parent);

            UI.createRowSeparator(parent).style.flexGrow = 1.0f;

            IMGUIContainer gameObjectFields     = UI.createIMGUIContainer(parent);
            gameObjectFields.style.flexShrink   = 0.0f;
            gameObjectFields.onGUIHandler       = () => 
            {
                var guiContent      = new GUIContent();
                guiContent.text     = "Source parent";
                guiContent.tooltip  = "The game object whose children will participate in the mesh combine process.";

                EditorGUI.BeginChangeCheck();
                GameObject newObject = EditorGUILayout.ObjectField(guiContent, GSpawn.active.meshCombineSourceParent, typeof(GameObject), true) as GameObject;
                if (EditorGUI.EndChangeCheck())
                {
                    GSpawn.active.meshCombineSourceParent = newObject;
                }

                guiContent.text     = "Destination parent";
                guiContent.tooltip  = "The game object that will store the combined mesh objects.";
                EditorGUI.BeginChangeCheck();
                newObject = EditorGUILayout.ObjectField(guiContent, GSpawn.active.meshCombineDestinationParent, typeof(GameObject), true) as GameObject;
                if (EditorGUI.EndChangeCheck())
                {
                    GSpawn.active.meshCombineDestinationParent = newObject;
                }
            };

            UI.createRowSeparator(parent);

            var buttonRow                   = new VisualElement();
            parent.Add(buttonRow);
            buttonRow.style.flexShrink      = 0.0f;
            buttonRow.style.flexDirection   = FlexDirection.Row;

            const float actionBtnWidth      = 130.0f;
            var btn                         = new Button();
            buttonRow.Add(btn);
            btn.text                        = "Combine children";
            btn.tooltip                     = "Combines all child meshes that reside under the specified source parent.";
            btn.style.width                 = actionBtnWidth;
            btn.clicked += () => { MeshCombiner.combineChildren(GSpawn.active.meshCombineSourceParent, 
                GSpawn.active.meshCombineDestinationParent, MeshCombineSettings.instance); };

            btn                             = new Button();
            buttonRow.Add(btn);
            btn.text                        = "Combine selected";
            btn.tooltip                     = "Combines the meshes that are associated with the currently selected objects.";
            btn.style.width = actionBtnWidth;
            btn.clicked += () => 
            {
                var selectedObjects = new List<GameObject>();
                ObjectSelection.instance.getSelectedObjects(selectedObjects);
                MeshCombiner.combine(selectedObjects, GSpawn.active.meshCombineDestinationParent, MeshCombineSettings.instance);
            };

            // Preview uses exactly the same validation and material grouping as Combine,
            // but creates no scene objects or mesh assets and does not change Undo history.
            var previewRow = new VisualElement();
            parent.Add(previewRow);
            previewRow.style.flexShrink = 0.0f;
            previewRow.style.flexDirection = FlexDirection.Row;

            btn = new Button();
            previewRow.Add(btn);
            btn.text = "Preview children";
            btn.tooltip = "Show eligible sources, exclusions, and estimated combined geometry without making changes.";
            btn.style.width = actionBtnWidth;
            btn.clicked += () => MeshCombiner.previewChildren(GSpawn.active.meshCombineSourceParent,
                GSpawn.active.meshCombineDestinationParent, MeshCombineSettings.instance);

            btn = new Button();
            previewRow.Add(btn);
            btn.text = "Preview selected";
            btn.tooltip = "Preview the combine operation for the currently selected scene objects.";
            btn.style.width = actionBtnWidth;
            btn.clicked += () =>
            {
                var selectedObjects = new List<GameObject>();
                ObjectSelection.instance.getSelectedObjects(selectedObjects);
                MeshCombiner.preview(selectedObjects, GSpawn.active.meshCombineDestinationParent, MeshCombineSettings.instance);
            };
        }

        protected override void onRefresh()
        {
        }
    }
}
#endif