using System.Collections.Generic;
using UnityEngine;

namespace PixelCrushers.DialogueSystem
{

    /// <summary>
    /// This holds a conversation that's queued to play once all active conversations 
    /// have ended. It can hold the specifics for a conversation or a 
    /// DialogueSystemTrigger. If a DialogueSystemTrigger is assigned, it runs the
    /// DialogueSystemTrigger's DoConversationAction().
    /// </summary>
    public struct QueuedConversation
    {
        private string conversationTitle;
        private Transform actor;
        private Transform conversant;
        private int initialDialogueEntryID;
        private IDialogueUI overrideDialogueUI;
        private List<ActorOverride> actorOverrides;
        private DialogueSystemTrigger dialogueSystemTrigger;

        public string ConversationTitle => conversationTitle;
        public Transform Actor => actor;
        public Transform Conversant => conversant;
        public int InitialDialogueEntryID => initialDialogueEntryID;
        public IDialogueUI OverrideDialogueUI => overrideDialogueUI;
        public List<ActorOverride> ActorOverrides => actorOverrides;
        public DialogueSystemTrigger DialogueSystemTrigger => dialogueSystemTrigger;

        public QueuedConversation(string conversationTitle, Transform actor, Transform conversant,
            int initialDialogueEntryID, IDialogueUI overrideDialogueUI, List<ActorOverride> actorOverrides)
        {
            this.conversationTitle = conversationTitle;
            this.actor = actor;
            this.conversant = conversant;
            this.initialDialogueEntryID = initialDialogueEntryID;
            this.overrideDialogueUI = overrideDialogueUI;
            this.actorOverrides = actorOverrides;
            this.dialogueSystemTrigger = null;
        }

        public QueuedConversation(DialogueSystemTrigger dialogueSystemTrigger, Transform actor)
        {
            this.conversationTitle = string.Empty;
            this.actor = actor;
            this.conversant = null;
            this.initialDialogueEntryID = -1;
            this.overrideDialogueUI = null;
            this.actorOverrides = null;
            this.dialogueSystemTrigger = dialogueSystemTrigger;
        }

    }

}
