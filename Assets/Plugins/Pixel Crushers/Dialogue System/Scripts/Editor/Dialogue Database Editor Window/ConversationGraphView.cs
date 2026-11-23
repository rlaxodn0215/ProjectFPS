// Copyright (c) Pixel Crushers. All rights reserved.

#if UNITY_2022_1_OR_NEWER

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.Experimental.GraphView;

namespace PixelCrushers.DialogueSystem.DialogueEditor
{

    /// <summary>
    /// GraphView canvas for one conversation. It only handles presentation and user gestures;
    /// the owning panel turns graphViewChanged and the request callbacks into data changes.
    /// </summary>
    public class ConversationGraphView : GraphView
    {

        /// <summary>Raised once after the selection changes (batched per frame).</summary>
        public event Action selectionChanged;

        /// <summary>
        /// Asks the owner to create an entry at a content-space position, linked from the
        /// given parent node (or null for the owner's default parent).
        /// </summary>
        public Action<Vector2, DialogueEntryNode> createNodeRequested;

        /// <summary>Asks the owner to create an entry linked from a node, at the default child position.</summary>
        public Action<DialogueEntryNode> createChildRequested;

        /// <summary>Raised when a node is double-clicked.</summary>
        public Action<DialogueEntryNode> nodeDoubleClicked;

        /// <summary>Lets the owner add its own items to the node and canvas context menus.</summary>
        public Action<DropdownMenu> appendSharedMenuItems;

        /// <summary>
        /// Lets the owner add its panel Menu's items to the blank-canvas context menu. If set,
        /// it replaces the canvas menu's Arrange Nodes, Frame All and appendSharedMenuItems items.
        /// </summary>
        public Action<DropdownMenu> appendCanvasMenuItems;

        /// <summary>Lets the owner add its own items to a node's context menu, such as links to other conversations.</summary>
        public Action<DialogueEntryNode, DropdownMenu> appendNodeMenuItems;

        /// <summary>Asks the owner to arrange nodes, vertically if true or horizontally if false.</summary>
        public Action<bool> arrangeNodesRequested;

        /// <summary>Asks the owner to create a node group around the selected nodes.</summary>
        public Action createGroupRequested;

        /// <summary>Asks the owner to let the user rename a node group.</summary>
        public Action<EntryGroupElement> renameGroupRequested;

        /// <summary>Raised when the user starts dragging a node group by its heading.</summary>
        public Action<EntryGroupElement> groupDragStarted;

        /// <summary>Raised during a group drag with the total content-space offset so far.</summary>
        public Action<EntryGroupElement, Vector2> groupDragged;

        /// <summary>Raised when a group drag that moved the group ends.</summary>
        public Action<EntryGroupElement> groupDragFinished;

        /// <summary>Raised when a resize that changed the group's size ends.</summary>
        public Action<EntryGroupElement> groupResizeFinished;

        private bool isSelectionNotificationPending = false;

        /// <summary>While true, selection changes don't raise selectionChanged, such as during a rebuild.</summary>
        public bool suppressSelectionEvents { get; set; }

        /// <summary>
        /// True if Shift was held at the last mouse event in the graph. A context menu's actions
        /// and an edge drop run with no key state of their own, so they read this.
        /// </summary>
        public bool isShiftDown { get; private set; }

