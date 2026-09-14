using UnityEngine;

[DisallowMultipleComponent]
public class LinaBackpackAnimationDriver : MonoBehaviour
{
    private const string DefaultBackpackBulwarkActiveParameter = "BackpackBulwarkActive";
    private const string DefaultGroundPoundingParameter = "GroundPounding";
    private const string DefaultJumpingParameter = "Jumping";
    private const string DefaultGlidingParameter = "Gliding";
    private const string DefaultZipliningParameter = "Ziplining";

    [SerializeField] private PlayerController player;
    [SerializeField] private LinaBackpackBulwark backpackBulwark;
    [SerializeField] private Animator animator;
    [SerializeField] private string backpackBulwarkActiveParameter = DefaultBackpackBulwarkActiveParameter;
    [SerializeField] private string groundPoundingParameter = DefaultGroundPoundingParameter;
    [SerializeField] private string jumpingParameter = DefaultJumpingParameter;
    [SerializeField] private string glidingParameter = DefaultGlidingParameter;
    [SerializeField] private string zipliningParameter = DefaultZipliningParameter;
    [SerializeField] private bool warnIfParametersMissing = true;

    private int backpackBulwarkActiveHash;
    private int groundPoundingHash;
    private int jumpingHash;
    private int glidingHash;
    private int zipliningHash;
    private bool canSetBackpackBulwarkActive;
    private bool canSetGroundPounding;
    private bool canSetJumping;
    private bool canSetGliding;
    private bool canSetZiplining;
    private bool reportedMissingBackpackBulwarkActive;
    private bool reportedMissingGroundPounding;
    private bool reportedMissingJumping;
    private bool reportedMissingGliding;
    private bool reportedMissingZiplining;
    private RuntimeAnimatorController cachedController;

    private void Awake()
    {
        ResolveReferences();
        RefreshParameterCache(true);
    }

    private void OnEnable()
    {
        ResolveReferences();
        RefreshParameterCache(true);
        ApplyAnimatorState();
    }

    private void Update()
    {
        ResolveReferences();
        RefreshParameterCache(false);
        ApplyAnimatorState();
    }

    private void ResolveReferences()
    {
        if (player == null)
        {
            player = GetComponent<PlayerController>();
        }

        if (player == null)
        {
            player = GetComponentInParent<PlayerController>();
        }

        if (backpackBulwark == null)
        {
            backpackBulwark = GetComponent<LinaBackpackBulwark>();
        }

        if (backpackBulwark == null)
        {
            backpackBulwark = GetComponentInParent<LinaBackpackBulwark>();
        }

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>(true);
        }
    }

    private void RefreshParameterCache(bool force)
    {
        NormalizeParameterNames();

        RuntimeAnimatorController controller = animator != null ? animator.runtimeAnimatorController : null;
        if (!force && cachedController == controller)
        {
            return;
        }

        cachedController = controller;
        backpackBulwarkActiveHash = Animator.StringToHash(backpackBulwarkActiveParameter);
        groundPoundingHash = Animator.StringToHash(groundPoundingParameter);
        jumpingHash = Animator.StringToHash(jumpingParameter);
        glidingHash = Animator.StringToHash(glidingParameter);
        zipliningHash = Animator.StringToHash(zipliningParameter);

        canSetBackpackBulwarkActive = HasBoolParameter(backpackBulwarkActiveHash);
        canSetGroundPounding = HasBoolParameter(groundPoundingHash);
        canSetJumping = HasBoolParameter(jumpingHash);
        canSetGliding = HasBoolParameter(glidingHash);
        canSetZiplining = HasBoolParameter(zipliningHash);
    }

    private void ApplyAnimatorState()
    {
        if (animator == null)
        {
            return;
        }

        TrySetBool(
            backpackBulwarkActiveHash,
            backpackBulwark != null && backpackBulwark.IsActive,
            canSetBackpackBulwarkActive,
            backpackBulwarkActiveParameter,
            ref reportedMissingBackpackBulwarkActive);

        TrySetBool(
            groundPoundingHash,
            player != null && player.IsGroundPounding,
            canSetGroundPounding,
            groundPoundingParameter,
            ref reportedMissingGroundPounding);

        TrySetBool(
            jumpingHash,
            player != null && player.IsAirborne && !player.IsGliding,
            canSetJumping,
            jumpingParameter,
            ref reportedMissingJumping);

        TrySetBool(
            glidingHash,
            player != null && player.IsGliding,
            canSetGliding,
            glidingParameter,
            ref reportedMissingGliding);

        TrySetBool(
            zipliningHash,
            player != null && player.IsOnZipline,
            canSetZiplining,
            zipliningParameter,
            ref reportedMissingZiplining);
    }

    private bool HasBoolParameter(int parameterHash)
    {
        if (animator == null)
        {
            return false;
        }

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];
            if (parameter.nameHash == parameterHash &&
                parameter.type == AnimatorControllerParameterType.Bool)
            {
                return true;
            }
        }

        return false;
    }

    private void TrySetBool(
        int parameterHash,
        bool value,
        bool canSetParameter,
        string parameterName,
        ref bool reportedMissingParameter)
    {
        if (canSetParameter)
        {
            animator.SetBool(parameterHash, value);
            return;
        }

        if (!warnIfParametersMissing || reportedMissingParameter)
        {
            return;
        }

        Debug.LogWarning(
            $"[LinaBackpackAnimationDriver] Animator is missing bool parameter '{parameterName}'.",
            this);
        reportedMissingParameter = true;
    }

    private void NormalizeParameterNames()
    {
        if (string.IsNullOrWhiteSpace(backpackBulwarkActiveParameter))
        {
            backpackBulwarkActiveParameter = DefaultBackpackBulwarkActiveParameter;
        }

        if (string.IsNullOrWhiteSpace(groundPoundingParameter))
        {
            groundPoundingParameter = DefaultGroundPoundingParameter;
        }

        if (string.IsNullOrWhiteSpace(jumpingParameter))
        {
            jumpingParameter = DefaultJumpingParameter;
        }

        if (string.IsNullOrWhiteSpace(glidingParameter))
        {
            glidingParameter = DefaultGlidingParameter;
        }

        if (string.IsNullOrWhiteSpace(zipliningParameter))
        {
            zipliningParameter = DefaultZipliningParameter;
        }
    }

    private void OnValidate()
    {
        NormalizeParameterNames();
    }
}
