using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR

using UnityEditor;

#endif

namespace NGS.SLO.MeshesCombine
{
    [DisallowMultipleComponent]
    public class CombinedObject : ToggleableObject
    {
        public void Destroy(string dataFolder = "")
        {
            if (gameObject)
            {
                if (!string.IsNullOrEmpty(dataFolder) && !Application.isPlaying)
                    DestroyMeshFile(dataFolder);

                DestroyImmediate(gameObject);
            }
        }

        private void DestroyMeshFile(string dataFolder)
        {
#if UNITY_EDITOR

            Mesh mesh = TryGetMesh();

            if (mesh == null)
                return;

            string assetPath = AssetDatabase.GetAssetPath(mesh);

            if (assetPath.Contains(dataFolder))
                AssetDatabase.DeleteAsset(assetPath);

#endif
        }

        private Mesh TryGetMesh()
        {
            MeshFilter meshFilter = GetComponent<MeshFilter>();

            if (meshFilter != null)
                return meshFilter.sharedMesh;

            SkinnedMeshRenderer skinnedMesh = GetComponent<SkinnedMeshRenderer>();

            if (skinnedMesh != null)
                return skinnedMesh.sharedMesh;

            return null;
        }
    }
}
