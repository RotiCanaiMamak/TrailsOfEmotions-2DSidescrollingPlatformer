using UnityEngine;

public interface IBackpackAbsorbable
{
    bool TryAbsorbByBackpack(LinaBackpackBulwark bulwark);
}

public interface IRegulationClearable
{
    bool TryClearByRegulation(Object source);
}

public interface IManualStatusEscapeSource
{
    bool IsManualEscapeActive { get; }
    bool IsManualEscapeVisible { get; }
    float ManualEscapeProgress01 { get; }
}
