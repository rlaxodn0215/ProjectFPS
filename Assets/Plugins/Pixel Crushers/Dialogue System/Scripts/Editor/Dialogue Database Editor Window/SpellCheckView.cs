// Copyright (c) Pixel Crushers. All rights reserved.

#if UNITY_2022_1_OR_NEWER

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;

namespace PixelCrushers.DialogueSystem.DialogueEditor
{

    /// <summary>
    /// Spell check UI: settings, a Check Spelling button, and the list of possible
    /// misspellings with Fix, Ignore and Add to Dictionary. Used by the Database panel's
    /// Spell Check section (whole database) and by SpellCheckWindow (one conversation).
    /// </summary>
    public class SpellCheckView : VisualElement
    {

        private const float RowHeight = 22;
        private const float LocationColumnWidthPercent = 28;
        private const float ReplacementWidth = 120;
        private const float SmallButtonWidth = 22;
        private const float FixButtonWidth = 40;
        private const float ButtonWidth = 120;
        private const float SettingsLabelWidth = 170;
        private const float DictionaryListHeight = 120;

        private readonly Func<DialogueDatabaseEditorWindow> getWindow;
        private readonly Func<int> getConversationID;
        private readonly Func<string> getScopeDescription;

        private HelpBox keyHelpBox;
        private Foldout settingsFoldout;
        private TextField apiKeyField;
        private readonly List<KeyValuePair<UnityEngine.UIElements.Toggle, Func<SpellCheckSettings, bool>>> settingToggles =
            new List<KeyValuePair<UnityEngine.UIElements.Toggle, Func<SpellCheckSettings, bool>>>();
        private TextField assetFieldsField;
        private Label dictionaryLabel;
        private ListView dictionaryListView;
        private Button clearCacheButton;
        private PopupField<string> languagePopup;
        private Button checkButton;
        private Button copyButton;
        private Label statusLabel;
        private HelpBox errorBox;
        private ListView listView;

        private readonly List<SpellCheckIssue> issues = new List<SpellCheckIssue>();
        private SpellCheckResult lastResult;
        private SpellCheckRun run;
        private bool isOutOfDate;
        private bool isApplyingFix;
        private int fixedCount;
        private string fixMessage;

        private static SpellCheckSettings settings { get { return SpellCheckSettings.instance; } }

        /// <summary>
        /// Smallest height of the results list. The list grows to fill the rest of the view's
        /// height, but a view in a ScrollView should raise this so the list stays usable when
        /// the section scrolls.
        /// </summary>
        public float ListMinHeight
        {
            get { return listView.resolvedStyle.minHeight.value; }
            set { listView.style.minHeight = value; }
        }

        /// <summary>True while a check is waiting for JSpell.</summary>
        public bool IsRunning { get { return run != null && run.isRunning; } }

        /// <param name="getWindow">The dialogue editor window that shows the database, or null if none.</param>
        /// <param name="getConversationID">The conversation to check, or -1 for the whole database.</param>
        /// <param name="getScopeDescription">What's checked, such as "the entire database".</param>
        public SpellCheckView(Func<DialogueDatabaseEditorWindow> getWindow, Func<int> getConversationID, Func<string> getScopeDescription)
        {
            this.getWindow = getWindow;
            this.getConversationID = getConversationID;
            this.getScopeDescription = getScopeDescription;
            style.flexGrow = 1;

            keyHelpBox = new HelpBox("Spell check uses the JSpell Checker API. Get an API key from RapidAPI (the Basic plan is free), " +
                "then enter it in Settings below.", UnityEngine.UIElements.HelpBoxMessageType.Info);
            var getKeyButton = new Button(OpenApiPage) { text = "Get API Key..." };
            getKeyButton.style.alignSelf = Align.FlexEnd;
            keyHelpBox.Add(getKeyButton);
            Add(keyHelpBox);

            Add(CreateSettingsFoldout());
            settingsFoldout.style.flexShrink = 0;
            keyHelpBox.style.flexShrink = 0;
            Add(CreateHeaderRow());

            errorBox = new HelpBox(string.Empty, UnityEngine.UIElements.HelpBoxMessageType.Error);
            errorBox.style.display = DisplayStyle.None;
            Add(errorBox);

            statusLabel = new Label();
            statusLabel.style.marginTop = 4;
            statusLabel.style.marginLeft = 3;
            statusLabel.style.whiteSpace = WhiteSpace.Normal;
            Add(statusLabel);

            listView = new ListView(issues, RowHeight, MakeRow, BindRow)
            {
                selectionType = SelectionType.Single,
                virtualizationMethod = CollectionVirtualizationMethod.FixedHeight,
                tooltip = "Double-click a row to show it."
            };
            listView.style.flexGrow = 1;
            listView.style.minHeight = 80;
            listView.style.marginTop = 4;
            listView.AddToClassList("ds-bordered");
            listView.itemsChosen += OnItemsChosen;
            Add(listView);

            RegisterCallback<AttachToPanelEvent>(evt => RefreshSettingsFields());
            RegisterCallback<DetachFromPanelEvent>(evt => CancelCheck());
            RefreshSettingsFields();
            RefreshStatus();
        }

