#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace GSPAWN
{
    // Read-only presentation of the Mesh Combiner's existing eligibility and geometry estimates.
    // No data is written to the Console, Editor.log, or any project file.
    public sealed class MeshCombinePreviewWindow : PluginWindow
    {
        public sealed class ExclusionInfo
        {
            public string               reason;
            public string               remedy;
            public string               example;
            public int                  count;
            public bool                 expected;
        }

        public sealed class Report
        {
            public string               message;
            public string               detail;
            public int                  inputObjects;
            public int                  renderersFound;
            public int                  candidateRenderers;
            public int                  eligibleRenderers;
            public int                  materialGroups;
            public int                  lodRenderers;
            public int                  outputMeshes;
            public long                 inputVertices;
            public long                 outputVertices;
            public long                 triangles;
            public string               indexFormat;
            public bool                 spatialChunks;
            public float                spatialCellSize;
            public bool                 generateLightmapUVs;
            public List<ExclusionInfo>  exclusions = new List<ExclusionInfo>();
        }

        [NonSerialized]
        private Report                  _report;

        private static Color            backgroundColor     { get { return UIValues.isProSkin ? new Color32(29, 33, 39, 255) : new Color32(235, 240, 246, 255); } }
        private static Color            panelColor          { get { return UIValues.isProSkin ? new Color32(39, 44, 52, 255) : Color.white; } }
        private static Color            borderColor         { get { return UIValues.isProSkin ? new Color32(57, 67, 78, 255) : new Color32(199, 210, 224, 255); } }
        private static Color            primaryTextColor    { get { return UIValues.isProSkin ? new Color32(236, 240, 244, 255) : new Color32(31, 43, 55, 255); } }
        private static Color            secondaryTextColor  { get { return UIValues.isProSkin ? new Color32(165, 179, 194, 255) : new Color32(84, 101, 119, 255); } }
        private static Color            blueColor           { get { return UIValues.isProSkin ? new Color32(102, 178, 255, 255) : new Color32(25, 98, 175, 255); } }
        private static Color            greenColor          { get { return UIValues.isProSkin ? new Color32(126, 216, 172, 255) : new Color32(22, 132, 83, 255); } }
        private static Color            amberColor          { get { return UIValues.isProSkin ? new Color32(255, 199, 109, 255) : new Color32(159, 101, 21, 255); } }

        public static void showReport(Report report)
        {
            var window = PluginWindow.showUtility<MeshCombinePreviewWindow>("Mesh Combine Preview");
            window._report = report;
            window.rootVisualElement.Clear();
            window.onBuildUI();
            window.Repaint();
        }

        protected override void onEnabled()
        {
            minSize = new Vector2(560.0f, 430.0f);
            if (position.width < 560.0f || position.height < 430.0f)
                setSize(new Vector2(740.0f, 650.0f));
        }

        protected override void onBuildUI()
        {
            var root = rootVisualElement;
            root.style.backgroundColor = backgroundColor;
            root.style.flexGrow = 1.0f;

            _header = new UIHeader(root, TexturePool.instance.chemistry, UIValues.mediumHeaderIconSize);
            _header.title = "Mesh Combine Preview";

            var content = new ScrollView(ScrollViewMode.Vertical);
            content.style.flexGrow = 1.0f;
            root.Add(content);

            var body = new VisualElement();
            body.style.paddingLeft = 16.0f;
            body.style.paddingRight = 16.0f;
            body.style.paddingTop = 14.0f;
            body.style.paddingBottom = 16.0f;
            content.Add(body);

            addLabel(body, "READ-ONLY ANALYSIS", 10, blueColor, true);
            var report = _report;
            if (report == null)
            {
                addLabel(body, "No preview data available.", 13, secondaryTextColor, false);
            }
            else if (!string.IsNullOrEmpty(report.message))
            {
                addMessage(body, report.message, report.detail, amberColor);
            }
            else
            {
                buildReport(body, report);
            }

            var footer = new VisualElement();
            footer.style.flexDirection = FlexDirection.Row;
            footer.style.alignItems = Align.Center;
            footer.style.justifyContent = Justify.SpaceBetween;
            footer.style.paddingLeft = 16.0f;
            footer.style.paddingRight = 16.0f;
            footer.style.paddingTop = 9.0f;
            footer.style.paddingBottom = 9.0f;
            footer.style.borderTopWidth = 1.0f;
            footer.style.borderTopColor = borderColor;
            footer.style.flexShrink = 0.0f;
            root.Add(footer);

            addLabel(footer, "No scene changes · No assets generated · No logs", 11, secondaryTextColor, false);
            var closeButton = new Button(Close);
            closeButton.text = "Close";
            closeButton.style.minWidth = 90.0f;
            closeButton.style.height = 26.0f;
            footer.Add(closeButton);
        }

        protected override void onGUI()
        {
            if (_header != null)
                _header.backgroundColor = focusedWindow == this ? UIValues.focusedHeaderColor : UIValues.unfocusedHeaderColor;
        }

        private static void buildReport(VisualElement body, Report report)
        {
            int expectedCount = 0;
            int unexpectedCount = 0;
            foreach (var exclusion in report.exclusions)
            {
                if (exclusion.expected) expectedCount += exclusion.count;
                else unexpectedCount += exclusion.count;
            }

            bool hasEligible = report.eligibleRenderers != 0;
            bool hasOutput = report.outputMeshes != 0;
            string headline = !hasEligible ? "No eligible meshes found with the current settings" :
                (hasOutput ? "Eligible meshes found" : "Eligible sources have no combinable triangles");
            string subtitle = !hasEligible ? "Review the exclusions below or change the Mesh Combine settings." :
                (hasOutput ? "These estimates show what a Combine operation would produce. Nothing has been modified." :
                    "The accepted sources produced no triangle data. No combined mesh would be created.");
            addMessage(body, headline, subtitle, hasEligible && hasOutput ? greenColor : amberColor);

            addSectionTitle(body, "Source eligibility");
            var row = addCardRow(body);
            addMetric(row, "INPUT OBJECTS", report.inputObjects.ToString("N0"), secondaryTextColor);
            addMetric(row, "RENDERERS FOUND", report.renderersFound.ToString("N0"), secondaryTextColor);
            addMetric(row, "ELIGIBLE", report.eligibleRenderers.ToString("N0"), hasEligible ? greenColor : amberColor);

            if (report.renderersFound != 0)
            {
                addProportionBar(body, report.eligibleRenderers, report.renderersFound);
                if (report.candidateRenderers != report.eligibleRenderers)
                    addLabel(body, report.candidateRenderers.ToString("N0") + " passed the initial checks; " +
                        report.eligibleRenderers.ToString("N0") + " passed full mesh and material validation.",
                        11, secondaryTextColor, false);
            }

            if (hasEligible)
            {
                addSectionTitle(body, "Estimated output");
                row = addCardRow(body);
                addMetric(row, "OUTPUT MESH OBJECTS", report.outputMeshes.ToString("N0"), blueColor);
                addMetric(row, "MATERIAL / RENDERER GROUPS", report.materialGroups.ToString("N0"), secondaryTextColor);
                addMetric(row, "TRIANGLES", report.triangles.ToString("N0"), secondaryTextColor);
                row = addCardRow(body);
                addMetric(row, "SOURCE VERTICES", report.inputVertices.ToString("N0"), secondaryTextColor);
                addMetric(row, "OUTPUT VERTICES (EST.)", report.outputVertices.ToString("N0"), blueColor);

                var settingsPanel = addPanel(body);
                addLabel(settingsPanel, "OUTPUT SETTINGS", 10, secondaryTextColor, true);
                addDetailRow(settingsPanel, "Index format", report.indexFormat ?? "Unknown");
                addDetailRow(settingsPanel, "Spatial chunks", report.spatialChunks
                    ? "Enabled · " + report.spatialCellSize.ToString("0.##") + " unit cells" : "Disabled");
                addDetailRow(settingsPanel, "Generate lightmap UVs", report.generateLightmapUVs ? "Enabled" : "Disabled");

                if (report.outputMeshes >= report.eligibleRenderers)
                    addNote(body, "The estimated output count is not lower than the eligible renderer count. " +
                        "Review materials and spatial chunk size.", amberColor);
                else
                    addNote(body, "Combining may reduce renderer and draw-call overhead, but large combined " +
                        "meshes can reduce culling efficiency.", blueColor);
                if (report.generateLightmapUVs)
                    addNote(body, "Lightmap UV unwrapping can change the final vertex count.", secondaryTextColor);
                if (report.lodRenderers != 0)
                    addNote(body, report.lodRenderers.ToString("N0") + " eligible renderers belong to LODGroups. " +
                        "Combined outputs are standalone renderers: they will not inherit the source " +
                        "LODGroups' distance-based switching.", amberColor);
            }

            addSectionTitle(body, "Exclusions");
            if (report.exclusions.Count == 0)
                addNote(body, "All discovered MeshRenderers passed the current eligibility checks.", greenColor);
            else
            {
                if (expectedCount != 0)
                    addExclusionGroup(body, report.exclusions, true, "Filtered by settings", expectedCount,
                        "Expected exclusions, such as other LOD levels or disabled mesh categories.", blueColor);
                if (unexpectedCount != 0)
                    addExclusionGroup(body, report.exclusions, false, "Needs attention", unexpectedCount,
                        "These sources require changes before they can be included.", amberColor);
            }

            if (report.renderersFound == 0)
                addNote(body, "No MeshRenderers were found. Select scene objects containing MeshRenderers; " +
                    "SkinnedMeshRenderers are not supported.", amberColor);
        }

        private static void addExclusionGroup(VisualElement parent, List<ExclusionInfo> exclusions, bool expected,
            string title, int count, string description, Color accent)
        {
            var panel = addPanel(parent);
            var heading = new VisualElement();
            heading.style.flexDirection = FlexDirection.Row;
            heading.style.alignItems = Align.Center;
            heading.style.justifyContent = Justify.SpaceBetween;
            panel.Add(heading);
            addLabel(heading, title, 13, accent, true);
            addLabel(heading, count.ToString("N0") + " renderer(s)", 11, accent, true);
            addLabel(panel, description, 11, secondaryTextColor, false);

            foreach (var exclusion in exclusions)
            {
                if (exclusion.expected != expected) continue;
                var item = new VisualElement();
                item.style.marginTop = 10.0f;
                item.style.paddingLeft = 11.0f;
                item.style.borderLeftWidth = 3.0f;
                item.style.borderLeftColor = accent;
                panel.Add(item);

                addLabel(item, exclusion.count.ToString("N0") + " × " + exclusion.reason,
                    12, primaryTextColor, true);
                if (!string.IsNullOrEmpty(exclusion.example))
                    addLabel(item, "Example: " + exclusion.example, 11, secondaryTextColor, false);
                if (!string.IsNullOrEmpty(exclusion.remedy))
                    addLabel(item, exclusion.remedy, 11, accent, false);
            }
        }

        private static VisualElement addCardRow(VisualElement parent)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.Wrap;
            row.style.marginTop = 2.0f;
            parent.Add(row);
            return row;
        }

        private static void addMetric(VisualElement row, string label, string value, Color valueColor)
        {
            var card = new VisualElement();
            card.style.flexGrow = 1.0f;
            card.style.flexBasis = 0.0f;
            card.style.minWidth = 150.0f;
            card.style.paddingTop = 12.0f;
            card.style.paddingBottom = 12.0f;
            card.style.paddingLeft = 13.0f;
            card.style.paddingRight = 13.0f;
            card.style.marginRight = 7.0f;
            card.style.marginBottom = 7.0f;
            card.style.backgroundColor = panelColor;
            card.style.borderTopLeftRadius = 6.0f;
            card.style.borderTopRightRadius = 6.0f;
            card.style.borderBottomLeftRadius = 6.0f;
            card.style.borderBottomRightRadius = 6.0f;
            card.style.borderBottomWidth = 1.0f;
            card.style.borderBottomColor = borderColor;
            row.Add(card);
            addLabel(card, label, 10, secondaryTextColor, true);
            var metric = addLabel(card, value, 22, valueColor, true);
            metric.style.marginTop = 4.0f;
        }

        private static void addProportionBar(VisualElement parent, int eligible, int total)
        {
            var bar = new VisualElement();
            bar.style.height = 6.0f;
            bar.style.marginTop = 3.0f;
            bar.style.marginBottom = 9.0f;
            bar.style.backgroundColor = borderColor;
            bar.style.borderTopLeftRadius = 3.0f;
            bar.style.borderBottomLeftRadius = 3.0f;
            bar.style.borderTopRightRadius = 3.0f;
            bar.style.borderBottomRightRadius = 3.0f;
            parent.Add(bar);
            var fill = new VisualElement();
            fill.style.width = new Length(Mathf.Clamp01((float)eligible / total) * 100.0f, LengthUnit.Percent);
            fill.style.height = 6.0f;
            fill.style.backgroundColor = greenColor;
            fill.style.borderTopLeftRadius = 3.0f;
            fill.style.borderBottomLeftRadius = 3.0f;
            bar.Add(fill);
        }

        private static VisualElement addPanel(VisualElement parent)
        {
            var panel = new VisualElement();
            panel.style.marginTop = 5.0f;
            panel.style.marginBottom = 6.0f;
            panel.style.paddingTop = 12.0f;
            panel.style.paddingBottom = 12.0f;
            panel.style.paddingLeft = 14.0f;
            panel.style.paddingRight = 14.0f;
            panel.style.backgroundColor = panelColor;
            panel.style.borderBottomWidth = 1.0f;
            panel.style.borderBottomColor = borderColor;
            panel.style.borderTopLeftRadius = 6.0f;
            panel.style.borderTopRightRadius = 6.0f;
            panel.style.borderBottomLeftRadius = 6.0f;
            panel.style.borderBottomRightRadius = 6.0f;
            parent.Add(panel);
            return panel;
        }

        private static void addMessage(VisualElement parent, string heading, string detail, Color accent)
        {
            var panel = addPanel(parent);
            panel.style.borderLeftWidth = 3.0f;
            panel.style.borderLeftColor = accent;
            addLabel(panel, heading, 15, accent, true);
            if (!string.IsNullOrEmpty(detail))
            {
                var label = addLabel(panel, detail, 12, secondaryTextColor, false);
                label.style.marginTop = 5.0f;
            }
        }

        private static void addSectionTitle(VisualElement parent, string title)
        {
            var label = addLabel(parent, title, 13, primaryTextColor, true);
            label.style.marginTop = 15.0f;
            label.style.marginBottom = 7.0f;
        }

        private static void addDetailRow(VisualElement parent, string name, string value)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.justifyContent = Justify.SpaceBetween;
            row.style.marginTop = 6.0f;
            parent.Add(row);
            addLabel(row, name, 11, secondaryTextColor, false);
            addLabel(row, value, 11, primaryTextColor, true);
        }

        private static void addNote(VisualElement parent, string message, Color accent)
        {
            var text = addLabel(parent, message, 11, accent, false);
            text.style.marginTop = 6.0f;
            text.style.marginBottom = 5.0f;
        }

        private static Label addLabel(VisualElement parent, string content, int fontSize, Color color, bool bold)
        {
            var label = new Label(content);
            label.style.fontSize = fontSize;
            label.style.color = color;
            label.style.whiteSpace = WhiteSpace.Normal;
            if (bold) label.style.unityFontStyleAndWeight = FontStyle.Bold;
            parent.Add(label);
            return label;
        }
    }
}
#endif
