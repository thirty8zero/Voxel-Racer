using System;
using UnityEngine;
namespace VoxelRacer
{
    [Serializable]
    public sealed class VoxelBossSettings
    {
        [Header("Traffic Approach")]
        [Min(0)] public float trafficDuration=25;
        [Min(.1f)] public float minimumWaveInterval=.8f;
        [Min(.1f)] public float maximumWaveInterval=1.4f;
        [Min(1)] public int minimumVehiclesPerWave=2;
        [Min(1)] public int maximumVehiclesPerWave=4;
        [Min(1)] public float minimumVehicleSpacing=10;
        [Min(1)] public float maximumVehicleSpacing=18;
        [Range(0,1)] public float oncomingTrafficChance=.6f;
        [Tooltip("Minimum delay after traffic spawning stops. The boss also waits for every remaining civilian vehicle to leave naturally; this is not a removal deadline.")]
        [Min(0)] public float roadClearDuration=2;
        [Header("Boss Identity & Durability")]
        public string bossName="RED MENACE";
        [Tooltip("Empty uses the red Transit boss prefab.")]
        public GameObject bossPrefab;
        [Tooltip("Seconds for the boss model to grow from zero to full size. Zero disables the entrance animation.")]
        [Min(0)] public float entranceDuration=.75f;
        [Min(1)] public float health=2000;
        [Min(.01f)] public float voxelHealth=8;
        [Min(0)] public int playerCollisionDamageMin=35;
        [Min(0)] public int playerCollisionDamageMax=50;
        [Header("Boss Movement")]
        [Min(12)] public float minimumDistanceAhead=50;
        [Min(12)] public float maximumDistanceAhead=120;
        [Header("Boss Escape")]
        [Min(1)] public float escapeWarningDistance=150;
        [Min(2)] public float escapeFailureDistance=200;
        [Tooltip("Fraction of the player's unboosted top speed. Boss never stops with the player.")]
        [Range(.1f,1f)] public float minimumCruiseSpeedFraction=.6f;
        [Tooltip("Beyond this gap, hold a fixed speed independent of player boost.")]
        [Min(1)] public float catchUpDistance=100;
        [Min(1)] public float catchUpResumeDistance=70;
        [Tooltip("Fixed catch-up speed relative to unboosted top speed. Near 1 keeps the chase moving; boost closes the gap faster.")]
        [Range(.1f,1f)] public float catchUpSpeedFraction=.95f;
        public float WarningDistance => Mathf.Clamp(escapeWarningDistance,1,Mathf.Max(2,escapeFailureDistance)-1);
        public float FailureDistance => Mathf.Max(2,escapeFailureDistance);
        [Header("Boss Manoeuvres")]
        [Tooltip("Maximum speed difference during attack manoeuvres, in metres per second. Catch-up mode ignores this bonus.")]
        [Min(.1f)] public float distanceAdjustmentSpeed=16;
        [Tooltip("Forward acceleration in metres per second squared.")]
        [Min(.1f)] public float acceleration=24;
        [Tooltip("Braking deceleration in metres per second squared.")]
        [Min(.1f)] public float braking=40;
        [Tooltip("Minimum pause after completing a lane change, in seconds.")]
        [Min(.1f)] public float minimumLaneChangeInterval=1;
        [Tooltip("Maximum pause after completing a lane change, in seconds.")]
        [Min(.1f)] public float maximumLaneChangeInterval=2;
        [Tooltip("Sideways speed in metres per second.")]
        [Min(.1f)] public float laneChangeSpeed=6;
        [Header("Spike Brake Attack")]
        public bool spikeAttackEnabled=true;
        [Range(0,1)] public float spikeAttackChance=.45f;
        [Min(1)] public float spikeAttackCheckInterval=12;
        [Tooltip("Doors open, then spikes extend, before braking begins. Mines stop immediately.")]
        [Min(.5f)] public float spikeAttackWarningDuration=2;
        [Min(.1f)] public float spikeAttackHoldDuration=2;
        [Min(1)] public float spikeAttackBraking=180;
        [Tooltip("How quickly the attack closes its remaining gap. Twice the original response is 6.")]
        [Min(.1f)] public float spikeAttackClosingResponse=6;
        [Tooltip("Maximum closing speed relative to the player, in m/s. The van reverses when necessary to achieve this during the spike slam.")]
        [Min(1)] public float spikeAttackClosingSpeed=60;
        [Tooltip("Distance ahead to reach when the player dodges. Zero brings the van alongside the player. Body separation still applies if lanes overlap.")]
        [Min(0)] public float spikeAttackMissDistance=0;
        [Min(1)] public float spikeAttackAcceleration=100;
        [Min(1)] public float spikeAttackPullAwaySpeed=55;
        [Min(20)] public float spikeAttackRetreatDistance=110;
        [Tooltip("Maximum approach duration before the boss abandons the slam and retreats.")]
        [Min(1)] public float spikeAttackApproachTimeout=8;
        [Min(.1f)] public float spikeAttackRetractDuration=1;
        [Tooltip("Player damage in voxel units. Applied once per spike attack on contact; armour retains its normal damage handling.")]
        [Min(0)] public int spikeAttackDamageMin=70;
        [Min(0)] public int spikeAttackDamageMax=100;
        [Header("Twin Mine Layers")]
        public bool minesEnabled=true;
        [Tooltip("Boss-specific mine model, hit area, damage and explosion. Empty uses the legacy mine settings below.")]
        public VoxelMineLayerTuning mineTuning;
        [Range(0,1)] public float mineDropChance=.6f;
        [Min(.1f)] public float mineDropInterval=3;
        [Min(0)] public float mineArmingDelay=.5f;
        [Min(1)] public float mineLifetime=25;
        [Tooltip("Legacy fallback only: when Mine Tuning is assigned, edit damage on that asset instead.")]
        [Min(1)] public int mineDamageMin=60;
        [Tooltip("Legacy fallback only: when Mine Tuning is assigned, edit damage on that asset instead.")]
        [Min(1)] public int mineDamageMax=80;
        [Tooltip("Legacy fallback only: when Mine Tuning is assigned, edit explosion scale on that asset instead.")]
        [Min(.1f)] public float mineExplosionScale=2;
        [Header("Damage Effects")]
        [Range(0,1)] public float smokeDamageThreshold=.25f;
        [Range(0,1)] public float fireDamageThreshold=.5f;
    }
}