        #region Public Methods

        /// <summary>Cancels a check in progress and clears the results, such as when the database changes.</summary>
        public void ClearResults()
        {
            CancelCheck();
            issues.Clear();
            lastResult = null;
            isOutOfDate = false;
            fixedCount = 0;
            fixMessage = null;
            HideError();
            listView.ClearSelection();
            listView.RefreshItems();
            RefreshStatus();
        }

        /// <summary>Notes that the database changed, such as after undo, so the results may be out of date.</summary>
        public void MarkOutOfDate()
        {
            if (isApplyingFix || lastResult == null) return;
            isOutOfDate = true;
            RefreshStatus();
        }

        /// <summary>Redraws the previews in the current theme's highlight color.</summary>
        public void RefreshTheme()
        {
            listView.RefreshItems();
        }

        /// <summary>Checks spelling, unless a check is already running.</summary>
        public void StartCheck()
        {
            if (IsRunning) return;
            var window = getWindow();
            if (window == null || window.Database == null) return;
            window.ApplyPendingChanges();
            issues.Clear();
            lastResult = null;
            isOutOfDate = false;
            fixedCount = 0;
            fixMessage = null;
            HideError();
            listView.ClearSelection();
            listView.RefreshItems();
            run = new SpellCheckRun(window.Database, getConversationID(), settings, OnRunCompleted);
            run.progressChanged = RefreshStatus;
            RefreshStatus();
            run.Start(); // May complete right away if every word is cached.
        }

        public void CancelCheck()
        {
            if (IsRunning) run.Cancel();
        }

        #endregion

        #region Settings

