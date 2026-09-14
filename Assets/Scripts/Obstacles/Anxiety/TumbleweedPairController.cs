using UnityEngine;

[DisallowMultipleComponent]
public sealed class TumbleweedPairController : MonoBehaviour
{
    [Header("Tumbleweeds")]
    [SerializeField] private TumbleweedObstacle solidTumbleweed;
    [SerializeField] private TumbleweedObstacle halfTransparentTumbleweed;

    [Header("Speed Options")]
    [Min(0f)] [SerializeField] private float speedOptionA = 4f;
    [Min(0f)] [SerializeField] private float speedOptionB = 6f;

    private void Reset()
    {
        ResolveChildren();
        ConfigureVariants();
    }

    private void Awake()
    {
        ResolveChildren();
    }

    private void OnEnable()
    {
        ConfigurePair();
    }

    private void OnValidate()
    {
        speedOptionA = Mathf.Max(0f, speedOptionA);
        speedOptionB = Mathf.Max(0f, speedOptionB);
        ResolveChildren();
        ConfigureVariants();
    }

    public void ConfigurePair()
    {
        ConfigureVariants();

        bool solidUsesFirstSpeed = Random.value < 0.5f;
        float solidSpeed = solidUsesFirstSpeed ? speedOptionA : speedOptionB;
        float transparentSpeed = solidUsesFirstSpeed ? speedOptionB : speedOptionA;

        if (solidTumbleweed != null)
        {
            solidTumbleweed.SetRuntimeSpeed(solidSpeed);
        }

        if (halfTransparentTumbleweed != null)
        {
            halfTransparentTumbleweed.SetRuntimeSpeed(transparentSpeed);
        }
    }

    private void ConfigureVariants()
    {
        if (solidTumbleweed != null)
        {
            solidTumbleweed.ConfigureVariant(TumbleweedObstacle.TumbleweedVariant.Solid);
        }

        if (halfTransparentTumbleweed != null)
        {
            halfTransparentTumbleweed.ConfigureVariant(TumbleweedObstacle.TumbleweedVariant.HalfTransparent);
        }
    }

    private void ResolveChildren()
    {
        if (solidTumbleweed != null && halfTransparentTumbleweed != null)
        {
            return;
        }

        TumbleweedObstacle[] tumbleweeds = GetComponentsInChildren<TumbleweedObstacle>(true);
        if (solidTumbleweed == null && tumbleweeds.Length > 0)
        {
            solidTumbleweed = tumbleweeds[0];
        }

        if (halfTransparentTumbleweed == null)
        {
            for (int i = 0; i < tumbleweeds.Length; i++)
            {
                TumbleweedObstacle candidate = tumbleweeds[i];
                if (candidate != null && candidate != solidTumbleweed)
                {
                    halfTransparentTumbleweed = candidate;
                    return;
                }
            }
        }
    }
}
