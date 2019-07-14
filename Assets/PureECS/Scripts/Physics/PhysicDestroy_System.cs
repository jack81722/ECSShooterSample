using System.Collections;
using System.Collections.Generic;
using Unity.Entities;

namespace MyECS.Physics
{
    [UpdateAfter(typeof(CollisionEvent_System))]
    public class PhysicDestroy_System : ComponentSystem
    {
        protected override void OnUpdate()
        {
            Entities.WithAll<Collider, Destruction>().ForEach(
                (Collider collider) =>
                {
                    collider.UnregisterFromWorld();
                });
        }
    }
}