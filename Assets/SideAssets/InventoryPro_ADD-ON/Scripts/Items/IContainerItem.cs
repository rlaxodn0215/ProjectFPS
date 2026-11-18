using UnityEngine;
using cowsins.Inventory;

namespace cowsins
{
    public interface IContainerItem
    {
        Vector2Int ContainerSize { get; }
        int Rows { get; }
        int Columns { get; }
        float WeightReductionPercentage { get; }
        SlotData[] DefaultContents { get; }
        bool CanStoreItem(Item_SO candidateItem);
    }
}
