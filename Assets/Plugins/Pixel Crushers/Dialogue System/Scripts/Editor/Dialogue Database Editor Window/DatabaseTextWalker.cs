// Copyright (c) Pixel Crushers. All rights reserved.

#if UNITY_2022_1_OR_NEWER

using System.Collections.Generic;
using UnityEditor;

namespace PixelCrushers.DialogueSystem.DialogueEditor
{

    /// <summary>
    /// A string in a dialogue database, as visited by DatabaseTextWalker.
    /// </summary>
    public struct DatabaseTextLocation
    {
        /// <summary>Serialized property path of the string, relative to the database.</summary>
        public string propertyPath;
        public GlobalSearchMatch.Kind kind;
        /// <summary>ID of the asset that owns the string. For entries, the conversation ID.</summary>
        public int assetID;
        /// <summary>Entry ID if kind is Entry, else -1.</summary>
        public int entryID;
        /// <summary>Where the string is, such as "Actor 'Player'".</summary>
        public string ownerLabel;
        /// <summary>What the string is, such as a field title or "Conditions".</summary>
        public string fieldLabel;
        /// <summary>The field whose value this is, or null for strings that aren't field values.</summary>
        public Field field;
        public string value;
    }

    /// <summary>
    /// Visits the strings in a dialogue database that tools such as global search and spell
    /// check work on: every field value of every asset, conversation and dialogue entry, plus
    /// entry Conditions and Script and the database's Description and Global User Script.
    /// Reads the database's C# objects and reports each string's SerializedProperty path,
    /// so changes can go through a SerializedObject. Shows a cancelable progress bar on
    /// large databases.
    /// </summary>
    public class DatabaseTextWalker
    {

        public delegate void Visitor(DatabaseTextLocation location);

        // Walks over fewer strings than this don't show a progress bar.
        private const int ProgressBarThreshold = 20000;

        private readonly DialogueDatabase database;
        private readonly string progressTitle;
        private readonly string progressVerb;
        private Visitor visit;
        private int total;
        private int done;
        private bool showProgress;

        /// <summary>True if the user canceled the last walk.</summary>
        public bool canceled { get; private set; }

        /// <param name="progressTitle">Title of the progress bar, such as "Global Search".</param>
        /// <param name="progressVerb">Verb in the progress bar's message, such as "Searching".</param>
        public DatabaseTextWalker(DialogueDatabase database, string progressTitle, string progressVerb)
        {
            this.database = database;
            this.progressTitle = progressTitle;
            this.progressVerb = progressVerb;
        }

        /// <summary>
        /// Visits the strings of the whole database, or of only one conversation if
        /// conversationID isn't -1. Returns false if the user canceled.
        /// </summary>
        public bool Walk(int conversationID, Visitor visit)
        {
            canceled = false;
            if (database == null || visit == null) return true;
            this.visit = visit;
            done = 0;
            total = database.actors.Count + database.items.Count + database.locations.Count + database.variables.Count;
            foreach (var conversation in database.conversations)
            {
                total += 1 + conversation.dialogueEntries.Count;
            }
            showProgress = total >= ProgressBarThreshold;
            try
            {
                Run(conversationID);
            }
            finally
            {
                this.visit = null;
                if (showProgress) EditorUtility.ClearProgressBar();
            }
            return !canceled;
        }

        private void Run(int conversationID)
        {
            if (conversationID == -1)
            {
                VisitString("description", database.description, null, GlobalSearchMatch.Kind.Database, -1, -1, "Database", "Description");
                VisitString("globalUserScript", database.globalUserScript, null, GlobalSearchMatch.Kind.Database, -1, -1, "Database", "Global User Script");
                if (!VisitAssets(database.actors, "actors", "Actor")) return;
                if (!VisitAssets(database.items, "items", null)) return;
                if (!VisitAssets(database.locations, "locations", "Location")) return;
                if (!VisitAssets(database.variables, "variables", "Variable")) return;
            }
            for (int i = 0; i < database.conversations.Count; i++)
            {
                var conversation = database.conversations[i];
                if (conversationID != -1 && (conversation == null || conversation.id != conversationID)) continue;
                if (!VisitConversation(conversation, i)) return;
            }
        }

