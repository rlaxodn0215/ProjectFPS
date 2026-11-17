// Copyright (c) Pixel Crushers. All rights reserved.

using UnityEngine;
using System.Collections.Generic;
using System.Text;

namespace PixelCrushers.DialogueSystem
{

    /// <summary>
    /// This static utility class merges the contents of a dialogue database into another
    /// dialogue database. Merged assets are copies, so the destination never shares
    /// objects with the source.
    /// </summary>
    public static class DatabaseMerger
    {

        public enum ConflictingIDRule
        {
            /// <summary>
            /// If the same ID exists in both databases, replace the original one with the new one.
            /// </summary>
            ReplaceConflictingIDs,

            /// <summary>
            /// Add assets even if IDs conflict.
            /// </summary>
            AllowConflictingIDs,

            /// <summary>
            /// Assign new IDs to assets from source database.
            /// </summary>
            AssignUniqueIDs
        };

        /// <summary>
        /// What a merge will do, per asset type. Returned by GetMergeSummary, which uses the
        /// same rules as Merge without changing either database.
        /// </summary>
        public class MergeSummary
        {

            public class AssetCounts
            {
                /// <summary>Source assets that will be added to the destination.</summary>
                public int added = 0;

                /// <summary>Source assets that will replace destination assets with the same ID.</summary>
                public int replaced = 0;

                /// <summary>
                /// Source assets that won't be added because the destination already has a
                /// matching one (same name, a Player actor, or an Alert variable). References
                /// to them will use the destination's asset.
                /// </summary>
                public int skipped = 0;

                /// <summary>Conversations that will be added with " Copy" after their titles.</summary>
                public int renamed = 0;

                /// <summary>Added assets whose IDs are already used in the destination.</summary>
                public int idConflicts = 0;
            }

            public bool mergeProperties = false;
            public bool mergeEmphases = false;

            /// <summary>Counts for each asset type, or null if that type isn't merged.</summary>
            public AssetCounts actors = null;
            public AssetCounts items = null;
            public AssetCounts locations = null;
            public AssetCounts variables = null;
            public AssetCounts conversations = null;

            /// <summary>Total ID conflicts across all merged asset types.</summary>
            public int totalIDConflicts
            {
                get
                {
                    int total = 0;
                    foreach (var counts in new AssetCounts[] { actors, items, locations, variables, conversations })
                    {
                        if (counts != null) total += counts.idConflicts;
                    }
                    return total;
                }
            }

            /// <summary>One line per merged part of the database.</summary>
            public override string ToString()
            {
                var sb = new StringBuilder();
                if (mergeProperties) sb.AppendLine(mergeEmphases ? "Database properties and [em#] settings" : "Database properties");
                AppendCounts(sb, "Actors", actors);
                AppendCounts(sb, "Items/Quests", items);
                AppendCounts(sb, "Locations", locations);
                AppendCounts(sb, "Variables", variables);
                AppendCounts(sb, "Conversations", conversations);
                return sb.ToString().TrimEnd();
            }

            private static void AppendCounts(StringBuilder sb, string label, AssetCounts counts)
            {
                if (counts == null) return;
                var parts = new List<string>();
                parts.Add(counts.added + " added");
                if (counts.replaced > 0) parts.Add(counts.replaced + " replaced");
                if (counts.skipped > 0) parts.Add(counts.skipped + " already in database");
                if (counts.renamed > 0) parts.Add(counts.renamed + " renamed with \" Copy\"");
                if (counts.idConflicts > 0) parts.Add(counts.idConflicts + " with conflicting IDs");
                sb.Append(label).Append(": ").AppendLine(string.Join(", ", parts.ToArray()));
            }

        }

