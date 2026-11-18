using System;
using System.Collections.Generic;
using System.Linq;

namespace cowsins.SaveLoad
{
    /// <summary>
    /// When loading instantiated objects, we only have information about the type and saveFields of such object.
    /// With the given information, we cannot instantiate a prefab that corresponds to the type.
    /// Because of this, we can stablish a relation between Types and Prefabs in TypeRegistry, so we can read the Type and Instantiate the Prefab associated to it.
    /// After that, remaining data is loaded
    /// </summary>
    public static class TypeRegistry
    {
        private static List<string> cachedTypes;
        public static List<string> GetAvailableTypes()
        {
            if (cachedTypes == null || cachedTypes.Count == 0)
            {
                var baseType = typeof(Identifiable);
                cachedTypes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(assembly => assembly.GetTypes())
                .Where(type => baseType.IsAssignableFrom(type) && !type.IsAbstract)
                .Select(type => type.Name.Split(',')[0])
                .ToList();
            }
            return cachedTypes;
        }
    }
}