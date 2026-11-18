#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace cowsins.Inventory
{
    [System.Serializable]
    [CustomEditor(typeof(InventoryItemPickeable))]
    public class InventoryItemPickeableEditor : Editor
    {
        override public void OnInspectorGUI()
        {
            serializedObject.Update();
            InventoryItemPickeable myScript = target as InventoryItemPickeable;

            Texture2D myTexture = Resources.Load<Texture2D>("CustomEditor/inventoryItemPickeable_CustomEditor") as Texture2D;
            GUILayout.Label(myTexture);

            if(myScript.Item is Weapon_SO || myScript.Item is BulletTypeIdentifier_SO || myScript.Item is AttachmentIdentifier_SO) 
                        EditorGUILayout.HelpBox("Avoid using Weapon_SO, BulletsItem_SO, or AttachmentIdentifier_SO directly. " +
                "Instead, use their corresponding prefabs: Weapon Pickeable, Bullets Pickeable, and Attachment Pickeable or assign an item_SO to resolve this issue.", MessageType.Error);
            DrawDefaultInspector(); 
            EditorGUILayout.Space(10f);
            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif