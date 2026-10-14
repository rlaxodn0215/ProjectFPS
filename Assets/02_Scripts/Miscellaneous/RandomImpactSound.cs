using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class RandomImpactSound : MonoBehaviour
{
    [SerializeField] private AudioClip[] clips;

    private AudioSource audioSource;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
    }

    public void PlayHitSound()
    {
        if (clips == null || clips.Length == 0) return;

        AudioClip clip = clips[Random.Range(0, clips.Length)];
        if (clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }
}