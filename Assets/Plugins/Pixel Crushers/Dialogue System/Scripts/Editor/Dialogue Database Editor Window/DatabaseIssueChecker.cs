// Copyright (c) Pixel Crushers. All rights reserved.

#if UNITY_2022_1_OR_NEWER

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEditor;

namespace PixelCrushers.DialogueSystem.DialogueEditor
{

    public enum IssueSeverity { Error, Warning, Info }

    public enum IssueCheck { Sequences, LuaSyntax, BrokenLinks, Unreachable, DuplicateIDs, DuplicateFields, MissingActors, UndefinedVariables, EntrytagAudio }

    /// <summary>
    /// One problem found by DatabaseIssueChecker. Kind, assetID and entryID use the same
    /// scheme as GlobalSearchMatch so the editor can navigate to it.
    /// </summary>
    public class DatabaseIssue
    {
        public IssueSeverity severity;
        public IssueCheck check;
        public GlobalSearchMatch.Kind kind;
        /// <summary>ID of the asset. For entries, the conversation ID. -1 for the database itself.</summary>
        public int assetID = -1;
        /// <summary>Entry ID if kind is Entry, else -1.</summary>
        public int entryID = -1;
        public string location;
        public string message;
        /// <summary>Extra information, such as the Sequence text or a parser error.</summary>
        public string detail;
    }

    public class IssueCheckOptions
    {
        public HashSet<IssueCheck> checks = new HashSet<IssueCheck>();
        public EntrytagFormat entrytagFormat = EntrytagFormat.ActorName_ConversationID_EntryID;
    }

