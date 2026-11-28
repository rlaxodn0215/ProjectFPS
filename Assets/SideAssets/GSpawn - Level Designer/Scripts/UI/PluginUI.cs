#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace GSPAWN
{
    public abstract class PluginUI : ScriptableObject
    {
        // Note: Must be non-serialized. Otherwise, it appears to serialize it.
        [NonSerialized]
        private bool                _ready = false;
        private VisualElement       _rootElement;
        private VisualElement       _contentContainer;
        private PluginWindow        _targetWindow;
        private Editor              _targetEditor;

        [NonSerialized]
        private SerializedObject    _serializedObject;
        [NonSerialized]
        private bool                _undoRefreshPending;
        [NonSerialized]
        private bool                _undoRefreshScheduled;

        protected VisualElement     rootElement         { get { return _rootElement; } }
        protected VisualElement     contentContainer    { get { return _contentContainer; } }

        public SerializedObject     serializedObject
        {
            get
            {
                if (_serializedObject == null) _serializedObject = new SerializedObject(this);
                return _serializedObject;
            }
        }
        public bool                 ready               { get { return _ready; } }
        protected bool              undoRefreshPending  { get { return _undoRefreshPending; } }
        public PluginWindow         targetWindow        { get { return _targetWindow; } }
        public Editor               targetEditor        { get { return _targetEditor; } }
        // The display style of this container is not enough: parent tools and windows
        // can be hidden while a nested view still has display: flex.
        public bool                 uiVisible
        {
            get
            {
                if (_contentContainer == null || _contentContainer.panel == null) return false;
                for (VisualElement element = _contentContainer; element != null; element = element.parent)
                {
                    if (element.style.display == DisplayStyle.None) return false;
                }
                return true;
            }
        }
        public bool                 uiVisibleAndReady   { get { return ready && uiVisible; } }
        public bool                 isEnabledSelf       { get { return _contentContainer != null && _contentContainer.enabledSelf; } }

        public void setVisible(bool visible)
        {
            if (_contentContainer == null) return;

            _contentContainer.setDisplayVisible(visible);
            if (uiVisibleAndReady)
            {
                if (_undoRefreshPending) schedulePendingUndoRefresh();
                else refresh();
            }
        }

        public void setEnabled(bool enabled)
        {
            if (_contentContainer == null) return;
            _contentContainer.SetEnabled(enabled);
        }

        public void refresh() 
        {
            if (uiVisibleAndReady) onRefresh();
        }

        public void build(VisualElement rootElement, PluginWindow targetWindow)
        {
            _targetWindow       = targetWindow;
            _rootElement        = rootElement;
            _contentContainer   = new VisualElement();
            _contentContainer.setDisplayVisible(true);
            _rootElement.Add(_contentContainer);
            onBuild();
            _ready = true;
            _undoRefreshPending = false;
        }

        public void build(VisualElement rootElement, Editor targetEditor)
        {
            _targetEditor       = targetEditor;
            _rootElement        = rootElement;
            _contentContainer   = new VisualElement();
            _contentContainer.setDisplayVisible(true);
            _rootElement.Add(_contentContainer);
            onBuild();
            _ready = true;
            _undoRefreshPending = false;
        }

        public virtual void onGUI() { }

        public void onPluginUIAssetWillBeDestroyed()
        {
            Undo.undoRedoPerformed -= onUndoRedoEvent;
            EditorApplication.delayCall -= applyPendingUndoRefresh;
            _undoRefreshScheduled = false;
            onDestroy();
        }

        protected abstract void onBuild();
        protected abstract void onRefresh();
        protected virtual void onEnabled() { }
        protected virtual void onDisabled() { }
        protected virtual void onDestroy() { }
        protected virtual void onUndoRedo() { }

        private void onUndoRedoEvent()
        {
            if (!_ready || _contentContainer == null) return;

            // Never rebuild a UI hierarchy synchronously inside Unity's Undo event.
            // Multiple rapid Undo/Redo notifications are collapsed into one refresh,
            // and hidden views remain dirty until they are displayed again.
            _undoRefreshPending = true;
            schedulePendingUndoRefresh();
        }

        private void schedulePendingUndoRefresh()
        {
            if (!uiVisible || _undoRefreshScheduled) return;
            _undoRefreshScheduled = true;
            EditorApplication.delayCall += applyPendingUndoRefresh;
        }

        private void applyPendingUndoRefresh()
        {
            _undoRefreshScheduled = false;
            if (this == null || !_ready || !uiVisible || !_undoRefreshPending) return;

            _undoRefreshPending = false;
            onUndoRedo();
        }

        private void OnEnable()
        {
            Undo.undoRedoPerformed += onUndoRedoEvent;
            onEnabled();
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= onUndoRedoEvent;
            EditorApplication.delayCall -= applyPendingUndoRefresh;
            _undoRefreshScheduled = false;
            onDisabled();
        }

        private void OnDestroy()
        {
            Undo.undoRedoPerformed -= onUndoRedoEvent;
            EditorApplication.delayCall -= applyPendingUndoRefresh;
            _undoRefreshScheduled = false;
            onDestroy();
        }
    }
}
#endif