        /// <summary>
        /// For AssignUniqueIDs: maps each source asset ID to the ID it will have in the
        /// destination, and records which source assets will be added. A source asset that
        /// isn't added maps to the destination's matching asset, so references to it still
        /// point to the right asset.
        /// </summary>
        private class NewIDs
        {
            public Dictionary<int, int> actor = new Dictionary<int, int>();
            public Dictionary<int, int> item = new Dictionary<int, int>();
            public Dictionary<int, int> location = new Dictionary<int, int>();
            public Dictionary<int, int> variable = new Dictionary<int, int>();
            public Dictionary<int, int> conversation = new Dictionary<int, int>();
            public HashSet<int> addedActors = new HashSet<int>();
            public HashSet<int> addedItems = new HashSet<int>();
            public HashSet<int> addedLocations = new HashSet<int>();
            public HashSet<int> addedVariables = new HashSet<int>();
        }

        public static void Merge(DialogueDatabase destination, DialogueDatabase source, ConflictingIDRule conflictingIDRule,
                                 bool mergeProperties, bool mergeActors, bool mergeItems, bool mergeLocations,
                                 bool mergeVariables, bool mergeConversations)
        {
            Merge(destination, source, conflictingIDRule, mergeProperties, true, mergeActors, mergeItems, mergeLocations, mergeVariables, mergeConversations);
        }


        /// <summary>
        /// Merges a source database into a destination database. Note that if the destination database
        /// has an actor marked IsPlayer, then the source database will use this actor instead of
        /// any IsPlayer actors in the source database. Similarly, only one Alert variable will be added.
        /// This variation allows selective merge of only certain types of assets.
        /// </summary>
        /// <param name="destination">Destination.</param>
        /// <param name="source">Source.</param>
        /// <param name="conflictingIDRule">Specifies how to handle conflicting IDs.</param>
        public static void Merge(DialogueDatabase destination, DialogueDatabase source, ConflictingIDRule conflictingIDRule,
                             bool mergeProperties, bool mergeEmphases, bool mergeActors, bool mergeItems, bool mergeLocations,
                             bool mergeVariables, bool mergeConversations)
        {
            if ((destination != null) && (source != null))
            {
                switch (conflictingIDRule)
                {
                    case ConflictingIDRule.ReplaceConflictingIDs:
                        MergeReplaceConflictingIDs(destination, source, mergeProperties, mergeEmphases, mergeActors, mergeItems, mergeLocations, mergeVariables, mergeConversations);
                        break;
                    case ConflictingIDRule.AllowConflictingIDs:
                        MergeAllowConflictingIDs(destination, source, mergeProperties, mergeEmphases, mergeActors, mergeItems, mergeLocations, mergeVariables, mergeConversations);
                        break;
                    case ConflictingIDRule.AssignUniqueIDs:
                        MergeAssignUniqueIDs(destination, source, mergeProperties, mergeEmphases, mergeActors, mergeItems, mergeLocations, mergeVariables, mergeConversations);
                        break;
                    default:
                        Debug.LogError(string.Format("{0}: Internal error. Unsupported merge type: {1}", new System.Object[] { DialogueDebug.Prefix, conflictingIDRule }));
                        break;
                }
            }
        }

        /// <summary>
        /// Merges a source database into a destination database. Note that if the destination database
        /// has an actor marked IsPlayer, then the source database will use this actor instead of
        /// any IsPlayer actors in the source database. Similarly, only one Alert variable will be added.
        /// </summary>
        /// <param name="destination">Destination.</param>
        /// <param name="source">Source.</param>
        /// <param name="conflictingIDRule">Specifies how to handle conflicting IDs.</param>
        public static void Merge(DialogueDatabase destination, DialogueDatabase source, ConflictingIDRule conflictingIDRule)
        {
            Merge(destination, source, conflictingIDRule, true, true, true, true, true, true);
        }

