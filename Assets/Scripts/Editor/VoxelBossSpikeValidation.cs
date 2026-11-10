using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
namespace VoxelRacer.Editor
{
    public static class VoxelBossSpikeValidation
    {
        const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        static void Set(object o,string n,object v)=>o.GetType().GetField(n,Flags).SetValue(o,v);
        static object Call(object o,string n,params object[] a)=>o.GetType().GetMethod(n,Flags).Invoke(o,a);
        static void Check(bool ok,string message) {if(!ok)throw new Exception(message);}
        public static void Run()
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(VoxelRedVanBossBuilder.PrefabPath);
            var root=Object.Instantiate(prefab);
            var playerObject=new GameObject("Spike test player");
            var tuning=ScriptableObject.CreateInstance<VoxelEnemyVehicleTuning>();
            tuning.mineLayer=ScriptableObject.CreateInstance<VoxelMineLayerTuning>();
            var traffic=ScriptableObject.CreateInstance<VoxelObstacleCarTuning>();
            var random=UnityEngine.Random.state;
            VoxelBossDefinition settings=null;
            VoxelBossSpikeAttackTuning spikeAttack=null;
            try
            {
                var rig=root.GetComponent<VoxelBossSpikeRig>();
                Check(rig!=null && rig.leftDoor.childCount>0 && rig.rightDoor.childCount>0,"Missing doors");
                rig.SetPose(1,1);
                Check(Mathf.Abs(Mathf.DeltaAngle(rig.leftDoor.localEulerAngles.y,180))<.1f && rig.spikes.localPosition.z < -2,"Attack visual pose");
                Check(rig.spikes.GetComponent<VoxelIndestructiblePart>()!=null,"Unprotected spikes");
                rig.SetPose(0,0);
                var enemy=root.AddComponent<VoxelEnemyCar>();enemy.enabled=false;
                var player=playerObject.AddComponent<VoxelCarController>();player.enabled=false;
                settings=ScriptableObject.CreateInstance<VoxelBossDefinition>();
                spikeAttack=ScriptableObject.CreateInstance<VoxelBossSpikeAttackTuning>();
                spikeAttack.chance=1;spikeAttack.checkInterval=1;
                settings.attacks=new VoxelBossAttackDefinition[]{spikeAttack};
                enemy.ConfigureBoss(settings,3,4);
                tuning.collisionHalfLength=6.4f;
                Set(enemy,"<Tuning>k__BackingField",tuning);Set(enemy,"target",player);Set(enemy,"trafficTuning",traffic);
                Set(enemy,"spikeRig",rig);Set(enemy,"trackDistance",100f);Set(enemy,"currentSpeed",30f);
                Set(player,"<CurrentSpeed>k__BackingField",30f);
                Call(enemy,"BeginSpikeAttack");
                Check(float.IsPositiveInfinity((float)typeof(VoxelEnemyCar).GetField("nextMineTime",Flags).GetValue(enemy)),"Mines not suspended");
                Call(enemy,"UpdateMineLayer");
                Call(enemy,"UpdateSpikeAttack",spikeAttack.warningDuration);
                Check(enemy.SpikePhase==VoxelEnemyCar.SpikeAttackPhase.Braking,"Warning did not finish");
                Check(Mathf.Approximately((float)Call(enemy,"GetSpikeAttackSpeed"),-30f),"Attack does not close at 60m/s relative to a 30m/s player");
                Check(Mathf.Approximately((float)Call(enemy,"AdvanceBossSpeed",.1f),12f),"Attack braking is not twice the original 90m/s²");
                float before=100, elapsed=0;
                bool held=false,retreated=false;
                while(enemy.SpikeAttackActive && elapsed<20)
                {
                    float speed=(float)Call(enemy,"AdvanceBossSpeed",.02f);
                    Set(enemy,"currentSpeed",speed);
                    before+=(speed-30)*.02f;Set(enemy,"trackDistance",before);
                    Call(enemy,"UpdateSpikeAttack",.02f);
                    held |= enemy.SpikePhase==VoxelEnemyCar.SpikeAttackPhase.Holding;
                    if(enemy.SpikePhase==VoxelEnemyCar.SpikeAttackPhase.Holding) Check(before<1f,"Missed attack stopped before reaching the player");
                    retreated |= enemy.SpikePhase==VoxelEnemyCar.SpikeAttackPhase.Retreating;
                    elapsed+=.02f;
                }
                Check(held && retreated && !enemy.SpikeAttackActive,"Attack did not complete all phases");
                Check(Mathf.Abs(before-110)<3,"Retreat missed 110m: "+before);
                Check(rig.spikes.localPosition==Vector3.zero && Quaternion.Angle(rig.leftDoor.localRotation,Quaternion.identity)<.1f,"Rig did not close");
                Check(!float.IsPositiveInfinity((float)typeof(VoxelEnemyCar).GetField("nextMineTime",Flags).GetValue(enemy)),"Mines never resumed");
                // A stopped player no longer prevents the boss from closing the gap.
                Set(player,"<CurrentSpeed>k__BackingField",0f);Set(enemy,"trackDistance",100f);
                Call(enemy,"BeginSpikeAttack");Call(enemy,"UpdateSpikeAttack",spikeAttack.warningDuration);
                Check(Mathf.Approximately((float)Call(enemy,"GetSpikeAttackSpeed"),-60f),"Boss does not reverse toward a stopped player");
                Call(enemy,"UpdateSpikeAttack",spikeAttack.approachTimeout+.1f);
                Check(enemy.SpikePhase==VoxelEnemyCar.SpikeAttackPhase.Retreating,"Stopped-player escape timeout failed");
                Check(VoxelVehicleCollision.Sweep(new Vector2(0,-30),new Vector2(0,0),new Vector2(3,10),out _),"Fast ram skips collision");
                Check(!VoxelVehicleCollision.Sweep(new Vector2(5,-30),new Vector2(5,0),new Vector2(3,10),out _),"Lane dodge still collides");
                spikeAttack.damageMin=spikeAttack.damageMax=0;
                Call(enemy,"BeginSpikeAttack");
                Set(enemy,"trackDistance",0f);Set(enemy,"currentSpeed",0f);
                Set(player,"<CurrentSpeed>k__BackingField",60f);
                Call(enemy,"ApplySpikeRam");
                Check(enemy.SpikePhase==VoxelEnemyCar.SpikeAttackPhase.Retreating,"Spike impact did not immediately retreat");
                Check(enemy.TrackDistance>=9f,"Impact left bodies overlapping");
                Check((float)typeof(VoxelEnemyCar).GetField("currentSpeed",Flags).GetValue(enemy)>=60f,"Van continues closing after hit");
                var burst=GameObject.Find("Boss Spike Impact Debris");
                Check(burst!=null && burst.GetComponent<ParticleSystem>().particleCount==56 && burst.GetComponent<ParticleSystemRenderer>().mesh!=null,"Large debris burst missing");
                Call(enemy,"ApplySpikeRam");
                Check(burst.GetComponent<ParticleSystem>().particleCount==56,"Repeated spike hit duplicated burst");
                Object.DestroyImmediate(burst);
                Set(enemy,"trackDistance",0f);Call(enemy,"KeepSpikeBodiesSeparated",new Vector2(0f,-20f));
                Check(enemy.TrackDistance>=9f,"Retreat does not preserve body clearance");
                CheckPlayerSideRams(enemy, player, tuning, traffic, spikeAttack, settings);
                CheckSpikeBodySeparation(enemy, player, tuning, spikeAttack, settings);
                ValidateRoadSideRam();
                Debug.Log("PASS spike attack: warning, braking, hold, 110m retreat, doors/spikes, mine suppression/resume, stopped-player timeout and swept lane dodge.");
            }
            finally {Object.DestroyImmediate(root);Object.DestroyImmediate(playerObject);Object.DestroyImmediate(tuning.mineLayer);Object.DestroyImmediate(tuning);Object.DestroyImmediate(traffic);if(settings!=null)Object.DestroyImmediate(settings);if(spikeAttack!=null)Object.DestroyImmediate(spikeAttack);UnityEngine.Random.state=random;}
        }
        private static void CheckPlayerSideRams(VoxelEnemyCar enemy, VoxelCarController player,
            VoxelEnemyVehicleTuning tuning, VoxelObstacleCarTuning traffic, VoxelBossSpikeAttackTuning spikeAttack,
            VoxelBossDefinition settings)
        {
            var authoredBoss = Resources.Load<VoxelBossDefinition>("Bosses/RedVanBoss");
            var authoredAttack = authoredBoss.GetAttack<VoxelBossSpikeAttackTuning>();
            spikeAttack.sideRamSparkDuration = authoredAttack.sideRamSparkDuration;
            spikeAttack.sideRamReboundDuration = authoredAttack.sideRamReboundDuration;
            float impactDuration = spikeAttack.sideRamReboundDuration;
            traffic.collisionCooldown = 0f;
            tuning.collisionHalfWidth = 4f;
            tuning.playerRamDamage = 20f;
            settings.ramDamageVoxelsMin = settings.ramDamageVoxelsMax = 1;
            Set(enemy, "<CurrentHealth>k__BackingField", 1000f);
            Set(enemy, "nextCollisionTime", 0f);
            Set(player, "nextDamageTime", float.PositiveInfinity);

            void PlacePlayer(float offset)
            {
                Set(player, "<CurrentLaneOffset>k__BackingField", offset);
                Set(player, "ramLateralOffset", 0f);
                Set(enemy, "sideRamOffset", 0f);
            }

            void SideContact(float side)
            {
                PlacePlayer(side * 2f);
                Set(enemy, "previousCollisionRelative", new Vector2(side * 5f, 0f));
                Call(enemy, "CheckVehicleContact");
            }

            void FinishRetreat()
            {
                Set(enemy, "trackDistance", spikeAttack.retreatDistance);
                Call(enemy, "UpdateSpikeAttack", spikeAttack.retractDuration);
                Check(!enemy.SpikeAttackActive, "Retreat did not end");
                Set(enemy, "trackDistance", 0f);
            }

            void BeginHold(bool dodge)
            {
                PlacePlayer(dodge ? 3f : 0f);
                Call(enemy, "BeginSpikeAttack");
                Call(enemy, "UpdateSpikeAttack", spikeAttack.warningDuration);
                Call(enemy, "UpdateSpikeAttack", .02f);
                Check(enemy.SpikePhase == VoxelEnemyCar.SpikeAttackPhase.Holding, "Side ram test did not reach hold");
            }

            FinishRetreat();
            SideContact(1f);
            Check(enemy.CurrentHealth == 1000f, "Side contact outside the attack damaged Vandito");
            BeginHold(false);
            SideContact(1f);
            Check(enemy.CurrentHealth == 1000f, "Side contact without a dodge damaged Vandito");

            BeginHold(true);
            SideContact(1f);
            Check(enemy.CurrentHealth == 980f, "First side ram after a dodge did not damage Vandito");
            Check(enemy.SpikePhase == VoxelEnemyCar.SpikeAttackPhase.SideRamImpact, "The first ram did not stage an impact");
            SideContact(-1f);
            Check(enemy.CurrentHealth == 980f, "Second side ram landed before the retreat began");
            Call(enemy, "UpdateSpikeAttack", impactDuration + .01f);
            Call(enemy, "ResolveSpikeBossContact");
            Check(enemy.SpikePhase == VoxelEnemyCar.SpikeAttackPhase.Retreating, "Vandito did not retreat after the rebound");
            SideContact(-1f);
            Check(enemy.CurrentHealth == 980f, "Second side ram landed during retreat");
            FinishRetreat();
            SideContact(-1f);
            Check(enemy.CurrentHealth == 980f, "Side ram became available again before the next dodge");

            BeginHold(true);
            SideContact(-1f);
            Check(enemy.CurrentHealth == 960f, "A new dodge did not restore the side ram on the opposite side");
            Call(enemy, "UpdateSpikeAttack", impactDuration + .01f);
            Call(enemy, "ResolveSpikeBossContact");
            FinishRetreat();

            PlacePlayer(0f);
            Set(enemy, "trackDistance", 2f);
            Set(enemy, "previousCollisionRelative", new Vector2(0f, -10f));
            Call(enemy, "CheckVehicleContact");
            Check(enemy.CurrentHealth == 940f, "Side ram gate blocked an ordinary rear ram");
            Debug.Log("PASS Vandito side rams: dodge required, one hit across impact and retreat, next dodge reopens either side, rear ram preserved.");
        }

