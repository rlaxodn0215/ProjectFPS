using UnityEngine;
using UnityEngine.InputSystem;

namespace cowsins.Inventory
{
    /// <summary>
    /// This Interactable allows you inspect an item up close, rotate it and decide to collect it or leave it where it was.
    /// </summary>
    public class ExamineItem : Interactable
    {
        [SerializeField, Tooltip("Rotation Sensitivity. The bigger this value is, the more it will rotate based on your mouse movement.")] private float rotationSpeed = 100f;
        [SerializeField, Tooltip("Transform modifications smoothness. The bigger this value, the faster")] private float lerpSpeed = 10f;
        [SerializeField, Tooltip("Distance from the camera to locate the examinated object while examinating.")] private float examinationDistance = 2f;
        [SerializeField, Tooltip("Item to collect.")] private Item_SO item;

        // INTERNAL USE
        private bool isExamining = false;
        private bool isLerping = false;
        private Transform originalParent;
        private Vector3 originalPosition;
        private Quaternion originalRotation;
        private Camera mainCamera;

        private Vector3 targetPosition;
        private Quaternion targetRotation;

        private InputManager inputManager;
        private PlayerControl playerControl;
        private WeaponAnimator weaponAnimator;
        private Rigidbody rb;
        private Collider[] colliders;
        
        private PlayerDependencies playerDependencies;

        private void Start()
        {
            // Gather References
            mainCamera = Camera.main;
            rb = GetComponent<Rigidbody>();
            colliders = GetComponentsInChildren<Collider>();
        }

        public override void Interact(Transform player)
        {
            interactableEvents.OnInteract?.Invoke();
            playerDependencies = player.GetComponent<PlayerDependencies>();
            playerControl = player.GetComponent<PlayerControl>();
            weaponAnimator = player.GetComponent<WeaponAnimator>();
            inputManager = playerDependencies.InputManager;
            playerControl.LoseControl();

            StartExamination();
        }

        private void Update()
        {
            if (isExamining)
            {
                if (!isLerping)
                {
                    // If examining the item, but the object is already at the desired position, handle rotation
                    Vector2 mouseDelta = Mouse.current.delta.ReadValue();
                    float rotationX = mouseDelta.x * rotationSpeed * Time.deltaTime;
                    float rotationY = mouseDelta.y * rotationSpeed * Time.deltaTime;

                    transform.Rotate(mainCamera.transform.up, -rotationX, Space.World);
                    transform.Rotate(mainCamera.transform.right, rotationY, Space.World);
                }

                // If we re-interact or open the Inventory, we�ll end the examination
                if (inputManager && inputManager.StartInteraction)
                {
                    EndExamination(true);
                }
                else if (inputManager.OpenInventory || inputManager.OpenFavMenu || inputManager.Pausing)
                {
                    EndExamination(false);
                }
                else if (CanCollectItem()) // Handle Collection
                {
                    CollectItem();
                }
            }

            // Handle Transform Lerping to the examination position
            if (isLerping) PerformLerp();
        }

        private bool CanCollectItem()
        {
            return (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame || inputManager && inputManager.SelectUI) && item != null;
        }
        private void CollectItem()
        {
            // Tuple that returns whether all items where added or not (success). If success == false, amount returns the amount of items that couldn�t be added
            var (success, amount) = InventoryProManager.instance.AddItem(item, 1);
            if (success)
            {
                ToastManager.Instance?.ShowToast($"{item._name} {ToastManager.Instance.CollectedMsg}");
                CowsinsUtilities.PlayAnim(Animator.StringToHash("finished"), weaponAnimator.HolsterMotionObject);
                playerDependencies?.Crosshair?.SetVisibility(true);
                InventoryProManager.instance.UIController.SetExaminationUIVisibility(false, null);
                playerControl?.CheckIfCanGrantControl();
                if(inputManager) inputManager.Melee = false;

                interactableEvents.OnInteract?.Invoke();
                alreadyInteracted = true;
#if SAVE_LOAD_ADD_ON
                StoreData();
                LoadedState();
#endif
            }
            else
            {
                ToastManager.Instance?.ShowToast(ToastManager.Instance.InventoryIsFullMsg);
            }
        }

        private void StartExamination()
        {
            // If the item has a rigidbody, make it kinematic so it cannot be affected by gravity
            if (rb) rb.isKinematic = true;

            // Disable any colliders to avoid colliding with the player while examining
            foreach (Collider collider in colliders)
            {
                collider.enabled = false;
            }

            isExamining = true;
            CowsinsUtilities.PlayAnim(Animator.StringToHash("hit"), weaponAnimator.HolsterMotionObject);
            playerDependencies?.Crosshair?.SetVisibility(false);

            InventoryProManager.instance.UIController.SetExaminationUIVisibility(true, item._name);

            originalParent = transform.parent;
            originalPosition = transform.position;
            originalRotation = transform.rotation;

            targetPosition = mainCamera.transform.position + mainCamera.transform.forward * examinationDistance;
            targetRotation = Quaternion.LookRotation(mainCamera.transform.forward, mainCamera.transform.up);

            transform.SetParent(null);

            isLerping = true;
        }

        private void EndExamination(bool allowPlayerControl)
        {
            CowsinsUtilities.PlayAnim(Animator.StringToHash("finished"), weaponAnimator.HolsterMotionObject);
            playerDependencies?.Crosshair?.SetVisibility(true);

            InventoryProManager.instance.UIController.SetExaminationUIVisibility(false, null);

            isExamining = false;
            isLerping = true;

            targetPosition = originalPosition;
            targetRotation = originalRotation;

            transform.SetParent(originalParent);

            Collider[] colliders = GetComponentsInChildren<Collider>();
            foreach (Collider collider in colliders)
            {
                collider.enabled = true;
            }
            if (allowPlayerControl) playerControl?.CheckIfCanGrantControl();
        }
        private void PerformLerp()
        {
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, Time.deltaTime * lerpSpeed);
            transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, Time.deltaTime * lerpSpeed);

            if (Mathf.Approximately(Vector3.Distance(transform.position, targetPosition), 0f) &&
                Mathf.Approximately(Quaternion.Angle(transform.rotation, targetRotation), 0f))
            {
                isLerping = false;
            }
        }

#if SAVE_LOAD_ADD_ON
        // Handle Save & Load
        public override void LoadedState()
        {
            if (alreadyInteracted) Destroy(this.gameObject);
        }
#endif
    }
}