        private VisualElement CreateSettingsFoldout()
        {
            settingsFoldout = new Foldout() { text = "Settings", value = settings.settingsFoldout };
            settingsFoldout.RegisterValueChangedCallback(evt =>
            {
                if (evt.target != settingsFoldout) return;
                settings.settingsFoldout = evt.newValue;
                settings.Save();
            });

            var keyRow = new VisualElement();
            keyRow.style.flexDirection = FlexDirection.Row;
            keyRow.style.flexShrink = 0;
            keyRow.style.alignItems = Align.Center;
            apiKeyField = new TextField("API Key")
            {
                isPasswordField = true,
                isDelayed = true,
                tooltip = "Your RapidAPI key for the JSpell Checker API. It's saved in this computer's editor preferences, not in the project."
            };
            apiKeyField.labelElement.style.minWidth = SettingsLabelWidth;
            apiKeyField.style.flexGrow = 1;
            apiKeyField.style.flexShrink = 1;
            apiKeyField.RegisterValueChangedCallback(evt =>
            {
                settings.apiKey = (evt.newValue ?? string.Empty).Trim();
                settings.Save();
                RefreshKeyHelp();
                RefreshButtons();
            });
            keyRow.Add(apiKeyField);
            var getKeyButton = new Button(OpenApiPage) { text = "Get Key...", tooltip = "Open the JSpell Checker API page on RapidAPI." };
            getKeyButton.style.flexShrink = 0;
            keyRow.Add(getKeyButton);
            settingsFoldout.Add(keyRow);

            AddSettingToggle("Check Localized Fields", "Also check localized fields, such as 'Menu Text fr', in languages that JSpell supports (English, French, Spanish, German and Italian).",
                s => s.checkLocalizedFields, (s, v) => s.checkLocalizedFields = v);
            AddSettingToggle("Ignore Database Names", "Don't flag words in actor, item, quest, location and variable names or conversation titles.",
                s => s.ignoreDatabaseNames, (s, v) => s.ignoreDatabaseNames = v);
            AddSettingToggle("Check Repeated Words", "Flag a word repeated with only spaces between, such as 'the the'.",
                s => s.checkDoubledWords, (s, v) => s.checkDoubledWords = v);
            AddSettingToggle("Ignore Words With Numbers", "Don't check words that contain digits, such as 'R2D2'.",
                s => s.ignoreWordsWithNumbers, (s, v) => s.ignoreWordsWithNumbers = v);
            AddSettingToggle("Ignore All-Caps Words", "Don't check words in all capitals, such as acronyms.",
                s => s.ignoreAllCapsWords, (s, v) => s.ignoreAllCapsWords = v);
            AddSettingToggle("Ignore Capitalized Words", "Don't check words that start with a capital letter, such as names. Misspellings at the start of sentences are missed too.",
                s => s.ignoreCapitalizedWords, (s, v) => s.ignoreCapitalizedWords = v);

            assetFieldsField = new TextField("Actor/Item/Location Fields")
            {
                isDelayed = true,
                tooltip = "Comma-separated titles of actor, item, quest and location fields to check. # matches a number and * matches anything. " +
                    "Dialogue entries always check Dialogue Text and Menu Text."
            };
            assetFieldsField.labelElement.style.minWidth = SettingsLabelWidth;
            assetFieldsField.RegisterValueChangedCallback(evt =>
            {
                settings.assetFieldTitles = evt.newValue ?? string.Empty;
                settings.Save();
            });
            settingsFoldout.Add(assetFieldsField);

            settingsFoldout.Add(CreateDictionaryEditor());

            var buttonRow = new VisualElement();
            buttonRow.style.flexDirection = FlexDirection.Row;
            buttonRow.style.flexShrink = 0;
            buttonRow.style.justifyContent = Justify.FlexEnd;
            buttonRow.style.marginTop = 4;
            var clearIgnoredButton = new Button(() =>
            {
                SpellChecker.ClearIgnoredWords();
                fixMessage = "Cleared ignored words. Check again to see them.";
                RefreshStatus();
            })
            {
                text = "Clear Ignored Words",
                tooltip = "Stop ignoring the words and lines that you clicked Ignore or Ignore All on in this editor session."
            };
            buttonRow.Add(clearIgnoredButton);
            clearCacheButton = new Button(ClearCache)
            {
                tooltip = "Forget JSpell's answers for the words already checked, so they're sent again next time. Uses API quota."
            };
            buttonRow.Add(clearCacheButton);
            settingsFoldout.Add(buttonRow);

            var privacyNote = new HelpBox("Spell check sends each distinct word, not your text, to the JSpell Checker API through RapidAPI. " +
                "Words already checked are remembered in " + SpellCheckWordCache.RelativePath + " so they aren't sent again.",
                UnityEngine.UIElements.HelpBoxMessageType.None);
            privacyNote.style.marginTop = 4;
            settingsFoldout.Add(privacyNote);
            return settingsFoldout;
        }

