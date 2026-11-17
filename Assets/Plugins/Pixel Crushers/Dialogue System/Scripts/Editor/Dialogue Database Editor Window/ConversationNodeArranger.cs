// Copyright (c) Pixel Crushers. All rights reserved.

#if UNITY_2022_1_OR_NEWER

using System;
using System.Collections.Generic;
using UnityEngine;

namespace PixelCrushers.DialogueSystem.DialogueEditor
{

    /// <summary>
    /// Computes tidy node positions for a conversation. Each node sits at the level of its
    /// shortest path from START, centered on its children, and subtrees are packed as closely as
    /// their shapes allow. Nodes that START can't reach form separate trees after the main one.
    /// </summary>
    public static class ConversationNodeArranger
    {

        private const float StartPosition = 20;
        private const float SiblingGap = 20; // Between neighboring nodes on the same level.
        private const float TreeGap = 60; // Between the main tree and each unreachable tree.
        private const float VerticalLevelGap = 54; // Same step as the IMGUI editor's vertical arrangement.
        private const float HorizontalLevelGap = 60;

        private class TreeNode
        {
            public DialogueEntry entry;
            public int level;
            public float breadth; // Size across a level: width if vertical, height if horizontal.
            public float length; // Size along the tree's growth: height if vertical, width if horizontal.
            public float offset; // Center relative to the parent's center.
            public float center; // Absolute center across the levels.
            public List<TreeNode> children = new List<TreeNode>();
            // Extents of this node's subtree on each level below it (0 = this node), relative to its center:
            public List<float> leftContour = new List<float>();
            public List<float> rightContour = new List<float>();
        }

        /// <summary>
        /// Returns the new top left canvas position of each entry, by entry ID.
        /// </summary>
        /// <param name="vertically">Grow the tree downward if true, or rightward if false.</param>
        /// <param name="getSize">Returns an entry's size on the canvas.</param>
        /// <param name="subset">
        /// If set, arranges only these entries, using only the links between them. The result
        /// stays where the entries are now instead of moving to the canvas's corner.
        /// </param>
        public static Dictionary<int, Vector2> Arrange(Conversation conversation, bool vertically, Func<DialogueEntry, Vector2> getSize, HashSet<int> subset = null)
        {
            var positions = new Dictionary<int, Vector2>();
            var entriesByID = new Dictionary<int, DialogueEntry>();
            foreach (var entry in conversation.dialogueEntries)
            {
                if (subset != null && !subset.Contains(entry.id)) continue;
                if (!entriesByID.ContainsKey(entry.id)) entriesByID.Add(entry.id, entry);
            }
            if (entriesByID.Count == 0) return positions;

            var allNodes = new List<TreeNode>();
            float nextTreeStart = StartPosition;
            var visitedIDs = new HashSet<int>();
            foreach (var root in GetRoots(conversation, entriesByID))
            {
                if (visitedIDs.Contains(root.id)) continue;
                var treeNodes = BuildTree(root, conversation.id, entriesByID, visitedIDs, vertically, getSize);
                for (int i = treeNodes.Count - 1; i >= 0; i--) // Breadth-first order reversed: children come first.
                {
                    PlaceChildren(treeNodes[i]);
                }
                var rootNode = treeNodes[0];
                var left = float.MaxValue;
                var right = float.MinValue;
                for (int i = 0; i < rootNode.leftContour.Count; i++)
                {
                    left = Mathf.Min(left, rootNode.leftContour[i]);
                    right = Mathf.Max(right, rootNode.rightContour[i]);
                }
                rootNode.center = nextTreeStart - left;
                foreach (var node in treeNodes) // A parent is always earlier in breadth-first order than its children.
                {
                    foreach (var child in node.children) child.center = node.center + child.offset;
                }
                nextTreeStart = rootNode.center + right + TreeGap;
                allNodes.AddRange(treeNodes);
            }

            // Each level is as long as its longest node, so levels line up across trees:
            var levelLengths = new List<float>();
            foreach (var node in allNodes)
            {
                while (levelLengths.Count <= node.level) levelLengths.Add(0);
                levelLengths[node.level] = Mathf.Max(levelLengths[node.level], node.length);
            }
            var levelGap = vertically ? VerticalLevelGap : HorizontalLevelGap;
            var levelStarts = new float[levelLengths.Count];
            levelStarts[0] = StartPosition;
            for (int i = 1; i < levelStarts.Length; i++)
            {
                levelStarts[i] = levelStarts[i - 1] + levelLengths[i - 1] + levelGap;
            }

            foreach (var node in allNodes)
            {
                var across = node.center - node.breadth / 2;
                var along = levelStarts[node.level];
                positions.Add(node.entry.id, vertically ? new Vector2(across, along) : new Vector2(along, across));
            }
            if (subset != null) KeepInPlace(positions, entriesByID, vertically, getSize);
            return positions;
        }

