#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
namespace GSPAWN
{
    public class IMGUIListView<T> : IMGUITreeView<T>
    {
        public IMGUIListView()
        {
            indentWidth = 0f;
            foldoutWidth = 0f;
            rowHeight = 22f;
        }
        public void SetItems(IEnumerable<T> items)
        {
            T selected = default(T);
            bool hadSelection = model.selectedItemCount != 0;
            if (hadSelection) selected = model.selectedItemData[0];
            model.Clear();
            if (items != null)
            {
                foreach (T item in items) model.AddRoot(item);
            }
            if (hadSelection && model.FindItemIndex(selected) >= 0)
                SelectOnlyItem(selected);
            else model.ClearSelection();
            ResetSelectionNavigation();
        }
    }
}
#endif