        /// <summary>
        /// Reports what Merge would do with the same arguments, without changing either database.
        /// Returns null if either database is null.
        /// </summary>
        public static MergeSummary GetMergeSummary(DialogueDatabase destination, DialogueDatabase source, ConflictingIDRule conflictingIDRule,
                             bool mergeProperties, bool mergeEmphases, bool mergeActors, bool mergeItems, bool mergeLocations,
                             bool mergeVariables, bool mergeConversations)
        {
            if (destination == null || source == null) return null;
            var summary = new MergeSummary();
            summary.mergeProperties = mergeProperties;
            summary.mergeEmphases = mergeProperties && mergeEmphases;
            NewIDs newIDs = null;
            if (conflictingIDRule == ConflictingIDRule.AssignUniqueIDs)
            {
                newIDs = GetNewIDs(destination, source, mergeActors, mergeItems, mergeLocations, mergeVariables);
            }
            if (mergeActors) summary.actors = CountAssets(destination.actors, source.actors, conflictingIDRule, (newIDs != null) ? newIDs.addedActors : null);
            if (mergeItems) summary.items = CountAssets(destination.items, source.items, conflictingIDRule, (newIDs != null) ? newIDs.addedItems : null);
            if (mergeLocations) summary.locations = CountAssets(destination.locations, source.locations, conflictingIDRule, (newIDs != null) ? newIDs.addedLocations : null);
            if (mergeVariables) summary.variables = CountAssets(destination.variables, source.variables, conflictingIDRule, (newIDs != null) ? newIDs.addedVariables : null);
            if (mergeConversations)
            {
                summary.conversations = CountAssets(destination.conversations, source.conversations, conflictingIDRule, null);
                if (conflictingIDRule != ConflictingIDRule.ReplaceConflictingIDs)
                {
                    // Same title check as the merge, against the titles added so far too.
                    var titles = new HashSet<string>();
                    foreach (var conversation in destination.conversations)
                    {
                        titles.Add(conversation.Title);
                    }
                    foreach (var conversation in source.conversations)
                    {
                        var isRenamed = titles.Contains(conversation.Title);
                        if (isRenamed) summary.conversations.renamed++;
                        titles.Add(isRenamed ? conversation.Title + " Copy" : conversation.Title);
                    }
                }
            }
            return summary;
        }

        /// <param name="added">For AssignUniqueIDs, the IDs of source assets that will be added, or null to add all.</param>
        private static MergeSummary.AssetCounts CountAssets<T>(List<T> destinationAssets, List<T> sourceAssets,
            ConflictingIDRule conflictingIDRule, HashSet<int> added) where T : Asset
        {
            var counts = new MergeSummary.AssetCounts();
            var destinationIDs = new HashSet<int>();
            foreach (var asset in destinationAssets)
            {
                destinationIDs.Add(asset.id);
            }
            foreach (var asset in sourceAssets)
            {
                switch (conflictingIDRule)
                {
                    case ConflictingIDRule.ReplaceConflictingIDs:
                        if (destinationIDs.Contains(asset.id)) counts.replaced++; else counts.added++;
                        break;
                    case ConflictingIDRule.AllowConflictingIDs:
                        counts.added++;
                        if (destinationIDs.Contains(asset.id)) counts.idConflicts++;
                        break;
                    case ConflictingIDRule.AssignUniqueIDs:
                        if (added == null || added.Contains(asset.id)) counts.added++; else counts.skipped++;
                        break;
                }
            }
            return counts;
        }

        /// <summary>
        /// Merges the database properties, assigning any values that are blank in the destination.
        /// </summary>
        /// <param name="destination">Destination.</param>
        /// <param name="source">Source.</param>
        private static void MergeDatabaseProperties(DialogueDatabase destination, DialogueDatabase source, bool mergeEmphases)
        {
            if (string.IsNullOrEmpty(destination.author)) destination.author = source.author;
            if (string.IsNullOrEmpty(destination.version)) destination.version = source.version;
            if (string.IsNullOrEmpty(destination.description)) destination.description = source.description;
            if (!string.IsNullOrEmpty(source.globalUserScript))
            {
                if (string.IsNullOrEmpty(destination.globalUserScript))
                {
                    destination.globalUserScript = source.globalUserScript;
                }
                else
                {
                    destination.globalUserScript = string.Format("{0}; {1}", new System.Object[] { destination.globalUserScript, source.globalUserScript });
                }
            }
            if (mergeEmphases)
            {
                if (source.emphasisSettings.Length > destination.emphasisSettings.Length)
                {
                    destination.emphasisSettings = new EmphasisSetting[source.emphasisSettings.Length];
                }
                for (int i = 0; i < source.emphasisSettings.Length; i++)
                {
                    var srcEm = source.emphasisSettings[i];
                    destination.emphasisSettings[i] = new EmphasisSetting(srcEm.color, srcEm.bold, srcEm.italic, srcEm.underline);
                }
            }
        }

