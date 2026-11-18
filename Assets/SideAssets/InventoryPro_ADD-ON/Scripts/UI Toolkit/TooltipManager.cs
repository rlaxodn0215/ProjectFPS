using UnityEngine;
using UnityEngine.UI;
using TMPro; 
using UnityEngine.InputSystem;

namespace cowsins.Inventory
{
    /// <summary>
    /// Allows to visually indicate the current hovered Item ( If available ) in the Inventory.
    /// </summary>
    public class TooltipManager : MonoBehaviour
    {
        [SerializeField, Title("TOOLTIP MANAGER"), Tooltip("Image that displays the Icon of the current Dragged Item.")] private Image dragImage;

        [SerializeField, Tooltip("Object that contains the Tooltip UI")] private GameObject tooltip;

        [SerializeField, Tooltip("Text that displays the Input Keybind to rotate text ( only visible if using Tetris Inventory )")] private GameObject rotateText;

        [SerializeField, Tooltip("Text that displays the current hovered Item Name")] private TextMeshProUGUI tooltipText;

        [SerializeField, Tooltip("Text that displays the current hovered Item Description")] private TextMeshProUGUI tooltipDescription;

        [SerializeField, Tooltip("Offset Position added to the tooltip to avoid displaying it exactly on the mouse, for better readability")] private Vector2 tooltipOffset = new Vector2(50,0);

        [SerializeField, Tooltip("Horizontal space added to the text at the end of the line")] private float rightPadding = 30;

        [SerializeField, Tooltip("Default wrapping width for item descriptions")] private float defaultDescriptionWrapWidth = 200f;

        [SerializeField, Tooltip("Minimum width for the entire tooltip")] private float minTooltipWidth = 140f;

        [SerializeField, Tooltip("Maximum width for the entire tooltip")] private float maxTooltipWidth = 360f;

        [SerializeField, Tooltip("Vertical spacing between item name and description")] private float verticalSpacing = 4f;

        [SerializeField, Tooltip("Bottom padding added below the description or title")] private float bottomPadding = 10f;

        private RectTransform rectTransform;

        private void Start()
        {
            rectTransform = GetComponent<RectTransform>();

            var canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
            canvasGroup.blocksRaycasts = false;

            if (dragImage != null) dragImage.raycastTarget = false;

            SetActive(false);
            Disable();
        }

        private void Update()
        {
            // If mouse is not available or it´s not moving, dont update its position
            if (Mouse.current?.delta.magnitude < .1f) return;

            // Update Tooltip position to the current mouse position
            Vector2 mousePosition = Mouse.current.position.ReadValue();
            rectTransform.position = mousePosition;
        }

        /// <summary>
        /// Visually enables or disables Tooltip and activates or deactivates it 
        /// </summary>
        public void SetActive(bool value) => tooltip.gameObject.SetActive(value);

