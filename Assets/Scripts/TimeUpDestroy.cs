using System.Collections;
using System.Collections.Generic;
using Unity.Entities;

[System.Serializable]
public struct TimeUpDestory : IComponentData
{
    public float timer;
    public float lifeTime;
}
