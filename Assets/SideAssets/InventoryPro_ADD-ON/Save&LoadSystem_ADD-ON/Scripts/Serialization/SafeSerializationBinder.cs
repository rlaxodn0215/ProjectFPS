using System;
using Newtonsoft.Json.Serialization;
using UnityEngine;

namespace cowsins.SaveLoad
{
    /// <summary>
    /// Avoids unrecognized types to be deserialized.
    /// </summary>
    public class SafeSerializationBinder : DefaultSerializationBinder
    {
        public override Type BindToType(string assemblyName, string typeName)
        {
            try
            {
                // Try to resolve the type
                return base.BindToType(assemblyName, typeName);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"<color=red>[COWSINS]</color> A type couldn´t be recognized during deserialization: {typeName}.<color=red> Error: {e.Message}</color>");
                // Return null for Types that cannot be recognized during deserialization
                return null;
            }
        }
    }
}