using System.Runtime.CompilerServices;
using UnityEngine;

namespace NGS.SLO.Shared
{
    public static class UnityAPI
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T[] FindObjectsOfType<T>() where T : Object
        {
#if UNITY_6000_4_OR_NEWER

            return Object.FindObjectsByType<T>();
            
#elif UNITY_2022_2_OR_NEWER

            return Object.FindObjectsByType<T>(FindObjectsSortMode.None);
            
#else

            return Object.FindObjectsOfType<T>();
            
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T FindObjectOfType<T>() where T : Object
        {
#if UNITY_2022_2_OR_NEWER

            return Object.FindAnyObjectByType<T>();

#else

            return Object.FindObjectOfType<T>();

#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string GetObjectIdentifier(UnityEngine.Object obj)
        {
#if UNITY_6000_4_OR_NEWER

            return EntityId.ToULong(obj.GetEntityId()).ToString();

#else

            return obj.GetInstanceID().ToString();

#endif
        }
    }
}
