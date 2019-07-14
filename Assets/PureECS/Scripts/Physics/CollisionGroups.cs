using System.Collections;
using System.Collections.Generic;
using BulletSharp;

namespace MyECS.Physics
{
    public class CollisionGroups
    {   
        public const short All = (short)CollisionFilterGroups.AllFilter;
        public const short Default = (short)CollisionFilterGroups.DefaultFilter;
        public const short Static = (short)CollisionFilterGroups.StaticFilter;
        public const short Kinematic = (short)CollisionFilterGroups.KinematicFilter;
        public const short Debris = (short)CollisionFilterGroups.DebrisFilter;
        public const short Sensor = (short)CollisionFilterGroups.SensorTrigger;
        public const short Character = (short)CollisionFilterGroups.CharacterFilter;

        // greater than 32
        public const short Bullet = 64;
    }
}