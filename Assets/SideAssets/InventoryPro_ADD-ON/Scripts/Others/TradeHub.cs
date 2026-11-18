using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace cowsins.Inventory
{
    /// <summary>
    /// A trade hub is an interactive object that allows you to acquire items by exchanging coins (Shops) or other items (Crafting Bench).
    /// </summary>
    public abstract class TradeHub : Interactable
    {
        [System.Serializable]
        public class InteractionEvents
        {
            public UnityEvent onDeclinedTrade, onSuccessfulTrade, onIncreaseAmount, onDecreaseAmount;
        }

        [SerializeField, Title("TRADE HUB", upMargin = 10)] private InteractionEvents events;
        [SerializeField] private AudioClip hoverSFX, successfulTradeSFX, forbiddenTradeSFX;

        // GETTERS 
        protected PlayerControl player;
        protected InputManager inputManager;

        protected bool isHubOpened;

        public ProgressionManager progressionManager { get; private set; }

        public InteractionEvents Events => events;

        public AudioClip HoverSFX => hoverSFX;

        public AudioClip SuccessfulTradeSFX => successfulTradeSFX;

        public AudioClip ForbiddenTradeSFX => forbiddenTradeSFX;

        public bool IsHubOpened => isHubOpened;


        private PlayerDependencies playerDependencies;

        public override void Interact(Transform player)
        {
            this.playerDependencies = player.GetComponent<PlayerDependencies>();
            this.inputManager = playerDependencies.InputManager;
            // Player needs to lose control when interacting with Trade Hub to avoid unvoluntary movement or other actions.
            this.player = player.GetComponent<PlayerControl>();
            this.progressionManager = player.GetComponent<ProgressionManager>();
            this.player.LoseControl();
            // Unlock mouse so we can interact with the Trade Hub
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            interactableEvents.OnInteract?.Invoke();
            playerDependencies.Crosshair?.SetVisibility(false);

            // Disables Player Inputs so we can only interact with UI
            // Avoids any possible Inputs Errors
            if (inputManager != null)
            {
                InputManager.ToggleGameControls(false);
                InputManager.ToggleUIControls(true);
            }

            // Base method to open the hub
            OpenHub();
        }

        protected virtual void Update()
        {
            if (!isHubOpened) return;

            // CLOSE HUB LOGIC
            if (inputManager && inputManager.BackUI) CloseHub();

            ShopButton shopButton = EventSystem.current?.currentSelectedGameObject != null ? EventSystem.current?.currentSelectedGameObject?.GetComponentInParent<ShopButton>() : null;

            if (shopButton != null)
            {
                if (inputManager && inputManager.WestButtonUI) shopButton.ReduceAmount(1);
                if (inputManager && inputManager.NorthButtonUI) shopButton.AddAmount(1);
            }
        }

        public virtual void OpenHub()
        {
            isHubOpened = true;
        }

        public virtual void CloseHub()
        {
            isHubOpened = false;
            // Give Control back to the player on closing the trade hub
            player?.CheckIfCanGrantControl();
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;

            playerDependencies?.Crosshair?.SetVisibility(true);

            if(inputManager == null) return;

            inputManager.Shooting = false;
            // Enable Player Controls again and disable UI Controls.
            InputManager.ToggleGameControls(true);
            InputManager.ToggleUIControls(false);
        }
    }
}
