using UnityEngine;
using System.Collections;
using UnityEditor;

namespace PixelCrushers.DialogueSystem
{

    [CustomFieldTypeService.Name("Actor")]
    public class CustomFieldType_Actor : CustomFieldType
    {
        public override FieldType storeFieldAsType
        {
            get
            {
                return FieldType.Actor;
            }
        }
        public override string Draw(string currentValue, DialogueDatabase database)
        {
            return DrawAssetPopup<Actor>(currentValue, (database != null) ? database.actors : null);
        }

        public override string Draw(Rect rect, string currentValue, DialogueDatabase database)
        {
            return DrawAssetPopup<Actor>(rect, currentValue, (database != null) ? database.actors : null);
        }

    }
}