        /// <summary>
        /// Moves arranged entries so they start where the originals started and stay centered
        /// across the levels, like the IMGUI editor's subset arrangement. The corner is kept
        /// positive, since the IMGUI editor's canvas has no negative coordinates.
        /// </summary>
        private static void KeepInPlace(Dictionary<int, Vector2> positions, Dictionary<int, DialogueEntry> entriesByID, bool vertically, Func<DialogueEntry, Vector2> getSize)
        {
            var oldBounds = new Rect();
            var newBounds = new Rect();
            var isFirst = true;
            foreach (var pair in positions)
            {
                var entry = entriesByID[pair.Key];
                var size = getSize(entry);
                var oldRect = new Rect(entry.canvasRect.position, size);
                var newRect = new Rect(pair.Value, size);
                oldBounds = isFirst ? oldRect : Rect.MinMaxRect(Mathf.Min(oldBounds.xMin, oldRect.xMin), Mathf.Min(oldBounds.yMin, oldRect.yMin), Mathf.Max(oldBounds.xMax, oldRect.xMax), Mathf.Max(oldBounds.yMax, oldRect.yMax));
                newBounds = isFirst ? newRect : Rect.MinMaxRect(Mathf.Min(newBounds.xMin, newRect.xMin), Mathf.Min(newBounds.yMin, newRect.yMin), Mathf.Max(newBounds.xMax, newRect.xMax), Mathf.Max(newBounds.yMax, newRect.yMax));
                isFirst = false;
            }
            var offset = vertically
                ? new Vector2(oldBounds.center.x - newBounds.center.x, oldBounds.yMin - newBounds.yMin)
                : new Vector2(oldBounds.xMin - newBounds.xMin, oldBounds.center.y - newBounds.center.y);
            offset.x = Mathf.Max(offset.x, 1 - newBounds.xMin);
            offset.y = Mathf.Max(offset.y, 1 - newBounds.yMin);
            foreach (var id in new List<int>(positions.Keys))
            {
                positions[id] += offset;
            }
        }

        /// <summary>
        /// Returns START, then entries that nothing else unreachable links to, then all entries,
        /// which picks up any cycles that have no such entry.
        /// </summary>
        private static List<DialogueEntry> GetRoots(Conversation conversation, Dictionary<int, DialogueEntry> entriesByID)
        {
            var roots = new List<DialogueEntry>();
            DialogueEntry start;
            if (entriesByID.TryGetValue(ConversationPropertyUtility.StartEntryID, out start)) roots.Add(start);
            var reachable = new HashSet<int>();
            var stack = new Stack<DialogueEntry>(roots);
            while (stack.Count > 0)
            {
                var entry = stack.Pop();
                if (!reachable.Add(entry.id)) continue;
                foreach (var link in entry.outgoingLinks)
                {
                    DialogueEntry destination;
                    if (link.destinationConversationID == conversation.id && entriesByID.TryGetValue(link.destinationDialogueID, out destination))
                    {
                        stack.Push(destination);
                    }
                }
            }
            var linkedIDs = new HashSet<int>();
            foreach (var entry in entriesByID.Values)
            {
                if (reachable.Contains(entry.id)) continue;
                foreach (var link in entry.outgoingLinks)
                {
                    if (link.destinationConversationID == conversation.id && link.destinationDialogueID != entry.id) linkedIDs.Add(link.destinationDialogueID);
                }
            }
            foreach (var entry in entriesByID.Values)
            {
                if (!reachable.Contains(entry.id) && !linkedIDs.Contains(entry.id)) roots.Add(entry);
            }
            roots.AddRange(entriesByID.Values);
            return roots;
        }

