using UnityEngine;

public class DestructibleOakTree : MonoBehaviour
{
    [SerializeField] private GameObject normalTree;
    //[SerializeField] private GameObject replacementTree;

[SerializeField] private ParticleSystem starEffect;
[SerializeField] private ParticleSystem dustInitialEffect;
[SerializeField] private ParticleSystem dustEffect;
[SerializeField] private ParticleSystem leavesEffect;
[SerializeField] private ParticleSystem woodChunksEffect;
[SerializeField] private ParticleSystem branches;

    private bool hasSwapped = false;

    private void OnTriggerEnter(Collider other)
    {
        if (hasSwapped)
            return;

        if (!other.CompareTag("Player"))
            return;

        SwapTree();
    }

    private void SwapTree()
    {
        hasSwapped = true;

        normalTree.SetActive(false);
        //replacementTree.SetActive(true);

starEffect.Play();
dustInitialEffect.Play();
dustEffect.Play();
leavesEffect.Play();
woodChunksEffect.Play();
branches.Play();
    }
}