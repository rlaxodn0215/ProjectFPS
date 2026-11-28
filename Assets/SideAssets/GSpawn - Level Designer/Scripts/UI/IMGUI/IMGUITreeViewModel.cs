#if UNITY_EDITOR
using System;
using System.Collections.Generic;
namespace GSPAWN
{
    public class IMGUITreeViewItem<T>
    {
        T       mData;                          
        int     mParentIndex        = -1;       
        int     mDepth              = 0;        
        bool    mExpanded           = true;     
        int     mSubtreeEndIndex    = 0;        
        int     mVisibleRow         = -1;       
        bool    mSelected           = false;    
        public T    data                { get { return mData; } }
        public int  parentIndex         { get { return mParentIndex; } }
        public int  depth               { get { return mDepth; } }
        public bool expanded            { get { return mExpanded; } }
        public int  subtreeEndIndex     { get { return mSubtreeEndIndex; } }
        public int  visibleRow          { get { return mVisibleRow; } }
        public bool selected            { get { return mSelected; } }
        public IMGUITreeViewItem(T data, bool expanded = true)
        {
            mData       = data;
            mExpanded   = expanded;
        }
        public void Internal_SetData(T data)
        {
            mData = data;
        }
        public void Internal_SetParentIndex(int parentIndex)
        {
            mParentIndex = parentIndex;
        }
        public void Internal_SetDepth(int depth)
        {
            mDepth = depth;
        }
        public void Internal_SetExpanded(bool expanded)
        {
            mExpanded = expanded;
        }
        public void Internal_SetSubtreeEndIndex(int subtreeEndIndex)
        {
            mSubtreeEndIndex = subtreeEndIndex;
        }
        public void Internal_SetVisibleRow(int visibleRow)
        {
            mVisibleRow = visibleRow;
        }
        public void Internal_SetSelected(bool selected)
        {
            mSelected = selected;
        }
    }
    public class IMGUITreeViewModel<T>
    {
        List<IMGUITreeViewItem<T>>  mItems                  = new();    
        List<int>                   mVisibleItemIndices     = new();    
        List<int>                   mSelectedItemIndices    = new();    
        List<T>                     mSelectedItemData       = new();    
        List<int>                   mRootItemIndices        = new();    
        List<int>                   mOpenItemIndices            = new();    
        List<IMGUITreeViewItem<T>>  mMoveBuffer                 = new();    
        List<IMGUITreeViewItem<T>>  mInsertBuffer               = new();    
        List<int>                   mInsertParentOffsetBuffer   = new();    
        HashSet<T>                  mInsertDataSet              = new();    
        Dictionary<T, int>          mItemIndexMap               = new();    
        bool                        mTraversalDirty             = true;     
        bool                        mSelectionDirty             = true;     
        bool                        mItemIndexMapDirty          = false;    
        Predicate<T>                mFilter;                                
        bool                        mFilterActive;                          
        int                         mBulkUpdateDepth            = 0;        
        bool                        mBulkVersionDirty           = false;    
        int                         mVersion                    = 0;        
        public int                                      itemCount           { get { return mItems.Count; } }
        public int                                      rootCount           { get { ValidateTraversal(); return mRootItemIndices.Count; } }
        public int                                      visibleItemCount    { get { ValidateTraversal(); return mVisibleItemIndices.Count; } }
        public int                                      version             { get { return mVersion; } }
        public IReadOnlyList<IMGUITreeViewItem<T>>      items               { get { return mItems; } }
        public IReadOnlyList<int>                       visibleItemIndices  { get { ValidateTraversal(); return mVisibleItemIndices; } }
        public int                                      selectedItemCount   { get { ValidateSelection(); return mSelectedItemIndices.Count; } }
        public IReadOnlyList<int>                       selectedItemIndices { get { ValidateSelection(); return mSelectedItemIndices; } }
        public IReadOnlyList<T>                         selectedItemData    { get { ValidateSelection(); return mSelectedItemData; } }
        public bool                                     filterActive        { get { return mFilterActive; } }
        public void RefreshTraversal()
        {
            MarkTraversalDirty();
            IncrementVersion();
        }
        public void BeginBulkUpdate()
        {
            ++mBulkUpdateDepth;
        }
        public void EndBulkUpdate()
        {
            if (mBulkUpdateDepth == 0)
                return;
            --mBulkUpdateDepth;
            if (mBulkUpdateDepth != 0 ||
                !mBulkVersionDirty)
                return;
            mBulkVersionDirty = false;
            ++mVersion;
        }
        public void EnsureCapacity(int itemCapacity)
        {
            if (itemCapacity <= 0)
                return;
            if (mItems.Capacity < itemCapacity)
                mItems.Capacity = itemCapacity;
            if (mVisibleItemIndices.Capacity < itemCapacity)
                mVisibleItemIndices.Capacity = itemCapacity;
        }
        public void Clear()
        {
            mItems.Clear();
            mVisibleItemIndices.Clear();
            mSelectedItemIndices.Clear();
            mSelectedItemData.Clear();
            mRootItemIndices.Clear();
            mOpenItemIndices.Clear();
            mMoveBuffer.Clear();
            mInsertBuffer.Clear();
            mInsertParentOffsetBuffer.Clear();
            mInsertDataSet.Clear();
            mItemIndexMap.Clear();
            mTraversalDirty       = false;
            mSelectionDirty       = false;
            mItemIndexMapDirty    = false;
            IncrementVersion();
        }
        public void FilterItems(Predicate<T> filter)
        {
            if (filter == null)
            {
                ClearFilter();
                return;
            }
            mFilter         = filter;
            mFilterActive   = true;
            MarkTraversalDirty();
            IncrementVersion();
        }
        public void ClearFilter()
        {
            if (!mFilterActive &&
                mFilter == null)
                return;
            mFilter         = null;
            mFilterActive   = false;
            MarkTraversalDirty();
            IncrementVersion();
        }
        public int AddRootAt(int rootRow, T data, bool expanded = true)
        {
            if (ContainsItem(data))
                return -1;
            ValidateTraversal();
            int insertIndex;
            if (mRootItemIndices.Count == 0)
                insertIndex = mItems.Count;
            else
            if (rootRow <= 0)
                insertIndex = mRootItemIndices[0];
            else
            if (rootRow >= mRootItemIndices.Count)
                insertIndex = mItems.Count;
            else
                insertIndex = mRootItemIndices[rootRow];
            IMGUITreeViewItem<T> item = new IMGUITreeViewItem<T>(data, expanded);
            item.Internal_SetParentIndex(-1);
            item.Internal_SetDepth(0);
            if (insertIndex == mItems.Count)
                mItems.Add(item);
            else
            {
                ShiftParentIndicesForInsert(insertIndex, 1);
                mItems.Insert(insertIndex, item);
            }
            MarkTraversalDirty();
            RegisterInsertedItemIndex(data, insertIndex);
            IncrementVersion();
            return insertIndex;
        }
        public int AddChildAt(int parentIndex, int childRow, T data, bool expanded = true)
        {
            if (!IsValidItemIndex(parentIndex))
                return -1;
            if (ContainsItem(data))
                return -1;
            int insertIndex = FindChildInsertIndex(parentIndex, childRow);
            IMGUITreeViewItem<T> item = new IMGUITreeViewItem<T>(data, expanded);
            item.Internal_SetParentIndex(parentIndex);
            item.Internal_SetDepth(mItems[parentIndex].depth + 1);
            if (insertIndex == mItems.Count)
                mItems.Add(item);
            else
            {
                ShiftParentIndicesForInsert(insertIndex, 1);
                mItems.Insert(insertIndex, item);
            }
            MarkTraversalDirty();
            RegisterInsertedItemIndex(data, insertIndex);
            IncrementVersion();
            return insertIndex;
        }
        public int AddRoot(T data, bool expanded = true)
        {
            if (ContainsItem(data))
                return -1;
            IMGUITreeViewItem<T> item = new IMGUITreeViewItem<T>(data, expanded);
            item.Internal_SetParentIndex(-1);
            item.Internal_SetDepth(0);
            int itemIndex = mItems.Count;
            mItems.Add(item);
            MarkTraversalDirty();
            RegisterInsertedItemIndex(data, itemIndex);
            IncrementVersion();
            return itemIndex;
        }
        public int AddChild(int parentIndex, T data, bool expanded = true)
        {
            if (!IsValidItemIndex(parentIndex))
                return -1;
            if (ContainsItem(data))
                return -1;
            int insertIndex = FindSubtreeEndIndex(parentIndex);
            IMGUITreeViewItem<T> item = new IMGUITreeViewItem<T>(data, expanded);
            item.Internal_SetParentIndex(parentIndex);
            item.Internal_SetDepth(mItems[parentIndex].depth + 1);
            if (insertIndex == mItems.Count)
                mItems.Add(item);
            else
            {
                ShiftParentIndicesForInsert(insertIndex, 1);
                mItems.Insert(insertIndex, item);
            }
            MarkTraversalDirty();
            RegisterInsertedItemIndex(data, insertIndex);
            IncrementVersion();
            return insertIndex;
        }
        public int AppendChild(int parentIndex, T data, bool expanded = true)
        {
            if (!IsValidItemIndex(parentIndex))
                return -1;
            if (ContainsItem(data))
                return -1;
            IMGUITreeViewItem<T> item = new IMGUITreeViewItem<T>(data, expanded);
            item.Internal_SetParentIndex(parentIndex);
            item.Internal_SetDepth(mItems[parentIndex].depth + 1);
            int itemIndex = mItems.Count;
            mItems.Add(item);
            MarkTraversalDirty();
            RegisterInsertedItemIndex(data, itemIndex);
            IncrementVersion();
            return itemIndex;
        }
        public int AppendChild(T parentData, T data, bool expanded = true)
        {
            int parentIndex = FindItemIndex(parentData);
            if (parentIndex < 0)
                return -1;
            return AppendChild(parentIndex, data, expanded);
        }
        public int AddChild(T parentData, T data, bool expanded = true)
        {
            int parentIndex = FindItemIndex(parentData);
            if (parentIndex < 0)
                return -1;
            return AddChild(parentIndex, data, expanded);
        }
        public void RemoveItem(int itemIndex)
        {
            if (!IsValidItemIndex(itemIndex))
                return;
            int subtreeEndIndex = FindSubtreeEndIndex(itemIndex);
            int subtreeSize     = subtreeEndIndex - itemIndex;
            mItems.RemoveRange(itemIndex, subtreeSize);
            ShiftParentIndicesForRemove(itemIndex, subtreeEndIndex, subtreeSize);
            MarkTraversalDirty();
            MarkSelectionDirty();
            MarkItemIndexMapDirty();
            IncrementVersion();
        }
        public void RemoveItems(IReadOnlyList<int> itemIndices)
        {
            if (itemIndices == null ||
                itemIndices.Count == 0 ||
                mItems.Count == 0)
                return;
            ValidateTraversal();
            int itemCount = mItems.Count;
            bool[] removeFlags = new bool[itemCount];
            bool changed = false;
            for (int i = 0; i < itemIndices.Count; ++i)
            {
                int itemIndex = itemIndices[i];
                if (!IsValidItemIndex(itemIndex))
                    continue;
                int subtreeEndIndex = mItems[itemIndex].subtreeEndIndex;
                for (int removeIndex = itemIndex; removeIndex < subtreeEndIndex; ++removeIndex)
                {
                    if (!removeFlags[removeIndex])
                    {
                        removeFlags[removeIndex] = true;
                        changed = true;
                    }
                }
            }
            if (!changed)
                return;
            int[] indexMap = new int[itemCount];
            List<IMGUITreeViewItem<T>> keptItems = new List<IMGUITreeViewItem<T>>(itemCount);
            for (int itemIndex = 0; itemIndex < itemCount; ++itemIndex)
            {
                if (removeFlags[itemIndex])
                    indexMap[itemIndex] = -1;
                else
                {
                    indexMap[itemIndex] = keptItems.Count;
                    keptItems.Add(mItems[itemIndex]);
                }
            }
            for (int itemIndex = 0; itemIndex < itemCount; ++itemIndex)
            {
                if (removeFlags[itemIndex])
                    continue;
                IMGUITreeViewItem<T> item = mItems[itemIndex];
                int parentIndex = item.parentIndex;
                item.Internal_SetParentIndex(parentIndex >= 0 ? indexMap[parentIndex] : -1);
            }
            mItems.Clear();
            mItems.AddRange(keptItems);
            MarkTraversalDirty();
            MarkSelectionDirty();
            MarkItemIndexMapDirty();
            IncrementVersion();
        }
        public void RemoveItem(T data)
        {
            int itemIndex = FindItemIndex(data);
            if (itemIndex < 0)
                return;
            RemoveItem(itemIndex);
        }
        public int SetItemParent(int itemIndex, int parentIndex)
        {
            if (!IsValidItemIndex(itemIndex) ||
                !IsValidParentIndex(parentIndex))
                return -1;
            if (mItems[itemIndex].parentIndex == parentIndex)
                return itemIndex;
            int subtreeEndIndex = FindSubtreeEndIndex(itemIndex);
            if (parentIndex >= itemIndex &&
                parentIndex < subtreeEndIndex)
                return -1;
            int subtreeSize         = subtreeEndIndex - itemIndex;
            int insertIndex         = parentIndex >= 0 ? FindSubtreeEndIndex(parentIndex) : mItems.Count;
            int oldDepth            = mItems[itemIndex].depth;
            int newDepth            = parentIndex >= 0 ? mItems[parentIndex].depth + 1 : 0;
            int depthDelta          = newDepth - oldDepth;
            int adjustedParentIndex = parentIndex;
            mMoveBuffer.Clear();
            for (int i = itemIndex; i < subtreeEndIndex; ++i)
                mMoveBuffer.Add(mItems[i]);
            mItems.RemoveRange(itemIndex, subtreeSize);
            ShiftParentIndicesForRemove(itemIndex, subtreeEndIndex, subtreeSize);
            if (insertIndex > itemIndex)
                insertIndex -= subtreeSize;
            if (adjustedParentIndex >= subtreeEndIndex)
                adjustedParentIndex -= subtreeSize;
            ShiftParentIndicesForInsert(insertIndex, subtreeSize);
            for (int i = 0; i < subtreeSize; ++i)
            {
                IMGUITreeViewItem<T> item = mMoveBuffer[i];
                int oldParentIndex        = item.parentIndex;
                item.Internal_SetDepth(item.depth + depthDelta);
                if (i == 0)
                    item.Internal_SetParentIndex(adjustedParentIndex);
                else
                    item.Internal_SetParentIndex(insertIndex + (oldParentIndex - itemIndex));
            }
            mItems.InsertRange(insertIndex, mMoveBuffer);
            mMoveBuffer.Clear();
            MarkTraversalDirty();
            MarkSelectionDirty();
            MarkItemIndexMapDirty();
            IncrementVersion();
            return insertIndex;
        }
        public int SetItemParent(T data, T parentData)
        {
            int itemIndex = FindItemIndex(data);
            if (itemIndex < 0)
                return -1;
            int parentIndex = FindItemIndex(parentData);
            if (parentIndex < 0)
                return -1;
            return SetItemParent(itemIndex, parentIndex);
        }
        public int SetItemAsRoot(T data)
        {
            int itemIndex = FindItemIndex(data);
            if (itemIndex < 0)
                return -1;
            return SetItemParent(itemIndex, -1);
        }
        public int MoveItemAfter(int itemIndex, int targetIndex)
        {
            if (!IsValidItemIndex(itemIndex) ||
                !IsValidItemIndex(targetIndex))
                return -1;
            if (itemIndex == targetIndex)
                return -1;
            int subtreeEndIndex = FindSubtreeEndIndex(itemIndex);
            if (targetIndex > itemIndex &&
                targetIndex < subtreeEndIndex)
                return -1;
            int targetSubtreeEndIndex    = FindSubtreeEndIndex(targetIndex);
            int targetParentIndex        = mItems[targetIndex].parentIndex;
            int targetDepth              = mItems[targetIndex].depth;
            if (mItems[itemIndex].parentIndex == targetParentIndex &&
                itemIndex == targetSubtreeEndIndex)
                return itemIndex;
            int subtreeSize                 = subtreeEndIndex - itemIndex;
            int insertIndex                 = targetSubtreeEndIndex;
            int oldDepth                    = mItems[itemIndex].depth;
            int depthDelta                  = targetDepth - oldDepth;
            int adjustedTargetParentIndex   = targetParentIndex;
            mMoveBuffer.Clear();
            for (int i = itemIndex; i < subtreeEndIndex; ++i)
                mMoveBuffer.Add(mItems[i]);
            mItems.RemoveRange(itemIndex, subtreeSize);
            ShiftParentIndicesForRemove(itemIndex, subtreeEndIndex, subtreeSize);
            if (insertIndex > itemIndex)
                insertIndex -= subtreeSize;
            if (adjustedTargetParentIndex >= subtreeEndIndex)
                adjustedTargetParentIndex -= subtreeSize;
            ShiftParentIndicesForInsert(insertIndex, subtreeSize);
            for (int i = 0; i < subtreeSize; ++i)
            {
                IMGUITreeViewItem<T> item = mMoveBuffer[i];
                int oldParentIndex        = item.parentIndex;
                item.Internal_SetDepth(item.depth + depthDelta);
                if (i == 0)
                    item.Internal_SetParentIndex(adjustedTargetParentIndex);
                else
                    item.Internal_SetParentIndex(insertIndex + (oldParentIndex - itemIndex));
            }
            mItems.InsertRange(insertIndex, mMoveBuffer);
            mMoveBuffer.Clear();
            MarkTraversalDirty();
            MarkSelectionDirty();
            MarkItemIndexMapDirty();
            IncrementVersion();
            return insertIndex;
        }
        public int MoveItemAfter(T data, T targetData)
        {
            int itemIndex = FindItemIndex(data);
            if (itemIndex < 0)
                return -1;
            int targetIndex = FindItemIndex(targetData);
            if (targetIndex < 0)
                return -1;
            return MoveItemAfter(itemIndex, targetIndex);
        }
        public int MoveItemBefore(int itemIndex, int targetIndex)
        {
            if (!IsValidItemIndex(itemIndex) ||
                !IsValidItemIndex(targetIndex))
                return -1;
            if (itemIndex == targetIndex)
                return -1;
            int subtreeEndIndex = FindSubtreeEndIndex(itemIndex);
            if (targetIndex > itemIndex &&
                targetIndex < subtreeEndIndex)
                return -1;
            int targetParentIndex        = mItems[targetIndex].parentIndex;
            int targetDepth              = mItems[targetIndex].depth;
            if (mItems[itemIndex].parentIndex == targetParentIndex &&
                subtreeEndIndex == targetIndex)
                return itemIndex;
            int subtreeSize                 = subtreeEndIndex - itemIndex;
            int insertIndex                 = targetIndex;
            int oldDepth                    = mItems[itemIndex].depth;
            int depthDelta                  = targetDepth - oldDepth;
            int adjustedTargetParentIndex   = targetParentIndex;
            mMoveBuffer.Clear();
            for (int i = itemIndex; i < subtreeEndIndex; ++i)
                mMoveBuffer.Add(mItems[i]);
            mItems.RemoveRange(itemIndex, subtreeSize);
            ShiftParentIndicesForRemove(itemIndex, subtreeEndIndex, subtreeSize);
            if (insertIndex > itemIndex)
                insertIndex -= subtreeSize;
            if (adjustedTargetParentIndex >= subtreeEndIndex)
                adjustedTargetParentIndex -= subtreeSize;
            ShiftParentIndicesForInsert(insertIndex, subtreeSize);
            for (int i = 0; i < subtreeSize; ++i)
            {
                IMGUITreeViewItem<T> item = mMoveBuffer[i];
                int oldParentIndex        = item.parentIndex;
                item.Internal_SetDepth(item.depth + depthDelta);
                if (i == 0)
                    item.Internal_SetParentIndex(adjustedTargetParentIndex);
                else
                    item.Internal_SetParentIndex(insertIndex + (oldParentIndex - itemIndex));
            }
            mItems.InsertRange(insertIndex, mMoveBuffer);
            mMoveBuffer.Clear();
            MarkTraversalDirty();
            MarkSelectionDirty();
            MarkItemIndexMapDirty();
            IncrementVersion();
            return insertIndex;
        }
        public int MoveItemBefore(T data, T targetData)
        {
            int itemIndex = FindItemIndex(data);
            if (itemIndex < 0)
                return -1;
            int targetIndex = FindItemIndex(targetData);
            if (targetIndex < 0)
                return -1;
            return MoveItemBefore(itemIndex, targetIndex);
        }
        public int InsertSubtreeAfter(int targetIndex, T rootData, Func<T, int> getChildCount,
            Func<T, int, T> getChild, bool expanded = true)
        {
            if (!IsValidItemIndex(targetIndex) ||
                getChildCount == null ||
                getChild == null)
                return -1;
            mInsertBuffer.Clear();
            mInsertParentOffsetBuffer.Clear();
            mInsertDataSet.Clear();
            int targetDepth = mItems[targetIndex].depth;
            if (!BuildInsertedSubtree(rootData, -1, targetDepth, expanded, getChildCount, getChild))
            {
                mInsertBuffer.Clear();
                mInsertParentOffsetBuffer.Clear();
                mInsertDataSet.Clear();
                return -1;
            }
            int insertIndex         = FindSubtreeEndIndex(targetIndex);
            int insertedItemCount   = mInsertBuffer.Count;
            int rootParentIndex     = mItems[targetIndex].parentIndex;
            if (insertIndex != mItems.Count)
                ShiftParentIndicesForInsert(insertIndex, insertedItemCount);
            for (int i = 0; i < insertedItemCount; ++i)
            {
                int parentOffset = mInsertParentOffsetBuffer[i];
                if (parentOffset < 0)
                    mInsertBuffer[i].Internal_SetParentIndex(rootParentIndex);
                else
                    mInsertBuffer[i].Internal_SetParentIndex(insertIndex + parentOffset);
            }
            mItems.InsertRange(insertIndex, mInsertBuffer);
            mInsertBuffer.Clear();
            mInsertParentOffsetBuffer.Clear();
            mInsertDataSet.Clear();
            MarkTraversalDirty();
            MarkSelectionDirty();
            MarkItemIndexMapDirty();
            IncrementVersion();
            return insertIndex;
        }
        public int InsertSubtreeAfter(T targetData, T rootData, Func<T, int> getChildCount,
            Func<T, int, T> getChild, bool expanded = true)
        {
            int targetIndex = FindItemIndex(targetData);
            if (targetIndex < 0)
                return -1;
            return InsertSubtreeAfter(targetIndex, rootData, getChildCount, getChild, expanded);
        }
        public bool ContainsItem(T data)
        {
            return FindItemIndex(data) >= 0;
        }
        public int FindItemIndex(T data)
        {
            ValidateItemIndexMap();
            if (mItemIndexMap.TryGetValue(data, out int itemIndex))
                return itemIndex;
            return -1;
        }
        public bool IsItemSelected(int itemIndex)
        {
            if (!IsValidItemIndex(itemIndex))
                return false;
            return mItems[itemIndex].selected;
        }
        public bool IsItemSelected(T data)
        {
            int itemIndex = FindItemIndex(data);
            if (itemIndex < 0)
                return false;
            return IsItemSelected(itemIndex);
        }
        public void SetItemSelected(int itemIndex, bool selected)
        {
            if (!IsValidItemIndex(itemIndex))
                return;
            if (mItems[itemIndex].selected == selected)
                return;
            mItems[itemIndex].Internal_SetSelected(selected);
            MarkSelectionDirty();
            IncrementVersion();
        }
        public void SetItemSelected(T data, bool selected)
        {
            int itemIndex = FindItemIndex(data);
            if (itemIndex < 0)
                return;
            SetItemSelected(itemIndex, selected);
        }
        public void ToggleItemSelected(int itemIndex)
        {
            if (!IsValidItemIndex(itemIndex))
                return;
            SetItemSelected(itemIndex, !mItems[itemIndex].selected);
        }
        public void ToggleItemSelected(T data)
        {
            int itemIndex = FindItemIndex(data);
            if (itemIndex < 0)
                return;
            ToggleItemSelected(itemIndex);
        }
        public void ClearSelection()
        {
            bool changed = false;
            int itemCount = mItems.Count;
            for (int i = 0; i < itemCount; ++i)
            {
                if (mItems[i].selected)
                {
                    mItems[i].Internal_SetSelected(false);
                    changed = true;
                }
            }
            if (changed)
            {
                MarkSelectionDirty();
                IncrementVersion();
            }
        }
        public void SelectOnlyItem(int itemIndex)
        {
            if (!IsValidItemIndex(itemIndex))
                return;
            bool changed = false;
            int itemCount = mItems.Count;
            for (int i = 0; i < itemCount; ++i)
            {
                bool selected = i == itemIndex;
                if (mItems[i].selected != selected)
                {
                    mItems[i].Internal_SetSelected(selected);
                    changed = true;
                }
            }
            if (changed)
            {
                MarkSelectionDirty();
                IncrementVersion();
            }
        }
        public void SelectOnlyItem(T data)
        {
            int itemIndex = FindItemIndex(data);
            if (itemIndex < 0)
                return;
            SelectOnlyItem(itemIndex);
        }
        public void SelectOnlyItems(IReadOnlyList<T> itemData)
        {
            if (itemData == null)
                return;
            bool changed = false;
            int itemCount = mItems.Count;
            for (int i = 0; i < itemCount; ++i)
            {
                if (mItems[i].selected)
                {
                    mItems[i].Internal_SetSelected(false);
                    changed = true;
                }
            }
            for (int i = 0; i < itemData.Count; ++i)
            {
                int itemIndex = FindItemIndex(itemData[i]);
                if (itemIndex < 0)
                    continue;
                if (!mItems[itemIndex].selected)
                {
                    mItems[itemIndex].Internal_SetSelected(true);
                    changed = true;
                }
            }
            if (changed)
            {
                MarkSelectionDirty();
                IncrementVersion();
            }
        }
        public void SetSelectionRange(int firstItemIndex, int lastItemIndex, bool selected)
        {
            if (mItems.Count == 0)
                return;
            int minIndex = UnityEngine.Mathf.Min(firstItemIndex, lastItemIndex);
            int maxIndex = UnityEngine.Mathf.Max(firstItemIndex, lastItemIndex);
            minIndex = UnityEngine.Mathf.Clamp(minIndex, 0, mItems.Count - 1);
            maxIndex = UnityEngine.Mathf.Clamp(maxIndex, 0, mItems.Count - 1);
            bool changed = false;
            for (int i = minIndex; i <= maxIndex; ++i)
            {
                if (mItems[i].selected != selected)
                {
                    mItems[i].Internal_SetSelected(selected);
                    changed = true;
                }
            }
            if (changed)
            {
                MarkSelectionDirty();
                IncrementVersion();
            }
        }
        public void SetVisibleSelectionRange(int firstVisibleRow, int lastVisibleRow, bool selected)
        {
            ValidateTraversal();
            if (mVisibleItemIndices.Count == 0)
                return;
            int minRow = UnityEngine.Mathf.Min(firstVisibleRow, lastVisibleRow);
            int maxRow = UnityEngine.Mathf.Max(firstVisibleRow, lastVisibleRow);
            minRow = UnityEngine.Mathf.Clamp(minRow, 0, mVisibleItemIndices.Count - 1);
            maxRow = UnityEngine.Mathf.Clamp(maxRow, 0, mVisibleItemIndices.Count - 1);
            bool changed = false;
            for (int row = minRow; row <= maxRow; ++row)
            {
                int itemIndex = mVisibleItemIndices[row];
                if (mItems[itemIndex].selected != selected)
                {
                    mItems[itemIndex].Internal_SetSelected(selected);
                    changed = true;
                }
            }
            if (changed)
            {
                MarkSelectionDirty();
                IncrementVersion();
            }
        }
        public int GetFirstSelectedItemIndex()
        {
            ValidateSelection();
            if (mSelectedItemIndices.Count == 0)
                return -1;
            return mSelectedItemIndices[0];
        }
        public void ExpandItemAncestors(int itemIndex)
        {
            if (!IsValidItemIndex(itemIndex))
                return;
            bool changed = false;
            int parentIndex = mItems[itemIndex].parentIndex;
            while (parentIndex >= 0)
            {
                if (!mItems[parentIndex].expanded)
                {
                    mItems[parentIndex].Internal_SetExpanded(true);
                    changed = true;
                }
                parentIndex = mItems[parentIndex].parentIndex;
            }
            if (changed)
            {
                MarkTraversalDirty();
                IncrementVersion();
            }
        }
        public void ExpandItemAncestors(T data)
        {
            int itemIndex = FindItemIndex(data);
            if (itemIndex < 0)
                return;
            ExpandItemAncestors(itemIndex);
        }
        public void SetItemExpanded(int itemIndex, bool expanded)
        {
            if (!IsValidItemIndex(itemIndex))
                return;
            if (mItems[itemIndex].expanded == expanded)
                return;
            mItems[itemIndex].Internal_SetExpanded(expanded);
            MarkTraversalDirty();
            IncrementVersion();
        }
        public void SetItemExpanded(T data, bool expanded)
        {
            int itemIndex = FindItemIndex(data);
            if (itemIndex < 0)
                return;
            SetItemExpanded(itemIndex, expanded);
        }
        public void ToggleItemExpanded(int itemIndex)
        {
            if (!IsValidItemIndex(itemIndex))
                return;
            SetItemExpanded(itemIndex, !mItems[itemIndex].expanded);
        }
        public void ToggleItemExpanded(T data)
        {
            int itemIndex = FindItemIndex(data);
            if (itemIndex < 0)
                return;
            ToggleItemExpanded(itemIndex);
        }
        public void SetSubtreeExpanded(int itemIndex, bool expanded)
        {
            if (!IsValidItemIndex(itemIndex))
                return;
            bool changed = false;
            int subtreeEndIndex = FindSubtreeEndIndex(itemIndex);
            for (int i = itemIndex; i < subtreeEndIndex; ++i)
            {
                if (mItems[i].expanded != expanded)
                {
                    mItems[i].Internal_SetExpanded(expanded);
                    changed = true;
                }
            }
            if (changed)
            {
                MarkTraversalDirty();
                IncrementVersion();
            }
        }
        public void SetSubtreeExpanded(T data, bool expanded)
        {
            int itemIndex = FindItemIndex(data);
            if (itemIndex < 0)
                return;
            SetSubtreeExpanded(itemIndex, expanded);
        }
        public void ToggleSubtreeExpanded(int itemIndex)
        {
            if (!IsValidItemIndex(itemIndex))
                return;
            SetSubtreeExpanded(itemIndex, !mItems[itemIndex].expanded);
        }
        public void ToggleSubtreeExpanded(T data)
        {
            int itemIndex = FindItemIndex(data);
            if (itemIndex < 0)
                return;
            ToggleSubtreeExpanded(itemIndex);
        }
        public void ExpandAll()
        {
            bool changed = false;
            int itemCount = mItems.Count;
            for (int i = 0; i < itemCount; ++i)
            {
                if (!mItems[i].expanded)
                {
                    mItems[i].Internal_SetExpanded(true);
                    changed = true;
                }
            }
            if (changed)
            {
                MarkTraversalDirty();
                IncrementVersion();
            }
        }
        public void CollapseAll()
        {
            bool changed = false;
            int itemCount = mItems.Count;
            for (int i = 0; i < itemCount; ++i)
            {
                if (mItems[i].expanded)
                {
                    mItems[i].Internal_SetExpanded(false);
                    changed = true;
                }
            }
            if (changed)
            {
                MarkTraversalDirty();
                IncrementVersion();
            }
        }
        public IMGUITreeViewItem<T> GetItem(int itemIndex)
        {
            if (!IsValidItemIndex(itemIndex))
                return null;
            return mItems[itemIndex];
        }
        public T GetItemData(int itemIndex)
        {
            if (!IsValidItemIndex(itemIndex))
                return default;
            return mItems[itemIndex].data;
        }
        public void SetItemData(int itemIndex, T data)
        {
            if (!IsValidItemIndex(itemIndex))
                return;
            EqualityComparer<T> comparer = EqualityComparer<T>.Default;
            if (comparer.Equals(mItems[itemIndex].data, data))
                return;
            if (ContainsItem(data))
                return;
            mItems[itemIndex].Internal_SetData(data);
            MarkItemIndexMapDirty();
            if (mItems[itemIndex].selected)
                MarkSelectionDirty();
            IncrementVersion();
        }
        public int GetItemParentIndex(int itemIndex)
        {
            if (!IsValidItemIndex(itemIndex))
                return -1;
            return mItems[itemIndex].parentIndex;
        }
        public int GetItemDepth(int itemIndex)
        {
            if (!IsValidItemIndex(itemIndex))
                return -1;
            return mItems[itemIndex].depth;
        }
        public int GetItemDepth(T data)
        {
            int itemIndex = FindItemIndex(data);
            if (itemIndex < 0)
                return -1;
            return GetItemDepth(itemIndex);
        }
        public int GetSubtreeEndIndex(int itemIndex)
        {
            if (!IsValidItemIndex(itemIndex))
                return -1;
            ValidateTraversal();
            return mItems[itemIndex].subtreeEndIndex;
        }
        public bool HasItemChildren(int itemIndex)
        {
            if (!IsValidItemIndex(itemIndex))
                return false;
            ValidateTraversal();
            return mItems[itemIndex].subtreeEndIndex > itemIndex + 1;
        }
        public bool HasItemChildren(T data)
        {
            int itemIndex = FindItemIndex(data);
            if (itemIndex < 0)
                return false;
            return HasItemChildren(itemIndex);
        }
        public bool IsItemExpanded(int itemIndex)
        {
            if (!IsValidItemIndex(itemIndex))
                return false;
            return mItems[itemIndex].expanded;
        }
        public bool IsItemExpanded(T data)
        {
            int itemIndex = FindItemIndex(data);
            if (itemIndex < 0)
                return false;
            return IsItemExpanded(itemIndex);
        }
        public bool IsItemVisible(int itemIndex)
        {
            if (!IsValidItemIndex(itemIndex))
                return false;
            ValidateTraversal();
            return mItems[itemIndex].visibleRow >= 0;
        }
        public bool IsItemVisible(T data)
        {
            int itemIndex = FindItemIndex(data);
            if (itemIndex < 0)
                return false;
            return IsItemVisible(itemIndex);
        }
        public int GetVisibleItemIndex(int visibleRow)
        {
            ValidateTraversal();
            if (visibleRow < 0 ||
                visibleRow >= mVisibleItemIndices.Count)
                return -1;
            return mVisibleItemIndices[visibleRow];
        }
        public int GetVisibleRow(int itemIndex)
        {
            if (!IsValidItemIndex(itemIndex))
                return -1;
            ValidateTraversal();
            return mItems[itemIndex].visibleRow;
        }
        public int GetVisibleRow(T data)
        {
            int itemIndex = FindItemIndex(data);
            if (itemIndex < 0)
                return -1;
            return GetVisibleRow(itemIndex);
        }
        public int GetRootItemIndex(int rootRow)
        {
            ValidateTraversal();
            if (rootRow < 0 ||
                rootRow >= mRootItemIndices.Count)
                return -1;
            return mRootItemIndices[rootRow];
        }
        public int GetChildCount(int itemIndex)
        {
            if (!IsValidItemIndex(itemIndex))
                return 0;
            ValidateTraversal();
            int childCount = 0;
            int itemDepth = mItems[itemIndex].depth;
            int childDepth = itemDepth + 1;
            int subtreeEndIndex = mItems[itemIndex].subtreeEndIndex;
            for (int i = itemIndex + 1; i < subtreeEndIndex; ++i)
            {
                int depth = mItems[i].depth;
                if (depth == childDepth)
                    ++childCount;
            }
            return childCount;
        }
        public int GetChildItemIndex(int itemIndex, int childRow)
        {
            if (!IsValidItemIndex(itemIndex) ||
                childRow < 0)
                return -1;
            ValidateTraversal();
            int childIndex = 0;
            int itemDepth = mItems[itemIndex].depth;
            int childDepth = itemDepth + 1;
            int subtreeEndIndex = mItems[itemIndex].subtreeEndIndex;
            for (int i = itemIndex + 1; i < subtreeEndIndex; ++i)
            {
                int depth = mItems[i].depth;
                if (depth == childDepth)
                {
                    if (childIndex == childRow)
                        return i;
                    ++childIndex;
                }
            }
            return -1;
        }
        void IncrementVersion()
        {
            if (mBulkUpdateDepth != 0)
            {
                mBulkVersionDirty = true;
                return;
            }
            ++mVersion;
        }
        bool BuildInsertedSubtree(T data, int parentOffset, int depth, bool expanded,
            Func<T, int> getChildCount, Func<T, int, T> getChild)
        {
            if (ContainsItem(data) ||
                mInsertDataSet.Contains(data))
                return false;
            mInsertDataSet.Add(data);
            int itemOffset = mInsertBuffer.Count;
            IMGUITreeViewItem<T> item = new IMGUITreeViewItem<T>(data, expanded);
            item.Internal_SetParentIndex(parentOffset);
            item.Internal_SetDepth(depth);
            mInsertBuffer.Add(item);
            mInsertParentOffsetBuffer.Add(parentOffset);
            int childCount = getChildCount(data);
            for (int childIndex = 0; childIndex < childCount; ++childIndex)
            {
                T childData = getChild(data, childIndex);
                if (!BuildInsertedSubtree(childData, itemOffset, depth + 1, expanded, getChildCount, getChild))
                    return false;
            }
            return true;
        }
        void ValidateSelection()
        {
            if (!mSelectionDirty)
                return;
            RebuildSelectedItemIndices();
            mSelectionDirty = false;
        }
        void ValidateTraversal()
        {
            if (!mTraversalDirty)
                return;
            RebuildSubtreeEndIndices();
            RebuildRootItemIndices();
            RebuildVisibleItemIndices();
            mTraversalDirty = false;
        }
        void RegisterInsertedItemIndex(T data, int itemIndex)
        {
            if (mItemIndexMapDirty)
                return;
            if (itemIndex == mItems.Count - 1)
            {
                mItemIndexMap.Add(data, itemIndex);
                return;
            }
            MarkItemIndexMapDirty();
        }
        void ValidateItemIndexMap()
        {
            if (!mItemIndexMapDirty)
                return;
            RebuildItemIndexMap();
            mItemIndexMapDirty = false;
        }
        void MarkSelectionDirty()
        {
            mSelectionDirty = true;
        }
        void MarkTraversalDirty()
        {
            mTraversalDirty = true;
        }
        void MarkItemIndexMapDirty()
        {
            mItemIndexMapDirty = true;
        }
        void RebuildSelectedItemIndices()
        {
            mSelectedItemIndices.Clear();
            mSelectedItemData.Clear();
            int itemCount = mItems.Count;
            for (int i = 0; i < itemCount; ++i)
            {
                if (mItems[i].selected)
                {
                    mSelectedItemIndices.Add(i);
                    mSelectedItemData.Add(mItems[i].data);
                }
            }
        }
        void RebuildItemIndexMap()
        {
            mItemIndexMap.Clear();
            int itemCount = mItems.Count;
            for (int i = 0; i < itemCount; ++i)
                mItemIndexMap.Add(mItems[i].data, i);
        }
        bool IsValidItemIndex(int itemIndex)
        {
            return itemIndex >= 0 &&
                   itemIndex < mItems.Count;
        }
        bool IsValidParentIndex(int parentIndex)
        {
            return parentIndex == -1 ||
                   IsValidItemIndex(parentIndex);
        }
        int FindSubtreeEndIndex(int itemIndex)
        {
            int itemDepth = mItems[itemIndex].depth;
            int itemCount = mItems.Count;
            for (int i = itemIndex + 1; i < itemCount; ++i)
            {
                if (mItems[i].depth <= itemDepth)
                    return i;
            }
            return itemCount;
        }
        int FindChildInsertIndex(int parentIndex, int childRow)
        {
            int targetChildRow = UnityEngine.Mathf.Max(0, childRow);
            int childIndex      = 0;
            int parentDepth     = mItems[parentIndex].depth;
            int childDepth      = parentDepth + 1;
            int subtreeEndIndex = FindSubtreeEndIndex(parentIndex);
            for (int i = parentIndex + 1; i < subtreeEndIndex; ++i)
            {
                int depth = mItems[i].depth;
                if (depth == childDepth)
                {
                    if (childIndex == targetChildRow)
                        return i;
                    ++childIndex;
                }
            }
            return subtreeEndIndex;
        }
        void ShiftParentIndicesForInsert(int insertIndex, int insertCount)
        {
            int itemCount = mItems.Count;
            for (int i = 0; i < itemCount; ++i)
            {
                if (mItems[i].parentIndex >= insertIndex)
                    mItems[i].Internal_SetParentIndex(mItems[i].parentIndex + insertCount);
            }
        }
        void ShiftParentIndicesForRemove(int removeIndex, int removeEnd, int removeCount)
        {
            int itemCount = mItems.Count;
            for (int i = 0; i < itemCount; ++i)
            {
                int parentIndex = mItems[i].parentIndex;
                if (parentIndex >= removeEnd)
                    mItems[i].Internal_SetParentIndex(mItems[i].parentIndex - removeCount);
                else
                if (parentIndex >= removeIndex)
                    mItems[i].Internal_SetParentIndex(-1);
            }
        }
        void RebuildSubtreeEndIndices()
        {
            mOpenItemIndices.Clear();
            int itemCount = mItems.Count;
            for (int itemIndex = 0; itemIndex < itemCount; ++itemIndex)
            {
                int depth = mItems[itemIndex].depth;
                while (mOpenItemIndices.Count > depth)
                {
                    int openItemIndex = mOpenItemIndices[mOpenItemIndices.Count - 1];
                    mItems[openItemIndex].Internal_SetSubtreeEndIndex(itemIndex);
                    mOpenItemIndices.RemoveAt(mOpenItemIndices.Count - 1);
                }
                mOpenItemIndices.Add(itemIndex);
            }
            while (mOpenItemIndices.Count != 0)
            {
                int openItemIndex = mOpenItemIndices[mOpenItemIndices.Count - 1];
                mItems[openItemIndex].Internal_SetSubtreeEndIndex(itemCount);
                mOpenItemIndices.RemoveAt(mOpenItemIndices.Count - 1);
            }
        }
        void RebuildRootItemIndices()
        {
            mRootItemIndices.Clear();
            int itemCount = mItems.Count;
            for (int i = 0; i < itemCount; ++i)
            {
                if (mItems[i].parentIndex < 0)
                    mRootItemIndices.Add(i);
            }
        }
        void RebuildVisibleItemIndices()
        {
            if (mFilterActive)
                RebuildFilteredVisibleItemIndices();
            else
                RebuildNormalVisibleItemIndices();
        }
        void RebuildNormalVisibleItemIndices()
        {
            int itemCount = mItems.Count;
            for (int i = 0; i < itemCount; ++i)
                mItems[i].Internal_SetVisibleRow(-1);
            mVisibleItemIndices.Clear();
            for (int itemIndex = 0; itemIndex < itemCount; ++itemIndex)
            {
                IMGUITreeViewItem<T> item = mItems[itemIndex];
                item.Internal_SetVisibleRow(mVisibleItemIndices.Count);
                mVisibleItemIndices.Add(itemIndex);
                if (!item.expanded &&
                    item.subtreeEndIndex > itemIndex + 1)
                {
                    itemIndex = item.subtreeEndIndex - 1;
                }
            }
        }
        void RebuildFilteredVisibleItemIndices()
        {
            int itemCount = mItems.Count;
            for (int i = 0; i < itemCount; ++i)
                mItems[i].Internal_SetVisibleRow(-1);
            mVisibleItemIndices.Clear();
            for (int itemIndex = 0; itemIndex < itemCount; ++itemIndex)
            {
                IMGUITreeViewItem<T> item = mItems[itemIndex];
                if (!mFilter(item.data))
                    continue;
                item.Internal_SetVisibleRow(mVisibleItemIndices.Count);
                mVisibleItemIndices.Add(itemIndex);
            }
        }
    }
}

#endif
