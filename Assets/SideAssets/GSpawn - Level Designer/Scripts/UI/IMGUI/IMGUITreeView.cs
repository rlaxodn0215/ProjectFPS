#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
namespace GSPAWN
{
    public enum EIMGUITreeViewDropMode
    {
        MoveBefore,
        MoveAfter,
        MakeChild
    }
    public class IMGUITreeViewItemData
    {
        public Texture2D    image;                      
        public string       label       = string.Empty; 
        public string       tooltip     = string.Empty; 
        public Color        tint        = Color.white;  
        public GUIStyle     labelStyle;                 
    }
    public class IMGUITreeView<T>
    {
        class DragPayload
        {
            public IMGUITreeView<T> sourceView;             
            public List<T>          itemData = new();       
        }
        string                  mControlName            = string.Empty;  
        string                  mRenameControlName      = string.Empty;  
        string                  mDragDataKey            = string.Empty;  
        IMGUITreeViewModel<T>   mModel                  = new();                        
        float                   mRowHeight              = 20.0f;  
        float                   mIndentWidth            = 14.0f;  
        float                   mFoldoutWidth           = 14.0f;  
        float                   mImageSize              = 16.0f;  
        float                   mItemPrefixWidth        = 0.0f;   
        float                   mItemSuffixWidth        = 0.0f;   
        float                   mDragStartDistance      = 4.0f;   
        Color                   mHoveredItemColor       = new Color(0.5f, 0.5f, 0.5f, 0.2f);         
        Color                   mDropIndicatorColor     = IMGUIViewUtility.selectedColor;  
        Color                   mBorderColor            = IMGUIViewUtility.borderColor;        
        int                     mHoveredItemIndex       = -1;  
        int                     mSelectionAnchor        = -1;  
        int                     mArrowCursor            = -1;  
        bool                    mScrollSelectionIntoView;      
        bool                    mItemDragEnabled;                    
        bool                    mItemDragActive;                     
        bool                    mDragCandidate;                      
        bool                    mCollapseSelectionOnMouseUp;         
        int                     mDragCandidateItemIndex       = -1;  
        Vector2                 mDragCandidateMousePos;              
        bool                    mRenaming;                            
        bool                    mFocusRenameField;                    
        Rect                    mRenameFieldScreenRect;
        Rect                    mCurrentViewRect;
        Vector2                 mCurrentScrollPos;
        int                     mRenameItemIndex     = -1;            
        T                       mRenameItemData;                      
        string                  mRenameText          = string.Empty;  
        string                  mRenameOriginalText  = string.Empty;  
        IMGUITreeViewItemData   mScratchItemData        = new();    
        List<T>                 mDraggedItemBuffer      = new(64);  
        int                     mDropTargetItemIndex     = -1;                                
        int                     mDropIndicatorVisibleRow = -1;                                
        EIMGUITreeViewDropMode  mDropMode                = EIMGUITreeViewDropMode.MoveAfter;  
        bool                    mCanDrop;                                                     
        public IMGUITreeViewModel<T> model                  { get { return mModel; } }
        public float                rowHeight               { get { return mRowHeight; } set { mRowHeight = Mathf.Max(1.0f, value); } }
        public float                indentWidth             { get { return mIndentWidth; } set { mIndentWidth = Mathf.Max(0.0f, value); } }
        public float                foldoutWidth            { get { return mFoldoutWidth; } set { mFoldoutWidth = Mathf.Max(0.0f, value); } }
        public float                imageSize               { get { return mImageSize; } set { mImageSize = Mathf.Max(0.0f, value); } }
        public float                itemPrefixWidth         { get { return mItemPrefixWidth; } set { mItemPrefixWidth = Mathf.Max(0.0f, value); } }
        public float                itemSuffixWidth         { get { return mItemSuffixWidth; } set { mItemSuffixWidth = Mathf.Max(0.0f, value); } }
        public float                dragStartDistance       { get { return mDragStartDistance; } set { mDragStartDistance = Mathf.Max(0.0f, value); } }
        public bool                 itemDragEnabled         { get { return mItemDragEnabled; } set { mItemDragEnabled = value; } }
        public Color                hoveredItemColor        { get { return mHoveredItemColor; } set { mHoveredItemColor = value; } }
        public Color                dropIndicatorColor      { get { return mDropIndicatorColor; } set { mDropIndicatorColor = value; } }
        public Color                borderColor             { get { return mBorderColor; } set { mBorderColor = value; } }
        public bool                 showBorder              { get; set; } = true;
        public GUIStyle             itemLabelStyle          { get; set; }
        public int                  hoveredItemIndex        { get { return mHoveredItemIndex; } }
        public IReadOnlyList<T>     selection               { get { return mModel.selectedItemData; } }
        public Action<T, IMGUITreeViewItemData>                     getItemData         { get; set; }
        public Action<T, Rect, bool, bool>                           drawItemPrefix      { get; set; }
        public Action<T, Rect, bool, bool>                           drawItemSuffix      { get; set; }
        public Func<T, float>                                        getItemSuffixWidth  { get; set; }
        public Action<IReadOnlyList<T>>                             selectionChanged    { get; set; }
        public Action<T>                                            itemDoubleClicked   { get; set; }
        public Action                                                copySelection       { get; set; }
        public Action                                                cutSelection        { get; set; }
        public Action                                                paste               { get; set; }
        public bool                                                 renameOnDoubleClick { get; set; }
        public Func<T, bool>                                        canRenameItem       { get; set; }
        public Action<T, string>                                    renameItem          { get; set; }
        public Action                                               duplicateSelected   { get; set; }
        public Action                                               deleteSelected      { get; set; }
        public Action<T>                                            itemContextClicked  { get; set; }
        public Func<IReadOnlyList<T>, T, EIMGUITreeViewDropMode, bool>  canDropItems    { get; set; }
        public Action<IReadOnlyList<T>, T, EIMGUITreeViewDropMode>      dropItems       { get; set; }
        public IMGUITreeView()
        {
            int hash                = GetHashCode();
            mControlName            = "TreeView_" + hash;
            mRenameControlName      = mControlName + "_Rename";
            mDragDataKey            = "GSPAWN_" + mControlName + "_DragData";
        }
        public void ResetSelectionNavigation()
        {
            mSelectionAnchor    = -1;
            mArrowCursor        = -1;
        }
        public void ScrollSelectionIntoView()
        {
            mScrollSelectionIntoView = true;
        }
        public void CollectSelectedItems(IList<T> items)
        {
            items.Clear();
            IReadOnlyList<T> selectedItems = mModel.selectedItemData;
            int count = selectedItems.Count;
            for (int i = 0; i < count; ++i)
                items.Add(selectedItems[i]);
        }
        public void SelectOnlyItem(T itemData)
        {
            int itemIndex = mModel.FindItemIndex(itemData);
            if (itemIndex < 0)
                return;
            mModel.SelectOnlyItem(itemIndex);
            mSelectionAnchor    = itemIndex;
            mArrowCursor        = itemIndex;
        }
        public void BeginRename(T itemData)
        {
            int itemIndex = mModel.FindItemIndex(itemData);
            if (itemIndex >= 0)
                BeginRename(itemIndex);
        }
        public Vector2 OnGUI(Vector2 scrollPos, bool windowHasFocus)
        {
            Rect viewRect = GUILayoutUtility.GetRect(0.0f, 0.0f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            return OnGUI(viewRect, scrollPos, windowHasFocus);
        }
        public Vector2 OnGUI(Rect viewRect, Vector2 scrollPos, bool windowHasFocus)
        {
            GUI.SetNextControlName(mControlName);
            GUI.Label(new Rect(viewRect.x, viewRect.y, 1.0f, 1.0f), GUIContent.none);
            Event e = Event.current;
            if (mRenaming && mRenameFieldScreenRect.width > 0.0f &&
                e.type == EventType.MouseDown &&
                !mRenameFieldScreenRect.Contains(e.mousePosition))
                EndRename(true);
            bool viewHovered = viewRect.Contains(e.mousePosition);
            if (!mRenaming && e.type == EventType.MouseDown && viewHovered)
                GUI.FocusControl(mControlName);
            GUIStyle labelStyle = itemLabelStyle != null ? itemLabelStyle : EditorStyles.label;
            bool hasFocus       = windowHasFocus && GUI.GetNameOfFocusedControl() == mControlName;
            Color selectedItemColor = hasFocus ? IMGUIViewUtility.selectedColor : IMGUIViewUtility.unfocusedSelectedColor;
            int     visibleItemCount         = mModel.visibleItemCount;
            float   contentHeight            = visibleItemCount * mRowHeight;
            float   verticalScrollbarWidth   = contentHeight > viewRect.height ? GUI.skin.verticalScrollbar.fixedWidth : 0.0f;
            float   contentWidth             = Mathf.Max(1.0f, viewRect.width - 1.0f - verticalScrollbarWidth);
            Rect    contentRect               = new Rect(0.0f, 0.0f, contentWidth, contentHeight);
            if (mScrollSelectionIntoView)
            {
                int selectedIndex = mModel.GetFirstSelectedItemIndex();
                int selectedRow   = selectedIndex >= 0 ? mModel.GetVisibleRow(selectedIndex) : -1;
                if (selectedRow >= 0)
                {
                    Rect selectedRect = new Rect(0.0f, selectedRow * mRowHeight, contentRect.width, mRowHeight);
                    scrollPos = IMGUIViewUtility.ScrollRectIntoView(selectedRect, scrollPos, viewRect.width, viewRect.height);
                }
                mScrollSelectionIntoView = false;
            }
            mHoveredItemIndex = -1;
            int firstVisibleRow = Mathf.Clamp(Mathf.FloorToInt(scrollPos.y / mRowHeight), 0, Mathf.Max(0, visibleItemCount - 1));
            int lastVisibleRow  = Mathf.Clamp(Mathf.CeilToInt((scrollPos.y + viewRect.height) / mRowHeight), 0, Mathf.Max(0, visibleItemCount - 1));
            mCurrentViewRect = viewRect;
            mCurrentScrollPos = scrollPos;
            scrollPos = GUI.BeginScrollView(viewRect, scrollPos, contentRect, false, false,
                GUIStyle.none, GUI.skin.verticalScrollbar);
            mCurrentScrollPos = scrollPos;
            if (visibleItemCount != 0)
            {
                for (int visibleRow = firstVisibleRow; visibleRow <= lastVisibleRow; ++visibleRow)
                {
                    int itemIndex = mModel.GetVisibleItemIndex(visibleRow);
                    if (itemIndex < 0)
                        break;
                    DrawItemRow(itemIndex, visibleRow, contentRect.width, viewHovered, selectedItemColor, labelStyle);
                }
            }
            HandleItemDrag(contentRect.width, viewHovered);
            GUI.EndScrollView();
            if (mRenaming && mRenameFieldScreenRect.width > 0.0f)
                DrawRenameField();
            if (!mRenaming && e.type == EventType.ContextClick && viewHovered &&
                mHoveredItemIndex >= 0 && itemContextClicked != null)
            {
                if (!mModel.IsItemSelected(mHoveredItemIndex))
                {
                    mModel.SelectOnlyItem(mHoveredItemIndex);
                    mSelectionAnchor = mHoveredItemIndex;
                    mArrowCursor     = mHoveredItemIndex;
                    NotifySelectionChanged();
                }
                itemContextClicked(mModel.GetItemData(mHoveredItemIndex));
                e.Use();
            }
            if (e.type == EventType.MouseDown &&
                e.button == 0 &&
                viewHovered &&
                mHoveredItemIndex < 0)
            {
                mModel.ClearSelection();
                ResetSelectionNavigation();
                NotifySelectionChanged();
                e.Use();
            }
            if (showBorder)
                IMGUIViewUtility.DrawBorder(viewRect, mBorderColor);
            if (!mRenaming && e.type == EventType.KeyDown && hasFocus)
                scrollPos = HandleKeyboardInput(scrollPos, viewRect);
            return scrollPos;
        }
        void DrawItemRow(int itemIndex, int visibleRow, float contentWidth, bool viewHovered,
            Color selectedItemColor, GUIStyle labelStyle)
        {
            float rowY       = visibleRow * mRowHeight;
            Rect rowRect     = new Rect(0.0f, rowY, contentWidth, mRowHeight);
            bool filterActive = mModel.filterActive;
            int itemDepth     = filterActive ? 0 : mModel.GetItemDepth(itemIndex);
            float indent      = itemDepth * mIndentWidth;
            Rect foldoutRect  = new Rect(indent, rowY, mFoldoutWidth, mRowHeight);
            Event e = Event.current;
            bool hovered = false;
            if (mHoveredItemIndex < 0 &&
                viewHovered &&
                rowRect.Contains(e.mousePosition))
            {
                hovered             = true;
                mHoveredItemIndex   = itemIndex;
            }
            bool selected = mModel.IsItemSelected(itemIndex);
            if (selected)
                EditorGUI.DrawRect(rowRect, selectedItemColor);
            else
            if (hovered)
                EditorGUI.DrawRect(rowRect, mHoveredItemColor);
            bool hasChildren = !filterActive && mModel.HasItemChildren(itemIndex);
            if (hasChildren)
            {
                bool expanded = mModel.IsItemExpanded(itemIndex);
                bool newExpanded = EditorGUI.Foldout(foldoutRect, expanded, GUIContent.none, false);
                if (newExpanded != expanded)
                {
                    if (e.alt)
                        mModel.SetSubtreeExpanded(itemIndex, newExpanded);
                    else
                        mModel.SetItemExpanded(itemIndex, newExpanded);
                }
            }
            T itemData = mModel.GetItemData(itemIndex);
            GetItemData(itemData, mScratchItemData);
            float contentX               = filterActive ? 2.0f : indent + mFoldoutWidth + 2.0f;
            Rect itemContentRect         = new Rect(contentX, rowY, Mathf.Max(0.0f, contentWidth - contentX), mRowHeight);
            float prefixWidth            = Mathf.Min(mItemPrefixWidth, itemContentRect.width);
            float remainingContentWidth  = Mathf.Max(0.0f, itemContentRect.width - prefixWidth);
            float requestedSuffixWidth   = getItemSuffixWidth != null ?
                Mathf.Max(0.0f, getItemSuffixWidth(itemData)) : mItemSuffixWidth;
            float suffixWidth            = Mathf.Min(requestedSuffixWidth, remainingContentWidth);
            Rect itemPrefixRect          = new Rect(itemContentRect.x, itemContentRect.y, prefixWidth, itemContentRect.height);
            Rect itemSuffixRect          = new Rect(itemContentRect.xMax - suffixWidth, itemContentRect.y, suffixWidth, itemContentRect.height);
            Rect standardContentRect     = new Rect(itemPrefixRect.xMax, itemContentRect.y,
                Mathf.Max(0.0f, remainingContentWidth - suffixWidth), itemContentRect.height);
            bool oldGUIEnabled       = GUI.enabled;
            Color oldGUIColor        = GUI.color;
            Color oldGUIContentColor = GUI.contentColor;
            Color oldGUIBgColor      = GUI.backgroundColor;
            if (drawItemPrefix != null && prefixWidth > 0.0f)
                drawItemPrefix(itemData, itemPrefixRect, selected, hovered);
            GUI.enabled         = oldGUIEnabled;
            GUI.color           = oldGUIColor;
            GUI.contentColor    = oldGUIContentColor;
            GUI.backgroundColor = oldGUIBgColor;
            if (drawItemSuffix != null && suffixWidth > 0.0f)
                drawItemSuffix(itemData, itemSuffixRect, selected, hovered);
            GUI.enabled         = oldGUIEnabled;
            GUI.color           = oldGUIColor;
            GUI.contentColor    = oldGUIContentColor;
            GUI.backgroundColor = oldGUIBgColor;
            if (!mRenaming && hovered &&
                e.type == EventType.MouseDown &&
                e.button == 0 &&
                (!hasChildren || !foldoutRect.Contains(e.mousePosition)))
            {
                HandleItemMouseDown(itemIndex, visibleRow, itemData, e);
            }
            if (mRenaming && itemIndex == mRenameItemIndex)
            {
                float fieldWidth = Mathf.Min(142.0f, Mathf.Max(0.0f, standardContentRect.width - 4.0f));
                mRenameFieldScreenRect = new Rect(
                    mCurrentViewRect.x + standardContentRect.x - mCurrentScrollPos.x,
                    mCurrentViewRect.y + standardContentRect.y - mCurrentScrollPos.y + 2.0f,
                    fieldWidth, Mathf.Max(0.0f, standardContentRect.height - 4.0f));
            }
            else
                DrawItemContent(standardContentRect, mScratchItemData, labelStyle);
        }
        void DrawItemContent(Rect contentRect, IMGUITreeViewItemData itemData, GUIStyle labelStyle)
        {
            float labelX = contentRect.x;
            Color oldColor = GUI.color;
            GUI.color *= itemData.tint;
            if (itemData.image != null && mImageSize > 0.0f)
            {
                float imageY = contentRect.y + (contentRect.height - mImageSize) * 0.5f;
                Rect imageRect = new Rect(contentRect.x, imageY, mImageSize, mImageSize);
                GUI.DrawTexture(imageRect, itemData.image, ScaleMode.ScaleToFit, true);
                labelX += mImageSize + 3.0f;
            }
            Rect labelRect = new Rect(labelX, contentRect.y, Mathf.Max(0.0f, contentRect.xMax - labelX), contentRect.height);
            GUIStyle resolvedLabelStyle = itemData.labelStyle != null ? itemData.labelStyle : labelStyle;
            GUI.Label(labelRect, new GUIContent(itemData.label, itemData.tooltip), resolvedLabelStyle);
            GUI.color = oldColor;
        }
        void DrawRenameField()
        {
            Event e = Event.current;
            bool commitRename = e.type == EventType.KeyDown &&
                (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter);
            bool cancelRename = e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape;
            GUI.SetNextControlName(mRenameControlName);
            mRenameText = EditorGUI.TextField(mRenameFieldScreenRect, mRenameText);
            if (mFocusRenameField && e.type == EventType.Repaint)
            {
                EditorGUI.FocusTextInControl(mRenameControlName);
                if (GUI.GetNameOfFocusedControl() == mRenameControlName)
                {
                    mFocusRenameField = false;
                    EditorGUIUtility.editingTextField = true;
                }
            }
            if (commitRename)
            {
                EndRename(true);
                e.Use();
            }
            else if (cancelRename)
            {
                EndRename(false);
                e.Use();
            }
        }
        void HandleItemMouseDown(int itemIndex, int visibleRow, T itemData, Event e)
        {
            if (e.control || e.command)
            {
                mModel.ToggleItemSelected(itemIndex);
                mCollapseSelectionOnMouseUp = false;
                mSelectionAnchor            = itemIndex;
                mArrowCursor                = itemIndex;
            }
            else
            if (e.shift && mSelectionAnchor >= 0)
            {
                int anchorVisibleRow = mModel.GetVisibleRow(mSelectionAnchor);
                if (anchorVisibleRow >= 0)
                {
                    mModel.ClearSelection();
                    mModel.SetVisibleSelectionRange(Mathf.Min(anchorVisibleRow, visibleRow),
                        Mathf.Max(anchorVisibleRow, visibleRow), true);
                }
                else mModel.SelectOnlyItem(itemIndex);
                mCollapseSelectionOnMouseUp = false;
                mArrowCursor                = itemIndex;
            }
            else
            {
                bool preserveSelectionForDrag = mItemDragEnabled &&
                    !mRenaming && !mModel.filterActive &&
                    mModel.IsItemSelected(itemIndex) && mModel.selectedItemCount > 1;
                if (!preserveSelectionForDrag)
                    mModel.SelectOnlyItem(itemIndex);
                mCollapseSelectionOnMouseUp = preserveSelectionForDrag;
                mSelectionAnchor            = itemIndex;
                mArrowCursor                = itemIndex;
            }
            NotifySelectionChanged();
            if (e.clickCount == 2)
            {
                bool beganRename = false;
                if (renameOnDoubleClick &&
                    !e.control && !e.command && !e.shift && !e.alt &&
                    CanRenameItem(itemIndex))
                {
                    BeginRename(itemIndex);
                    beganRename = true;
                }
                if (!beganRename && itemDoubleClicked != null)
                    itemDoubleClicked(itemData);
            }
            if (mItemDragEnabled && !mRenaming && !mModel.filterActive)
            {
                mDragCandidate          = true;
                mDragCandidateItemIndex = itemIndex;
                mDragCandidateMousePos  = e.mousePosition;
            }
            e.Use();
        }
        Vector2 HandleKeyboardInput(Vector2 scrollPos, Rect viewRect)
        {
            Event e = Event.current;
            if (IMGUIViewUtility.HandleCopyPasteShortcut(e, copySelection, cutSelection, paste))
                return scrollPos;
            if (e.keyCode == KeyCode.D && (e.control || e.command) && !e.shift && !e.alt && duplicateSelected != null)
            {
                duplicateSelected();
                ResetSelectionNavigation();
                e.Use();
                return scrollPos;
            }
            if (e.keyCode == KeyCode.Delete && deleteSelected != null)
            {
                deleteSelected();
                ResetSelectionNavigation();
                e.Use();
                return scrollPos;
            }
            if (e.keyCode == KeyCode.A && (e.control || e.command))
            {
                if (mModel.visibleItemCount != 0)
                    mModel.SetVisibleSelectionRange(0, mModel.visibleItemCount - 1, true);
                ResetSelectionNavigation();
                NotifySelectionChanged();
                e.Use();
                return scrollPos;
            }
            if (e.keyCode == KeyCode.F2)
            {
                int renameIndex = ResolveKeyboardItemIndex();
                if (renameIndex >= 0)
                    BeginRename(renameIndex);
                e.Use();
                return scrollPos;
            }
            if (e.keyCode != KeyCode.UpArrow &&
                e.keyCode != KeyCode.DownArrow &&
                e.keyCode != KeyCode.LeftArrow &&
                e.keyCode != KeyCode.RightArrow &&
                e.keyCode != KeyCode.Home &&
                e.keyCode != KeyCode.End)
                return scrollPos;
            if (e.keyCode == KeyCode.Home || e.keyCode == KeyCode.End)
            {
                int visibleItemCount = mModel.visibleItemCount;
                if (visibleItemCount == 0)
                    return scrollPos;
                int visibleRow       = e.keyCode == KeyCode.Home ? 0 : visibleItemCount - 1;
                int targetItemIndex  = mModel.GetVisibleItemIndex(visibleRow);
                ApplyKeyboardSelection(targetItemIndex, e.shift);
                scrollPos = ScrollItemIntoView(targetItemIndex, scrollPos, viewRect);
                e.Use();
                return scrollPos;
            }
            int itemIndex = ResolveKeyboardItemIndex();
            if (itemIndex < 0)
            {
                int visibleItemCount = mModel.visibleItemCount;
                if (visibleItemCount == 0)
                    return scrollPos;
                int visibleRow = e.keyCode == KeyCode.UpArrow ? visibleItemCount - 1 : 0;
                itemIndex = mModel.GetVisibleItemIndex(visibleRow);
                ApplyKeyboardSelection(itemIndex, e.shift);
                scrollPos = ScrollItemIntoView(itemIndex, scrollPos, viewRect);
                e.Use();
                return scrollPos;
            }
            if (e.keyCode == KeyCode.LeftArrow)
            {
                if (mModel.HasItemChildren(itemIndex) && mModel.IsItemExpanded(itemIndex))
                {
                    mModel.SetItemExpanded(itemIndex, false);
                }
                else
                {
                    int parentIndex = mModel.GetItemParentIndex(itemIndex);
                    if (parentIndex >= 0)
                    {
                        ApplyKeyboardSelection(parentIndex, e.shift);
                        scrollPos = ScrollItemIntoView(parentIndex, scrollPos, viewRect);
                    }
                }
                e.Use();
                return scrollPos;
            }
            if (e.keyCode == KeyCode.RightArrow)
            {
                if (mModel.HasItemChildren(itemIndex))
                {
                    if (!mModel.IsItemExpanded(itemIndex))
                        mModel.SetItemExpanded(itemIndex, true);
                    else
                    {
                        int childIndex = mModel.GetChildItemIndex(itemIndex, 0);
                        if (childIndex >= 0)
                        {
                            ApplyKeyboardSelection(childIndex, e.shift);
                            scrollPos = ScrollItemIntoView(childIndex, scrollPos, viewRect);
                        }
                    }
                }
                e.Use();
                return scrollPos;
            }
            int currentVisibleRow = mModel.GetVisibleRow(itemIndex);
            int rowDelta          = e.keyCode == KeyCode.UpArrow ? -1 : 1;
            int newVisibleRow     = currentVisibleRow + rowDelta;
            if (newVisibleRow >= 0 && newVisibleRow < mModel.visibleItemCount)
            {
                int newItemIndex = mModel.GetVisibleItemIndex(newVisibleRow);
                ApplyKeyboardSelection(newItemIndex, e.shift);
                scrollPos = ScrollItemIntoView(newItemIndex, scrollPos, viewRect);
            }
            e.Use();
            return scrollPos;
        }
        void ApplyKeyboardSelection(int itemIndex, bool extendRange)
        {
            if (extendRange && mSelectionAnchor >= 0)
            {
                int anchorVisibleRow = mModel.GetVisibleRow(mSelectionAnchor);
                int itemVisibleRow   = mModel.GetVisibleRow(itemIndex);
                if (anchorVisibleRow >= 0 && itemVisibleRow >= 0)
                {
                    mModel.ClearSelection();
                    mModel.SetVisibleSelectionRange(Mathf.Min(anchorVisibleRow, itemVisibleRow),
                        Mathf.Max(anchorVisibleRow, itemVisibleRow), true);
                }
                else mModel.SelectOnlyItem(itemIndex);
            }
            else
            {
                mModel.SelectOnlyItem(itemIndex);
                mSelectionAnchor = itemIndex;
            }
            mArrowCursor = itemIndex;
            NotifySelectionChanged();
        }
        int ResolveKeyboardItemIndex()
        {
            if (mArrowCursor >= 0 && mModel.IsItemVisible(mArrowCursor))
                return mArrowCursor;
            int selectedIndex = mModel.GetFirstSelectedItemIndex();
            if (selectedIndex >= 0 && mModel.IsItemVisible(selectedIndex))
            {
                mArrowCursor = selectedIndex;
                return selectedIndex;
            }
            return -1;
        }
        Vector2 ScrollItemIntoView(int itemIndex, Vector2 scrollPos, Rect viewRect)
        {
            int visibleRow = mModel.GetVisibleRow(itemIndex);
            if (visibleRow < 0)
                return scrollPos;
            Rect itemRect = new Rect(0.0f, visibleRow * mRowHeight, viewRect.width, mRowHeight);
            return IMGUIViewUtility.ScrollRectIntoView(itemRect, scrollPos, viewRect.width, viewRect.height);
        }
        bool CanRenameItem(int itemIndex)
        {
            if (renameItem == null)
                return false;
            if (itemIndex < 0 || itemIndex >= mModel.itemCount)
                return false;
            T itemData = mModel.GetItemData(itemIndex);
            return canRenameItem == null || canRenameItem(itemData);
        }
        void BeginRename(int itemIndex)
        {
            if (!CanRenameItem(itemIndex))
                return;
            T itemData = mModel.GetItemData(itemIndex);
            GetItemData(itemData, mScratchItemData);
            mRenaming               = true;
            mFocusRenameField       = true;
            mRenameItemIndex        = itemIndex;
            mRenameItemData         = itemData;
            mRenameText             = mScratchItemData.label;
            mRenameOriginalText     = mRenameText;
            mRenameFieldScreenRect = Rect.zero;
            mModel.ExpandItemAncestors(itemIndex);
            ScrollSelectionIntoView();
        }
        void EndRename(bool commit)
        {
            if (!mRenaming)
                return;
            T itemData         = mRenameItemData;
            string renameText  = mRenameText;
            string original    = mRenameOriginalText;
            mRenaming               = false;
            mFocusRenameField       = false;
            mRenameItemIndex        = -1;
            mRenameItemData         = default;
            mRenameText             = string.Empty;
            mRenameOriginalText     = string.Empty;
            mRenameFieldScreenRect  = Rect.zero;
            if (commit && renameItem != null && renameText != original)
                renameItem(itemData, renameText);
            GUI.FocusControl(mControlName);
        }
        void HandleItemDrag(float contentWidth, bool viewHovered)
        {
            if (!mItemDragEnabled)
            {
                mDragCandidate              = false;
                mCollapseSelectionOnMouseUp = false;
                mDragCandidateItemIndex     = -1;
                if (mItemDragActive)
                    EndItemDrag();
                else
                    ClearDropTarget();
                return;
            }
            Event e = Event.current;
            if (mDragCandidate && e.type == EventType.MouseDrag)
            {
                if (Vector2.Distance(e.mousePosition, mDragCandidateMousePos) >= mDragStartDistance)
                {
                    StartItemDrag();
                    mDragCandidate              = false;
                    mCollapseSelectionOnMouseUp = false;
                    e.Use();
                    return;
                }
            }
            if (e.rawType == EventType.MouseUp)
            {
                if (mDragCandidate && mCollapseSelectionOnMouseUp &&
                    mDragCandidateItemIndex >= 0 && mDragCandidateItemIndex < mModel.itemCount)
                {
                    mModel.SelectOnlyItem(mDragCandidateItemIndex);
                    mSelectionAnchor = mDragCandidateItemIndex;
                    mArrowCursor     = mDragCandidateItemIndex;
                    NotifySelectionChanged();
                }
                mDragCandidate              = false;
                mCollapseSelectionOnMouseUp = false;
                mDragCandidateItemIndex     = -1;
            }
            if (e.type == EventType.DragExited)
            {
                EndItemDrag();
                return;
            }
            if (!mItemDragActive)
            {
                ClearDropTarget();
                return;
            }
            DragPayload payload = DragAndDrop.GetGenericData(mDragDataKey) as DragPayload;
            if (payload == null || payload.sourceView != this)
            {
                EndItemDrag();
                return;
            }
            if (viewHovered)
            {
                mCanDrop = ResolveDropTarget(out mDropTargetItemIndex, out mDropIndicatorVisibleRow, out mDropMode) &&
                           CanDropItems(payload.itemData, mDropTargetItemIndex, mDropMode);
            }
            else ClearDropTarget();
            if (e.type == EventType.Repaint && mDropTargetItemIndex >= 0)
                DrawDropIndicator(mDropIndicatorVisibleRow, contentWidth, mDropMode, mCanDrop);
            if (e.type != EventType.DragUpdated && e.type != EventType.DragPerform)
                return;
            DragAndDrop.visualMode = mCanDrop ? DragAndDropVisualMode.Move : DragAndDropVisualMode.Rejected;
            if (e.type == EventType.DragPerform && mCanDrop)
            {
                T targetData = mModel.GetItemData(mDropTargetItemIndex);
                DragAndDrop.AcceptDrag();
                if (dropItems != null)
                    dropItems(payload.itemData, targetData, mDropMode);
                else
                    ApplyDropToModel(payload.itemData, targetData, mDropMode);
                ResetSelectionNavigation();
                EndItemDrag();
            }
            e.Use();
        }
        void EndItemDrag()
        {
            mItemDragActive = false;
            DragAndDrop.SetGenericData(mDragDataKey, null);
            ClearDropTarget();
        }
        void ClearDropTarget()
        {
            mDropTargetItemIndex        = -1;
            mDropIndicatorVisibleRow    = -1;
            mDropMode                   = EIMGUITreeViewDropMode.MoveAfter;
            mCanDrop                    = false;
        }
        void StartItemDrag()
        {
            if (mDragCandidateItemIndex < 0 ||
                mDragCandidateItemIndex >= mModel.itemCount)
                return;
            CollectDraggedItems(mDraggedItemBuffer);
            if (mDraggedItemBuffer.Count == 0)
                return;
            DragPayload payload = new DragPayload();
            payload.sourceView = this;
            payload.itemData.AddRange(mDraggedItemBuffer);
            T primaryData = mModel.GetItemData(mDragCandidateItemIndex);
            GetItemData(primaryData, mScratchItemData);
            string dragTitle = string.IsNullOrEmpty(mScratchItemData.label) ? "Tree Items" : mScratchItemData.label;
            DragAndDrop.PrepareStartDrag();
            DragAndDrop.SetGenericData(mDragDataKey, payload);
            DragAndDrop.StartDrag(dragTitle);
            mItemDragActive = true;
        }
        void CollectDraggedItems(List<T> itemData)
        {
            itemData.Clear();
            IReadOnlyList<int> selectedIndices = mModel.selectedItemIndices;
            int count = selectedIndices.Count;
            for (int i = 0; i < count; ++i)
            {
                int itemIndex = selectedIndices[i];
                if (!HasSelectedAncestor(itemIndex))
                    itemData.Add(mModel.GetItemData(itemIndex));
            }
        }
        bool HasSelectedAncestor(int itemIndex)
        {
            int parentIndex = mModel.GetItemParentIndex(itemIndex);
            while (parentIndex >= 0)
            {
                if (mModel.IsItemSelected(parentIndex))
                    return true;
                parentIndex = mModel.GetItemParentIndex(parentIndex);
            }
            return false;
        }
        bool ResolveDropTarget(out int targetItemIndex, out int indicatorVisibleRow,
            out EIMGUITreeViewDropMode dropMode)
        {
            targetItemIndex       = -1;
            indicatorVisibleRow   = -1;
            dropMode              = EIMGUITreeViewDropMode.MoveAfter;
            int visibleItemCount = mModel.visibleItemCount;
            if (visibleItemCount == 0)
                return false;
            float mouseY = Event.current.mousePosition.y;
            int visibleRow = Mathf.FloorToInt(mouseY / mRowHeight);
            visibleRow = Mathf.Clamp(visibleRow, 0, visibleItemCount - 1);
            int currentItemIndex = mModel.GetVisibleItemIndex(visibleRow);
            int currentDepth     = mModel.GetItemDepth(currentItemIndex);
            float rowY           = visibleRow * mRowHeight;
            float rowLocalY      = mouseY - rowY;
            float insertBand     = Mathf.Min(mRowHeight * 0.25f, 4.0f);
            if (rowLocalY <= insertBand)
            {
                if (visibleRow > 0)
                {
                    int previousVisibleRow = visibleRow - 1;
                    int previousItemIndex   = mModel.GetVisibleItemIndex(previousVisibleRow);
                    int previousDepth       = mModel.GetItemDepth(previousItemIndex);
                    if (previousDepth > currentDepth)
                    {
                        dropMode              = EIMGUITreeViewDropMode.MoveAfter;
                        targetItemIndex       = previousItemIndex;
                        indicatorVisibleRow   = previousVisibleRow;
                    }
                    else
                    {
                        dropMode              = EIMGUITreeViewDropMode.MoveBefore;
                        targetItemIndex       = currentItemIndex;
                        indicatorVisibleRow   = visibleRow;
                    }
                }
                else
                {
                    dropMode              = EIMGUITreeViewDropMode.MoveBefore;
                    targetItemIndex       = currentItemIndex;
                    indicatorVisibleRow   = visibleRow;
                }
            }
            else
            if (rowLocalY >= mRowHeight - insertBand)
            {
                if (visibleRow + 1 < visibleItemCount)
                {
                    int nextVisibleRow = visibleRow + 1;
                    int nextItemIndex   = mModel.GetVisibleItemIndex(nextVisibleRow);
                    int nextDepth       = mModel.GetItemDepth(nextItemIndex);
                    if (currentDepth > nextDepth)
                    {
                        dropMode              = EIMGUITreeViewDropMode.MoveAfter;
                        targetItemIndex       = currentItemIndex;
                        indicatorVisibleRow   = visibleRow;
                    }
                    else
                    {
                        dropMode              = EIMGUITreeViewDropMode.MoveBefore;
                        targetItemIndex       = nextItemIndex;
                        indicatorVisibleRow   = nextVisibleRow;
                    }
                }
                else
                {
                    dropMode              = EIMGUITreeViewDropMode.MoveAfter;
                    targetItemIndex       = currentItemIndex;
                    indicatorVisibleRow   = visibleRow;
                }
            }
            else
            {
                dropMode              = EIMGUITreeViewDropMode.MakeChild;
                targetItemIndex       = currentItemIndex;
                indicatorVisibleRow   = visibleRow;
            }
            return targetItemIndex >= 0;
        }
        bool CanDropItems(IReadOnlyList<T> itemData, int targetItemIndex, EIMGUITreeViewDropMode dropMode)
        {
            if (targetItemIndex < 0 || targetItemIndex >= mModel.itemCount)
                return false;
            int count = itemData.Count;
            for (int i = 0; i < count; ++i)
            {
                int draggedItemIndex = mModel.FindItemIndex(itemData[i]);
                if (draggedItemIndex < 0 ||
                    targetItemIndex == draggedItemIndex ||
                    IsItemDescendantOf(targetItemIndex, draggedItemIndex))
                    return false;
            }
            if (canDropItems != null)
                return canDropItems(itemData, mModel.GetItemData(targetItemIndex), dropMode);
            return true;
        }
        bool IsItemDescendantOf(int itemIndex, int ancestorIndex)
        {
            int parentIndex = mModel.GetItemParentIndex(itemIndex);
            while (parentIndex >= 0)
            {
                if (parentIndex == ancestorIndex)
                    return true;
                parentIndex = mModel.GetItemParentIndex(parentIndex);
            }
            return false;
        }
        void ApplyDropToModel(IReadOnlyList<T> itemData, T targetData, EIMGUITreeViewDropMode dropMode)
        {
            int count = itemData.Count;
            if (dropMode == EIMGUITreeViewDropMode.MoveBefore)
            {
                for (int i = 0; i < count; ++i)
                    mModel.MoveItemBefore(itemData[i], targetData);
                return;
            }
            if (dropMode == EIMGUITreeViewDropMode.MoveAfter)
            {
                for (int i = count - 1; i >= 0; --i)
                    mModel.MoveItemAfter(itemData[i], targetData);
                return;
            }
            for (int i = 0; i < count; ++i)
                mModel.SetItemParent(itemData[i], targetData);
        }
        void DrawDropIndicator(int visibleRow, float contentWidth, EIMGUITreeViewDropMode dropMode, bool canDrop)
        {
            if (!canDrop || visibleRow < 0)
                return;
            float rowY = visibleRow * mRowHeight;
            if (dropMode == EIMGUITreeViewDropMode.MakeChild)
            {
                Rect targetRect = new Rect(0.0f, rowY, contentWidth, mRowHeight);
                EditorGUI.DrawRect(targetRect, new Color(mDropIndicatorColor.r, mDropIndicatorColor.g, mDropIndicatorColor.b, 0.25f));
            }
            else
            {
                float lineY = dropMode == EIMGUITreeViewDropMode.MoveBefore ? rowY : rowY + mRowHeight;
                EditorGUI.DrawRect(new Rect(0.0f, lineY - 1.0f, contentWidth, 2.0f), mDropIndicatorColor);
            }
        }
        void NotifySelectionChanged()
        {
            if (selectionChanged != null)
                selectionChanged(mModel.selectedItemData);
        }
        void GetItemData(T itemData, IMGUITreeViewItemData drawItemData)
        {
            drawItemData.image       = null;
            drawItemData.label       = !object.ReferenceEquals(itemData, null) ? itemData.ToString() : string.Empty;
            drawItemData.tooltip     = string.Empty;
            drawItemData.tint        = Color.white;
            drawItemData.labelStyle  = null;
            if (getItemData != null)
                getItemData(itemData, drawItemData);
        }
    }
}
#endif
