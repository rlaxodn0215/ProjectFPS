using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
#if INVENTORY_PRO_ADD_ON
using cowsins.Inventory;
#endif
namespace cowsins
{
    // Disallow creating Items if the Inventory Pro Add-On is not available
    [System.Serializable]
#if INVENTORY_PRO_ADD_ON
[CreateAssetMenu(fileName = "newInventoryItem", menuName = "COWSINS/Inventory/New Inventory Item", order = 1)]
#endif
    public class Item_SO : ScriptableObject
    {
        [Tooltip("Your item name. Ex: Glock")] public string _name;
        [Tooltip("You can describe your item here. Inventory Add-On will show this description on hovering slots in the inventory.")] public string description;
        [Tooltip("Custom image of your item")] public Sprite icon;
        [Tooltip("Visuals that will appear on a dropped item.")] public GameObject pickUpGraphics;
        [Range(1, 99)] public int maxStack = 1;
        public Vector2Int itemSize = new Vector2Int(1, 1);
        public Sprite irregularItemIcon;


        [Tooltip("Velocity will be decreased depending on the weight of the weapon if this is true.")] public bool applyWeight;
        [Range(.2f, 1)]
        [Tooltip("Represents the weight of an entire stack of this item.")] public float weightMultiplier = 1f;

#if INVENTORY_PRO_ADD_ON
    [SerializeField] private bool useDurability;
    [SerializeField, Min(1f)] private float maxDurability = 100f;
    [SerializeField] private DurabilityReductionMethod durabilityMethod = DurabilityReductionMethod.Usage;

    public bool UseDurability => useDurability;
    public float MaxDurability => maxDurability;
    public DurabilityReductionMethod DurabilityMethod => durabilityMethod;

    public virtual bool CanUse(InventorySlot slot, InventoryProManager manager)
    {
        var useMethod = GetType().GetMethod(nameof(Use), new[] { typeof(InventoryProManager), typeof(InventorySlot) });
        return useMethod != null && useMethod.DeclaringType != typeof(Item_SO)
            && useMethod.GetBaseDefinition().DeclaringType == typeof(Item_SO);
    }

    public virtual string GetContextActionName(InventorySlot slot, InventoryProManager manager) => "USE";
    public virtual bool ShowUseToast => true;
    public virtual string GetUseToastMessage() => ToastManager.Instance != null
        ? $"{_name} {ToastManager.Instance.ItemUsed}" : null;
    public virtual SlotData[] CreateDefaultContainedItems() => null;

    public virtual void OnEquipCustomSlot(InventorySlot slot, PlayerDependencies player)
    {
        ApplyCustomSlotEffects(slot, player);
    }

    public virtual void OnUnequipCustomSlot(InventorySlot slot, PlayerDependencies player)
    {
        RemoveCustomSlotEffects(slot, player);
    }

    public virtual void ApplyCustomSlotEffects(InventorySlot slot, PlayerDependencies player) { }
    public virtual void RemoveCustomSlotEffects(InventorySlot slot, PlayerDependencies player) { }
    public virtual void OnDurabilityReduced(InventorySlot slot, float previousDurability, float currentDurability) { }
    // InventorySlot.BreakItem handles removal, unequipping and feedback after this callback.
    public virtual void OnDurabilityDepleted(InventorySlot slot, PlayerDependencies player) { }

    public virtual void Use(InventoryProManager inventoryProManager, InventorySlot inventorySlot)
    {
        InventorySlot anchor = inventorySlot.GetAnchorSlot();
        anchor.slotData.amount -= 1;
        if(anchor.slotData.amount <= 0)
        {
            inventoryProManager._GridGenerator.Operations.ClearSlotArea(inventorySlot);
            return;
        }
        Vector2Int size = inventorySlot.GetAnchorSlot().GetItemSize();
        int startRow = inventorySlot.row;
        int startCol = inventorySlot.col;

        for (int i = 0; i < size.y; i++)
        {
            for (int j = 0; j < size.x; j++)
            {
                InventorySlot[,] inventorySlots = inventorySlot.IsInventorySlot ? inventoryProManager.InventorySlots : inventoryProManager.ChestSlots;

                inventorySlots[startRow + i, startCol + j].anchorSlot = anchor;
                inventorySlots[startRow + i, startCol + j].UpdateSlotGraphics();
            }
        }
    }
#endif
    }
}

#if UNITY_EDITOR

namespace cowsins
{
    [CustomEditor(typeof(Item_SO), true)]
    public class Item_SOEditor : Editor
    {
        private bool isInventoryAddonAvailable;
        private void OnEnable()
        {
            // Check if the Inventory Addon is available
            #if INVENTORY_PRO_ADD_ON
            isInventoryAddonAvailable = true;
            #else
            isInventoryAddonAvailable = false;
            #endif
        }

        override public void OnInspectorGUI()
        {
            serializedObject.Update();
            var myScript = target as Item_SO;

            EditorGUILayout.PropertyField(serializedObject.FindProperty("_name"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("icon"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("pickUpGraphics"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("applyWeight"));
            if (myScript.applyWeight)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(serializedObject.FindProperty("weightMultiplier"));
                EditorGUI.indentLevel--;
            }
            else myScript.weightMultiplier = 1;

            GUI.enabled = isInventoryAddonAvailable;

            EditorGUILayout.Space(15);
            EditorGUILayout.LabelField("INVENTORY PRO MANAGER", EditorStyles.boldLabel);
            if (!isInventoryAddonAvailable)
            {
                EditorGUILayout.Space(10);
                EditorGUILayout.HelpBox("These features are locked. Inventory Pro Manager + Save & Load Add-On is not installed.", MessageType.Info);
                EditorGUILayout.Space(5);
            }
            EditorGUILayout.PropertyField(serializedObject.FindProperty("maxStack"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("itemSize"));
            if (myScript.itemSize.x != myScript.itemSize.y)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.Space(5);
                EditorGUILayout.HelpBox($"Your item has an irregular size [{myScript.itemSize.x} : {myScript.itemSize.y}], which may cause icon stretching." +
                "To maintain the correct aspect ratio, consider setting 'irregularItemIcon' with a properly scaled icon.", MessageType.Info);
                EditorGUILayout.Space(5);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("irregularItemIcon"));
                EditorGUI.indentLevel--;
            }
            GUI.enabled = true;
            EditorGUILayout.Space(15);
            EditorGUILayout.LabelField("SPECIFIC SETTINGS", EditorStyles.boldLabel);
            DrawPropertiesExcluding(serializedObject, "m_Script", "id", "icon", "_name", "pickUpGraphics", "applyWeight", "weightMultiplier", "maxStack", "itemSize", "irregularItemIcon");

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif
