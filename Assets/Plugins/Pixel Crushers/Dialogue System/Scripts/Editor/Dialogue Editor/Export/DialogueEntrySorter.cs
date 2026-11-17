// Copyright (c) Pixel Crushers. All rights reserved.

using System.Collections.Generic;

namespace PixelCrushers.DialogueSystem.DialogueEditor
{

    /// <summary>
    /// Orders a conversation's dialogue entries by following links from the START entry,
    /// so exported scripts read in conversation order. Entries that the START entry's tree
    /// doesn't reach follow, in trees rooted at unlinked entries ordered left to right on
    /// the canvas, then any remaining entries.
    /// </summary>
    public static class DialogueEntrySorter
    {

        public enum SortMode { DoNotSort, DepthFirst, BreadthFirst }

        /// <summary>Returns the entries in a new list in sorted order. The input list isn't changed.</summary>
        public static List<DialogueEntry> Sort(List<DialogueEntry> entries, SortMode mode)
        {
            switch (mode)
            {
                case SortMode.DepthFirst:
                    return DepthFirstSortEntries(entries);
                case SortMode.BreadthFirst:
                    return BreadthFirstSortEntries(entries);
                default:
                    return new List<DialogueEntry>(entries);
            }
        }

        /// <summary>
        /// Reorders the entries of every conversation in a database. Use it on a copy of the
        /// database, such as one made for exporting, since it changes the database.
        /// </summary>
        public static void SortAllConversations(DialogueDatabase database, SortMode mode)
        {
            if (database == null || mode == SortMode.DoNotSort) return;
            foreach (var conversation in database.conversations)
            {
                conversation.dialogueEntries = Sort(conversation.dialogueEntries, mode);
            }
        }

        public static List<DialogueEntry> DepthFirstSortEntries(List<DialogueEntry> entries)
        {
            var sorted = new List<DialogueEntry>();
            var unprocessed = new List<DialogueEntry>(entries);
            var start = entries.Find(x => x.id == 0);
            if (start == null) return unprocessed;
            SortDepthFirstRecursive(start, sorted, unprocessed);
            DepthFirstSortOrphans(entries, sorted, unprocessed);
            return sorted;
        }

        private static void DepthFirstSortOrphans(List<DialogueEntry> allEntries, List<DialogueEntry> sorted, List<DialogueEntry> unprocessed)
        {
            var orphanRootNodes = GetOrphanRootNodes(allEntries, unprocessed);
            foreach (var orphanRootNode in orphanRootNodes)
            {
                SortDepthFirstRecursive(orphanRootNode, sorted, unprocessed);
            }
            sorted.AddRange(unprocessed);
        }

        private static void SortDepthFirstRecursive(DialogueEntry entry, List<DialogueEntry> sorted, List<DialogueEntry> unprocessed)
        {
            if (entry == null) return;
            unprocessed.Remove(entry);
            if (!sorted.Contains(entry)) sorted.Add(entry);
            foreach (var link in entry.outgoingLinks)
            {
                if (link.destinationConversationID != entry.conversationID) continue;
                var child = unprocessed.Find(x => x.id == link.destinationDialogueID);
                if (child == null) continue;
                SortDepthFirstRecursive(child, sorted, unprocessed);
            }
        }

        public static List<DialogueEntry> BreadthFirstSortEntries(List<DialogueEntry> entries)
        {
            var sorted = new List<DialogueEntry>();
            var unprocessed = new List<DialogueEntry>(entries);
            var start = entries.Find(x => x.id == 0);
            if (start == null) return unprocessed;
            BreadthFirstSortTree(start, sorted, unprocessed);
            BreadthFirstSortOrphans(entries, sorted, unprocessed);
            return sorted;
        }

        private static void BreadthFirstSortOrphans(List<DialogueEntry> allEntries, List<DialogueEntry> sorted, List<DialogueEntry> unprocessed)
        {
            var orphanRootNodes = GetOrphanRootNodes(allEntries, unprocessed);
            foreach (var orphanRootNode in orphanRootNodes)
            {
                BreadthFirstSortTree(orphanRootNode, sorted, unprocessed);
            }
            sorted.AddRange(unprocessed);
        }

        private static void BreadthFirstSortTree(DialogueEntry start, List<DialogueEntry> sorted, List<DialogueEntry> unprocessed)
        {
            var queued = new Queue<DialogueEntry>();
            queued.Enqueue(start);
            while (queued.Count > 0)
            {
                var entry = queued.Dequeue();
                unprocessed.Remove(entry);
                if (!sorted.Contains(entry)) sorted.Add(entry);
                foreach (var link in entry.outgoingLinks)
                {
                    if (link.destinationConversationID != entry.conversationID) continue;
                    var child = unprocessed.Find(x => x.id == link.destinationDialogueID);
                    if (child == null) continue;
                    queued.Enqueue(child);
                }
            }
        }

        // Find unprocessed nodes that aren't in the <START> node's conversation tree but
        // are at the root of their own "orphan" tree. Return a list of these nodes sorted
        // by their horizontal canvas position.
        private static List<DialogueEntry> GetOrphanRootNodes(List<DialogueEntry> allEntries, List<DialogueEntry> unprocessed)
        {
            var orphanRootNodes = new List<DialogueEntry>();
            foreach (var entry in unprocessed)
            {
                if (!DoesAnythingLinkToThisEntry(entry, allEntries))
                {
                    orphanRootNodes.Add(entry);
                }
            }
            orphanRootNodes.Sort((a, b) => a.canvasRect.x.CompareTo(b.canvasRect.x));
            return orphanRootNodes;
        }

        private static bool DoesAnythingLinkToThisEntry(DialogueEntry entry, List<DialogueEntry> allEntries)
        {
            foreach (var potentialParent in allEntries)
            {
                foreach (var link in potentialParent.outgoingLinks)
                {
                    if (link.destinationConversationID == entry.conversationID &&
                        link.destinationDialogueID == entry.id)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

    }

}
