using System;
using UnityEngine;
namespace VoxelRacer
{
    /// <summary>Track-specific setup for the traffic approach before its referenced boss appears.</summary>
    [Serializable]
    public sealed class VoxelBossEncounterSettings
    {
        [Header("Traffic Approach")]
        [Tooltip("Chance per eligible traffic vehicle to spawn the objective. Only one Radar Interceptor is active at a time; missed targets can reappear.")]
        [Range(1,100)] public float radarInterceptorSpawnChance=10;
        [Min(.1f)] public float minimumWaveInterval=.8f;
        [Min(.1f)] public float maximumWaveInterval=1.4f;
        [Min(1)] public int minimumVehiclesPerWave=2;
        [Min(1)] public int maximumVehiclesPerWave=4;
        [Min(1)] public float minimumVehicleSpacing=10;
        [Min(1)] public float maximumVehicleSpacing=18;
        [Range(0,1)] public float oncomingTrafficChance=.6f;
        [Tooltip("Minimum delay after traffic spawning stops. The boss also waits for every remaining civilian vehicle to leave naturally; this is not a removal deadline.")]
        [Min(0)] public float roadClearDuration=2;
    }
}
