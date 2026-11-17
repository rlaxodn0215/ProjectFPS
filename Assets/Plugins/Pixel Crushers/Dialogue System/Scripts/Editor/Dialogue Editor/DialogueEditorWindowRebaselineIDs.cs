// Copyright (c) Pixel Crushers. All rights reserved.

namespace PixelCrushers.DialogueSystem.DialogueEditor
{

    /// <summary>
    /// This part of the Dialogue Editor window handles rebaselining internal database IDs.
    /// </summary>
    public partial class DialogueEditorWindow
    {

        private void ConfirmRebaselineAllIDs()
        {
            if (database == null) return;
            if (DialogueDatabaseIDRebaseliner.Confirm())
            {
                try
                {
                    DialogueDatabaseIDRebaseliner.Rebaseline(database);
                }
                finally
                {
                    Reset();
                }
            }
        }

    }

}
