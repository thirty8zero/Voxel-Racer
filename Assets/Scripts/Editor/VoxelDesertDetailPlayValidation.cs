using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace VoxelRacer.Editor
{
    public static class VoxelDesertDetailPlayValidation
    {
        private const string Pending="VoxelRacer.DesertDetailPlayQA";
        private static GameObject root;
        private static EndlessVoxelRoad road;
        private static VoxelCarController player;
        private static Mesh[] oldMeshes;
        private static float started;
        private static int phase,clumps,visible,batches;
        [InitializeOnLoadMethod]
        private static void Hook(){EditorApplication.playModeStateChanged-=Changed;EditorApplication.playModeStateChanged+=Changed;}
        [MenuItem("Tools/Voxel Racer/Validate Desert Details in Play Mode")]
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Start in Edit Mode.");
            SessionState.SetBool(Pending+".Background",Application.runInBackground);SessionState.SetFloat(Pending+".Scale",Time.timeScale);
            SessionState.SetBool(Pending+".Restore",true);SessionState.SetBool(Pending,true);
            Application.runInBackground=true;EditorApplication.isPlaying=true;
        }
        private static void Changed(PlayModeStateChange state)
        {
            if(state==PlayModeStateChange.EnteredEditMode && SessionState.GetBool(Pending+".Restore",false))
            {
                Application.runInBackground=SessionState.GetBool(Pending+".Background",false);
                Time.timeScale=SessionState.GetFloat(Pending+".Scale",1);SessionState.SetBool(Pending+".Restore",false);
            }
            if(!SessionState.GetBool(Pending,false))return;
            if(state==PlayModeStateChange.EnteredPlayMode)EditorApplication.delayCall+=Setup;
            if(state==PlayModeStateChange.ExitingPlayMode)
            {EditorApplication.update-=Tick;SessionState.SetBool(Pending,false);root=null;road=null;player=null;oldMeshes=null;}
        }
        private static void Setup()
        {
            try
            {
                foreach(var go in SceneManager.GetActiveScene().GetRootGameObjects())go.SetActive(false);
                var scene=SceneManager.CreateScene("Temporary Desert Detail Live QA");SceneManager.SetActiveScene(scene);
                root=new GameObject("Temporary Desert Detail Live QA");VoxelRacerBootstrap.BuildPrototype(root.transform);
                player=root.GetComponentInChildren<VoxelCarController>();player.enabled=false;
                road=root.GetComponentInChildren<EndlessVoxelRoad>();
                foreach(var fade in root.GetComponentsInChildren<VoxelFadeIn>())
                {VoxelMissileValidation.Call(fade,"RestoreOpaqueMaterials");fade.enabled=false;}
                foreach(var countdown in Object.FindObjectsByType<VoxelStartCountdown>(FindObjectsSortMode.None))countdown.enabled=false;
                foreach(var t in root.GetComponentsInChildren<Transform>(true))if(t.name=="Race Opening Fade")t.gameObject.SetActive(false);
                VoxelMissionProgress.Active?.SetStartCountdown(null);
                foreach(var spawner in root.GetComponentsInChildren<VoxelObstacleSpawner>())spawner.enabled=false;
                foreach(var spawner in root.GetComponentsInChildren<VoxelRoadsideTurretSpawner>())spawner.enabled=false;
                Time.timeScale=0;phase=0;started=Time.realtimeSinceStartup;Directory.CreateDirectory("Temp/DesertDetails");
                EditorApplication.update+=Tick;
            }
            catch(Exception e){Finish("FAIL: "+e);}
        }
        private static void Tick()
        {
            if(!Application.isPlaying)return;
            try
            {
                if(Time.realtimeSinceStartup-started>20)throw new InvalidOperationException("Live scenery QA timeout");
                if(phase==0 && Time.realtimeSinceStartup-started>2)
                {
                    var covers=root.GetComponentsInChildren<VoxelDesertGroundCover>();clumps=covers.Sum(c=>c.ClumpCount);
                    if(covers.Length!=road.segmentCount || clumps<700)throw new InvalidOperationException("Missing live segment foliage");
                    oldMeshes=covers.Select(c=>c.GetComponent<MeshFilter>().sharedMesh).ToArray();
                    var distant=road.GetComponent<VoxelDistantScenery>();distant.Prepare(Camera.main);
                    visible=distant.VisiblePropCount;batches=distant.VisibleBatchCount;
                    if(visible<=0 || batches>40)throw new InvalidOperationException("Live instancing groups invalid");
                    ScreenCapture.CaptureScreenshot("Temp/DesertDetails/LiveRace.png");phase=1;started=Time.realtimeSinceStartup;
                }
                else if(phase==1 && Time.realtimeSinceStartup-started>1)
                {
                    VoxelMissileValidation.Set(player,"<TrackDistance>k__BackingField",160f);
                    VoxelMissileValidation.Call(player,"ApplyTrackPose");phase=2;started=Time.realtimeSinceStartup;
                }
                else if(phase==2 && Time.realtimeSinceStartup-started>1)
                {
                    var covers=root.GetComponentsInChildren<VoxelDesertGroundCover>();
                    if(covers.Length!=road.segmentCount || !oldMeshes.Any(m=>m==null))throw new InvalidOperationException("Recycling leaked or removed foliage batches");
                    if(!File.Exists("Temp/DesertDetails/LiveRace.png"))throw new InvalidOperationException("Live capture not written");
                    Finish("PASS (Play Mode): real race bootstrap/materials/camera/HUD, "+clumps+" close foliage clumps in "+road.segmentCount+
                        " segment renderers; "+visible+" visible distant props in "+batches+" mesh/material groups; player moved 160m, road chunks recycled with foliage, old combined meshes released and renderer count stayed bounded. Temporary scene removed on return to Edit Mode; original editor objects, time scale and background setting restored. Screenshot: LiveRace.png. Target-phone frame-time profiling remains unmeasured.");
                }
            }
            catch(Exception e){Finish("FAIL: "+e);}
        }
        private static void Finish(string report)
        {
            EditorApplication.update-=Tick;Directory.CreateDirectory("Temp/DesertDetails");File.WriteAllText("Temp/DesertDetails/PlayValidation.txt",report);
            if(report.StartsWith("FAIL"))Debug.LogError(report);else Debug.Log(report);
            EditorApplication.isPlaying=false;
        }
    }
}