        private static void CheckSpikeBodySeparation(VoxelEnemyCar enemy, VoxelCarController player,
            VoxelEnemyVehicleTuning tuning, VoxelBossSpikeAttackTuning spikeAttack, VoxelBossDefinition settings)
        {
            var track = Resources.Load<VoxelTrackDefinition>("Tracks/TrackBoss01");
            float laneWidth = track.roadTuning.roadWidth / track.roadTuning.laneCount;
            enemy.ConfigureBoss(settings, laneWidth, track.roadTuning.laneCount);
            tuning.collisionHalfWidth = laneWidth * .9f + 1f;
            tuning.collisionHalfLength = settings.bodyCollisionHalfLength * laneWidth * 1.8f / settings.referenceBodyWidth;
            var playerModel = Resources.Load<VoxelCarDefinition>("Cars/SpyCar2PlayerCar").visualPrefab;
            Object.Instantiate(playerModel, player.transform);
            Call(enemy, "CacheSpikePlayerBounds");
            var bossModel = Resources.Load<VoxelBossDefinition>("Bosses/RedVanBoss").bossPrefab;
            Call(enemy, "CacheSpikeBossSideProfile", bossModel);
            var bodySize = typeof(VoxelEnemyCar).GetProperty("SpikeBodyHalfSize", Flags);
            Vector2 clearance = (Vector2)bodySize.GetValue(enemy);
            Vector2 Profile(Vector2 previous, Vector2 current) =>
                (Vector2)Call(enemy, "GetSpikeBodyHalfSize", previous, current);
            float outerLane = (track.roadTuning.laneCount - 1) * .5f * laneWidth;
            float dodgeOffset = laneWidth * spikeAttack.dodgeLaneFraction;
            Check(dodgeOffset < clearance.x && clearance.x < outerLane, "Fixture does not cover the gap between dodge and body clearance");

            Vector2 Step(Vector2 previous, Vector2 current, float bossLane = 0f, float boostOffset = 0f)
            {
                const float bossDistance = 100f;
                Set(enemy, "trackDistance", bossDistance);
                Set(enemy, "laneOffset", bossLane);
                Set(enemy, "sideRamOffset", 0f);
                Set(enemy, "rearRamForwardOffset", 0f);
                Set(player, "ramLateralOffset", 0f);
                Set(player, "ramForwardOffset", 0f);
                Set(player, "boostForwardOffset", boostOffset);
                Set(player, "<CurrentLaneOffset>k__BackingField", bossLane + current.x);
                Set(player, "<TrackDistance>k__BackingField", bossDistance + current.y - boostOffset);
                Set(enemy, "previousCollisionRelative", previous);
                Call(enemy, "ResolveSpikeBossContact");
                var relative = player.CollisionTrackPosition - new Vector2(enemy.LaneOffset, enemy.TrackDistance);
                Vector2 halfSize = Profile(previous, current);
                Check(Mathf.Abs(relative.x) >= halfSize.x || Mathf.Abs(relative.y) >= halfSize.y,
                    $"Vandito and player remained inside each other. Relative position {relative}");
                Check((Vector2)typeof(VoxelEnemyCar).GetField("previousCollisionRelative", Flags).GetValue(enemy) == relative,
                    "Next sweep started from the uncorrected position");
                return relative;
            }

            foreach (float side in new[] { -1f, 1f })
            {
                // The boss can enter Holding before the player's Update completes the dodge.
                Set(player, "<CurrentLaneOffset>k__BackingField", 0f);
                Set(player, "<TrackDistance>k__BackingField", enemy.TrackDistance);
                Set(player, "boostForwardOffset", 0f);
                Set(player, "ramForwardOffset", 0f);
                Call(enemy, "BeginSpikeAttack");
                Call(enemy, "UpdateSpikeAttack", spikeAttack.warningDuration);
                Call(enemy, "UpdateSpikeAttack", .02f);
                Check(enemy.SpikePhase == VoxelEnemyCar.SpikeAttackPhase.Holding, "Body separation fixture missed Holding");
                Set(enemy, "nextCollisionTime", 0f);
                float health = enemy.CurrentHealth;
                float nearDodge = side * (dodgeOffset + .1f);
                Vector2 glanced = Step(new Vector2(nearDodge, -clearance.y - 1f), new Vector2(nearDodge, 0f));
                Check(side * glanced.x >= clearance.x && enemy.CurrentHealth == health,
                    "Near dodge did not slip outside the body without spending the side ram");

                float sideFace = Profile(Vector2.zero, Vector2.zero).x;
                float wheelFace = Profile(new Vector2(0f, 6f), new Vector2(0f, 6f)).x;
                Check(sideFace < clearance.x && wheelFace > sideFace + .15f,
                    $"Vandito's centre and wheel profiles did not match its silhouette ({sideFace}, {wheelFace}, {clearance.x})");
                float approach = side * (sideFace + .06f);
                Step(new Vector2(side * outerLane, 0f), new Vector2(approach, 0f));
                Check(enemy.CurrentHealth == health, "Side ram triggered before the visible van side");
                Vector2 impact = Step(new Vector2(approach, 0f), new Vector2(nearDodge, 0f));
                Check(enemy.CurrentHealth < health, "Returning through the side did not land the counterattack");
                Check(Mathf.Abs(side * impact.x - sideFace) <= .025f,
                    "The impact frame skipped past visible contact into recoil");
                Debug.Log($"Vandito side {side}: contact at {Mathf.Abs(impact.x):F2} m, wheel clearance {wheelFace:F2} m.");
                health = enemy.CurrentHealth;
                float rebound = spikeAttack.sideRamReboundDuration;
                float speed = (float)Call(enemy, "AdvanceBossSpeed", 1f / 60f);
                Check(speed > player.CurrentSpeed, "Vandito held the player's speed after the side impact");
                float vanOffset = enemy.LaneOffset;
                Call(enemy, "UpdateSpikeAttack", rebound * .5f);
                Check(side * (enemy.LaneOffset - vanOffset) < -.5f, "Vandito did not recoil away from the side impact");
                Call(enemy, "UpdateSpikeAttack", rebound * .5f + .01f);
                Call(enemy, "ResolveSpikeBossContact");
                Check(enemy.SpikePhase == VoxelEnemyCar.SpikeAttackPhase.Retreating, "Side ram did not start retreat");

                // Sustained steering into the boss, including a dropped frame that crosses its entire width.
                Set(enemy, "nextCollisionTime", float.PositiveInfinity);
                foreach (float dt in new[] { 1f / 120f, 1f / 60f, 1f / 30f, .25f })
                {
                    float start = side * (sideFace + .02f);
                    Vector2 end = Step(new Vector2(start, 0f), new Vector2(start - side * 80f * dt, 0f));
                    Check(side * end.x >= sideFace && Mathf.Abs(end.y) < .01f, "Blocked side contact crossed the boss or moved it forward");
                }
                Vector2 escaped = Step(new Vector2(side * sideFace, 0f), new Vector2(side * outerLane, 0f));
                Check(Mathf.Approximately(escaped.x, side * outerLane), "Player could not steer clear of Vandito");
                Step(new Vector2(0f, -clearance.y - 2f), new Vector2(0f, clearance.y + 2f), boostOffset: 12f);
                Step(new Vector2(side * laneWidth * .5f, 0f), new Vector2(side * laneWidth * .5f, 0f), side * laneWidth);
                Check(enemy.CurrentHealth == health, "Separation during retreat applied extra ram damage");
            }

            float beforeRearRam = enemy.CurrentHealth;
            Set(enemy, "nextCollisionTime", 0f);
            Step(new Vector2(0f, -clearance.y - 1f), new Vector2(0f, -clearance.y + .1f));
            Check(enemy.CurrentHealth < beforeRearRam, "A rear ram during retreat was blocked");

            Set(enemy, "nextCollisionTime", float.PositiveInfinity);
            Set(player, "oilSpinDuration", 1f);
            Set(player, "oilSpinElapsed", .5f);
            Set(player, "oilSpinYaw", 90f);
            Vector2 spinningSize = (Vector2)bodySize.GetValue(enemy);
            Check(spinningSize.x > clearance.x && spinningSize.y > clearance.y, "Oil spin did not widen body clearance");
            Step(new Vector2(spinningSize.x + 1f, 0f), new Vector2(clearance.x + .1f, 0f));
            Set(player, "oilSpinDuration", 0f);
            Set(player, "oilSpinYaw", 0f);
            Debug.Log("PASS Vandito visible contact and body clearance: centre/wheel profile, opposing recoil, sustained steering on both sides, 250ms crossing, boost sweep, edge-lane squeeze and oil spin.");
        }

