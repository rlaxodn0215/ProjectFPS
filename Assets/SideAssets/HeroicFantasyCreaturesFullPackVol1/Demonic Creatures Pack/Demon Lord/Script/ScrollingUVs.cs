using UnityEngine;
using System.Collections.Generic;

namespace PROTOFACTOR
{
    public class UVScroller : MonoBehaviour
    {
        [Header("PROTOFACTOR UV Scroller")]
        public int materialIndex = 0;
        public Vector2 scrollSpeed = new Vector2(1f, 1f);

        private Vector2 offset = Vector2.zero;
        private Renderer rend;
        private MaterialPropertyBlock propBlock;
        private List<string> textureProps = new List<string>();

        void Start()
        {
            rend = GetComponent<Renderer>();
            propBlock = new MaterialPropertyBlock();

            if (materialIndex >= rend.sharedMaterials.Length)
            {
                Debug.LogError($"Invalid materialIndex {materialIndex}", this);
                enabled = false;
                return;
            }

            // Discover textures once
            Material mat = rend.sharedMaterials[materialIndex];
            foreach (string p in mat.GetTexturePropertyNames())
            {
                if (mat.GetTexture(p) != null)
                    textureProps.Add(p);
            }

            Debug.Log($"[PROTOFACTOR] UVScroller on slot {materialIndex} - Found textures: {string.Join(", ", textureProps)} | Shader: {mat.shader.name}");
        }

        void LateUpdate()
        {
            offset += scrollSpeed * Time.deltaTime;

            rend.GetPropertyBlock(propBlock, materialIndex);

            foreach (string prop in textureProps)
            {
                propBlock.SetVector(prop + "_ST", new Vector4(1f, 1f, offset.x, offset.y));
            }

            rend.SetPropertyBlock(propBlock, materialIndex);
        }
    }
}