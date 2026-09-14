using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public sealed class ScoreManager : MonoBehaviour
{
    private const string PlayerTag = "Player";

    public static ScoreManager Instance { get; private set; }

    [Header("Distance")]
    [SerializeField] private PlayerController player;
    [Min(0f)] [SerializeField] private float scorePerMeter = 1f;

    [Header("Display")]
    [Min(1)] [SerializeField] private int scoreUpdateStep = 10;

    [Header("Bonuses")]
    [Min(0)] [SerializeField] private int orbPickupPoints = 25;
    [Min(0)] [SerializeField] private int groundPoundClearPoints = 50;
    [Min(0)] [SerializeField] private int absorptionPoints = 50;

    [Header("Events")]
    public UnityEvent<int> onScoreChanged = new UnityEvent<int>();
    public UnityEvent<int> onPointsAwarded = new UnityEvent<int>();

    private float rawScore;
    private int score;
    private bool warnedMissingPlayerTag;

    public float RawScore => rawScore;
    public int Score => score;

    private void Awake()
    {
        if (onScoreChanged == null)
        {
            onScoreChanged = new UnityEvent<int>();
        }

        if (onPointsAwarded == null)
        {
            onPointsAwarded = new UnityEvent<int>();
        }

        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        ResolvePlayerReference();
        ResetScore();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Update()
    {
        GameManager gameManager = GameManager.Instance;
        if (gameManager == null || !gameManager.IsRunning || gameManager.IsWaitingToMove)
        {
            return;
        }

        ResolvePlayerReference();
        if (player == null)
        {
            return;
        }

        AddDistance(player.ProgressSpeed * Time.deltaTime);
    }

    private void OnValidate()
    {
        scorePerMeter = Mathf.Max(0f, scorePerMeter);
        scoreUpdateStep = Mathf.Max(1, scoreUpdateStep);
        orbPickupPoints = Mathf.Max(0, orbPickupPoints);
        groundPoundClearPoints = Mathf.Max(0, groundPoundClearPoints);
        absorptionPoints = Mathf.Max(0, absorptionPoints);
    }

    public void ResetScore()
    {
        rawScore = 0f;
        SetDisplayedScore(0, true);
    }

    public void AddPoints(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        AddRawScore(amount);
        onPointsAwarded?.Invoke(amount);
    }

    public void AddDistance(float meters)
    {
        if (meters <= 0f || scorePerMeter <= 0f)
        {
            return;
        }

        AddRawScore(meters * scorePerMeter);
    }

    public void AddOrbPickupScore()
    {
        AddPoints(orbPickupPoints);
    }

    public void AddGroundPoundClearScore()
    {
        AddPoints(groundPoundClearPoints);
    }

    public void AddAbsorptionScore()
    {
        AddPoints(absorptionPoints);
    }

    private void AddRawScore(float amount)
    {
        if (amount <= 0f)
        {
            return;
        }

        rawScore = Mathf.Max(0f, rawScore + amount);
        int roundedScore = Mathf.FloorToInt(rawScore);
        if (roundedScore < score + Mathf.Max(1, scoreUpdateStep))
        {
            return;
        }

        SetDisplayedScore(roundedScore, false);
    }

    private void SetDisplayedScore(int value, bool force)
    {
        int nextScore = Mathf.Max(0, value);
        if (!force && nextScore == score)
        {
            return;
        }

        score = nextScore;
        onScoreChanged?.Invoke(score);
    }

    private void ResolvePlayerReference()
    {
        if (player != null)
        {
            return;
        }

        GameObject playerObject = FindPlayerObject();
        if (playerObject == null)
        {
            return;
        }

        player = playerObject.GetComponent<PlayerController>();
        if (player == null)
        {
            player = playerObject.GetComponentInParent<PlayerController>();
        }

        if (player == null)
        {
            player = playerObject.GetComponentInChildren<PlayerController>();
        }
    }

    private GameObject FindPlayerObject()
    {
        try
        {
            return GameObject.FindGameObjectWithTag(PlayerTag);
        }
        catch (UnityException)
        {
            if (!warnedMissingPlayerTag)
            {
                Debug.LogWarning($"[ScoreManager] No Unity tag named '{PlayerTag}' exists.", this);
                warnedMissingPlayerTag = true;
            }

            return null;
        }
    }
}