        /// <summary>
        /// Update Drag Image State ( Whether it is visible or not ) and the visuals to display
        /// </summary>
        public void SetDragImageVisibility(bool visible, Sprite sprite, bool showRotateText)
        {
            dragImage.gameObject.SetActive(visible);
            if (dragImage != null) dragImage.raycastTarget = false;
            dragImage.sprite = visible && sprite ? sprite : null;
            rotateText.SetActive(showRotateText);
            if (visible)
            {
                if (Mouse.current != null && rectTransform != null)
                    rectTransform.position = Mouse.current.position.ReadValue();
                SetTooltipVisibility(false, null, null);
            }
        }
        /// <summary>
        /// Update Tooltip State ( Whether it is visible or not ) and the Item Parameters to represent ( Name & Description )
        /// </summary>
        public void SetTooltipVisibility(bool visible, string name, string description)
        {
            // If dragging, Tooltip cannot be shown
            if (dragImage != null && dragImage.gameObject.activeInHierarchy)
            {
                tooltip.SetActive(false);
                return;
            }

            if (!visible || (string.IsNullOrEmpty(name) && string.IsNullOrEmpty(description)))
            {
                tooltip.SetActive(false);
                return;
            }

            // Update Texts
            tooltipText.text = name ?? string.Empty;
            SetWordWrapping(tooltipText, false);
            tooltipText.ForceMeshUpdate();

            bool hasDescription = !string.IsNullOrWhiteSpace(description);
            tooltipDescription.gameObject.SetActive(hasDescription);

            const float textLeftOffset = 34f;
            const float topMargin = 5f;

            float nameWidth = tooltipText.preferredWidth;
            float nameHeight = Mathf.Max(20f, tooltipText.preferredHeight);

            float contentWidth;
            float totalHeight;

            if (hasDescription)
            {
                tooltipDescription.text = description;
                SetWordWrapping(tooltipDescription, true);

                // Base wrapping width for description
                float wrapTarget = defaultDescriptionWrapWidth > 0 ? defaultDescriptionWrapWidth : 200f;
                float maxAllowedDescWidth = Mathf.Max(wrapTarget, maxTooltipWidth - textLeftOffset - rightPadding);
                float desiredWrapWidth = Mathf.Clamp(Mathf.Max(wrapTarget, nameWidth), wrapTarget, maxAllowedDescWidth);

                // Check unwrapped single-line width
                Vector2 unwrappedDescValues = tooltipDescription.GetPreferredValues(description, Mathf.Infinity, Mathf.Infinity);
                float finalDescWidth = unwrappedDescValues.x <= desiredWrapWidth ? unwrappedDescValues.x : desiredWrapWidth;

                tooltipDescription.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, finalDescWidth);

                // Calculate wrapped height based on the final description width
                Vector2 wrappedDescValues = tooltipDescription.GetPreferredValues(description, finalDescWidth, Mathf.Infinity);
                float descHeight = Mathf.Max(16f, wrappedDescValues.y);

                tooltipDescription.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, descHeight);
                tooltipDescription.ForceMeshUpdate();

                // Position description below title
                float spacing = verticalSpacing > 0 ? verticalSpacing : 4f;
                float descY = -(topMargin + nameHeight + spacing);
                tooltipDescription.rectTransform.anchoredPosition = new Vector2(textLeftOffset, descY);

                contentWidth = Mathf.Max(nameWidth, finalDescWidth);
                float bPadding = bottomPadding > 0 ? bottomPadding : 10f;
                totalHeight = topMargin + nameHeight + spacing + descHeight + bPadding;
            }
            else
            {
                contentWidth = nameWidth;
                totalHeight = topMargin + nameHeight + 6f;
            }

            float minWidth = minTooltipWidth > 0 ? minTooltipWidth : 140f;
            float totalWidth = Mathf.Max(minWidth, textLeftOffset + contentWidth + rightPadding);
            if (maxTooltipWidth > minWidth)
                totalWidth = Mathf.Min(maxTooltipWidth, totalWidth);

            RectTransform tooltipRect = tooltip.GetComponent<RectTransform>();
            tooltipRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, totalWidth);
            tooltipRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, totalHeight);

            tooltip.SetActive(true);
        }

        /// <summary>
        /// Disables the Tooltip so it cannot be shown nor used.
        /// </summary>
        public void Disable()
        {
            SetTooltipVisibility(false, "", "");
            SetDragImageVisibility(false, null, false);
            SetActive(false);
        }

        /// <summary>
        /// Rotates Drag Image. Used by Tetris Inventory only. Items in Grid Inventory cannot be rotated.
        /// </summary>
        /// <param name="rotation"></param>
        public void Rotate(int rotation) => dragImage.transform.rotation = Quaternion.Euler(0, 0, -rotation);

        /// <summary>
        /// Updates Tooltip Position in the UI
        /// </summary>
        /// <param name="pos"></param>
        public void SetPosition(Vector2 pos) => rectTransform.position = (Vector3)pos + new Vector3(tooltipOffset.x, tooltipOffset.y, 0);

        private static void SetWordWrapping(TMP_Text textComponent, bool enable)
        {
            if (textComponent == null) return;
#if UNITY_6000_0_OR_NEWER
            textComponent.textWrappingMode = enable ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
#else
#pragma warning disable CS0618
            textComponent.enableWordWrapping = enable;
#pragma warning restore CS0618
#endif
        }
    }
}