        public ConversationGraphView()
        {
            style.flexGrow = 1;
            DialogueDatabaseEditorTheme.KeepLightThemeOnAttach(this); // GraphView forces Dark-theme styling on the window.
            SetupZoom(0.1f, 2f); // Same range as the IMGUI editor's zoom.
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());
            var grid = new GridBackground();
            Insert(0, grid);
            grid.StretchToParentSize();
            // Trickle down, so these run before the ports and edges handle the same events.
            RegisterCallback<PointerDownEvent>(evt => isShiftDown = evt.shiftKey, TrickleDown.TrickleDown);
            RegisterCallback<PointerMoveEvent>(evt => isShiftDown = evt.shiftKey, TrickleDown.TrickleDown);
            RegisterCallback<PointerUpEvent>(evt => isShiftDown = evt.shiftKey, TrickleDown.TrickleDown);
        }

        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            return ports.ToList().Where(port => port.direction != startPort.direction && port.node != startPort.node).ToList();
        }

        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            // Replace GraphView's default items (cut/copy/paste/duplicate), which this editor doesn't support yet.
            isShiftDown = evt.shiftKey;
            var target = evt.target as VisualElement;
            var node = (target != null) ? target.GetFirstOfType<DialogueEntryNode>() : null;
            var edge = (target != null) ? target.GetFirstOfType<Edge>() : null;
            var group = (target != null) ? target.GetFirstOfType<EntryGroupElement>() : null;
            var position = contentViewContainer.WorldToLocal(evt.mousePosition);
            var hasMultipleNodes = selection.OfType<DialogueEntryNode>().Count() > 1;
            if (node != null)
            {
                if (hasMultipleNodes && selection.Contains(node))
                {
                    evt.menu.AppendAction("Create Group", a => createGroupRequested?.Invoke());
                }
                else
                {
                    evt.menu.AppendAction("Create Child Node", a => createChildRequested?.Invoke(node));
                }
                AppendArrangeNodesItems(evt.menu);
                appendNodeMenuItems?.Invoke(node, evt.menu);
                evt.menu.AppendSeparator();
                evt.menu.AppendAction("Delete", a => DeleteNodeOrSelection(node),
                    a => node.isStart && !HasDeletableSelection() ? DropdownMenuAction.Status.Disabled : DropdownMenuAction.Status.Normal);
                evt.menu.AppendSeparator();
                appendSharedMenuItems?.Invoke(evt.menu);
            }
            else if (edge != null)
            {
                evt.menu.AppendAction("Delete", a => DeleteElements(new GraphElement[] { edge }));
            }
            else if (group != null)
            {
                evt.menu.AppendAction("Rename", a => renameGroupRequested?.Invoke(group));
                evt.menu.AppendAction("Delete", a => DeleteElements(new GraphElement[] { group }));
            }
            else
            {
                if (hasMultipleNodes)
                {
                    evt.menu.AppendAction("Create Group", a => createGroupRequested?.Invoke());
                    evt.menu.AppendSeparator();
                }
                evt.menu.AppendAction("Create Node", a => createNodeRequested?.Invoke(position, null));
                if (appendCanvasMenuItems != null)
                {
                    // The owner's Menu items, which include Arrange Nodes and Frame All.
                    evt.menu.AppendSeparator();
                    appendCanvasMenuItems(evt.menu);
                }
                else
                {
                    AppendArrangeNodesItems(evt.menu);
                    evt.menu.AppendAction("Frame All", a => FrameAll());
                    evt.menu.AppendSeparator();
                    appendSharedMenuItems?.Invoke(evt.menu);
                }
            }
        }

        private void AppendArrangeNodesItems(DropdownMenu menu)
        {
            menu.AppendAction("Arrange Nodes/Vertically", a => arrangeNodesRequested?.Invoke(true));
            menu.AppendAction("Arrange Nodes/Horizontally", a => arrangeNodesRequested?.Invoke(false));
        }

        private bool HasDeletableSelection()
        {
            return selection.OfType<GraphElement>().Any(element => !(element is DialogueEntryNode entryNode && entryNode.isStart));
        }

        private void DeleteNodeOrSelection(DialogueEntryNode node)
        {
            if (selection.Contains(node)) DeleteSelection();
            else DeleteElements(new GraphElement[] { node });
        }

        public override void AddToSelection(ISelectable selectable)
        {
            base.AddToSelection(selectable);
            QueueSelectionChanged();
        }

        public override void RemoveFromSelection(ISelectable selectable)
        {
            base.RemoveFromSelection(selectable);
            QueueSelectionChanged();
        }

        public override void ClearSelection()
        {
            base.ClearSelection();
            QueueSelectionChanged();
        }

        private void QueueSelectionChanged()
        {
            if (suppressSelectionEvents || isSelectionNotificationPending) return;
            isSelectionNotificationPending = true;
            schedule.Execute(() =>
            {
                isSelectionNotificationPending = false;
                if (!suppressSelectionEvents) selectionChanged?.Invoke();
            });
        }

        /// <summary>Removes all nodes and edges without raising graphViewChanged.</summary>
        public void ClearGraph()
        {
            foreach (var element in graphElements.ToList())
            {
                RemoveElement(element);
            }
        }

        public void SelectGroupOnly(EntryGroupElement group)
        {
            ClearSelection();
            AddToSelection(group);
        }

        /// <summary>Moves the view's contents by an offset in view pixels, keeping the zoom.</summary>
        public void PanBy(Vector2 offset)
        {
            var content = contentViewContainer.resolvedStyle;
            UpdateViewTransform(content.translate + (Vector3)offset, content.scale.value);
        }

        /// <summary>Scrolls so a content-space position is a margin in from the view's top left corner.</summary>
        public void ScrollToTopLeft(Vector2 contentPosition, float margin)
        {
            var scale = contentViewContainer.resolvedStyle.scale.value;
            UpdateViewTransform(new Vector3(margin - contentPosition.x * scale.x, margin - contentPosition.y * scale.y, 0), scale);
        }

        /// <summary>Returns the topmost node at a world-space position, or null if there isn't one.</summary>
        public DialogueEntryNode GetNodeAt(Vector2 worldPosition)
        {
            return nodes.ToList().OfType<DialogueEntryNode>().LastOrDefault(node => node.worldBound.Contains(worldPosition));
        }

        /// <summary>
        /// Scrolls so the node is near the top center of the view, at the current zoom.
        /// </summary>
        public void FrameNode(DialogueEntryNode node)
        {
            if (node == null) return;
            var scale = contentViewContainer.style.scale.value.value.x;
            var rect = node.GetPosition();
            var position = new Vector3(layout.width / 2 - (rect.x + rect.width / 2) * scale, 40 - rect.y * scale, 0);
            UpdateViewTransform(position, new Vector3(scale, scale, 1));
        }

    }

    /// <summary>
    /// Graph node for one dialogue entry. Ports are vertical (input on top, output on the
    /// bottom) so conversations flow top-down, like the IMGUI node editor.
    /// </summary>
    public class DialogueEntryNode : Node
    {

        private const int MaxTitleLength = 200; // The label also ellipsizes to the node width.

        public int entryID { get; private set; }
        public bool isStart { get { return entryID == ConversationPropertyUtility.StartEntryID; } }
        public Port inputPort { get; private set; }
        public Port outputPort { get; private set; }

        private Label titleLabel;
        private Label actorLabel;
        private Label badgeLabel;
        private Label incomingBadgeLabel;
        private VisualElement iconRow;
        private Image sequenceIcon, conditionsIcon, scriptIcon, eventIcon;
        private Image portraitImage;

        private bool showFullText = false;
        private string fullText = null;
        private bool needsFullTextLayout = false;

        public DialogueEntryNode(int entryID, IEdgeConnectorListener edgeConnectorListener)
        {
            this.entryID = entryID;

            // Same size as the IMGUI editor's default nodes, ignoring any width set in the IMGUI
            // editor: one colored row with the entry text and icons. Show Full Node Text widens it.
            // Hide the default port area and collapse button.
            style.width = NodeDefaultWidth;
            topContainer.style.display = DisplayStyle.None;
            titleButtonContainer.style.display = DisplayStyle.None;
            titleContainer.style.height = NodeHeight;
            titleContainer.style.minHeight = NodeHeight;
            titleContainer.style.paddingLeft = 4;
            titleContainer.style.paddingRight = 2;
            titleLabel = titleContainer.Q<Label>("title-label");
            if (titleLabel != null)
            {
                titleLabel.style.flexShrink = 1;
                titleLabel.style.marginLeft = 2;
                titleLabel.style.marginRight = 2;
                titleLabel.style.fontSize = 11;
                titleLabel.style.overflow = Overflow.Hidden;
                titleLabel.style.textOverflow = TextOverflow.Ellipsis;
                titleLabel.style.whiteSpace = WhiteSpace.NoWrap;
                titleLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
                titleLabel.style.marginTop = 0;
                titleLabel.style.marginBottom = 0;
                titleLabel.style.paddingTop = 0;
                titleLabel.style.paddingBottom = 0;

                // The actor's name, when shown, goes on its own line above the text.
                actorLabel = new Label() { enableRichText = false };
                actorLabel.style.display = DisplayStyle.None;
                actorLabel.style.fontSize = 11;
                actorLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                actorLabel.style.marginLeft = 2;
                actorLabel.style.marginRight = 2;
                actorLabel.style.marginTop = 0;
                actorLabel.style.marginBottom = 0;
                actorLabel.style.paddingTop = 0;
                actorLabel.style.paddingBottom = 0;
                actorLabel.style.overflow = Overflow.Hidden;
                actorLabel.style.textOverflow = TextOverflow.Ellipsis;
                actorLabel.style.whiteSpace = WhiteSpace.NoWrap;
                actorLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
                var textColumn = new VisualElement();
                textColumn.style.flexGrow = 1;
                textColumn.style.flexShrink = 1;
                textColumn.style.minWidth = 0;
                textColumn.style.justifyContent = Justify.Center;
                var titleParent = titleLabel.parent;
                titleParent.Insert(titleParent.IndexOf(titleLabel), textColumn);
                textColumn.Add(actorLabel);
                textColumn.Add(titleLabel);
            }

            iconRow = new VisualElement();
            iconRow.style.flexDirection = FlexDirection.Row;
            iconRow.style.alignItems = Align.Center;
            iconRow.style.flexShrink = 0;
            sequenceIcon = CreateIcon("Dialogue System/Sequence.png", "Has a Sequence.");
            conditionsIcon = CreateIcon("Dialogue System/Conditions.png", "Has Conditions.");
            scriptIcon = CreateIcon("Dialogue System/Script.png", "Has a Script.");
            eventIcon = CreateIcon("Dialogue System/Event.png", "Has an OnExecute() event.");
            badgeLabel = new Label() { tooltip = "Links to other conversations." };
            badgeLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            badgeLabel.style.fontSize = 10;
            badgeLabel.style.marginLeft = 2;
            badgeLabel.style.marginRight = 2;
            iconRow.Add(badgeLabel);
            titleContainer.Add(iconRow);

            // Incoming links from other conversations show on the left, before the text.
            incomingBadgeLabel = new Label() { tooltip = "Linked from other conversations." };
            incomingBadgeLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            incomingBadgeLabel.style.fontSize = 10;
            incomingBadgeLabel.style.marginLeft = 0;
            incomingBadgeLabel.style.marginRight = 2;
            incomingBadgeLabel.style.flexShrink = 0;
            incomingBadgeLabel.style.alignSelf = Align.Center;
            incomingBadgeLabel.style.display = DisplayStyle.None;
            titleContainer.Insert(0, incomingBadgeLabel);

            // Ports sit on the top and bottom edges, outside the node's layout.
            inputPort = DialogueEntryPort.Create(Direction.Input, edgeConnectorListener);
            hierarchy.Add(CreatePortRow(inputPort, true));
            outputPort = DialogueEntryPort.Create(Direction.Output, edgeConnectorListener);
            hierarchy.Add(CreatePortRow(outputPort, false));

            // Text can only be measured once the node is in a panel.
            RegisterCallback<AttachToPanelEvent>(evt => { if (needsFullTextLayout) ApplyTextLayout(); });
        }

        private const float NodeDefaultWidth = DialogueEntry.CanvasRectWidth;
        private const float NodeHeight = DialogueEntry.CanvasRectHeight;
        private const float PortSize = 16;
        private const float IconSize = 14;
        private const float PortraitSize = 30;
        private const float IconMargin = 2;
        private const float FullTextVerticalPadding = 6;
        private const float FullTextWidthStep = 10;
        private const float FullTextWrapAllowance = 1.15f; // Word wrapping leaves some space at line ends.

        private static VisualElement CreatePortRow(Port port, bool isTop)
        {
            var row = new VisualElement();
            row.style.position = Position.Absolute;
            row.style.left = 0;
            row.style.right = 0;
            if (isTop) row.style.top = -PortSize / 2 - 1;
            else row.style.bottom = -PortSize / 2 - 1;
            row.style.flexDirection = FlexDirection.Row;
            row.style.justifyContent = Justify.Center;
            row.pickingMode = PickingMode.Ignore;
            port.style.height = PortSize;
            port.style.width = PortSize;
            port.style.paddingLeft = 0;
            port.style.paddingRight = 0;
            port.style.marginLeft = 0;
            port.style.marginRight = 0;
            var typeLabel = port.Q<Label>("type");
            if (typeLabel != null) typeLabel.style.display = DisplayStyle.None;
            row.Add(port);
            return row;
        }

        private Image CreateIcon(string path, string tooltip)
        {
            var image = new Image() { image = EditorGUIUtility.Load(path) as Texture2D, tooltip = tooltip };
            image.style.width = IconSize;
            image.style.height = IconSize;
            image.style.marginLeft = IconMargin;
            image.style.display = DisplayStyle.None;
            iconRow.Add(image);
            return image;
        }

        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            // The graph view builds the node menu; skip Node's default "Disconnect" items.
        }

        /// <summary>
        /// The node's position in graph content space. Reads the style that SetPosition sets,
        /// because GetPosition returns the layout, which isn't updated until the next layout pass.
        /// </summary>
        public Vector2 GetCanvasPosition()
        {
            var left = style.left;
            var top = style.top;
            if (left.keyword == StyleKeyword.Undefined && top.keyword == StyleKeyword.Undefined)
            {
                return new Vector2(left.value.value, top.value.value);
            }
            return GetPosition().position;
        }

        /// <summary>
        /// Updates the node's text, color, icons and cross-conversation link badges.
        /// </summary>
        /// <param name="actorText">Text to show on its own line above the node text, or null for none.</param>
        /// <param name="showFullText">Wrap the full text and size the node to fit it, roughly square, instead of cutting it off.</param>
        /// <param name="externalLinkCount">Number of links from this entry to other conversations.</param>
        /// <param name="incomingLinkCount">Number of links from other conversations to this entry.</param>
        public void SetContent(string text, string actorText, bool showFullText, Color color, bool hasSequence, bool hasConditions,
            bool hasScript, bool hasEvent, int externalLinkCount, int incomingLinkCount, string tooltipText)
        {
            if (text == null) text = string.Empty;
            var showActor = !string.IsNullOrEmpty(actorText) && actorLabel != null;
            var textOrIconsChanged = text != fullText || showFullText != this.showFullText ||
                (actorLabel != null && (showActor != IsShown(actorLabel) || (showActor && actorText != actorLabel.text))) ||
                hasSequence != IsShown(sequenceIcon) || hasConditions != IsShown(conditionsIcon) ||
                hasScript != IsShown(scriptIcon) || hasEvent != IsShown(eventIcon) || (externalLinkCount > 0) != IsShown(badgeLabel) ||
                (incomingLinkCount > 0) != IsShown(incomingBadgeLabel);
            fullText = text;
            this.showFullText = showFullText;
            title = (!showFullText && text.Length > MaxTitleLength) ? text.Substring(0, MaxTitleLength) : text;
            titleContainer.style.backgroundColor = color;
            var luminance = 0.299f * color.r + 0.587f * color.g + 0.114f * color.b;
            var textColor = (luminance > 0.6f) ? Color.black : Color.white;
            if (titleLabel != null) titleLabel.style.color = textColor;
            if (actorLabel != null)
            {
                actorLabel.text = showActor ? actorText : string.Empty;
                actorLabel.style.color = textColor;
                actorLabel.style.display = showActor ? DisplayStyle.Flex : DisplayStyle.None;
            }
            badgeLabel.style.color = textColor;
            sequenceIcon.style.display = hasSequence ? DisplayStyle.Flex : DisplayStyle.None;
            conditionsIcon.style.display = hasConditions ? DisplayStyle.Flex : DisplayStyle.None;
            scriptIcon.style.display = hasScript ? DisplayStyle.Flex : DisplayStyle.None;
            eventIcon.style.display = hasEvent ? DisplayStyle.Flex : DisplayStyle.None;
            badgeLabel.text = (externalLinkCount > 0) ? "↗ " + externalLinkCount : string.Empty;
            badgeLabel.style.display = (externalLinkCount > 0) ? DisplayStyle.Flex : DisplayStyle.None;
            incomingBadgeLabel.style.color = textColor;
            incomingBadgeLabel.text = (incomingLinkCount > 0) ? "↘ " + incomingLinkCount : string.Empty;
            incomingBadgeLabel.style.display = (incomingLinkCount > 0) ? DisplayStyle.Flex : DisplayStyle.None;
            tooltip = tooltipText;
            if (textOrIconsChanged) ApplyTextLayout();
        }

        /// <summary>Shows a portrait to the left of the node, like the IMGUI editor, or hides it if null.</summary>
        public void SetPortrait(Sprite portrait)
        {
            if (portrait == null)
            {
                if (portraitImage != null) portraitImage.style.display = DisplayStyle.None;
                return;
            }
            if (portraitImage == null)
            {
                portraitImage = new Image() { scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
                portraitImage.style.position = Position.Absolute;
                portraitImage.style.left = -PortraitSize;
                portraitImage.style.top = 0;
                portraitImage.style.width = PortraitSize;
                portraitImage.style.height = PortraitSize;
                hierarchy.Add(portraitImage);
            }
            portraitImage.sprite = portrait;
            portraitImage.style.display = DisplayStyle.Flex;
        }

        private static bool IsShown(VisualElement element)
        {
            return element.style.display != DisplayStyle.None;
        }

        /// <summary>
        /// Collapsed: one row at the default width, with the text cut off. Full text: the text
        /// wraps and the node is sized so its width and height are about equal.
        /// </summary>
        private void ApplyTextLayout()
        {
            needsFullTextLayout = false;
            if (titleLabel == null) return;
            if (!showFullText)
            {
                style.width = NodeDefaultWidth;
                titleContainer.style.height = NodeHeight;
                titleLabel.style.whiteSpace = WhiteSpace.NoWrap;
                titleLabel.style.textOverflow = TextOverflow.Ellipsis;
                return;
            }
            titleContainer.style.height = StyleKeyword.Auto;
            titleLabel.style.whiteSpace = WhiteSpace.Normal;
            titleLabel.style.textOverflow = TextOverflow.Clip;
            if (panel == null)
            {
                needsFullTextLayout = true;
                return;
            }
            style.width = GetSquareFullTextWidth();
        }

        /// <summary>
        /// Finds the narrowest node width, at least the default width, at which the node is at
        /// least as wide as the wrapped text is tall.
        /// </summary>
        private float GetSquareFullTextWidth()
        {
            var undefined = MeasureMode.Undefined;
            var singleLine = titleLabel.MeasureTextSize(fullText, 0, undefined, 0, undefined);
            var lineHeight = Mathf.Max(singleLine.y, 1);
            var textWidth = singleLine.x * FullTextWrapAllowance;
            // Space in the row that isn't the text: padding, label margins, and the icons.
            // Icons and badge sizes are set in code, since they may not be laid out yet.
            float iconsWidth = 0;
            foreach (var icon in new VisualElement[] { sequenceIcon, conditionsIcon, scriptIcon, eventIcon })
            {
                if (IsShown(icon)) iconsWidth += IconSize + IconMargin;
            }
            if (IsShown(badgeLabel)) iconsWidth += badgeLabel.MeasureTextSize(badgeLabel.text, 0, undefined, 0, undefined).x + 4;
            if (IsShown(incomingBadgeLabel)) iconsWidth += incomingBadgeLabel.MeasureTextSize(incomingBadgeLabel.text, 0, undefined, 0, undefined).x + 2;
            var extraWidth = 4 + 2 + 2 + 2 + iconsWidth;
            var actorLines = (actorLabel != null && IsShown(actorLabel)) ? 1 : 0;
            var width = NodeDefaultWidth;
            while (true)
            {
                var availableWidth = Mathf.Max(width - extraWidth, FullTextWidthStep);
                var lines = Mathf.Max(1, Mathf.Ceil(textWidth / availableWidth)) + actorLines;
                var height = Mathf.Max(NodeHeight, lines * lineHeight + FullTextVerticalPadding);
                if (width >= height) return width;
                width += FullTextWidthStep;
            }
        }

        /// <summary>
        /// Port that uses a custom IEdgeConnectorListener, so dropping an edge on empty canvas
        /// can create a new linked entry.
        /// </summary>
        private class DialogueEntryPort : Port
        {
            private DialogueEntryPort(Direction direction)
                : base(Orientation.Vertical, direction, Capacity.Multi, typeof(bool)) { }

            public static DialogueEntryPort Create(Direction direction, IEdgeConnectorListener listener)
            {
                var port = new DialogueEntryPort(direction) { portName = string.Empty };
                port.m_EdgeConnector = new EdgeConnector<DialogueEdge>(listener);
                port.AddManipulator(port.m_EdgeConnector);
                return port;
            }
        }

    }

    /// <summary>
    /// Edge for a link. Edge resets its EdgeControl's width from the --edge-width style every
    /// time it updates, so a per-link width (for non-Normal priority) is applied afterward here.
    /// </summary>
    public class DialogueEdge : Edge
    {

        private const float OrderLabelWidth = 18;
        private const float OrderLabelHeight = 14;

        private int m_WidthOverride = 0;
        private Label orderLabel;

        /// <summary>Shows the link's number at the middle of the edge, or hides it if null.</summary>
        public void SetOrderText(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                if (orderLabel != null) orderLabel.style.display = DisplayStyle.None;
                return;
            }
            if (orderLabel == null)
            {
                orderLabel = new Label() { pickingMode = PickingMode.Ignore };
                orderLabel.style.position = Position.Absolute;
                orderLabel.style.width = OrderLabelWidth;
                orderLabel.style.height = OrderLabelHeight;
                orderLabel.style.fontSize = 9;
                orderLabel.style.paddingLeft = 0;
                orderLabel.style.paddingRight = 0;
                orderLabel.style.paddingTop = 0;
                orderLabel.style.paddingBottom = 0;
                orderLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                orderLabel.AddToClassList("ds-edge-order-label");
                Add(orderLabel);
            }
            orderLabel.text = text;
            orderLabel.style.display = DisplayStyle.Flex;
            UpdateOrderLabelPosition();
        }

        private void UpdateOrderLabelPosition()
        {
            if (orderLabel == null || panel == null || output == null || input == null) return;
            var middle = this.WorldToLocal((output.GetGlobalCenter() + input.GetGlobalCenter()) / 2);
            orderLabel.style.left = middle.x - OrderLabelWidth / 2;
            orderLabel.style.top = middle.y - OrderLabelHeight / 2;
        }

        /// <summary>The link's entries, remembered because a drag changes the edge's ports. -1 until set.</summary>
        public int originEntryID { get; private set; } = -1;
        public int destinationEntryID { get; private set; } = -1;

        /// <summary>True if this edge shows a link in the data, not a new link being dragged out of a port.</summary>
        public bool isExistingLink { get { return originEntryID >= 0; } }

        public void SetLink(int originEntryID, int destinationEntryID)
        {
            this.originEntryID = originEntryID;
            this.destinationEntryID = destinationEntryID;
        }

        /// <summary>
        /// True while the user is dragging an end of this edge, up to when GraphView finishes
        /// the drop. GraphView asks to delete an existing edge during the drop.
        /// </summary>
        public bool isBeingDragged { get { return IsDraggedFrom(output) || IsDraggedFrom(input); } }

        /// <summary>True if the end being dragged is the origin end, so the destination stays connected.</summary>
        public bool isOriginEndDragged { get { return IsDraggedFrom(input); } }

        /// <summary>GraphView stores a dragged edge in the drag helper of the port that stays connected.</summary>
        private bool IsDraggedFrom(Port port)
        {
            var dragHelper = (port != null && port.edgeConnector != null) ? port.edgeConnector.edgeDragHelper : null;
            return dragHelper != null && dragHelper.edgeCandidate == this;
        }

        /// <summary>Width to draw this edge with, or 0 to use the style's width.</summary>
        public int widthOverride
        {
            get { return m_WidthOverride; }
            set
            {
                if (m_WidthOverride == value) return;
                m_WidthOverride = value;
                // Only refresh when attached; EdgeControl can't lay out a detached edge.
                if (panel != null) UpdateEdgeControl();
            }
        }

        /// <summary>
        /// RectangleSelector finds elements with Overlaps, so this keeps lasso selection to
        /// nodes. Clicking an edge uses ContainsPoint and still selects it.
        /// </summary>
        public override bool Overlaps(Rect rectangle)
        {
            return false;
        }

        private Color? m_ColorOverride = null;

        /// <summary>
        /// Color to draw this edge with, even when selected, or null to use the normal colors.
        /// Used to show the current runtime entry's links.
        /// </summary>
        public Color? colorOverride
        {
            get { return m_ColorOverride; }
            set
            {
                if (m_ColorOverride == value) return;
                m_ColorOverride = value;
                if (panel == null || edgeControl == null) return;
                if (m_ColorOverride.HasValue) ApplyColorOverride();
                else RestoreDefaultColors();
                edgeControl.MarkDirtyRepaint();
            }
        }

        // Edge resets the width and colors when it draws, when its style resolves, and when
        // it's selected or unselected, so reapply the overrides after each.

        public override bool UpdateEdgeControl()
        {
            var result = base.UpdateEdgeControl();
            if (result)
            {
                ApplyWidthOverride();
                UpdateOrderLabelPosition();
            }
            return result;
        }

        protected override void DrawEdge()
        {
            base.DrawEdge();
            ApplyColorOverride();
        }

        protected override void OnCustomStyleResolved(ICustomStyle styles)
        {
            base.OnCustomStyleResolved(styles);
            ApplyWidthOverride();
            ApplyColorOverride();
        }

        public override void OnSelected()
        {
            base.OnSelected();
            ApplyWidthOverride();
            ApplyColorOverride();
        }

        public override void OnUnselected()
        {
            base.OnUnselected();
            ApplyWidthOverride();
            ApplyColorOverride();
        }

        /// <summary>
        /// Sets the colors Edge uses itself: the selected color, else the port colors. Edge's
        /// own method for this is private, and DrawEdge skips it when the geometry is unchanged.
        /// </summary>
        private void RestoreDefaultColors()
        {
            if (selected)
            {
                edgeControl.inputColor = selectedColor;
                edgeControl.outputColor = selectedColor;
            }
            else
            {
                edgeControl.inputColor = (input != null) ? input.portColor : defaultColor;
                edgeControl.outputColor = (output != null) ? output.portColor : defaultColor;
            }
        }

        private void ApplyColorOverride()
        {
            if (!m_ColorOverride.HasValue || panel == null || edgeControl == null) return;
            edgeControl.inputColor = m_ColorOverride.Value;
            edgeControl.outputColor = m_ColorOverride.Value;
        }

        private void ApplyWidthOverride()
        {
            if (m_WidthOverride > 0 && panel != null && edgeControl != null && edgeControl.edgeWidth != m_WidthOverride)
            {
                edgeControl.edgeWidth = m_WidthOverride;
            }
        }

    }

    /// <summary>
    /// Graph element for a conversation's EntryGroup, a named colored box behind nodes. A group
    /// has no member list, only a rect, so this doesn't use GraphView's Group. Like the IMGUI
    /// editor, only the heading and resize grip respond to the mouse, so lassoing and panning
    /// still work over the body.
    /// </summary>
    public class EntryGroupElement : GraphElement
    {

        private const float MinWidth = DialogueEntry.CanvasRectWidth;
        private const float MinHeight = 2 * DialogueEntry.CanvasRectHeight;
        private const float GripSize = 16;
        private const float BorderWidth = 2;

        /// <summary>Index in the conversation's entryGroups list, since groups have no ID.</summary>
        public int groupIndex { get; private set; }

        private readonly VisualElement heading;
        private readonly Label nameLabel;
        private Rect rect;

        public EntryGroupElement(int groupIndex)
        {
            this.groupIndex = groupIndex;
            capabilities = Capabilities.Selectable | Capabilities.Deletable; // Dragged by EntryGroupManipulator, not SelectionDragger.
            layer = -1; // Behind nodes and edges.
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
            AddToClassList("ds-entry-group"); // The selected border color comes from the theme stylesheet.
            style.borderLeftWidth = BorderWidth;
            style.borderRightWidth = BorderWidth;
            style.borderTopWidth = BorderWidth;
            style.borderBottomWidth = BorderWidth;

            heading = new VisualElement();
            heading.style.height = ConversationPropertyUtility.GroupHeadingHeight;
            heading.style.flexShrink = 0;
            heading.style.justifyContent = Justify.Center;
            heading.style.paddingLeft = 4;
            heading.style.paddingRight = 4;
            heading.AddManipulator(new EntryGroupManipulator(this, false));
            Add(heading);

            nameLabel = new Label();
            nameLabel.pickingMode = PickingMode.Ignore;
            nameLabel.enableRichText = false;
            nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            nameLabel.style.overflow = Overflow.Hidden;
            nameLabel.style.textOverflow = TextOverflow.Ellipsis;
            nameLabel.style.whiteSpace = WhiteSpace.NoWrap;
            heading.Add(nameLabel);

            var grip = new Image() { image = EditorGUIUtility.Load("Dialogue System/Resize.png") as Texture2D, tooltip = "Resize this group." };
            grip.pickingMode = PickingMode.Position;
            grip.style.position = Position.Absolute;
            grip.style.right = 2;
            grip.style.bottom = 2;
            grip.style.width = GripSize;
            grip.style.height = GripSize;
            grip.AddManipulator(new EntryGroupManipulator(this, true));
            Add(grip);
        }

        public void SetContent(string name, Color color)
        {
            nameLabel.text = name;
            style.backgroundColor = color;
            heading.style.backgroundColor = new Color(color.r, color.g, color.b, 1);
            var luminance = 0.299f * color.r + 0.587f * color.g + 0.114f * color.b;
            nameLabel.style.color = (luminance > 0.6f) ? Color.black : Color.white;
        }

        public override Rect GetPosition()
        {
            return rect;
        }

        public override void SetPosition(Rect newPos)
        {
            rect = newPos;
            style.left = newPos.x;
            style.top = newPos.y;
            style.width = newPos.width;
            style.height = newPos.height;
        }

        public override void OnSelected()
        {
            base.OnSelected();
            AddToClassList("ds-entry-group--selected");
        }

        public override void OnUnselected()
        {
            base.OnUnselected();
            RemoveFromClassList("ds-entry-group--selected");
        }

        /// <summary>RectangleSelector finds elements with Overlaps, so this keeps lasso selection to nodes.</summary>
        public override bool Overlaps(Rect rectangle)
        {
            return false;
        }

        /// <summary>
        /// Selects the group on mouse down, then moves it (on the heading) or resizes it (on the
        /// grip). Double-clicking the heading asks to rename it.
        /// </summary>
        private class EntryGroupManipulator : MouseManipulator
        {
            private readonly EntryGroupElement group;
            private readonly bool isResize;
            private ConversationGraphView graphView;
            private bool isActive = false;
            private bool hasChanged = false;
            private Vector2 startMousePosition;
            private Rect startRect;

            public EntryGroupManipulator(EntryGroupElement group, bool isResize)
            {
                this.group = group;
                this.isResize = isResize;
                activators.Add(new ManipulatorActivationFilter() { button = MouseButton.LeftMouse });
            }

            protected override void RegisterCallbacksOnTarget()
            {
                target.RegisterCallback<MouseDownEvent>(OnMouseDown);
                target.RegisterCallback<MouseMoveEvent>(OnMouseMove);
                target.RegisterCallback<MouseUpEvent>(OnMouseUp);
            }

            protected override void UnregisterCallbacksFromTarget()
            {
                target.UnregisterCallback<MouseDownEvent>(OnMouseDown);
                target.UnregisterCallback<MouseMoveEvent>(OnMouseMove);
                target.UnregisterCallback<MouseUpEvent>(OnMouseUp);
            }

            private Vector2 GetContentPosition(IMouseEvent evt)
            {
                return graphView.contentViewContainer.WorldToLocal(evt.mousePosition);
            }

            private void OnMouseDown(MouseDownEvent evt)
            {
                if (isActive || !CanStartManipulation(evt)) return;
                graphView = group.GetFirstAncestorOfType<ConversationGraphView>();
                if (graphView == null) return;
                evt.StopPropagation(); // Keeps SelectionDragger and RectangleSelector from reacting.
                graphView.SelectGroupOnly(group);
                if (!isResize && evt.clickCount == 2)
                {
                    graphView.renameGroupRequested?.Invoke(group);
                    return;
                }
                isActive = true;
                hasChanged = false;
                startMousePosition = GetContentPosition(evt);
                startRect = group.GetPosition();
                target.CaptureMouse();
                if (!isResize) graphView.groupDragStarted?.Invoke(group);
            }

            private void OnMouseMove(MouseMoveEvent evt)
            {
                if (!isActive) return;
                evt.StopPropagation();
                var delta = GetContentPosition(evt) - startMousePosition;
                hasChanged = true;
                if (isResize)
                {
                    group.SetPosition(new Rect(startRect.x, startRect.y,
                        Mathf.Max(MinWidth, startRect.width + delta.x), Mathf.Max(MinHeight, startRect.height + delta.y)));
                }
                else
                {
                    graphView.groupDragged?.Invoke(group, delta);
                }
            }

            private void OnMouseUp(MouseUpEvent evt)
            {
                if (!isActive || !CanStopManipulation(evt)) return;
                evt.StopPropagation();
                isActive = false;
                target.ReleaseMouse();
                if (!hasChanged) return;
                if (isResize) graphView.groupResizeFinished?.Invoke(group);
                else graphView.groupDragFinished?.Invoke(group);
            }
        }

    }

    /// <summary>
    /// Creates links when an edge is dropped on a port, like GraphView's default listener, and
    /// forwards other drops so the owner can link to the node there or create a new linked entry.
    /// Existing links dropped on a port are forwarded too, so the owner can move them.
    /// </summary>
    public class DialogueEdgeConnectorListener : IEdgeConnectorListener
    {

        private readonly Action<Edge, Vector2> droppedOutsidePort;
        private readonly Action<DialogueEdge> existingLinkDroppedOnPort;

        public DialogueEdgeConnectorListener(Action<Edge, Vector2> droppedOutsidePort, Action<DialogueEdge> existingLinkDroppedOnPort)
        {
            this.droppedOutsidePort = droppedOutsidePort;
            this.existingLinkDroppedOnPort = existingLinkDroppedOnPort;
        }

        public void OnDropOutsidePort(Edge edge, Vector2 position)
        {
            droppedOutsidePort?.Invoke(edge, position);
        }

        public void OnDrop(GraphView graphView, Edge edge)
        {
            var dialogueEdge = edge as DialogueEdge;
            if (dialogueEdge != null && dialogueEdge.isExistingLink)
            {
                existingLinkDroppedOnPort?.Invoke(dialogueEdge);
                return;
            }
            var change = new GraphViewChange() { edgesToCreate = new List<Edge>() { edge } };
            var edgesToCreate = (graphView.graphViewChanged != null) ? graphView.graphViewChanged(change).edgesToCreate : change.edgesToCreate;
            foreach (var newEdge in edgesToCreate)
            {
                graphView.AddElement(newEdge);
                newEdge.input.Connect(newEdge);
                newEdge.output.Connect(newEdge);
                var newDialogueEdge = newEdge as DialogueEdge;
                var origin = newEdge.output.node as DialogueEntryNode;
                var destination = newEdge.input.node as DialogueEntryNode;
                if (newDialogueEdge != null && origin != null && destination != null) newDialogueEdge.SetLink(origin.entryID, destination.entryID);
            }
        }

    }

}

#endif