        /// <summary>
        /// Copies a conversation for merging. Conversation's copy constructor is meant for
        /// duplicating entries in the editor, so it resets each entry's condition priority and
        /// drops its OnExecute event and scene event. A merged conversation keeps them.
        /// </summary>
        private static Conversation CopyConversationForMerge(Conversation sourceConversation)
        {
            var copy = new Conversation(sourceConversation);
            for (int i = 0; i < copy.dialogueEntries.Count && i < sourceConversation.dialogueEntries.Count; i++)
            {
                var sourceEntry = sourceConversation.dialogueEntries[i];
                var copyEntry = copy.dialogueEntries[i];
                copyEntry.conditionPriority = sourceEntry.conditionPriority;
                copyEntry.onExecute = sourceEntry.onExecute;
                var sceneEventGuidField = Field.Lookup(sourceEntry.fields, DialogueSystemFields.SceneEventGuidFieldName);
                if (sceneEventGuidField != null && Field.Lookup(copyEntry.fields, DialogueSystemFields.SceneEventGuidFieldName) == null)
                {
                    copyEntry.fields.Add(new Field(sceneEventGuidField));
                }
            }
            return copy;
        }

        #region Replace Conflicting IDs

        /// <summary>
        /// Merge, replacing the destination's assets with the source's assets when they have the same IDs.
        /// </summary>
        private static void MergeReplaceConflictingIDs(DialogueDatabase destination, DialogueDatabase source,
                                                     bool mergeProperties, bool mergeEmphases, bool mergeActors, bool mergeItems, bool mergeLocations,
                                                     bool mergeVariables, bool mergeConversations)
        {
            if (mergeProperties) MergeDatabaseProperties(destination, source, mergeEmphases);
            if (mergeActors) MergeActorsReplaceConflictingIDs(destination, source);
            if (mergeItems) MergeItemsReplaceConflictingIDs(destination, source);
            if (mergeLocations) MergeLocationsReplaceConflictingIDs(destination, source);
            if (mergeVariables) MergeVariablesReplaceConflictingIDs(destination, source);
            if (mergeConversations) MergeConversationsReplaceConflictingIDs(destination, source);
        }

        private static void MergeActorsReplaceConflictingIDs(DialogueDatabase destination, DialogueDatabase source)
        {
            foreach (Actor actor in source.actors)
            {
                destination.actors.RemoveAll(x => x.id == actor.id);
                destination.actors.Add(new Actor(actor));
            }
        }

        private static void MergeItemsReplaceConflictingIDs(DialogueDatabase destination, DialogueDatabase source)
        {
            foreach (Item item in source.items)
            {
                destination.items.RemoveAll(x => x.id == item.id);
                destination.items.Add(new Item(item));
            }
        }

        private static void MergeLocationsReplaceConflictingIDs(DialogueDatabase destination, DialogueDatabase source)
        {
            foreach (Location location in source.locations)
            {
                destination.locations.RemoveAll(x => x.id == location.id);
                destination.locations.Add(new Location(location));
            }
        }

        private static void MergeVariablesReplaceConflictingIDs(DialogueDatabase destination, DialogueDatabase source)
        {
            foreach (Variable variable in source.variables)
            {
                destination.variables.RemoveAll(x => x.id == variable.id);
                destination.variables.Add(new Variable(variable));
            }
        }

