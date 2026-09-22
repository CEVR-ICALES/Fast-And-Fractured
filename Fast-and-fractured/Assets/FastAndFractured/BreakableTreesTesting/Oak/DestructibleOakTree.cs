using UnityEngine;

public class DestructibleOakTree : MonoBehaviour
{
    private const string PLAYER_TAG = "Player";

    [SerializeField] private GameObject normalTree;
    [SerializeField] private ParticleSystem starEffect;
    [SerializeField] private ParticleSystem dustInitialEffect;
    [SerializeField] private ParticleSystem dustEffect;
    [SerializeField] private ParticleSystem leavesEffect;
    [SerializeField] private ParticleSystem woodChunksEffect;
    [SerializeField] private ParticleSystem branches;

    private bool _hasSwapped;

    private void OnTriggerEnter(Collider other)
    {
        if (_hasSwapped)
            return;

        if (!other.CompareTag(PLAYER_TAG))
            return;

        SwapTree();
    }

    private void SwapTree()
    {
        _hasSwapped = true;

        if (normalTree != null)
            normalTree.SetActive(false);

        PlayEffect(starEffect);
        PlayEffect(dustInitialEffect);
        PlayEffect(dustEffect);
        PlayEffect(leavesEffect);
        PlayEffect(woodChunksEffect);
        PlayEffect(branches);
    }

    private static void PlayEffect(ParticleSystem effect)
    {
        if (effect != null)
            effect.Play();
    }
}
