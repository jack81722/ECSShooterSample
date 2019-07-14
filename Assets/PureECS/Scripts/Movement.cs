using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

[System.Serializable]
public struct Movement : IComponentData
{
    public float3 direction;
    public float speed;
}