        private static void MergeConversationsReplaceConflictingIDs(DialogueDatabase destination, DialogueDatabase source)
        {
            foreach (Conversation conversation in source.conversations)
            {
                destination.conversations.RemoveAll(x => x.id == conversation.id);
                destination.conversations.Add(CopyConversationForMerge(conversation));
            }
        }

        #endregion

        #region Allow Conflicting IDs

        private static void MergeAllowConflictingIDs(DialogueDatabase destination, DialogueDatabase source,
                                                     bool mergeProperties, bool mergeEmphases, bool mergeActors, bool mergeItems, bool mergeLocations,
                                                     bool mergeVariables, bool mergeConversations)
        {
            if (mergeProperties) MergeDatabaseProperties(destination, source, mergeEmphases);
            if (mergeActors) MergeActorsAllowConflictingIDs(destination, source);
            if (mergeItems) MergeItemsAllowConflictingIDs(destination, source);
            if (mergeLocations) MergeLocationsAllowConflictingIDs(destination, source);
            if (mergeVariables) MergeVariablesAllowConflictingIDs(destination, source);
            if (mergeConversations) MergeConversationsAllowConflictingIDs(destination, source);
        }

        private static void MergeActorsAllowConflictingIDs(DialogueDatabase destination, DialogueDatabase source)
        {
            foreach (Actor actor in source.actors)
            {
                destination.actors.Add(new Actor(actor));
            }
        }

        private static void MergeItemsAllowConflictingIDs(DialogueDatabase destination, DialogueDatabase source)
        {
            foreach (Item item in source.items)
            {
                destination.items.Add(new Item(item));
            }
        }

        private static void MergeLocationsAllowConflictingIDs(DialogueDatabase destination, DialogueDatabase source)
        {
            foreach (Location location in source.locations)
            {
                destination.locations.Add(new Location(location));
            }
        }

        private static void MergeVariablesAllowConflictingIDs(DialogueDatabase destination, DialogueDatabase source)
        {
            foreach (Variable variable in source.variables)
            {
                destination.variables.Add(new Variable(variable));
            }
        }

        private static void MergeConversationsAllowConflictingIDs(DialogueDatabase destination, DialogueDatabase source)
        {
            foreach (Conversation conversation in source.conversations)
            {
                var copy = CopyConversationForMerge(conversation);
                var existingByTitle = destination.conversations.Find(c => string.Equals(c.Title, conversation.Title));
                if (existingByTitle != null) copy.Title = conversation.Title + " Copy";
                destination.conversations.Add(copy);
            }
        }

        #endregion

        #region Assign Unique IDs

        /// <summary>
        /// Merges a source database into a destination database by first determining new IDs,
        /// then assigning those IDs as it copies assets over.
        /// </summary>
        /// <param name="destination">Destination database.</param>
        /// <param name="source">Source database.</param>
        private static void MergeAssignUniqueIDs(DialogueDatabase destination, DialogueDatabase source,
                                                 bool mergeProperties, bool mergeEmphases, bool mergeActors, bool mergeItems, bool mergeLocations,
                                                 bool mergeVariables, bool mergeConversations)
        {
            if (mergeProperties) MergeDatabaseProperties(destination, source, mergeEmphases);
            var newIDs = GetNewIDs(destination, source, mergeActors, mergeItems, mergeLocations, mergeVariables);
            if (mergeActors) MergeActors(destination, source, newIDs);
            if (mergeItems) MergeItems(destination, source, newIDs);
            if (mergeLocations) MergeLocations(destination, source, newIDs);
            if (mergeVariables) MergeVariables(destination, source, newIDs);
            if (mergeConversations) MergeConversations(destination, source, newIDs);
        }

