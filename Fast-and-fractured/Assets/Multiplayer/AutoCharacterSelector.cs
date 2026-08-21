using FishNet.Object;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace FastAndFractured.Multiplayer
{
    /// <summary>
    /// Deterministic auto-selection for distributed multiplayer tests.
    /// Runs only on the owner and retries until dependencies are available.
    /// </summary>
    public class AutoCharacterSelector : NetworkBehaviour
    {
        [Header("Auto-Selection Settings")]
        [SerializeField] private bool autoSelectEnabled = true;
        [SerializeField] private float selectionStartDelay = 0.2f;
        [SerializeField] private float retryInterval = 0.3f;
        [SerializeField] private int maxRetries = 20;

        [Tooltip("Character order by ClientId modulo this list count.")]
        [SerializeField] private List<string> characterPool = new List<string>
        {
            "Pepe",
            "Josefino",
            "MariaAntonia",
            "Carme"
        };

        [Header("References")]
        [SerializeField] private TMP_Dropdown dropdown;
        [SerializeField] private DebugCharacterSelector debugSelector;

        private Coroutine _autoSelectCoroutine;
        private bool _selectionSent;

        public override void OnStartClient()
        {
            base.OnStartClient();

            if (!IsOwner)
            {
                if (dropdown != null)
                    dropdown.interactable = false;
                return;
            }

            if (!autoSelectEnabled)
                return;

            if (_autoSelectCoroutine != null)
                StopCoroutine(_autoSelectCoroutine);

            _autoSelectCoroutine = StartCoroutine(AutoSelectWhenReady());
        }

        public override void OnStopClient()
        {
            base.OnStopClient();

            if (_autoSelectCoroutine != null)
            {
                StopCoroutine(_autoSelectCoroutine);
                _autoSelectCoroutine = null;
            }

            _selectionSent = false;
        }

        private IEnumerator AutoSelectWhenReady()
        {
            if (selectionStartDelay > 0f)
                yield return new WaitForSeconds(selectionStartDelay);

            int attempts = Mathf.Max(1, maxRetries);
            for (int attempt = 1; attempt <= attempts; attempt++)
            {
                if (_selectionSent)
                    yield break;

                if (TryAutoSelect())
                    yield break;

                yield return new WaitForSeconds(Mathf.Max(0.05f, retryInterval));
            }

            Debug.LogWarning("[AutoCharacterSelector] Failed to auto-select after all retries.");
        }

        private bool TryAutoSelect()
        {
            if (!IsOwner || !IsClientInitialized || LocalConnection == null || !LocalConnection.IsValid)
                return false;

            if (characterPool == null || characterPool.Count == 0)
            {
                Debug.LogWarning("[AutoCharacterSelector] Character pool is empty.");
                return false;
            }

            EnsureReferences();
            if (debugSelector == null)
            {
                Debug.LogWarning("[AutoCharacterSelector] DebugCharacterSelector not found yet.");
                return false;
            }

            int clientId = LocalConnection.ClientId;
            int characterIndex = Mathf.Abs(clientId) % characterPool.Count;
            string selectedCharacter = characterPool[characterIndex];

            int dropdownIndex = ResolveDropdownIndex(selectedCharacter, characterIndex);
            bool submitted;
            if (dropdownIndex >= 0)
            {
                if (dropdown != null && dropdownIndex < dropdown.options.Count)
                {
                    dropdown.SetValueWithoutNotify(dropdownIndex);
                }

                submitted = debugSelector.SelectCharacterFromAuto(dropdownIndex);
            }
            else
            {
                submitted = debugSelector.SelectCharacterByNameFromAuto(selectedCharacter);
            }

            if (!submitted)
            {
                Debug.LogWarning($"[AutoCharacterSelector] Could not submit character '{selectedCharacter}'.");
                return false;
            }

            _selectionSent = true;
            Debug.Log($"<color=cyan>[AutoCharacterSelector] ClientId={clientId} auto-selected '{selectedCharacter}' (dropdownIndex={dropdownIndex}).</color>");
            return true;
        }

        private int ResolveDropdownIndex(string selectedCharacter, int fallbackIndex)
        {
            if (dropdown == null || dropdown.options == null || dropdown.options.Count == 0)
                return -1;

            for (int i = 0; i < dropdown.options.Count; i++)
            {
                string optionText = dropdown.options[i].text;
                if (optionText == selectedCharacter ||
                    optionText == selectedCharacter + "_0" ||
                    optionText + "_0" == selectedCharacter)
                {
                    return i;
                }
            }

            if (fallbackIndex >= 0 && fallbackIndex < dropdown.options.Count)
                return fallbackIndex;

            return -1;
        }

        private void EnsureReferences()
        {
            if (dropdown == null)
                dropdown = GetComponent<TMP_Dropdown>();

            if (debugSelector == null)
                debugSelector = GetComponent<DebugCharacterSelector>();
        }

        private void OnValidate()
        {
            EnsureReferences();
        }
    }
}
