// Copyright (c) Pixel Crushers. All rights reserved.

#if UNITY_2022_1_OR_NEWER

using UnityEngine.UIElements;

namespace PixelCrushers.DialogueSystem.DialogueEditor
{

    /// <summary>
    /// Database panel's Spell Check section, which checks the whole database. The
    /// Conversations panel's menu opens SpellCheckWindow to check one conversation.
    /// </summary>
    public partial class DatabasePanel
    {

        private const float SpellCheckListMinHeight = 240;

        private SpellCheckView spellCheckView = null;

        private void AddSpellCheckSection(ScrollView scrollView)
        {
            spellCheckView = new SpellCheckView(() => window, () => -1, () => "the entire database");
            spellCheckView.ListMinHeight = SpellCheckListMinHeight;
            scrollView.Add(spellCheckView);
            FillScrollViewViewport(scrollView);
        }

    }

}

#endif