        /// <summary>
        /// Works out the destination ID of every source asset. Assets of types that aren't
        /// merged only map to matching destination assets, since they won't be added.
        /// </summary>
        private static NewIDs GetNewIDs(DialogueDatabase destination, DialogueDatabase source,
            bool mergeActors, bool mergeItems, bool mergeLocations, bool mergeVariables)
        {
            var newIDs = new NewIDs();
            GetNewActorIDs(destination, source, newIDs, mergeActors);
            GetNewItemIDs(destination, source, newIDs, mergeItems);
            GetNewLocationIDs(destination, source, newIDs, mergeLocations);
            GetNewVariableIDs(destination, source, newIDs, mergeVariables);
            GetNewConversationIDs(destination, source, newIDs);
            return newIDs;
        }

        private static int GetNextID<T>(DialogueDatabase destination, List<T> destinationAssets) where T : Asset
        {
            int highestID = destination.baseID - 1;
            foreach (var asset in destinationAssets)
            {
                highestID = Mathf.Max(asset.id, highestID);
            }
            return highestID + 1;
        }

        private static void GetNewActorIDs(DialogueDatabase destination, DialogueDatabase source, NewIDs newIDs, bool willAdd)
        {
            var destinationPlayer = destination.actors.Find(x => x.IsPlayer);
            int newID = GetNextID(destination, destination.actors);
            foreach (var actor in source.actors)
            {
                // Use the destination's player actor instead of the source's, and existing
                // actors with the same name instead of adding duplicates.
                var existing = (actor.IsPlayer && destinationPlayer != null) ? destinationPlayer
                    : destination.actors.Find(x => string.Equals(x.Name, actor.Name));
                if (existing != null)
                {
                    newIDs.actor[actor.id] = existing.id;
                }
                else if (willAdd)
                {
                    newIDs.actor[actor.id] = newID;
                    newIDs.addedActors.Add(actor.id);
                    newID++;
                }
            }
        }

        private static void GetNewItemIDs(DialogueDatabase destination, DialogueDatabase source, NewIDs newIDs, bool willAdd)
        {
            int newID = GetNextID(destination, destination.items);
            foreach (var item in source.items)
            {
                var existing = destination.items.Find(x => string.Equals(x.Name, item.Name));
                if (existing != null)
                {
                    newIDs.item[item.id] = existing.id;
                }
                else if (willAdd)
                {
                    newIDs.item[item.id] = newID;
                    newIDs.addedItems.Add(item.id);
                    newID++;
                }
            }
        }

        private static void GetNewLocationIDs(DialogueDatabase destination, DialogueDatabase source, NewIDs newIDs, bool willAdd)
        {
            int newID = GetNextID(destination, destination.locations);
            foreach (var location in source.locations)
            {
                var existing = destination.locations.Find(x => string.Equals(x.Name, location.Name));
                if (existing != null)
                {
                    newIDs.location[location.id] = existing.id;
                }
                else if (willAdd)
                {
                    newIDs.location[location.id] = newID;
                    newIDs.addedLocations.Add(location.id);
                    newID++;
                }
            }
        }

        private static void GetNewVariableIDs(DialogueDatabase destination, DialogueDatabase source, NewIDs newIDs, bool willAdd)
        {
            int newID = GetNextID(destination, destination.variables);
            foreach (var variable in source.variables)
            {
                // Same name includes the Alert variable, so only one Alert variable is kept.
                var existing = destination.variables.Find(x => string.Equals(x.Name, variable.Name));
                if (existing != null)
                {
                    newIDs.variable[variable.id] = existing.id;
                }
                else if (willAdd)
                {
                    newIDs.variable[variable.id] = newID;
                    newIDs.addedVariables.Add(variable.id);
                    newID++;
                }
            }
        }

        private static void GetNewConversationIDs(DialogueDatabase destination, DialogueDatabase source, NewIDs newIDs)
        {
            int newID = GetNextID(destination, destination.conversations);
            foreach (var conversation in source.conversations)
            {
                newIDs.conversation[conversation.id] = newID;
                newID++;
            }
        }

