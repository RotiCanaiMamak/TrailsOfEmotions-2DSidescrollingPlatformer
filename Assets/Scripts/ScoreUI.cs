using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ScoreUI : MonoBehaviour
{
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private string scoreFormat = "Score {0}";

    private ScoreManager scoreManager;
    private bool warnedMissingText;

    private void Start()
    {
        ResolveText();
        SubscribeToScoreManager();
        RefreshScore();
    }

    private void OnDestroy()
    {
        UnsubscribeFromScoreManager();
    }

    private void OnValidate()
    {
        if (string.IsNullOrEmpty(scoreFormat))
        {
            scoreFormat = "{0}";
        }
    }

    private void OnScoreChanged(int value)
    {
        SetScoreText(value);
    }

    private void SubscribeToScoreManager()
    {
        scoreManager = ScoreManager.Instance;
        if (scoreManager == null)
        {
            Debug.LogWarning("[ScoreUI] No ScoreManager instance found.", this);
            return;
        }

        scoreManager.onScoreChanged.AddListener(OnScoreChanged);
    }

    private void UnsubscribeFromScoreManager()
    {
        if (scoreManager == null)
        {
            return;
        }

        scoreManager.onScoreChanged.RemoveListener(OnScoreChanged);
        scoreManager = null;
    }

    private void RefreshScore()
    {
        SetScoreText(scoreManager != null ? scoreManager.Score : 0);
    }

    private void SetScoreText(int value)
    {
        if (!TryGetText(out TMP_Text target))
        {
            return;
        }

        target.text = string.Format(scoreFormat, value);
    }

    private bool TryGetText(out TMP_Text target)
    {
        ResolveText();
        target = scoreText;
        if (target != null)
        {
            return true;
        }

        WarnMissingText();
        return false;
    }

    private void ResolveText()
    {
        if (scoreText == null)
        {
            scoreText = GetComponent<TMP_Text>();
        }
    }

    private void WarnMissingText()
    {
        if (warnedMissingText)
        {
            return;
        }

        Debug.LogWarning("[ScoreUI] Assign a TMP_Text component for the score display.", this);
        warnedMissingText = true;
    }
}
