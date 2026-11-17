// Copyright (c) Pixel Crushers. All rights reserved.

#if UNITY_2022_1_OR_NEWER && !USE_OPENAI

using System;
using UnityEngine.UIElements;

namespace PixelCrushers.DialogueSystem.DialogueEditor
{

    /// <summary>
    /// Default AI hook implementations for the UI Toolkit dialogue database editor window
    /// when no AI content generation addon is installed. They add nothing. The Addon for
    /// OpenAI implements them in DialogueDatabaseEditorWindowOpenAI.cs when USE_OPENAI is defined.
    /// Create* hooks return null, and callers only add the element if it's not null.
    /// </summary>
    public partial class DialogueDatabaseEditorWindow
    {

        /// <summary>
        /// True if an AI addon is installed. The Database panel only shows its AI Addon section
        /// if this is true. Static because the panel builds its sections before its window is assigned.
        /// </summary>
        public static bool HasAIAddon { get { return false; } }

        /// <summary>Fills the Database panel's AI Addon section.</summary>
        public void AddAIAddonSection(VisualElement parent)
        {
        }

        /// <summary>Opens the addon's Translate Database panel with the localization languages.</summary>
        public void OpenTranslateDatabasePanel()
        {
        }

        /// <summary>Button next to the Conversations list's add button.</summary>
        public VisualElement CreateAIGenerateConversationButton()
        {
            return null;
        }

        /// <summary>Button in the conversation inspector's header.</summary>
        public VisualElement CreateAIBranchingConversationButton(int conversationID)
        {
            return null;
        }

        /// <summary>Button to revise or translate a text field.</summary>
        public VisualElement CreateAIReviseTextButton(AIFieldTarget target)
        {
            return null;
        }

        /// <summary>Button to translate or voice a localized text field.</summary>
        public VisualElement CreateAILocalizeTextButton(AIFieldTarget target)
        {
            return null;
        }

        /// <summary>Button to generate voiceover for a localized sequence field.</summary>
        public VisualElement CreateAISequenceButton(AIFieldTarget target)
        {
            return null;
        }

        /// <summary>Button in an actor's Portrait Sprites foldout.</summary>
        public VisualElement CreateAIPortraitButton(int actorID)
        {
            return null;
        }

        /// <summary>Row that shows and selects an actor's AI voice.</summary>
        public VisualElement CreateAIVoiceRow(int actorID)
        {
            return null;
        }

        /// <summary>
        /// IMGUI button drawn next to a Sequence field's label. target is null when editing
        /// multiple entries, which are the entries selected in the node graph.
        /// </summary>
        public void DrawAISequenceButton(AIFieldTarget? target)
        {
        }

        /// <summary>
        /// Adds buttons below a Template panel category's fields.
        /// </summary>
        /// <param name="categoryLabel">Category, such as "Actors" or "Dialogue Entries".</param>
        /// <param name="modifyTemplateAndAssets">Runs a change to the template and database as one undoable step.</param>
        public void AddAITemplateButtons(VisualElement parent, string categoryLabel,
            Action<string, Action<DialogueDatabaseTemplateHolder, DialogueDatabase>> modifyTemplateAndAssets)
        {
        }

    }

}

#endif