        private static void ConvertFieldIDs(List<Field> fields, NewIDs newIDs)
        {
            if (fields == null) return;
            foreach (var field in fields)
            {
                int oldID = Tools.StringToInt(field.value);
                switch (field.type)
                {
                    case FieldType.Actor:
                        if (newIDs.actor.ContainsKey(oldID)) field.value = newIDs.actor[oldID].ToString();
                        break;
                    case FieldType.Item:
                        if (newIDs.item.ContainsKey(oldID)) field.value = newIDs.item[oldID].ToString();
                        break;
                    case FieldType.Location:
                        if (newIDs.location.ContainsKey(oldID)) field.value = newIDs.location[oldID].ToString();
                        break;
                }
            }
        }

        private static void MergeActors(DialogueDatabase destination, DialogueDatabase source, NewIDs newIDs)
        {
            foreach (var actor in source.actors)
            {
                if (newIDs.addedActors.Contains(actor.id))
                {
                    Actor newActor = new Actor(actor);
                    newActor.id = newIDs.actor[actor.id];
                    ConvertFieldIDs(newActor.fields, newIDs);
                    destination.actors.Add(newActor);
                }
            }
        }

        private static void MergeItems(DialogueDatabase destination, DialogueDatabase source, NewIDs newIDs)
        {
            foreach (var item in source.items)
            {
                if (newIDs.addedItems.Contains(item.id))
                {
                    Item newItem = new Item(item);
                    newItem.id = newIDs.item[item.id];
                    ConvertFieldIDs(newItem.fields, newIDs);
                    destination.items.Add(newItem);
                }
            }
        }

        private static void MergeLocations(DialogueDatabase destination, DialogueDatabase source, NewIDs newIDs)
        {
            foreach (var location in source.locations)
            {
                if (newIDs.addedLocations.Contains(location.id))
                {
                    Location newLocation = new Location(location);
                    newLocation.id = newIDs.location[location.id];
                    ConvertFieldIDs(newLocation.fields, newIDs);
                    destination.locations.Add(newLocation);
                }
            }
        }

        private static void MergeVariables(DialogueDatabase destination, DialogueDatabase source, NewIDs newIDs)
        {
            foreach (var variable in source.variables)
            {
                if (newIDs.addedVariables.Contains(variable.id))
                {
                    Variable newVariable = new Variable(variable);
                    newVariable.id = newIDs.variable[variable.id];
                    ConvertFieldIDs(newVariable.fields, newIDs);
                    destination.variables.Add(newVariable);
                }
            }
        }

        private static void MergeConversations(DialogueDatabase destination, DialogueDatabase source, NewIDs newIDs)
        {
            foreach (var conversation in source.conversations)
            {
                if (newIDs.conversation.ContainsKey(conversation.id))
                {
                    Conversation newConversation = CopyConversationForMerge(conversation);
                    newConversation.id = newIDs.conversation[conversation.id];
                    var existingByTitle = destination.conversations.Find(c => string.Equals(c.Title, conversation.Title));
                    if (existingByTitle != null) newConversation.Title = conversation.Title + " Copy";
                    ConvertFieldIDs(newConversation.fields, newIDs);
                    foreach (DialogueEntry newEntry in newConversation.dialogueEntries)
                    {
                        newEntry.conversationID = newConversation.id;
                        ConvertFieldIDs(newEntry.fields, newIDs); // Actor, Conversant, and other asset fields.
                        foreach (var newLink in newEntry.outgoingLinks)
                        {
                            if (newIDs.conversation.ContainsKey(newLink.originConversationID)) newLink.originConversationID = newIDs.conversation[newLink.originConversationID];
                            if (newIDs.conversation.ContainsKey(newLink.destinationConversationID)) newLink.destinationConversationID = newIDs.conversation[newLink.destinationConversationID];
                        }
                    }
                    destination.conversations.Add(newConversation);
                }
            }
        }

        #endregion

    }

}
