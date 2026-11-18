#if UNITY_EDITOR
using UnityEditor;

namespace cowsins.SaveLoad
{
    [CustomEditor(typeof(DataPersistenceManager))]
    public class DataPersistenceManagerEditor : Editor
    {
        override public void OnInspectorGUI()
        {
            serializedObject.Update();
            var myScript = target as DataPersistenceManager;
            
           EditorGUILayout.HelpBox(
            "Ensure that there is only one instance of the DataPersistenceManager in your game." +
            "This manager should be unique and exist only once throughout the entire game.",
            MessageType.Warning);

              EditorGUILayout.HelpBox(
            "The DataPersistenceManager should be placed in an introductory scene (such as the Main Menu), " +
            "before any game data is loaded, to guarantee proper initialization and data management.",
            MessageType.Warning);

            if(myScript.DataPersistenceSettings == null) 
                EditorGUILayout.HelpBox(
            "Please assign a Data Persistence SO, without it, the Save & Load system won´t work.",
            MessageType.Error);

            EditorGUILayout.Space(10); 

            DrawDefaultInspector();
           
            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif