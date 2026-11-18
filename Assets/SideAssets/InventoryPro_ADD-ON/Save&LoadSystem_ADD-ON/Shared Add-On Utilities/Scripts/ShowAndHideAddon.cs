using UnityEngine;
using UnityEngine.InputSystem;

namespace cowsins
{
    public class ShowAndHideAddon : MonoBehaviour
    {
        [SerializeField] private GameObject panel;

        private void Update()
        {
            if (Keyboard.current.yKey.wasPressedThisFrame) panel.SetActive(!panel.activeSelf);
        }
    }
}