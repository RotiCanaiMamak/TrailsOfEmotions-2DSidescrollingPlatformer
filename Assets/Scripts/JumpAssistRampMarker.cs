using UnityEngine;

[DisallowMultipleComponent]
public sealed class JumpAssistRampMarker : MonoBehaviour
{
    public enum Source
    {
        Obstacle,
        Gap
    }

    [SerializeField] private Source source = Source.Obstacle;

    public Source RampSource => source;

    public void Initialize(Source rampSource)
    {
        source = rampSource;
    }
}
