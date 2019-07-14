using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;
using Unity.Transforms;
using BulletSharp;
using System;
using Unity.Mathematics;

namespace MyECS.Physics
{
    public class Physic_System : ComponentSystem
    {
        private DefaultCollisionConfiguration configuration;
        private CollisionDispatcher dispatcher;
        private DbvtBroadphase broadphase;
        private DiscreteDynamicsWorld world;

        protected override void OnCreate()
        {
            configuration = new DefaultCollisionConfiguration();
            dispatcher = new CollisionDispatcher(configuration);
            broadphase = new DbvtBroadphase();
            world = new DiscreteDynamicsWorld(dispatcher, broadphase, null, configuration);
        }

        protected override void OnUpdate()
        {
            float deltaTime = UnityEngine.Time.deltaTime;

            AddColliders();

            UpdateColliders();

            DetectCollision(deltaTime);
        }

        protected override void OnDestroy()
        {
            world.Dispose();
        }

        private void AddColliders()
        {
            Entities.WithAll<BoxCollider>().ForEach(
              (Entity entity, ref BoxCollider collider, ref Translation translation) =>
              {
                  CollisionShape shape = new BoxShape(collider.halfWidth.x, collider.halfWidth.y, collider.halfWidth.z);
                  CollisionObject collision = CreateCollision(shape, entity, collider.group, collider.mask);
                  SetPosition(collision, translation);

                  PostUpdateCommands.RemoveComponent<BoxCollider>(entity);
                  Collider colData = new Collider(world, collision);
                  PostUpdateCommands.AddSharedComponent(entity, colData);
              });

            Entities.WithAll<SphereCollider>().ForEach(
               (Entity entity, ref SphereCollider collider, ref Translation translation) =>
               {   
                   CollisionShape shape = new SphereShape(collider.radius);
                   CollisionObject collision = CreateCollision(shape, entity, collider.group, collider.mask);
                   SetPosition(collision, translation);

                   PostUpdateCommands.RemoveComponent<SphereCollider>(entity);
                   Collider colData = new Collider(world, collision);
                   PostUpdateCommands.AddSharedComponent(entity, colData);
               });
        }

        private CollisionObject CreateCollision(CollisionShape shape, object userObject, short group, short mask)
        {
            CollisionObject collision = new CollisionObject();
            collision.CollisionShape = shape;
            collision.UserObject = userObject;
            world.AddCollisionObject(collision, group, mask);
            return collision;
        }

        private void UpdateColliders()
        {
            // update collider position by translation
            Entities.WithAll<Collider>().ForEach(
                (Entity e, Collider collider, ref Translation translation) =>
                {
                    SetPosition(collider.collision, translation);
                    collider.ResetEvent();
                });
        }

        private void DetectCollision(float deltaTime)
        {
            world.StepSimulation(deltaTime);
            int numManifolds = world.Dispatcher.NumManifolds;
            for (int i = 0; i < numManifolds; i++)
            {
                PersistentManifold manifold = world.Dispatcher.GetManifoldByIndexInternal(i);
                CollisionObject colA = manifold.Body0;
                CollisionObject colB = manifold.Body1;

                if (colA.UserObject != null && colB.UserObject != null)
                {
                    Entity entityA = (Entity)colA.UserObject;
                    Entity entityB = (Entity)colB.UserObject;

                    AddCollisionEvent(entityA, entityB);
                    AddCollisionEvent(entityB, entityA);
                }
            }
        }

        private void SetPosition(CollisionObject collision, Translation translation)
        {
            var matrix = collision.WorldTransform;
            matrix.M41 = translation.Value.x;
            matrix.M42 = translation.Value.y;
            matrix.M43 = translation.Value.z;
            collision.WorldTransform = matrix;
        }

        private void AddCollisionEvent(Entity self, Entity other)
        {   
            Collider colliderSelf = EntityManager.GetSharedComponentData<Collider>(self);
            colliderSelf.AddEvent(other);
            EntityManager.SetSharedComponentData(self, colliderSelf);
        }
    }

    [Serializable]
    public struct Collider : ISharedComponentData, IEquatable<Collider>
    {
        public bool trigger { get; private set; }
        private DiscreteDynamicsWorld world;
        public CollisionObject collision;
        public Entity self { get { return (Entity)collision.UserObject; } }
        public Queue<Entity> others;

        public Collider(DiscreteDynamicsWorld world, CollisionObject collision)
        {
            trigger = false;
            this.world = world;
            this.collision = collision;
            others = new Queue<Entity>();
        }

        public void AddEvent(Entity other)
        {   
            trigger = true;
            others.Enqueue(other);
        }

        public void ResetEvent()
        {
            trigger = false;
            others.Clear();
        }

        public void UnregisterFromWorld()
        {
            world.RemoveCollisionObject(collision);
        }

        public bool Equals(Collider other)
        {
            return collision == other.collision;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
}