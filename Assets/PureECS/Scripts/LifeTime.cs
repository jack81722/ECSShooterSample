using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

[System.Serializable]
public struct LifeTime : IComponentData
{
    public float timer;
    public float lifeTime;
}
