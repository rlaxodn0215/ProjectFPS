#if UNITY_EDITOR

using UnityEditor;

namespace cowsins.SaveLoad
{
    /// <summary>
    /// When Save & Load Add-On is installed, this script adds the SAVE_LOAD_ADD_ON Symbol to your project, as it is required to work seamlessly.
    /// </summary>
    [InitializeOnLoad]
    public static class AddonDefineSetterSaveLoad
    {
        private const string DefineSymbol = "SAVE_LOAD_ADD_ON";

        static AddonDefineSetterSaveLoad()
        {
            BuildTargetGroup currentBuildTargetGroup = EditorUserBuildSettings.selectedBuildTargetGroup;

            if (currentBuildTargetGroup == BuildTargetGroup.Unknown)
                return;

#pragma warning disable CS0618
            var defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(currentBuildTargetGroup);

            if (!defines.Contains(DefineSymbol))
            {
                PlayerSettings.SetScriptingDefineSymbolsForGroup(currentBuildTargetGroup, defines + (string.IsNullOrEmpty(defines) ? "" : ";") + DefineSymbol);
            }
#pragma warning restore CS0618
        }
    }
}
#endif