    /// <summary>
    /// Checks a dialogue database for problems such as sequence and Lua syntax errors, broken
    /// links, unreachable entries and duplicate IDs. Reads the database without changing it.
    /// </summary>

#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    public static class DatabaseIssueChecker
    {

        public class Result
        {
            public List<DatabaseIssue> issues = new List<DatabaseIssue>();
            public bool canceled = false;
            public DateTime checkedAt;
        }

        /// <summary>Display names of the checks, indexed by IssueCheck.</summary>
        public static readonly string[] CheckNames =
        {
            "Sequences", "Lua Syntax", "Broken Links", "Unreachable Entries", "Duplicate IDs",
            "Duplicate Fields & Titles", "Missing Actors", "Undefined Variables", "Entrytag Audio"
        };

        public static readonly string[] CheckTooltips =
        {
            "Syntax errors in dialogue entries' Sequence, Response Menu Sequence and localized Sequence fields.",
            "Syntax errors in dialogue entries' Conditions and Script, and in the database's Global User Script. The code is parsed, not run.",
            "Links to conversations or dialogue entries that don't exist.",
            "Dialogue entries that can't be reached from their conversation's START entry or from a link in another conversation.",
            "Actors, items/quests, locations, variables or conversations that share an ID, and dialogue entries in a conversation that share an ID.",
            "Assets and dialogue entries that have more than one field with the same title, and conversations with the same title.",
            "Conversations and dialogue entries whose Actor or Conversant isn't an actor in the database.",
            "Variables used in [var=] tags, Conditions or Script that aren't defined in the database's Variables. This is usually fine, since Lua doesn't require variables to be defined.",
            "Non-blank dialogue entries without an entrytag audio clip in a Resources folder or local Addressables. Checking the asset database can take a while."
        };

        /// <summary>Checks that are on until the user changes them.</summary>
        public static readonly IssueCheck[] DefaultChecks =
        {
            IssueCheck.Sequences, IssueCheck.LuaSyntax, IssueCheck.BrokenLinks, IssueCheck.Unreachable,
            IssueCheck.DuplicateIDs, IssueCheck.DuplicateFields, IssueCheck.MissingActors
        };

#if USE_NLUA || OVERRIDE_LUA
        /// <summary>The embedded parser may not accept every construct of the Lua implementation in use.</summary>
        public static readonly bool IsLuaSyntaxCheckAvailable = false;
#else
        public static readonly bool IsLuaSyntaxCheckAvailable = true;
#endif

        // Databases with fewer entries than this don't show a progress bar.
        private const int ProgressBarThreshold = 5000;

        private const string LocalizedSequencePrefix = "Sequence ";

        private static readonly Regex VarTagRegex = new Regex(@"\[var=([^\]]+)\]");
        private static readonly Regex VariableIndexRegex = new Regex(@"\bVariable\s*\[\s*(""([^""]*)""|'([^']*)')\s*\]");
        private static readonly Regex VariableDotRegex = new Regex(@"\bVariable\s*\.\s*([A-Za-z_][A-Za-z0-9_]*)");

        // Variables the Dialogue System sets at runtime, so they don't need to be defined.
        private static readonly HashSet<string> BuiltInVariables = new HashSet<string>
        {
            "Actor", "ActorIndex", "Conversant", "ConversantIndex", "Speaker", "SpeakerIndex", "Alert"
        };

        /// <summary>
        /// Runs the checks. entrytagFormat is used only by the Entrytag Audio check.
        /// </summary>
        public static Result Run(DialogueDatabase database, IssueCheckOptions options)
        {
            var result = new Result() { checkedAt = DateTime.Now };
            if (database == null || options == null) return result;
            var context = new Context(database, options, result);
            try
            {
                context.Run();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            return result;
        }

        /// <summary>
        /// Returns the issues as plain text grouped by check, for the clipboard.
        /// </summary>
        public static string ToText(DialogueDatabase database, Result result)
        {
            var sb = new StringBuilder();
            sb.AppendFormat("Check For Issues: {0} ({1:g})", (database != null) ? database.name : string.Empty, result.checkedAt).AppendLine();
            sb.AppendLine(GetSummary(result.issues));
            for (int c = 0; c < CheckNames.Length; c++)
            {
                var check = (IssueCheck)c;
                var issues = result.issues.FindAll(issue => issue.check == check);
                if (issues.Count == 0) continue;
                sb.AppendLine();
                sb.AppendFormat("{0} ({1}):", CheckNames[c], issues.Count).AppendLine();
                foreach (var issue in issues)
                {
                    sb.AppendFormat("  [{0}] {1}: {2}", issue.severity, issue.location, issue.message).AppendLine();
                    if (!string.IsNullOrEmpty(issue.detail))
                    {
                        sb.Append("      ").AppendLine(issue.detail.Replace("\r\n", "\n").Replace("\n", "\n      "));
                    }
                }
            }
            return sb.ToString();
        }

        /// <summary>Such as "2 errors, 5 warnings, 1 info."</summary>
        public static string GetSummary(List<DatabaseIssue> issues)
        {
            int errors = 0, warnings = 0, infos = 0;
            foreach (var issue in issues)
            {
                switch (issue.severity)
                {
                    case IssueSeverity.Error: errors++; break;
                    case IssueSeverity.Warning: warnings++; break;
                    default: infos++; break;
                }
            }
            if (errors + warnings + infos == 0) return "No issues found.";
            return string.Format("{0} error{1}, {2} warning{3}, {4} info.", errors, (errors == 1) ? string.Empty : "s", warnings, (warnings == 1) ? string.Empty : "s", infos);
        }

        /// <summary>
        /// Checks Lua syntax by parsing the code without running it. Conditions are parsed as
        /// "return (conditions)", the way they're evaluated. Returns null if the syntax is valid.
        /// </summary>
        public static string GetLuaSyntaxError(string luaCode, bool isCondition)
        {
            if (string.IsNullOrWhiteSpace(luaCode) || IsOnlyComment(luaCode)) return null;
            try
            {
                Language.Lua.LuaInterpreter.Parse(isCondition ? "return " + luaCode : luaCode);
                return null;
            }
            catch (Exception e)
            {
                var message = e.Message ?? "Syntax error.";
                const string Prefix = "Code has syntax errors:";
                if (message.StartsWith(Prefix)) message = message.Substring(Prefix.Length).Trim();
                return string.IsNullOrEmpty(message) ? "Syntax error." : message;
            }
        }

        private static bool IsOnlyComment(string luaCode)
        {
            foreach (var line in luaCode.Split('\n'))
            {
                var trimmed = line.Trim();
                if (trimmed.Length > 0 && !trimmed.StartsWith("--")) return false;
            }
            return true;
        }

        private static bool HasRuntimeTags(string s)
        {
            return s.Contains("[var=") || s.Contains("[lua(");
        }

        private class Context
        {
            private readonly DialogueDatabase database;
            private readonly IssueCheckOptions options;
            private readonly Result result;
            private readonly HashSet<int> actorIDs = new HashSet<int>();
            private readonly HashSet<string> definedVariables = new HashSet<string>();
            private readonly bool showProgress;
            private readonly int totalEntries;
            private int entriesDone = 0;

            public Context(DialogueDatabase database, IssueCheckOptions options, Result result)
            {
                this.database = database;
                this.options = options;
                this.result = result;
                foreach (var actor in database.actors)
                {
                    if (actor != null) actorIDs.Add(actor.id);
                }
                foreach (var variable in database.variables)
                {
                    if (variable != null) definedVariables.Add(DialogueLua.StringToTableIndex(variable.Name));
                }
                foreach (var conversation in database.conversations)
                {
                    if (conversation != null) totalEntries += conversation.dialogueEntries.Count;
                }
                showProgress = totalEntries >= ProgressBarThreshold || Has(IssueCheck.EntrytagAudio);
            }

            private bool Has(IssueCheck check)
            {
                if (check == IssueCheck.LuaSyntax && !IsLuaSyntaxCheckAvailable) return false;
                return options.checks.Contains(check);
            }

            private void Add(IssueSeverity severity, IssueCheck check, GlobalSearchMatch.Kind kind, int assetID, int entryID,
                string location, string message, string detail = null)
            {
                result.issues.Add(new DatabaseIssue()
                {
                    severity = severity,
                    check = check,
                    kind = kind,
                    assetID = assetID,
                    entryID = entryID,
                    location = location,
                    message = message,
                    detail = detail
                });
            }

            private void AddEntryIssue(IssueSeverity severity, IssueCheck check, Conversation conversation, DialogueEntry entry, string message, string detail = null)
            {
                Add(severity, check, GlobalSearchMatch.Kind.Entry, conversation.id, entry.id, EntryLocation(conversation, entry), message, detail);
            }

            private static string ConversationLocation(Conversation conversation)
            {
                return string.Format("Conversation '{0}'", conversation.Title);
            }

            private static string EntryLocation(Conversation conversation, DialogueEntry entry)
            {
                return string.Format("Conversation '{0}' entry {1}", conversation.Title, entry.id);
            }

            private static string AssetLocation(Asset asset, GlobalSearchMatch.Kind kind)
            {
                return (asset is Conversation) ? ConversationLocation(asset as Conversation)
                    : string.Format("{0} '{1}'", kind, EditorTools.GetAssetName(asset));
            }

            private static GlobalSearchMatch.Kind GetKind(Asset asset)
            {
                if (asset is Actor) return GlobalSearchMatch.Kind.Actor;
                if (asset is Item) return (asset as Item).IsItem ? GlobalSearchMatch.Kind.Item : GlobalSearchMatch.Kind.Quest;
                if (asset is Location) return GlobalSearchMatch.Kind.Location;
                if (asset is Variable) return GlobalSearchMatch.Kind.Variable;
                return GlobalSearchMatch.Kind.Conversation;
            }

            public void Run()
            {
                if (Has(IssueCheck.LuaSyntax))
                {
                    var error = GetLuaSyntaxError(database.globalUserScript, false);
                    if (error != null) Add(IssueSeverity.Error, IssueCheck.LuaSyntax, GlobalSearchMatch.Kind.Database, -1, -1,
                        "Database", "Global User Script has a Lua syntax error.", error);
                }
                if (Has(IssueCheck.DuplicateIDs))
                {
                    CheckDuplicateAssetIDs(database.actors, "actors");
                    CheckDuplicateAssetIDs(database.items, "items/quests");
                    CheckDuplicateAssetIDs(database.locations, "locations");
                    CheckDuplicateAssetIDs(database.variables, "variables");
                    CheckDuplicateAssetIDs(database.conversations, "conversations");
                }
                if (Has(IssueCheck.DuplicateFields))
                {
                    CheckDuplicateFieldsInAssets(database.actors);
                    CheckDuplicateFieldsInAssets(database.items);
                    CheckDuplicateFieldsInAssets(database.locations);
                    CheckDuplicateFieldsInAssets(database.variables);
                    CheckDuplicateConversationTitles();
                }
                if (Has(IssueCheck.UndefinedVariables))
                {
                    foreach (var item in database.items)
                    {
                        if (item != null) CheckUndefinedVariablesInFields(item.fields, GetKind(item), item.id, -1, AssetLocation(item, GetKind(item)));
                    }
                }

                var reachable = Has(IssueCheck.Unreachable) ? FindReachableEntries() : null;
                List<string> audioPaths = null;
                HashSet<string> addressableKeys = null;
                if (Has(IssueCheck.EntrytagAudio)) FindAudioClips(out audioPaths, out addressableKeys);

                foreach (var conversation in database.conversations)
                {
                    if (conversation == null) continue;
                    if (showProgress && EditorUtility.DisplayCancelableProgressBar("Checking For Issues",
                        string.Format("Conversation '{0}'. Please wait...", conversation.Title), (float)entriesDone / Mathf.Max(1, totalEntries)))
                    {
                        result.canceled = true;
                        return;
                    }
                    entriesDone += conversation.dialogueEntries.Count;
                    CheckConversation(conversation, reachable, audioPaths, addressableKeys);
                }
            }

            private void CheckConversation(Conversation conversation, Dictionary<int, HashSet<int>> reachable,
                List<string> audioPaths, HashSet<string> addressableKeys)
            {
                var conversationLocation = ConversationLocation(conversation);
                if (Has(IssueCheck.DuplicateFields)) CheckDuplicateFields(conversation.fields, GlobalSearchMatch.Kind.Conversation, conversation.id, -1, conversationLocation);
                if (Has(IssueCheck.MissingActors))
                {
                    CheckActorID(conversation.ActorID, "Actor", GlobalSearchMatch.Kind.Conversation, conversation.id, -1, conversationLocation);
                    CheckActorID(conversation.ConversantID, "Conversant", GlobalSearchMatch.Kind.Conversation, conversation.id, -1, conversationLocation);
                }
                if (Has(IssueCheck.UndefinedVariables)) CheckUndefinedVariablesInFields(conversation.fields, GlobalSearchMatch.Kind.Conversation, conversation.id, -1, conversationLocation);

                var entryIDs = new HashSet<int>();
                var reportedDuplicateEntryIDs = new HashSet<int>();
                HashSet<int> reachableInConversation = null;
                if (reachable != null) reachable.TryGetValue(conversation.id, out reachableInConversation);

                foreach (var entry in conversation.dialogueEntries)
                {
                    if (entry == null) continue;
                    var location = EntryLocation(conversation, entry);

                    if (Has(IssueCheck.DuplicateIDs) && !entryIDs.Add(entry.id) && reportedDuplicateEntryIDs.Add(entry.id))
                    {
                        AddEntryIssue(IssueSeverity.Error, IssueCheck.DuplicateIDs, conversation, entry,
                            string.Format("More than one entry has ID {0}. Links to this ID go to only one of them.", entry.id));
                    }
                    if (Has(IssueCheck.Sequences)) CheckSequences(conversation, entry);
                    if (Has(IssueCheck.LuaSyntax))
                    {
                        CheckLua(conversation, entry, entry.conditionsString, true, "Conditions");
                        CheckLua(conversation, entry, entry.userScript, false, "Script");
                    }
                    if (Has(IssueCheck.BrokenLinks)) CheckLinks(conversation, entry);
                    if (reachableInConversation != null && entry.id != ConversationPropertyUtility.StartEntryID && !reachableInConversation.Contains(entry.id))
                    {
                        AddEntryIssue(IssueSeverity.Warning, IssueCheck.Unreachable, conversation, entry,
                            "Can't be reached from START or from another conversation.", GetEntryPreview(entry));
                    }
                    if (Has(IssueCheck.DuplicateFields)) CheckDuplicateFields(entry.fields, GlobalSearchMatch.Kind.Entry, conversation.id, entry.id, location);
                    if (Has(IssueCheck.MissingActors))
                    {
                        CheckActorID(entry.ActorID, "Actor", GlobalSearchMatch.Kind.Entry, conversation.id, entry.id, location);
                        CheckActorID(entry.ConversantID, "Conversant", GlobalSearchMatch.Kind.Entry, conversation.id, entry.id, location);
                    }
                    if (Has(IssueCheck.UndefinedVariables))
                    {
                        CheckUndefinedVariablesInFields(entry.fields, GlobalSearchMatch.Kind.Entry, conversation.id, entry.id, location);
                        CheckUndefinedVariablesInLua(entry.conditionsString, "Conditions", conversation.id, entry.id, location);
                        CheckUndefinedVariablesInLua(entry.userScript, "Script", conversation.id, entry.id, location);
                    }
                    if (audioPaths != null) CheckEntrytagAudio(conversation, entry, audioPaths, addressableKeys);
                }
            }

            private static string GetEntryPreview(DialogueEntry entry)
            {
                var text = entry.DialogueText;
                if (string.IsNullOrEmpty(text)) text = entry.MenuText;
                if (string.IsNullOrEmpty(text)) text = entry.Title;
                return text;
            }

            #region Sequences & Lua

            private void CheckSequences(Conversation conversation, DialogueEntry entry)
            {
                if (entry.fields == null) return;
                foreach (var field in entry.fields)
                {
                    if (field == null || string.IsNullOrEmpty(field.title) || string.IsNullOrEmpty(field.value)) continue;
                    var isSequence = field.title == DialogueSystemFields.Sequence || field.title == DialogueSystemFields.ResponseMenuSequence ||
                        (field.title.StartsWith(LocalizedSequencePrefix) && field.title.Length > LocalizedSequencePrefix.Length);
                    if (!isSequence) continue;
                    if (HasRuntimeTags(field.value))
                    {
                        AddEntryIssue(IssueSeverity.Info, IssueCheck.Sequences, conversation, entry,
                            string.Format("Can't check {0} because it has [var=] or [lua()] tags whose content is set at runtime.", field.title), field.value);
                        continue;
                    }
                    SequenceSyntaxState state;
                    try
                    {
                        state = SequenceEditorTools.CheckSyntax(field.value);
                    }
                    catch (Exception)
                    {
                        state = SequenceSyntaxState.Error;
                    }
                    if (state == SequenceSyntaxState.Error)
                    {
                        AddEntryIssue(IssueSeverity.Error, IssueCheck.Sequences, conversation, entry,
                            string.Format("{0} has a syntax error.", field.title), field.value);
                    }
                }
            }

            private void CheckLua(Conversation conversation, DialogueEntry entry, string code, bool isCondition, string label)
            {
                if (string.IsNullOrWhiteSpace(code)) return;
                if (HasRuntimeTags(code))
                {
                    AddEntryIssue(IssueSeverity.Info, IssueCheck.LuaSyntax, conversation, entry,
                        string.Format("Can't check {0} because it has [var=] or [lua()] tags whose content is set at runtime.", label), code);
                    return;
                }
                var error = GetLuaSyntaxError(code, isCondition);
                if (error != null)
                {
                    AddEntryIssue(IssueSeverity.Error, IssueCheck.LuaSyntax, conversation, entry,
                        string.Format("{0} has a Lua syntax error.", label), code + "\n" + error);
                }
            }

            #endregion

            #region Links

            private void CheckLinks(Conversation conversation, DialogueEntry entry)
            {
                foreach (var link in entry.outgoingLinks)
                {
                    if (link == null) continue;
                    var destination = (link.destinationConversationID == conversation.id) ? conversation : database.GetConversation(link.destinationConversationID);
                    if (destination == null)
                    {
                        AddEntryIssue(IssueSeverity.Error, IssueCheck.BrokenLinks, conversation, entry,
                            string.Format("Links to conversation {0}, which doesn't exist.", link.destinationConversationID));
                    }
                    else if (destination.GetDialogueEntry(link.destinationDialogueID) == null)
                    {
                        AddEntryIssue(IssueSeverity.Error, IssueCheck.BrokenLinks, conversation, entry,
                            (destination == conversation)
                                ? string.Format("Links to entry {0}, which doesn't exist.", link.destinationDialogueID)
                                : string.Format("Links to entry {0} in conversation '{1}', which doesn't exist.", link.destinationDialogueID, destination.Title));
                    }
                }
            }

            /// <summary>
            /// Returns, for each conversation ID, the entries reachable by following links from
            /// every conversation's START and from every entry that a link in another
            /// conversation points to.
            /// </summary>
            private Dictionary<int, HashSet<int>> FindReachableEntries()
            {
                var entriesByConversation = new Dictionary<int, Dictionary<int, DialogueEntry>>();
                var reachable = new Dictionary<int, HashSet<int>>();
                foreach (var conversation in database.conversations)
                {
                    if (conversation == null || entriesByConversation.ContainsKey(conversation.id)) continue;
                    var entries = new Dictionary<int, DialogueEntry>();
                    foreach (var entry in conversation.dialogueEntries)
                    {
                        if (entry != null && !entries.ContainsKey(entry.id)) entries.Add(entry.id, entry);
                    }
                    entriesByConversation.Add(conversation.id, entries);
                    reachable.Add(conversation.id, new HashSet<int>());
                }

                var queue = new Queue<KeyValuePair<int, int>>();
                Action<int, int> visit = (conversationID, entryID) =>
                {
                    HashSet<int> set;
                    if (reachable.TryGetValue(conversationID, out set) && set.Add(entryID))
                    {
                        queue.Enqueue(new KeyValuePair<int, int>(conversationID, entryID));
                    }
                };
                foreach (var kvp in entriesByConversation)
                {
                    visit(kvp.Key, ConversationPropertyUtility.StartEntryID);
                    foreach (var entry in kvp.Value.Values)
                    {
                        foreach (var link in entry.outgoingLinks)
                        {
                            if (link != null && link.destinationConversationID != kvp.Key) visit(link.destinationConversationID, link.destinationDialogueID);
                        }
                    }
                }
                while (queue.Count > 0)
                {
                    var item = queue.Dequeue();
                    DialogueEntry entry;
                    if (!entriesByConversation[item.Key].TryGetValue(item.Value, out entry)) continue;
                    foreach (var link in entry.outgoingLinks)
                    {
                        if (link != null) visit(link.destinationConversationID, link.destinationDialogueID);
                    }
                }
                return reachable;
            }

            #endregion

            #region IDs & Fields

            private void CheckDuplicateAssetIDs<T>(List<T> assets, string typeName) where T : Asset
            {
                var byID = new Dictionary<int, List<T>>();
                foreach (var asset in assets)
                {
                    if (asset == null) continue;
                    List<T> list;
                    if (!byID.TryGetValue(asset.id, out list))
                    {
                        list = new List<T>();
                        byID.Add(asset.id, list);
                    }
                    list.Add(asset);
                }
                foreach (var kvp in byID)
                {
                    if (kvp.Value.Count < 2) continue;
                    var names = new List<string>();
                    foreach (var asset in kvp.Value) names.Add("'" + EditorTools.GetAssetName(asset) + "'");
                    var first = kvp.Value[0];
                    Add(IssueSeverity.Error, IssueCheck.DuplicateIDs, GetKind(first), first.id, -1, AssetLocation(first, GetKind(first)),
                        string.Format("{0} {1} share ID {2}: {3}.", kvp.Value.Count, typeName, kvp.Key, string.Join(", ", names)));
                }
            }

            private void CheckDuplicateFieldsInAssets<T>(List<T> assets) where T : Asset
            {
                foreach (var asset in assets)
                {
                    if (asset == null) continue;
                    var kind = GetKind(asset);
                    CheckDuplicateFields(asset.fields, kind, asset.id, -1, AssetLocation(asset, kind));
                }
            }

            private void CheckDuplicateFields(List<Field> fields, GlobalSearchMatch.Kind kind, int assetID, int entryID, string location)
            {
                if (fields == null) return;
                var titles = new HashSet<string>();
                var duplicates = new List<string>();
                foreach (var field in fields)
                {
                    if (field == null) continue;
                    var title = field.title ?? string.Empty;
                    if (!titles.Add(title) && !duplicates.Contains(title)) duplicates.Add(title);
                }
                if (duplicates.Count == 0) return;
                Add(IssueSeverity.Warning, IssueCheck.DuplicateFields, kind, assetID, entryID, location,
                    string.Format("Duplicate field{0}: {1}.", (duplicates.Count == 1) ? string.Empty : "s", string.Join(", ", duplicates)));
            }

            private void CheckDuplicateConversationTitles()
            {
                var byTitle = new Dictionary<string, List<Conversation>>();
                foreach (var conversation in database.conversations)
                {
                    if (conversation == null) continue;
                    var title = conversation.Title ?? string.Empty;
                    List<Conversation> list;
                    if (!byTitle.TryGetValue(title, out list))
                    {
                        list = new List<Conversation>();
                        byTitle.Add(title, list);
                    }
                    list.Add(conversation);
                }
                foreach (var kvp in byTitle)
                {
                    if (kvp.Value.Count < 2) continue;
                    var first = kvp.Value[0];
                    Add(IssueSeverity.Warning, IssueCheck.DuplicateFields, GlobalSearchMatch.Kind.Conversation, first.id, -1, ConversationLocation(first),
                        string.Format("{0} conversations have the title '{1}'. Starting a conversation by title uses only the first.", kvp.Value.Count, kvp.Key));
                }
            }

            private void CheckActorID(int actorID, string role, GlobalSearchMatch.Kind kind, int assetID, int entryID, string location)
            {
                if (actorIDs.Contains(actorID)) return;
                Add(IssueSeverity.Warning, IssueCheck.MissingActors, kind, assetID, entryID, location,
                    string.Format("{0} ID {1} isn't an actor in the database.", role, actorID));
            }

            #endregion

            #region Variables

            private void CheckUndefinedVariablesInFields(List<Field> fields, GlobalSearchMatch.Kind kind, int assetID, int entryID, string location)
            {
                if (fields == null) return;
                foreach (var field in fields)
                {
                    if (field == null || string.IsNullOrEmpty(field.value) || field.value.IndexOf("[var=", StringComparison.Ordinal) < 0) continue;
                    foreach (Match match in VarTagRegex.Matches(field.value))
                    {
                        var name = match.Groups[1].Value;
                        if (name.StartsWith("?")) continue; // [var=?name] is a text input prompt.
                        ReportIfUndefined(name, field.title, kind, assetID, entryID, location);
                    }
                }
            }

            private void CheckUndefinedVariablesInLua(string code, string label, int conversationID, int entryID, string location)
            {
                if (string.IsNullOrEmpty(code) || code.IndexOf("Variable", StringComparison.Ordinal) < 0) return;
                foreach (Match match in VariableIndexRegex.Matches(code))
                {
                    var name = match.Groups[2].Success && match.Groups[2].Length > 0 ? match.Groups[2].Value : match.Groups[3].Value;
                    ReportIfUndefined(name, label, GlobalSearchMatch.Kind.Entry, conversationID, entryID, location);
                }
                foreach (Match match in VariableDotRegex.Matches(code))
                {
                    ReportIfUndefined(match.Groups[1].Value, label, GlobalSearchMatch.Kind.Entry, conversationID, entryID, location);
                }
            }

            private void ReportIfUndefined(string name, string fieldLabel, GlobalSearchMatch.Kind kind, int assetID, int entryID, string location)
            {
                if (string.IsNullOrEmpty(name) || BuiltInVariables.Contains(name)) return;
                var index = DialogueLua.StringToTableIndex(name);
                if (definedVariables.Contains(index) || BuiltInVariables.Contains(index)) return;
                Add(IssueSeverity.Info, IssueCheck.UndefinedVariables, kind, assetID, entryID, location,
                    string.Format("{0} uses variable '{1}', which isn't defined in Variables.", fieldLabel, name));
            }

            #endregion

            #region Entrytag Audio

            private static void FindAudioClips(out List<string> resourcesPaths, out HashSet<string> addressableKeys)
            {
                EditorUtility.DisplayProgressBar("Checking For Issues", "Finding audio clips. Please wait...", 0);
                resourcesPaths = new List<string>();
                foreach (var guid in AssetDatabase.FindAssets("t:AudioClip"))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!path.Contains("/Resources/")) continue;
                    var extPos = path.LastIndexOf('.');
                    resourcesPaths.Add((extPos > 0) ? path.Substring(0, extPos) : path);
                }
                addressableKeys = new HashSet<string>();
#if USE_ADDRESSABLES
                foreach (var assetPath in AssetDatabase.FindAssets("t:AddressableAssetGroup"))
                {
                    var group = AssetDatabase.LoadAssetAtPath<UnityEditor.AddressableAssets.Settings.AddressableAssetGroup>(AssetDatabase.GUIDToAssetPath(assetPath));
                    if (group == null) continue;
                    var entries = new List<UnityEditor.AddressableAssets.Settings.AddressableAssetEntry>();
                    group.GatherAllAssets(entries, true, true, true);
                    foreach (var entry in entries)
                    {
                        if (entry.TargetAsset is AudioClip) addressableKeys.Add(entry.address);
                    }
                }
#endif
            }

            private void CheckEntrytagAudio(Conversation conversation, DialogueEntry entry, List<string> resourcesPaths, HashSet<string> addressableKeys)
            {
                if (entry.id == ConversationPropertyUtility.StartEntryID) return;
                if (string.IsNullOrEmpty(entry.DialogueText) && string.IsNullOrEmpty(entry.MenuText)) return;
                var entrytag = database.GetEntrytag(conversation, entry, options.entrytagFormat);
                if (string.IsNullOrEmpty(entrytag) || addressableKeys.Contains(entrytag)) return;
                var slashEntrytag = "/" + entrytag;
                foreach (var path in resourcesPaths)
                {
                    if (path.EndsWith(slashEntrytag)) return;
                }
                AddEntryIssue(IssueSeverity.Warning, IssueCheck.EntrytagAudio, conversation, entry,
                    string.Format("No audio clip named '{0}' in a Resources folder or Addressables.", entrytag), GetEntryPreview(entry));
            }

            #endregion

        }

    }

}

#endif
