using UnityEngine;

public class DestructibleTree : MonoBehaviour
{
    private const string CHARACTER_TAG = "Character";

    [SerializeField] private GameObject normalTree;
    [SerializeField] private ParticleSystem starEffect;
    [SerializeField] private ParticleSystem dustInitialEffect;
    [SerializeField] private ParticleSystem dustEffect;
    [SerializeField] private ParticleSystem leavesEffect;
    [SerializeField] private ParticleSystem woodChunksEffect;
    [SerializeField] private Collider collider;

    private bool _hasSwapped;

    private void Start()
    {
        if (collider == null)
        {
            collider = GetComponent<Collider>();
        }
    }
    private void OnTriggerEnter(Collider other)
    {
        if (_hasSwapped)
            return;

        if (!other.gameObject.CompareTag(CHARACTER_TAG))
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
        collider.enabled = false;
    }

    private static void PlayEffect(ParticleSystem effect)
    {
        if (effect != null)
            effect.Play();
    }
}
