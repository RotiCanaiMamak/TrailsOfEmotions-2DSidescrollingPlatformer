using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Can be configured in GameScene, or added automatically by IrisTransition.
[DisallowMultipleComponent]
public sealed class GameplayUIReveal : MonoBehaviour
{
    [Tooltip("UI objects to fade. Leave empty to fade this object's UI hierarchy.")]
    [SerializeField] private GameObject[] uiRoots = new GameObject[0];
    [Min(0f)]
    [SerializeField] private float fadeDuration = 0.75f;

    private readonly List<CanvasGroup> fadeGroups = new List<CanvasGroup>();
    private IrisTransition transition;
    private bool initialized;

    public bool HasCompleted { get; private set; }

    public void Initialize(IrisTransition owner)
    {
        if (initialized)
        {
            return;
        }

        initialized = true;
        transition = owner;
        // Use a separate group to preserve opacity and interaction controlled by other UI scripts.
        if (uiRoots == null || uiRoots.Length == 0)
        {
            HideRoot(gameObject);
        }
        else
        {
            foreach (GameObject root in uiRoots)
            {
                HideRoot(root);
            }
        }
    }

    private void HideRoot(GameObject root)
    {
        if (root == null)
        {
            return;
        }

        foreach (CanvasGroup group in fadeGroups)
        {
            if (group.gameObject == root)
            {
                return;
            }
        }

        CanvasGroup fadeGroup = root.AddComponent<CanvasGroup>();
        fadeGroup.alpha = 0f;
        fadeGroup.interactable = false;
        fadeGroup.blocksRaycasts = false;
        fadeGroups.Add(fadeGroup);
    }

    private IEnumerator Start()
    {
        if (!initialized)
        {
            IrisTransition owner = null;
            foreach (IrisTransition candidate in FindObjectsByType<IrisTransition>(
                FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (candidate.IsRevealingScene(gameObject.scene))
                {
                    owner = candidate;
                    break;
                }
            }

            Initialize(owner);
        }

        // Start the HUD as soon as the old start scene finishes unloading.
        while (transition != null && transition.IsRevealingScene(gameObject.scene))
        {
            yield return null;
        }

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            foreach (CanvasGroup group in fadeGroups)
            {
                if (group != null)
                {
                    group.alpha = Mathf.SmoothStep(0f, 1f, elapsed / fadeDuration);
                }
            }
            yield return null;
        }

        foreach (CanvasGroup group in fadeGroups)
        {
            if (group != null)
            {
                group.alpha = 1f;
                Destroy(group);
            }
        }
        fadeGroups.Clear();
        HasCompleted = true;
    }
}