        public static void ValidateRoadSideRam(bool render = false)
        {
            var random = UnityEngine.Random.state;
            var track = Resources.Load<VoxelTrackDefinition>("Tracks/TrackBoss01");
            var authored = Resources.Load<VoxelBossDefinition>("Bosses/RedVanBoss");
            var settings = Object.Instantiate(authored);
            settings.entranceDuration = 0f;
            var tuning = settings.CreateRuntimeVehicleTuning();
            var preview = new PreviewRenderUtility();
            try
            {
                VoxelRacerBootstrap.ReloadGeneratedMaterials();
                float laneWidth = track.roadTuning.roadWidth / track.roadTuning.laneCount;
                tuning.collisionHalfWidth = laneWidth * .9f + 1f;
                tuning.collisionHalfLength = settings.bodyCollisionHalfLength * laneWidth * 1.8f / settings.referenceBodyWidth;
                var root = new GameObject("Side ram QA");
                preview.AddSingleGO(root);
                var road = root.AddComponent<EndlessVoxelRoad>();
                road.enabled = false;
                road.roadWidth = track.roadTuning.roadWidth;
                road.laneCount = track.roadTuning.laneCount;
                road.minimumCactiPerSegment = road.maximumCactiPerSegment = 0;
                road.turnChancePerSegment = 1f;
                road.minimumStraightSegmentsBetweenTurns = 0;
                road.BuildInitialRoad();
                var attack = settings.GetAttack<VoxelBossSpikeAttackTuning>();

                foreach (float side in new[] { -1f, 1f })
                {
                    var pair = new GameObject("Side ram cars");
                    pair.transform.SetParent(root.transform, false);
                    var playerRoot = new GameObject("Player QA");
                    playerRoot.transform.SetParent(pair.transform, false);
                    Object.Instantiate(Resources.Load<VoxelCarDefinition>("Cars/SpyCar2PlayerCar").visualPrefab, playerRoot.transform);
                    var player = playerRoot.AddComponent<VoxelCarController>();
                    player.enabled = false;
                    int outsideLane = side < 0 ? 1 : 2;
                    Set(player, "currentLane", outsideLane);
                    player.SetLaneLayout(4, laneWidth);
                    player.SetTrack(road, 110f);
                    Set(player, "<CurrentSpeed>k__BackingField", 30f);
                    Set(player, "nextDamageTime", float.PositiveInfinity);

                    var bossRoot = new GameObject("Vandito QA");
                    bossRoot.transform.SetParent(pair.transform, false);
                    var enemy = bossRoot.AddComponent<VoxelEnemyCar>();
                    enemy.ConfigureBoss(settings, laneWidth, 4);
                    Set(enemy, "bossLanePair", side < 0f ? 2 : 0);
                    enemy.Configure(player, track.obstacleCarTuning, tuning, road, 110f, 0f);
                    Set(enemy, "currentSpeed", 30f);
                    Call(enemy, "BeginSpikeAttack");
                    Set(enemy, "spikeAttackDodged", true);
                    Call(enemy, "SetSpikePhase", VoxelEnemyCar.SpikeAttackPhase.Holding);
                    enemy.GetComponentInChildren<VoxelBossSpikeRig>().SetPose(1f, 1f);
                    Call(player, "RequestLaneChange", outsideLane - (int)side);
                    Set(player, "visualYaw", -side * 6f);

                    var camera = preview.camera;
                    camera.nearClipPlane = .1f;
                    camera.farClipPlane = 200f;
                    camera.fieldOfView = preview.cameraFieldOfView = 55f;
                    var follow = camera.gameObject.AddComponent<VoxelCameraFollow>();
                    follow.target = player.transform;
                    follow.SetBossCameraTarget(enemy);
                    float dt = side < 0f ? 1f / 30f : 1f / 60f;

                    void Step(float seconds)
                    {
                        Set(player, "<TrackDistance>k__BackingField", player.TrackDistance + player.CurrentSpeed * seconds);
                        Call(player, "AdvanceLaneChange", seconds);
                        Call(player, "AdvanceBossSideRamRecovery", seconds);
                        Call(player, "ApplyTrackPose");
                        Call(enemy, "UpdateSpikeAttack", seconds);
                        Call(enemy, "UpdateRamResponse");
                        float speed = (float)Call(enemy, "AdvanceBossSpeed", seconds);
                        Set(enemy, "currentSpeed", speed);
                        Set(enemy, "trackDistance", enemy.TrackDistance + speed * seconds);
                        Call(enemy, "ApplyTrackPose");
                        Call(enemy, "ResolveSpikeBossContact");
                        Call(follow, "UpdateFollow", seconds);
                    }

                    void Steer(int direction)
                    {
                        if (Application.isPlaying) player.RequestLaneChangeDirection(direction);
                        else Call(player, "RequestLaneChange", (int)typeof(VoxelCarController).GetField("currentLane", Flags).GetValue(player) + direction);
                    }

                    void Capture(string name)
                    {
                        if (!render) return;
                        Directory.CreateDirectory("Temp/VanditoSideRam");
                        preview.BeginStaticPreview(new Rect(0, 0, 1000, 562));
                        preview.Render(true);
                        var image = preview.EndStaticPreview();
                        File.WriteAllBytes($"Temp/VanditoSideRam/{(side < 0 ? "Left" : "Right")}-{name}.png", image.EncodeToPNG());
                        Object.DestroyImmediate(image);
                    }

                    for (int i = 0; i < 25 && enemy.SpikePhase != VoxelEnemyCar.SpikeAttackPhase.SideRamImpact; i++) Step(dt);
                    Check(enemy.SpikePhase == VoxelEnemyCar.SpikeAttackPhase.SideRamImpact, "Real model steering did not reach Vandito's side");
                    Vector3 contact = (Vector3)typeof(VoxelEnemyCar).GetField("spikeSideImpactPoint", Flags).GetValue(enemy);
                    Vector3 localContact = enemy.transform.InverseTransformPoint(contact);
                    float contactDistance = float.PositiveInfinity;
                    foreach (var piece in player.GetComponentsInChildren<MeshRenderer>())
                        contactDistance = Mathf.Min(contactDistance, Vector3.Distance(contact, piece.bounds.ClosestPoint(contact)));
                    Check(contactDistance < .12f, $"The side impact floated {contactDistance:F2}m from the actual player mesh");
                    Vector3 pressed = enemy.transform.InverseTransformPoint(player.transform.position);
                    float health = enemy.CurrentHealth;
                    float impactGap = enemy.TrackDistance - player.CollisionTrackPosition.y;
                    float impactLane = player.TargetLaneOffset;
                    Steer(-(int)side);
                    Check(Mathf.Approximately(player.TargetLaneOffset, impactLane), "Recovery accepted steering towards the van");
                    ParticleSystem sparks = null;
                    if (Application.isPlaying)
                    {
                        foreach (var effect in enemy.GetComponentsInChildren<ParticleSystem>())
                            if (effect.name == "Boss Side Ram Sparks") sparks = effect;
                        Check(sparks != null && sparks.main.simulationSpace == ParticleSystemSimulationSpace.World,
                            "The actual side ram did not attach a world-space spark trail to the contact");
                        Check(road.transform.Find("Vehicle Ram Sparks") == null, "The Vandito counter spawned a second, smaller spark burst");
                    }
                    else if (render)
                    {
                        sparks = (ParticleSystem)typeof(VoxelSpikeImpact).GetMethod("PlaySideRamSparks", BindingFlags.Static | BindingFlags.NonPublic)
                            .Invoke(null, new object[] { enemy.transform, enemy.transform.TransformPoint(localContact), side, attack.sideRamSparkDuration, road });
                    }
                    if (sparks != null)
                    {
                        var main = sparks.main;
                        Check(Mathf.Approximately(main.duration, attack.sideRamSparkDuration) &&
                            Mathf.Approximately(main.startSize.constantMin, .08f) && Mathf.Approximately(main.startSize.constantMax, .23f) &&
                            Mathf.Approximately(main.startLifetime.constantMin, .16f) && Mathf.Approximately(main.startLifetime.constantMax, .34f),
                            "The side ram did not use the authored spark duration, size and lifetime");
                        Check(Mathf.Approximately(sparks.emission.rateOverTime.constant, 180f) &&
                            Mathf.Approximately(sparks.emission.GetBurst(0).count.constant, 32f) && sparks.shape.angle > 55f,
                            "The side ram did not use the denser, wider spark burst");
                        var plane = sparks.collision.GetPlane(0);
                        Check(sparks.collision.enabled && sparks.collision.type == ParticleSystemCollisionType.Planes &&
                            plane != null && plane.parent == road.transform && plane.GetComponent<Collider>() == null,
                            "The sparks did not use the shared collider-free road plane");
                    }
                    if (render && sparks != null)
                    {
                        sparks.Simulate(.13f, true, true, false);
                        Check(sparks.particleCount >= 40, "The denser impact spark trail was not visible");
                    }
                    Vector3 hitViewport = camera.WorldToViewportPoint(enemy.transform.TransformPoint(localContact));
                    Check(hitViewport.z > 0f && hitViewport.x > .25f && hitViewport.x < .75f &&
                        hitViewport.y > .15f && hitViewport.y < .85f, "Chase camera did not frame the side contact");
                    Check(side * (enemy.transform.InverseTransformPoint(camera.transform.position).x - pressed.x) > 1f,
                        "Camera did not reach the player's side before contact");
                    Capture("Contact");
                    Step(dt);
                    Check(enemy.TrackDistance - player.CollisionTrackPosition.y > impactGap + .005f,
                        "Vandito did not start pulling away on the first motion step after contact");
                    Steer((int)side);
                    float awayLane = player.TargetLaneOffset;
                    Check(Mathf.Approximately(awayLane - impactLane, side * laneWidth), "Recovery blocked the open lane away from Vandito");
                    Steer(-(int)side);
                    Check(Mathf.Approximately(player.TargetLaneOffset, awayLane), "Recovery accepted a reversal toward the van");
                    Step(attack.sideRamReboundDuration * .55f);
                    Check(enemy.SpikePhase == VoxelEnemyCar.SpikeAttackPhase.SideRamImpact &&
                        Mathf.Abs(enemy.transform.InverseTransformPoint(player.transform.position).x - pressed.x) > 1f,
                        "Gameplay-scale rebound did not open a visible gap");
                    var skidYaw = typeof(VoxelCarController).GetProperty("BossSideRamSkidYaw", Flags);
                    Check(side * (float)skidYaw.GetValue(player) > 3f, "Player did not visibly slide into the first recovery arc");
                    if (render && sparks != null) sparks.Simulate(.16f, true, false, false);
                    Capture("Rebound");
                    Step(.25f);
                    Check(enemy.SpikePhase == VoxelEnemyCar.SpikeAttackPhase.Retreating && enemy.CurrentHealth == health &&
                        Mathf.Abs(enemy.transform.InverseTransformPoint(player.transform.position).x - pressed.x) > 2.5f,
                        "Long-frame release missed the peak pose, repeated damage or left the cars joined");
                    Check(side * (float)skidYaw.GetValue(player) < 0f, "Player recovery did not counter-swing after the skid");
                    for (int i = 0; i < 60 && (bool)Call(enemy, "IsAlongsideForSideRamRecovery"); i++) Step(dt);
                    Steer(-(int)side);
                    Check(Mathf.Approximately(player.TargetLaneOffset, impactLane), "Steering did not return once Vandito cleared the player");
                    Step(.85f);
                    Check(Mathf.Approximately((float)skidYaw.GetValue(player), 0f), "Player skid did not settle");
                    if (sparks != null) CheckSparkBounce(sparks, enemy.transform.forward);
                    Object.DestroyImmediate(follow);
                    Object.DestroyImmediate(pair);
                }
                Debug.Log("PASS gameplay-scale Vandito side ram: mesh contact, immediate acceleration, skid, directional steering, clearance release and long-frame separation on both sides.");
            }
            finally
            {
                preview.Cleanup();
                Object.DestroyImmediate(tuning);
                Object.DestroyImmediate(settings);
                UnityEngine.Random.state = random;
            }
        }

