using UnityEngine;

namespace cowsins.Inventory
{
    public class WorkLight : Interactable
    {
        [SerializeField, Title("Work Light"), Tooltip("If true, the lights will turn on, on starting the game")] private bool initiallyTurnedOn;
        [SerializeField, Tooltip("Reference to all lights of the WorkLight. These well be turned on and off upon interacting")] private GameObject[] lights;
        [SerializeField, Tooltip("SFX To play when interacting with WorkLight")] private AudioClip switchSFX;

        private void Awake() => SwitchLights(initiallyTurnedOn, false);
        public override void Interact(Transform player)
        {
            SwitchLights();
#if SAVE_LOAD_ADD_ON
            StoreData();
#endif
        }

        /// <summary>
        /// Toggles WorkLight state to turn it on or off accordingly.
        /// </summary>
        public void SwitchLights()
        {
            alreadyInteracted = !alreadyInteracted;
            foreach (var light in lights) light.SetActive(alreadyInteracted);
            SoundManager.Instance.PlaySound(switchSFX, 0, 0, false);
            interactText = alreadyInteracted ? "Turn Off" : "Turn On";
        }

        /// <summary>
        /// Turns lights on or off, but forces the state to be applied instead of relying on toggling it.
        /// </summary>
        /// <param name="forceState"></param>
        /// <param name="playAudio"></param>
        public void SwitchLights(bool forceState, bool playAudio)
        {
            alreadyInteracted = forceState;
            foreach (var light in lights) light.SetActive(alreadyInteracted);
            if (playAudio) SoundManager.Instance?.PlaySound(switchSFX, 0, 0, false);
            interactText = alreadyInteracted ? "Turn Off" : "Turn On";
        }

#if SAVE_LOAD_ADD_ON
        public override void LoadedState() => SwitchLights(alreadyInteracted, false);
#endif
    }
}