        /// <summary>
        /// Builds a tree breadth-first, so an entry with several parents goes under the one
        /// closest to the root, and links back up the tree are ignored.
        /// </summary>
        private static List<TreeNode> BuildTree(DialogueEntry root, int conversationID, Dictionary<int, DialogueEntry> entriesByID,
            HashSet<int> visitedIDs, bool vertically, Func<DialogueEntry, Vector2> getSize)
        {
            Func<DialogueEntry, int, TreeNode> createNode = (entry, level) =>
            {
                var size = getSize(entry);
                return new TreeNode() { entry = entry, level = level, breadth = vertically ? size.x : size.y, length = vertically ? size.y : size.x };
            };
            var rootNode = createNode(root, 0);
            visitedIDs.Add(root.id);
            var nodes = new List<TreeNode>() { rootNode };
            for (int i = 0; i < nodes.Count; i++) // Nodes grows during the loop.
            {
                var node = nodes[i];
                foreach (var link in node.entry.outgoingLinks)
                {
                    DialogueEntry destination;
                    if (link.destinationConversationID != conversationID ||
                        !entriesByID.TryGetValue(link.destinationDialogueID, out destination) ||
                        !visitedIDs.Add(destination.id)) continue;
                    var child = createNode(destination, node.level + 1);
                    node.children.Add(child);
                    nodes.Add(child);
                }
            }
            return nodes;
        }

        /// <summary>
        /// Sets the offsets of a node's children, whose own subtrees are already placed, and
        /// the node's contours. Packing from either end leaves small subtrees against one side,
        /// so the children use the average of both directions. The average is still free of
        /// overlaps, since each pair is at least as far apart as it needs to be in both.
        /// </summary>
        private static void PlaceChildren(TreeNode node)
        {
            node.leftContour.Add(-node.breadth / 2);
            node.rightContour.Add(node.breadth / 2);
            var count = node.children.Count;
            if (count == 0) return;
            var packedLeft = PackChildren(node.children, false);
            var packedRight = PackChildren(node.children, true);
            var firstCenter = (packedLeft[0] + packedRight[0]) / 2;
            var lastCenter = (packedLeft[count - 1] + packedRight[count - 1]) / 2;
            var parentCenter = (firstCenter + lastCenter) / 2;
            for (int i = 0; i < count; i++)
            {
                var child = node.children[i];
                child.offset = (packedLeft[i] + packedRight[i]) / 2 - parentCenter;
                for (int level = 0; level < child.leftContour.Count; level++)
                {
                    var left = child.leftContour[level] + child.offset;
                    var right = child.rightContour[level] + child.offset;
                    if (level + 1 < node.leftContour.Count)
                    {
                        node.leftContour[level + 1] = Mathf.Min(node.leftContour[level + 1], left);
                        node.rightContour[level + 1] = Mathf.Max(node.rightContour[level + 1], right);
                    }
                    else
                    {
                        node.leftContour.Add(left);
                        node.rightContour.Add(right);
                    }
                }
            }
        }

        /// <summary>
        /// Packs sibling subtrees side by side, starting from the first or last child, and
        /// returns each child's center. Only the first child's center is 0 when packing forward.
        /// </summary>
        private static float[] PackChildren(List<TreeNode> children, bool reverse)
        {
            var centers = new float[children.Count];
            var edge = new List<float>(); // The packed subtrees' outer extent on each level.
            for (int i = 0; i < children.Count; i++)
            {
                var index = reverse ? children.Count - 1 - i : i;
                var child = children[index];
                var near = reverse ? child.rightContour : child.leftContour;
                var far = reverse ? child.leftContour : child.rightContour;
                float center = 0;
                var hasOverlap = false;
                for (int level = 0; level < near.Count && level < edge.Count; level++)
                {
                    var required = reverse ? edge[level] - SiblingGap - near[level] : edge[level] + SiblingGap - near[level];
                    center = !hasOverlap ? required : (reverse ? Mathf.Min(center, required) : Mathf.Max(center, required));
                    hasOverlap = true;
                }
                centers[index] = center;
                for (int level = 0; level < far.Count; level++)
                {
                    var value = far[level] + center;
                    if (level < edge.Count) edge[level] = reverse ? Mathf.Min(edge[level], value) : Mathf.Max(edge[level], value);
                    else edge.Add(value);
                }
            }
            return centers;
        }

    }

}

#endif