        private bool VisitAssets<T>(List<T> assets, string listName, string typeName) where T : Asset
        {
            for (int i = 0; i < assets.Count; i++)
            {
                var asset = assets[i];
                if (!Progress(1)) return false;
                if (asset == null) continue;
                var kind = GetKind(asset);
                var ownerLabel = string.Format("{0} '{1}'", typeName ?? kind.ToString(), EditorTools.GetAssetName(asset));
                VisitFields(asset.fields, string.Format("{0}.Array.data[{1}].fields", listName, i), kind, asset.id, -1, ownerLabel);
            }
            return true;
        }

        private bool VisitConversation(Conversation conversation, int conversationIndex)
        {
            if (conversation == null) return true;
            if (!Progress(1, conversation.Title)) return false;
            var conversationPath = string.Format("conversations.Array.data[{0}]", conversationIndex);
            var conversationLabel = string.Format("Conversation '{0}'", conversation.Title);
            VisitFields(conversation.fields, conversationPath + ".fields", GlobalSearchMatch.Kind.Conversation, conversation.id, -1, conversationLabel);
            for (int i = 0; i < conversation.dialogueEntries.Count; i++)
            {
                var entry = conversation.dialogueEntries[i];
                if (entry == null) continue;
                var entryPath = string.Format("{0}.dialogueEntries.Array.data[{1}]", conversationPath, i);
                var entryLabel = string.Format("{0} entry {1}", conversationLabel, entry.id);
                VisitFields(entry.fields, entryPath + ".fields", GlobalSearchMatch.Kind.Entry, conversation.id, entry.id, entryLabel);
                VisitString(entryPath + ".conditionsString", entry.conditionsString, null, GlobalSearchMatch.Kind.Entry, conversation.id, entry.id, entryLabel, "Conditions");
                VisitString(entryPath + ".userScript", entry.userScript, null, GlobalSearchMatch.Kind.Entry, conversation.id, entry.id, entryLabel, "Script");
            }
            return Progress(conversation.dialogueEntries.Count, conversation.Title);
        }

        private void VisitFields(List<Field> fields, string fieldsPath, GlobalSearchMatch.Kind kind, int assetID, int entryID, string ownerLabel)
        {
            if (fields == null) return;
            for (int i = 0; i < fields.Count; i++)
            {
                var field = fields[i];
                if (field == null) continue;
                VisitString(string.Format("{0}.Array.data[{1}].value", fieldsPath, i), field.value, field, kind, assetID, entryID, ownerLabel, field.title);
            }
        }

        private void VisitString(string propertyPath, string value, Field field, GlobalSearchMatch.Kind kind, int assetID, int entryID, string ownerLabel, string fieldLabel)
        {
            visit(new DatabaseTextLocation()
            {
                propertyPath = propertyPath,
                kind = kind,
                assetID = assetID,
                entryID = entryID,
                ownerLabel = ownerLabel,
                fieldLabel = fieldLabel,
                field = field,
                value = value
            });
        }

        public static GlobalSearchMatch.Kind GetKind(Asset asset)
        {
            if (asset is Actor) return GlobalSearchMatch.Kind.Actor;
            if (asset is Item) return (asset as Item).IsItem ? GlobalSearchMatch.Kind.Item : GlobalSearchMatch.Kind.Quest;
            if (asset is Location) return GlobalSearchMatch.Kind.Location;
            if (asset is Variable) return GlobalSearchMatch.Kind.Variable;
            return GlobalSearchMatch.Kind.Conversation;
        }

        /// <summary>Advances the progress bar. Returns false if the user canceled.</summary>
        private bool Progress(int amount, string conversationTitle = null)
        {
            done += amount;
            if (!showProgress) return true;
            var info = string.IsNullOrEmpty(conversationTitle)
                ? string.Format("{0} database. Please wait...", progressVerb)
                : string.Format("{0} conversation '{1}'. Please wait...", progressVerb, conversationTitle);
            if (EditorUtility.DisplayCancelableProgressBar(progressTitle, info, (float)done / total))
            {
                canceled = true;
                return false;
            }
            return true;
        }

    }

}

#endif
