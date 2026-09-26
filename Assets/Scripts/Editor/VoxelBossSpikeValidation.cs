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
                Set(enemy,"trackDistance",0f);Call(enemy,"KeepSpikeBodiesSeparated");
                Check(enemy.TrackDistance>=9f,"Retreat does not preserve body clearance");
                Debug.Log("PASS spike attack: warning, braking, hold, 110m retreat, doors/spikes, mine suppression/resume, stopped-player timeout and swept lane dodge.");
            }
            finally {Object.DestroyImmediate(root);Object.DestroyImmediate(playerObject);Object.DestroyImmediate(tuning.mineLayer);Object.DestroyImmediate(tuning);Object.DestroyImmediate(traffic);if(settings!=null)Object.DestroyImmediate(settings);if(spikeAttack!=null)Object.DestroyImmediate(spikeAttack);UnityEngine.Random.state=random;}
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