        private static void CheckSparkBounce(ParticleSystem sparks, Vector3 forward)
        {
            Vector3 ground = sparks.collision.GetPlane(0).position;
            var main = sparks.main;
            main.stopAction = ParticleSystemStopAction.None;
            sparks.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            var emission = sparks.emission;
            emission.enabled = false;
            sparks.Play(false);
            sparks.Emit(new ParticleSystem.EmitParams
            {
                position = ground + Vector3.up * .2f,
                velocity = forward * 6f + Vector3.down * 8f,
                startSize = .1f,
                startLifetime = .34f
            }, 1);
            sparks.Simulate(.05f, false, false, false);
            var particles = new ParticleSystem.Particle[1];
            int count = sparks.GetParticles(particles);
            Check(count == 1 && particles[0].velocity.y > 0f &&
                Vector3.Dot(particles[0].position - ground, forward) > .1f,
                $"A downward test spark did not rebound and keep moving along the road (count {count}, position {particles[0].position - ground}, velocity {particles[0].velocity})");
        }

        public static void Render()
        {
            var preview=new PreviewRenderUtility();
            try
            {
                var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(VoxelRedVanBossBuilder.PrefabPath));
                root.GetComponent<VoxelBossSpikeRig>().SetPose(1,1);preview.AddSingleGO(root);
                preview.camera.transform.position=new Vector3(5,3.6f,-10);preview.camera.transform.LookAt(new Vector3(0,1,-.8f));
                preview.camera.fieldOfView=38;preview.camera.nearClipPlane=.1f;preview.camera.farClipPlane=50;
                preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.camera.backgroundColor=new Color(.17f,.2f,.23f);
                preview.lights[0].intensity=2;preview.lights[0].transform.rotation=Quaternion.Euler(40,155,0);
                preview.lights[1].intensity=1.3f;preview.lights[1].transform.rotation=Quaternion.Euler(30,20,0);
                preview.ambientColor=new Color(.5f,.5f,.5f);
                preview.BeginStaticPreview(new Rect(0,0,1200,800));preview.Render(true);
                var image=preview.EndStaticPreview();File.WriteAllBytes("Temp/VanditoBossSpikeAttack.png",image.EncodeToPNG());Object.DestroyImmediate(image);
            }
            finally {preview.Cleanup();}
        }
    }
}