        private void AddSettingToggle(string label, string tooltip, Func<SpellCheckSettings, bool> getValue, Action<SpellCheckSettings, bool> setValue)
        {
            var toggle = new UnityEngine.UIElements.Toggle(label) { tooltip = tooltip };
            toggle.labelElement.style.minWidth = SettingsLabelWidth;
            toggle.RegisterValueChangedCallback(evt =>
            {
                setValue(settings, evt.newValue);
                settings.Save();
            });
            settingToggles.Add(new KeyValuePair<UnityEngine.UIElements.Toggle, Func<SpellCheckSettings, bool>>(toggle, getValue));
            settingsFoldout.Add(toggle);
        }

        private VisualElement CreateDictionaryEditor()
        {
            var container = new VisualElement();
            container.style.marginTop = 6;
            dictionaryLabel = new Label()
            {
                tooltip = "Extra words that are spelled correctly, such as names. Saved in " + SpellCheckDictionary.RelativePath +
                    " so your team shares them through version control."
            };
            dictionaryLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            dictionaryLabel.style.marginLeft = 3;
            container.Add(dictionaryLabel);

            dictionaryListView = new ListView(SpellCheckDictionary.Words, 20, MakeDictionaryRow, BindDictionaryRow)
            {
                selectionType = SelectionType.None,
                virtualizationMethod = CollectionVirtualizationMethod.FixedHeight
            };
            dictionaryListView.style.height = DictionaryListHeight;
            dictionaryListView.style.flexShrink = 0;
            dictionaryListView.AddToClassList("ds-bordered");
            container.Add(dictionaryListView);

            var addRow = new VisualElement();
            addRow.style.flexDirection = FlexDirection.Row;
            addRow.style.flexShrink = 0;
            addRow.style.alignItems = Align.Center;
            var addField = new TextField() { tooltip = "A word to add to the dictionary." };
            addField.style.flexGrow = 1;
            addField.style.flexShrink = 1;
            addRow.Add(addField);
            Action addWord = () =>
            {
                var word = (addField.value ?? string.Empty).Trim();
                if (word.Length == 0) return;
                SpellCheckDictionary.Add(word);
                addField.SetValueWithoutNotify(string.Empty);
                RemoveIssuesForWord(word, false);
                RefreshDictionary();
            };
            addField.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter) addWord();
            });
            var addButton = new Button(addWord) { text = "Add", tooltip = "Add the word to the dictionary." };
            addButton.style.flexShrink = 0;
            addRow.Add(addButton);
            container.Add(addRow);
            return container;
        }

        private VisualElement MakeDictionaryRow()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            var label = new Label();
            label.style.flexGrow = 1;
            label.style.marginLeft = 3;
            row.Add(label);
            var removeButton = new Button() { text = "-", tooltip = "Remove the word from the dictionary." };
            removeButton.style.width = SmallButtonWidth;
            removeButton.clicked += () =>
            {
                if (!(row.userData is string word)) return;
                SpellCheckDictionary.Remove(word);
                RefreshDictionary();
            };
            row.Add(removeButton);
            return row;
        }

        private void BindDictionaryRow(VisualElement row, int index)
        {
            var words = SpellCheckDictionary.Words;
            var word = (0 <= index && index < words.Count) ? words[index] : string.Empty;
            row.userData = word;
            row.Q<Label>().text = word;
        }

        private void RefreshDictionary()
        {
            if (dictionaryListView == null) return;
            var words = SpellCheckDictionary.Words;
            dictionaryListView.itemsSource = words;
            dictionaryListView.RefreshItems();
            dictionaryLabel.text = string.Format("Dictionary ({0} word{1})", words.Count, (words.Count == 1) ? string.Empty : "s");
        }

        /// <summary>Shows the saved settings, which another spell check view may have changed.</summary>
        private void RefreshSettingsFields()
        {
            var s = settings;
            apiKeyField.SetValueWithoutNotify(s.apiKey ?? string.Empty);
            foreach (var kvp in settingToggles)
            {
                kvp.Key.SetValueWithoutNotify(kvp.Value(s));
            }
            assetFieldsField.SetValueWithoutNotify(s.assetFieldTitles ?? string.Empty);
            if (languagePopup != null) languagePopup.SetValueWithoutNotify(s.mainLanguage);
            RefreshDictionary();
            RefreshCacheButton();
            RefreshKeyHelp();
            RefreshButtons();
        }

        private void RefreshKeyHelp()
        {
            var hasKey = settings.HasApiKey;
            keyHelpBox.style.display = hasKey ? DisplayStyle.None : DisplayStyle.Flex;
            if (!hasKey && !settingsFoldout.value) settingsFoldout.value = true;
        }

        private void RefreshCacheButton()
        {
            if (clearCacheButton != null) clearCacheButton.text = string.Format("Clear Cache ({0} words)", SpellCheckWordCache.Count);
        }

        private void ClearCache()
        {
            if (!EditorUtility.DisplayDialog("Clear Spell Check Cache",
                "Forget JSpell's answers for all words checked so far? They'll be sent again on the next check, which uses API quota.", "Clear", "Cancel")) return;
            SpellCheckWordCache.Clear();
            RefreshCacheButton();
        }

        private static void OpenApiPage()
        {
            Application.OpenURL(JSpellClient.ApiPageUrl);
        }

        #endregion

        #region Header and Status

        private VisualElement CreateHeaderRow()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginTop = 6;
            row.style.flexShrink = 0;

            var codes = new List<string>(JSpellClient.LanguageCodes);
            var language = codes.Contains(settings.mainLanguage) ? settings.mainLanguage : codes[0];
            languagePopup = new PopupField<string>("Language", codes, codes.IndexOf(language), JSpellClient.GetLanguageName, JSpellClient.GetLanguageName)
            {
                tooltip = "Language of the main text. Localized fields are checked in their own languages."
            };
            languagePopup.style.flexGrow = 1;
            languagePopup.style.flexShrink = 1;
            languagePopup.RegisterValueChangedCallback(evt =>
            {
                settings.mainLanguage = evt.newValue;
                settings.Save();
            });
            row.Add(languagePopup);

            checkButton = new Button(OnCheckButtonClicked);
            checkButton.style.width = ButtonWidth;
            row.Add(checkButton);
            copyButton = new Button(CopyAsText) { text = "Copy as Text", tooltip = "Copy the list to the clipboard as text." };
            copyButton.style.width = ButtonWidth;
            row.Add(copyButton);
            return row;
        }

        private void OnCheckButtonClicked()
        {
            if (IsRunning) CancelCheck(); else StartCheck();
        }

        private void OnRunCompleted(SpellCheckResult result)
        {
            run = null;
            lastResult = result;
            issues.Clear();
            issues.AddRange(result.issues);
            if (!string.IsNullOrEmpty(result.error)) ShowError(result.error); else HideError();
            listView.ClearSelection();
            listView.RefreshItems();
            RefreshCacheButton();
            RefreshStatus();
        }

        private void RefreshButtons()
        {
            if (checkButton == null) return;
            var window = getWindow();
            var canCheck = window != null && window.Database != null;
            checkButton.text = IsRunning ? "Cancel" : "Check Spelling";
            checkButton.tooltip = IsRunning ? "Stop checking. Words already checked are kept."
                : canCheck ? "Check spelling and list possible misspellings below." : "Open the database in the Dialogue Editor first.";
            checkButton.SetEnabled(IsRunning || canCheck);
            copyButton.SetEnabled(issues.Count > 0);
        }

        private void RefreshStatus()
        {
            if (statusLabel == null) return;
            RefreshButtons();
            if (IsRunning)
            {
                statusLabel.text = run.progressText ?? "Checking spelling...";
                return;
            }
            if (lastResult == null)
            {
                statusLabel.text = (fixMessage != null) ? fixMessage :
                    string.Format("Click Check Spelling to check {0}.", (getScopeDescription != null) ? getScopeDescription() : "the database");
                return;
            }
            var r = lastResult;
            if (!string.IsNullOrEmpty(r.error) && r.wordsSent == 0 && r.wordsFromCache == 0 && issues.Count == 0)
            {
                statusLabel.text = "Not checked.";
                return;
            }
            var text = (issues.Count == 0) ? "No possible misspellings found." :
                string.Format("{0} possible misspelling{1} in {2} field{3}.", issues.Count, (issues.Count == 1) ? string.Empty : "s",
                    CountFieldsWithIssues(), (CountFieldsWithIssues() == 1) ? string.Empty : "s");
            text += string.Format(" {0} distinct word{1} in {2} field{3}: {4} request{5} sent, {6} word{7} already cached.",
                r.checkedWordCount, (r.checkedWordCount == 1) ? string.Empty : "s",
                r.targetCount, (r.targetCount == 1) ? string.Empty : "s",
                r.requestsSent, (r.requestsSent == 1) ? string.Empty : "s",
                r.wordsFromCache, (r.wordsFromCache == 1) ? string.Empty : "s");
            if (r.unsupportedLanguages.Count > 0) text += " Skipped languages JSpell doesn't support: " + string.Join(", ", r.unsupportedLanguages) + ".";
            if (fixedCount > 0) text += string.Format(" Fixed {0}.", fixedCount);
            if (fixMessage != null) text += " " + fixMessage;
            if (r.canceled) text += " (Canceled; results are partial.)";
            else if (!string.IsNullOrEmpty(r.error)) text += " (Results are partial.)";
            if (isOutOfDate) text += " (May be out of date. Check again.)";
            statusLabel.text = text;
        }

        private int CountFieldsWithIssues()
        {
            var targets = new HashSet<SpellCheckTarget>();
            foreach (var issue in issues)
            {
                targets.Add(issue.target);
            }
            return targets.Count;
        }

        private void ShowError(string message)
        {
            errorBox.text = message;
            errorBox.style.display = DisplayStyle.Flex;
        }

        private void HideError()
        {
            if (errorBox != null) errorBox.style.display = DisplayStyle.None;
        }

        private void CopyAsText()
        {
            if (issues.Count == 0) return;
            EditorGUIUtility.systemCopyBuffer = SpellChecker.ToText(issues);
            fixMessage = "Copied to clipboard.";
            RefreshStatus();
        }

        #endregion

        #region Results List

        private VisualElement MakeRow()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.height = RowHeight;

            var locationLabel = new Label() { name = "location" };
            locationLabel.style.width = Length.Percent(LocationColumnWidthPercent);
            locationLabel.style.flexShrink = 0;
            locationLabel.style.overflow = Overflow.Hidden;
            locationLabel.style.textOverflow = TextOverflow.Ellipsis;
            locationLabel.style.marginLeft = 3;
            row.Add(locationLabel);

            var previewLabel = new Label() { name = "preview", enableRichText = true };
            previewLabel.style.flexGrow = 1;
            previewLabel.style.flexShrink = 1;
            previewLabel.style.overflow = Overflow.Hidden;
            previewLabel.style.textOverflow = TextOverflow.Ellipsis;
            row.Add(previewLabel);

            var replacementField = new TextField() { name = "replacement", tooltip = "The text that Fix replaces the highlighted text with." };
            replacementField.style.width = ReplacementWidth;
            replacementField.style.flexShrink = 0;
            replacementField.RegisterValueChangedCallback(evt =>
            {
                if (!(row.userData is SpellCheckIssue issue)) return;
                issue.replacement = evt.newValue;
                RefreshRowButtons(row, issue);
            });
            row.Add(replacementField);

            var suggestionsButton = new Button() { name = "suggestions", text = "▾", tooltip = "Choose a suggestion." };
            suggestionsButton.style.width = SmallButtonWidth;
            suggestionsButton.style.flexShrink = 0;
            suggestionsButton.style.marginLeft = 0;
            suggestionsButton.clicked += () => ShowSuggestionsMenu(row, suggestionsButton);
            row.Add(suggestionsButton);

            var fixButton = new Button() { name = "fix", text = "Fix", tooltip = "Replace the highlighted text. You can undo it." };
            fixButton.style.width = FixButtonWidth;
            fixButton.style.flexShrink = 0;
            fixButton.clicked += () =>
            {
                if (row.userData is SpellCheckIssue issue) Fix(new List<SpellCheckIssue>() { issue });
            };
            row.Add(fixButton);

            var moreButton = new Button() { name = "more", text = "⋯", tooltip = "More actions: Fix All, Ignore, Ignore All, Add to Dictionary." };
            moreButton.style.width = SmallButtonWidth;
            moreButton.style.flexShrink = 0;
            moreButton.clicked += () => ShowMoreMenu(row, moreButton);
            row.Add(moreButton);
            return row;
        }

        private void BindRow(VisualElement row, int index)
        {
            if (index < 0 || index >= issues.Count) return;
            var issue = issues[index];
            row.userData = issue;
            var target = issue.target;
            var locationLabel = row.Q<Label>("location");
            locationLabel.text = target.ownerLabel + " / " + target.fieldLabel;
            locationLabel.tooltip = locationLabel.text;
            var previewLabel = row.Q<Label>("preview");
            previewLabel.text = RichTextPreview.Build(target.value, issue.position, issue.original.Length);
            previewLabel.tooltip = issue.isDoubledWord ? "Repeated word." : (issue.suggestions.Length == 0) ? "No suggestions." :
                "Suggestions: " + string.Join(", ", issue.suggestions);
            row.Q<TextField>("replacement").SetValueWithoutNotify(issue.replacement ?? string.Empty);
            row.Q<Button>("suggestions").SetEnabled(issue.suggestions.Length > 0);
            RefreshRowButtons(row, issue);
        }

        private void RefreshRowButtons(VisualElement row, SpellCheckIssue issue)
        {
            row.Q<Button>("fix").SetEnabled(!string.IsNullOrEmpty(issue.replacement) && issue.replacement != issue.original);
        }

        private void ShowSuggestionsMenu(VisualElement row, VisualElement anchor)
        {
            if (!(row.userData is SpellCheckIssue issue) || issue.suggestions.Length == 0) return;
            var menu = new GenericMenu();
            foreach (var suggestion in issue.suggestions)
            {
                var s = suggestion;
                menu.AddItem(new GUIContent(EscapeMenuText(s)), issue.replacement == s, () =>
                {
                    issue.replacement = s;
                    listView.RefreshItems();
                });
            }
            menu.DropDown(anchor.worldBound);
        }

        private void ShowMoreMenu(VisualElement row, VisualElement anchor)
        {
            if (!(row.userData is SpellCheckIssue issue)) return;
            var menu = new GenericMenu();
            var sameIssues = FindSameIssues(issue);
            var canFix = !string.IsNullOrEmpty(issue.replacement) && issue.replacement != issue.original;
            var fixAllText = string.Format("Fix All '{0}' With '{1}' ({2})", EscapeMenuText(issue.original), EscapeMenuText(issue.replacement), sameIssues.Count);
            if (canFix) menu.AddItem(new GUIContent(fixAllText), false, () => FixAll(issue)); else menu.AddDisabledItem(new GUIContent(fixAllText));
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Ignore"), false, () =>
            {
                SpellChecker.IgnoreOccurrence(issue);
                RemoveIssues(new List<SpellCheckIssue>() { issue });
            });
            menu.AddItem(new GUIContent(string.Format("Ignore All '{0}'", EscapeMenuText(issue.word))), false, () =>
            {
                SpellChecker.IgnoreWord(issue.word);
                RemoveIssuesForWord(issue.word, issue.isDoubledWord);
            });
            var addText = string.Format("Add '{0}' to Dictionary", EscapeMenuText(issue.word));
            if (issue.isDoubledWord)
            {
                menu.AddDisabledItem(new GUIContent(addText));
            }
            else
            {
                menu.AddItem(new GUIContent(addText), false, () =>
                {
                    SpellCheckDictionary.Add(issue.word);
                    RemoveIssuesForWord(issue.word, false);
                    RefreshDictionary();
                });
            }
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Show"), false, () => GoTo(issue));
            menu.DropDown(anchor.worldBound);
        }

        /// <summary>Keeps '/' in words from making submenus.</summary>
        private static string EscapeMenuText(string s)
        {
            return string.IsNullOrEmpty(s) ? string.Empty : s.Replace('/', '∕');
        }

        private List<SpellCheckIssue> FindSameIssues(SpellCheckIssue issue)
        {
            return issues.FindAll(other => other.isDoubledWord == issue.isDoubledWord && string.Equals(other.original, issue.original, StringComparison.Ordinal));
        }

        private void FixAll(SpellCheckIssue issue)
        {
            var sameIssues = FindSameIssues(issue);
            foreach (var other in sameIssues)
            {
                other.replacement = issue.replacement;
            }
            Fix(sameIssues);
        }

        private void Fix(List<SpellCheckIssue> toFix)
        {
            var window = getWindow();
            if (window == null || window.SerializedObject == null || window.SerializedObject.targetObject == null)
            {
                ShowError("Open this database in the Dialogue Editor to fix words.");
                return;
            }
            window.ApplyPendingChanges();
            List<SpellCheckIssue> fixedIssues;
            isApplyingFix = true;
            try
            {
                fixedIssues = SpellChecker.ApplyFixes(window.SerializedObject, toFix, issues);
                if (fixedIssues.Count > 0) window.RefreshAllPanels(); // Updates node text and lists, like Global Search & Replace.
            }
            finally
            {
                isApplyingFix = false;
            }
            fixedCount += fixedIssues.Count;
            var skipped = toFix.Count - fixedIssues.Count;
            fixMessage = (skipped > 0) ? string.Format("{0} couldn't be fixed because the text changed. Check again.", skipped) : null;
            RemoveIssues(fixedIssues);
        }

        private void RemoveIssuesForWord(string word, bool isDoubledWord)
        {
            if (string.IsNullOrEmpty(word)) return;
            RemoveIssues(issues.FindAll(issue => issue.isDoubledWord == isDoubledWord && string.Equals(issue.word, word, StringComparison.OrdinalIgnoreCase)));
        }

        private void RemoveIssues(List<SpellCheckIssue> toRemove)
        {
            if (toRemove.Count > 0)
            {
                var removeSet = new HashSet<SpellCheckIssue>(toRemove);
                issues.RemoveAll(issue => removeSet.Contains(issue));
                listView.ClearSelection();
                listView.RefreshItems();
            }
            RefreshStatus();
        }

        private void OnItemsChosen(IEnumerable<object> items)
        {
            foreach (var item in items)
            {
                if (item is SpellCheckIssue issue) GoTo(issue);
                return;
            }
        }

        private void GoTo(SpellCheckIssue issue)
        {
            var window = getWindow();
            if (window == null) return;
            var target = issue.target;
            switch (target.kind)
            {
                case GlobalSearchMatch.Kind.Actor:
                    window.GoToAsset(DialogueDatabaseEditorWindow.Category.Actors, target.assetID);
                    break;
                case GlobalSearchMatch.Kind.Item:
                case GlobalSearchMatch.Kind.Quest:
                    window.GoToAsset(DialogueDatabaseEditorWindow.Category.ItemsQuests, target.assetID);
                    break;
                case GlobalSearchMatch.Kind.Location:
                    window.GoToAsset(DialogueDatabaseEditorWindow.Category.Locations, target.assetID);
                    break;
                case GlobalSearchMatch.Kind.Variable:
                    window.GoToAsset(DialogueDatabaseEditorWindow.Category.Variables, target.assetID);
                    break;
                case GlobalSearchMatch.Kind.Conversation:
                case GlobalSearchMatch.Kind.Entry:
                    window.GoToAsset(DialogueDatabaseEditorWindow.Category.Conversations, target.assetID, target.entryID);
                    break;
                default:
                    return;
            }
            window.Focus();
        }

        #endregion

    }

}

#endif
