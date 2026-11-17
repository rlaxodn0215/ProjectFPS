// Copyright (c) Pixel Crushers. All rights reserved.

using UnityEngine;

namespace PixelCrushers.DialogueSystem
{

    /// <summary>
    /// Add [DialogueEntryPopup] to an int show a popup of dialogue entries in the most recent conversation selection shown in the inspector.
    /// Use [DialogueEntryPopup(true)] to also show a filter field that filters the popup by entry Title, Dialogue Text, and Menu Text.
    /// </summary>
    public class DialogueEntryPopupAttribute : PropertyAttribute
    {

        public bool showFilter = false;

        public DialogueEntryPopupAttribute(bool showFilter = false)
        {
            this.showFilter = showFilter;
        }
    }
}
