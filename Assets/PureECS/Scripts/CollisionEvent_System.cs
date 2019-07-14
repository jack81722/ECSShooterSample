using MyECS.Physics;
using System.Collections;
using System.Collections.Generic;
using Unity.Entities;

[UpdateAfter(typeof(Physic_System))]
public class CollisionEvent_System : ComponentSystem
{
    protected override void OnUpdate()
    {
        var colliderEntities = Entities.WithAll<Collider>();
        colliderEntities.WithAll<Target>().ForEach(
            (Entity entity, Collider collider, ref Target target) =>
            {
                if (collider.trigger)
                {
                    var others = collider.others.ToArray();
                    foreach(var other in others)
                    {
                        if (EntityManager.HasComponent<Bullet>(other))
                        {
                            // destruction of target
                            //PostUpdateCommands.AddComponent(entity, new Destruction());
                            // destruction of bullet
                            PostUpdateCommands.AddComponent(other, new Destruction());
                            break;
                        }
                    }
                }
            });
    }
}
