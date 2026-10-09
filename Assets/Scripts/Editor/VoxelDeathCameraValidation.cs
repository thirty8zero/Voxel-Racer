using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace VoxelRacer.Editor
{
    public static class VoxelDeathCameraValidation
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static void Call(VoxelCameraFollow follow, string method, params object[] args) =>
            typeof(VoxelCameraFollow).GetMethod(method, Private, null,
                Array.ConvertAll(args, argument => argument.GetType()), null).Invoke(follow, args);
        private static void Check(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }

        [MenuItem("Tools/Voxel Racer/Validate Death Camera")]
        public static void Run()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Run death camera validation in Edit Mode.");
            var root = new GameObject("Temporary death camera QA");
            var settings = Object.Instantiate(VoxelCameraTuning.Load());
            settings.playerVehicleImpactShakeDuration = settings.objectExplosionShakeDuration = 0;
            try
            {
                foreach (float yaw in new[] { 0f, 75f, -130f })
                foreach (bool alternateView in new[] { false, true })
                {
                    Vector3 previousHalfway = Vector3.zero;
                    foreach (int fps in new[] { 30, 60, 120 })
                    {
                        var carObject = new GameObject("Wreck"); carObject.transform.SetParent(root.transform);
                        var car = carObject.AddComponent<VoxelCarController>(); car.enabled = false;
                        var cameraObject = new GameObject("Camera"); cameraObject.transform.SetParent(root.transform);
                        var camera = cameraObject.AddComponent<Camera>();
                        var follow = cameraObject.AddComponent<VoxelCameraFollow>(); follow.enabled = false;
                        follow.target = car.transform; follow.tuning = settings;
                        var heading = Quaternion.Euler(0, yaw, 0);
                        var start = new Vector3(100, 0, 200);
                        car.transform.SetPositionAndRotation(start, heading);
                        Call(follow, "UpdateFollow", 1f / fps);
                        // Exercise an unfinished lane move and a surviving boost offset.
                        typeof(VoxelCarController).GetProperty("CurrentLaneOffset").SetValue(car, 2f);
                        typeof(VoxelCarController).GetField("boostForwardOffset", Private).SetValue(car, 7f);
                        Call(follow, "UpdateFollow", 1f / fps);
                        Check(Vector3.Distance(cameraObject.transform.position,
                            start + heading * (settings.chaseOffset - Vector3.right * 2 - Vector3.forward * 7)) < .001f,
                            "Normal chase lane/boost composition changed.");
                        if (alternateView)
                        {
                            cameraObject.transform.position = start + heading * settings.bossAttackCameraOffset;
                            cameraObject.transform.LookAt(start + heading * Vector3.forward * settings.chaseLookAhead);
                            Call(follow, "ApplyShake");
                        }
                        Vector3 cleanPosition = cameraObject.transform.position;
                        Quaternion cleanRotation = cameraObject.transform.rotation;
                        var steadyObject = new GameObject("Untumbled reference car"); steadyObject.transform.SetParent(root.transform);
                        var steadyCar = steadyObject.AddComponent<VoxelCarController>(); steadyCar.enabled = false;
                        steadyCar.transform.SetPositionAndRotation(start, heading);
                        var steadyCameraObject = new GameObject("Reference camera"); steadyCameraObject.transform.SetParent(root.transform);
                        var steadyCamera = steadyCameraObject.AddComponent<Camera>();
                        var steadyFollow = steadyCameraObject.AddComponent<VoxelCameraFollow>(); steadyFollow.enabled = false;
                        steadyFollow.target = steadyCar.transform; steadyFollow.tuning = settings;
                        steadyCameraObject.transform.SetPositionAndRotation(cleanPosition, cleanRotation);
                        typeof(VoxelCameraFollow).GetField("lastRoadHeading", Private).SetValue(steadyFollow, heading);
                        Call(steadyFollow, "ApplyShake");
                        typeof(VoxelCarController).GetProperty("IsDestroyed").SetValue(steadyCar, true);
                        Call(steadyFollow, "UpdateFollow", 0f);
                        // A shaken render pose must not become the permanent follow offset.
                        cameraObject.transform.position += Vector3.one;
                        cameraObject.transform.rotation *= Quaternion.Euler(4, 5, 6);
                        follow.BeginFinishSequence(car);
                        typeof(VoxelCarController).GetProperty("IsDestroyed").SetValue(car, true);
                        car.transform.rotation = Quaternion.Euler(80, yaw + 110, -45);
                        Call(follow, "UpdateFollow", 0f);
                        Check(Vector3.Distance(cameraObject.transform.position, cleanPosition) < .001f &&
                            Quaternion.Angle(cameraObject.transform.rotation, cleanRotation) < .05f,
                            "Death entry snapped or inherited shake/tumbling.");
                        Check(!(bool)typeof(VoxelCameraFollow).GetField("finishSequenceActive", Private).GetValue(follow),
                            "Finish orbit overrode death follow.");
                        Vector3 halfway = Vector3.zero;
                        for (int frame = 1; frame <= 4 * fps; frame++)
                        {
                            float time = Mathf.Min(2f, (float)frame / fps);
                            car.transform.position = start + heading * new Vector3(4 * time,
                                Mathf.Max(0, 10 * time - 5 * time * time), 8 * time);
                            car.transform.rotation = Quaternion.Euler(frame * 23, frame * 37, frame * -41);
                            steadyCar.transform.position = car.transform.position;
                            Vector3 before = cameraObject.transform.position;
                            Call(follow, "UpdateFollow", 1f / fps);
                            Call(steadyFollow, "UpdateFollow", 1f / fps);
                            Check(Vector3.Distance(cameraObject.transform.position, steadyCameraObject.transform.position) < .001f &&
                                Quaternion.Angle(cameraObject.transform.rotation, steadyCameraObject.transform.rotation) < .05f &&
                                Mathf.Abs(camera.fieldOfView - steadyCamera.fieldOfView) < .001f,
                                "Wreck tumbling changed the planned front-view swoop.");
                            Check(Vector3.Distance(before, cameraObject.transform.position) < 1.5f,
                                "Death camera jumped between frames.");
                            if (frame == fps) halfway = cameraObject.transform.position;
                        }
                        Check(follow.DeathSequenceComplete && Vector3.Distance(cameraObject.transform.position,
                            car.transform.position + heading * settings.finishOffset) < .002f &&
                            Mathf.Abs(camera.fieldOfView - settings.finishFieldOfView) < .001f,
                            "Camera did not finish in the shared front-view composition.");
                        var centre = camera.WorldToViewportPoint(car.WreckFocusPosition);
                        Check(Mathf.Abs(centre.x-.5f)<.001f && Mathf.Abs(centre.y-.5f)<.001f,
                            "Death camera must centre the wreck instead of using the successful-finish side offset.");
                        if (fps > 30) Check(Vector3.Distance(halfway, previousHalfway) < .15f,
                            "Death follow depends too strongly on frame rate.");
                        previousHalfway = halfway;
                        Object.DestroyImmediate(cameraObject); Object.DestroyImmediate(carObject);
                        Object.DestroyImmediate(steadyCameraObject); Object.DestroyImmediate(steadyObject);
                    }
                }
                CheckDeathScreen(root);
                Directory.CreateDirectory("Temp/DeathCamera");
                const string report = "PASS: normal lane/boost composition; smooth death entry; arbitrary wreck rotation; " +
                    "moving/airborne/resting wreck; stable view on curved headings and either boss camera side; " +
                    "shake isolation; shared front-view swoop/FOV; death priority over finish; 30/60/120 FPS; " +
                    "centred final wreck; immediate quarter-speed; real-time two-second restoration and physics cadence; " +
                    "resting-wreck/camera UI gate; opposite-edge UI slides; clock cleanup and boss-escape behavior; " +
                    "responsive 4:3/16:9/20:9 UI renders. Edit Mode simulation.";
                File.WriteAllText("Temp/DeathCamera/Validation.txt", report);
                Debug.Log(report);
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(settings); }
        }

        private static void CheckDeathScreen(GameObject root)
        {
            float savedScale = Time.timeScale;
            float savedStep = Time.fixedDeltaTime;
            var oldEvent = Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();
            var savedActive = VoxelPlayerDeathScreen.Active;
            bool savedShowing = VoxelPlayerDeathScreen.IsShowing;
            var test = new GameObject("Death screen QA"); test.transform.SetParent(root.transform);
            try
            {
                Time.timeScale = 1f;
                var car = test.AddComponent<VoxelCarController>(); car.enabled = false;
                typeof(VoxelCarController).GetProperty("IsDestroyed").SetValue(car, true);
                var screen = test.AddComponent<VoxelPlayerDeathScreen>(); screen.Configure(car);
                typeof(VoxelPlayerDeathScreen).GetMethod("OnEnable",Private).Invoke(screen,null);
                screen.BeginPlayerDeath(car);
                Check(Time.timeScale == .25f && Mathf.Abs(Time.fixedDeltaTime-savedStep*.25f)<.000001f && VoxelPlayerDeathScreen.IsShowing,
                    "Death must slow immediately and gate gameplay HUD before revealing UI: scale="+Time.timeScale+", step="+Time.fixedDeltaTime+", showing="+VoxelPlayerDeathScreen.IsShowing);
                void Tick(float seconds) => typeof(VoxelPlayerDeathScreen).GetMethod("TickDeathSequence", Private)
                    .Invoke(screen, new object[] { seconds });
                Tick(1f);
                Check(Time.timeScale == .25f && test.transform.Find("Mission Failed UI") == null,
                    "Slowdown ended or UI appeared before the wreck rested.");
                Tick(1f);
                Check(Time.timeScale == 1f && Mathf.Approximately(Time.fixedDeltaTime,savedStep) && test.transform.Find("Mission Failed UI") == null,
                    "Slowdown must last two real seconds; zero driving speed is not a settled wreck.");
                var cameraObject = new GameObject("Death UI camera gate"); cameraObject.transform.SetParent(test.transform);
                var follow = cameraObject.AddComponent<VoxelCameraFollow>(); follow.enabled = false;
                typeof(VoxelPlayerDeathScreen).GetField("deathCamera", Private).SetValue(screen, follow);
                typeof(VoxelCarController).GetField("wreckResting", Private).SetValue(car, true);
                Tick(0f);
                Check(test.transform.Find("Mission Failed UI") == null, "UI interrupted the front-view swoop.");
                typeof(VoxelCameraFollow).GetProperty("DeathSequenceComplete").SetValue(follow, true);
                Tick(0f);
                var group = test.transform.Find("Mission Failed UI").GetComponent<CanvasGroup>();
                var header = (RectTransform)group.transform.Find("Failure Header");
                var restart = (RectTransform)group.transform.Find("Failure Restart");
                Check(group.alpha == 0f && !group.interactable && header.anchoredPosition.y>0 && restart.anchoredPosition.y<0,
                    "Failure UI must enter from opposite screen edges after settling.");
                Tick(.3f);
                Check(header.anchoredPosition.y<270f && restart.anchoredPosition.y>-210f && !group.interactable,
                    "Failure UI did not slide, or accepted input during its reveal.");
                Tick(.3f);
                Check(group.alpha == 1f && group.interactable && header.anchoredPosition.y==-28f && restart.anchoredPosition.y==32f,
                    "Failure UI never reached its top/bottom resting layout.");
                RenderDeathUi(test,group);
                typeof(VoxelPlayerDeathScreen).GetMethod("OnDisable",Private).Invoke(screen,null);
                Object.DestroyImmediate(screen);
                Object.DestroyImmediate(group.gameObject);

                screen = test.AddComponent<VoxelPlayerDeathScreen>(); screen.Configure(car);
                typeof(VoxelPlayerDeathScreen).GetMethod("OnEnable",Private).Invoke(screen,null);
                screen.BeginPlayerDeath(car);
                typeof(VoxelPlayerDeathScreen).GetMethod("OnDisable",Private).Invoke(screen,null); screen.enabled = false;
                Check(Time.timeScale == 1f && Mathf.Approximately(Time.fixedDeltaTime,savedStep) && !VoxelPlayerDeathScreen.IsShowing,
                    "Interrupted death leaked slow motion, physics cadence or HUD gating.");
                Object.DestroyImmediate(screen);
                screen = test.AddComponent<VoxelPlayerDeathScreen>(); screen.Configure(car);
                typeof(VoxelPlayerDeathScreen).GetMethod("OnEnable",Private).Invoke(screen,null);
                screen.BeginPlayerDeath(car); Time.timeScale = 0f;
                typeof(VoxelPlayerDeathScreen).GetMethod("OnDisable",Private).Invoke(screen,null); screen.enabled = false;
                Check(Time.timeScale == 0f, "Slow-motion cleanup overwrote another system's pause.");
                Object.DestroyImmediate(screen);

                Time.timeScale = 1f;
                typeof(VoxelCarController).GetProperty("IsDestroyed").SetValue(car, false);
                screen = test.AddComponent<VoxelPlayerDeathScreen>(); screen.Configure(car);
                typeof(VoxelPlayerDeathScreen).GetMethod("OnEnable",Private).Invoke(screen,null);
                screen.ShowBossEscaped(); Tick(.35f);
                Check(Time.timeScale == 0f && test.transform.Find("Mission Failed UI") != null,
                    "Boss escape must retain its immediate failure flow.");
                typeof(VoxelPlayerDeathScreen).GetMethod("OnDisable",Private).Invoke(screen,null); screen.enabled = false;
                Check(Time.timeScale == 1f, "Boss escape pause leaked on cleanup.");
            }
            finally
            {
                Object.DestroyImmediate(test);
                Time.timeScale = savedScale;
                Time.fixedDeltaTime = savedStep;
                typeof(VoxelPlayerDeathScreen).GetProperty("Active").SetValue(null, savedActive);
                typeof(VoxelPlayerDeathScreen).GetProperty("IsShowing").SetValue(null, savedShowing);
                if(oldEvent==null)
                {
                    var created=Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();
                    if(created!=null) Object.DestroyImmediate(created.gameObject);
                }
            }
        }

        private const string PhysicsPending="VoxelRacer.WreckPhysicsValidation";

        [InitializeOnLoadMethod]
        private static void HookPhysicsValidation()
        {
            EditorApplication.playModeStateChanged-=PhysicsPlayChanged;
            EditorApplication.playModeStateChanged+=PhysicsPlayChanged;
        }

        [MenuItem("Tools/Voxel Racer/Validate Wreck Physics in Play Mode")]
        public static void RunWreckPhysics()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Start in Edit Mode.");
            SessionState.SetBool(PhysicsPending,true);
            EditorApplication.isPlaying=true;
        }

        private static void PhysicsPlayChanged(PlayModeStateChange state)
        {
            if(state!=PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(PhysicsPending,false)) return;
            SessionState.EraseBool(PhysicsPending);
            EditorApplication.delayCall+=RunPhysicsAndExit;
        }

        private static void RunPhysicsAndExit()
        {
            var settings=Object.Instantiate(VoxelCameraTuning.Load());
            settings.playerVehicleImpactShakeDuration=settings.objectExplosionShakeDuration=0;
            Directory.CreateDirectory("Temp/DeathCamera");
            void Finish(string report)
            {
                File.WriteAllText("Temp/DeathCamera/WreckPhysics.txt",report);
                if(report.StartsWith("PASS")) Debug.Log(report);else Debug.LogError(report);
                Object.DestroyImmediate(settings);EditorApplication.isPlaying=false;
            }
            try
            {
                var released=CheckWreckPhysics(settings);
                double deadline=EditorApplication.timeSinceStartup+3;
                void WaitForCleanup()
                {
                    if(!Array.TrueForAll(released,item=>item==null) && EditorApplication.timeSinceStartup<deadline) return;
                    EditorApplication.update-=WaitForCleanup;
                    Finish(Array.TrueForAll(released,item=>item==null)
                        ? "PASS (isolated Play Mode PhysX simulation): one dynamic hull/body, upright/side/roof contacts at three headings, no ground penetration, continued impact/skid motion, stable eventual rest, centred wreck camera, unrelated bodies excluded, and deferred ground/material cleanup. Original scene returns to Edit Mode."
                        : "FAIL: Wreck ground/material cleanup did not finish.");
                }
                EditorApplication.update+=WaitForCleanup;
            }
            catch(Exception exception) {Finish("FAIL: "+exception);}
        }

        private static Object[] CheckWreckPhysics(VoxelCameraTuning settings)
        {
            var scene=SceneManager.CreateScene("Isolated wreck physics QA",new CreateSceneParameters(LocalPhysicsMode.Physics3D));
            var physics=scene.GetPhysicsScene();
            var random=UnityEngine.Random.state;
            var released=new List<Object>();
            try
            {
                var definition=Resources.Load<VoxelCarDefinition>("Cars/SpyCar2PlayerCar");
                foreach(float roll in new[]{0f,90f,170f})
                foreach(float yaw in new[]{0f,75f,-130f})
                {
                    var root=new GameObject("Physics wreck QA");SceneManager.MoveGameObjectToScene(root,scene);
                    Object.Instantiate(definition.visualPrefab,root.transform);
                    var car=root.AddComponent<VoxelCarController>();car.enabled=false;
                    car.transform.rotation=Quaternion.Euler(0,yaw,0);
                    typeof(VoxelCarController).GetProperty("IsDestroyed").SetValue(car,true);
                    typeof(VoxelCarController).GetMethod("BeginDestroyedWreck",Private).Invoke(car,new object[]{car.transform.forward});
                    var body=car.GetComponent<Rigidbody>();
                    var collider=car.GetComponentInChildren<BoxCollider>();
                    Check(root.GetComponentsInChildren<Collider>().Length==1 && root.GetComponentsInChildren<Rigidbody>().Length==1,
                        "Wreck physics must use one hull/body rather than per-voxel collisions.");
                    Check(body.interpolation==RigidbodyInterpolation.Interpolate,"Wreck rendering must interpolate physics poses.");
                    // Manual offline steps render the solved poses directly; normal gameplay retains interpolation.
                    body.interpolation=RigidbodyInterpolation.None;
                    body.position=new Vector3(0,2,0);body.rotation=Quaternion.Euler(0,yaw,roll);
                    var cameraObject=new GameObject("Physics wreck camera");SceneManager.MoveGameObjectToScene(cameraObject,scene);
                    var camera=cameraObject.AddComponent<Camera>();var follow=cameraObject.AddComponent<VoxelCameraFollow>();follow.enabled=false;
                    follow.target=car.transform;follow.tuning=settings;
                    camera.transform.position=body.position+Quaternion.Euler(0,yaw,0)*settings.chaseOffset;
                    camera.transform.LookAt(car.WreckFocusPosition);
                    follow.BeginDeathSequence(car);
                    var unrelated=new GameObject("Unrelated falling body");SceneManager.MoveGameObjectToScene(unrelated,scene);
                    unrelated.transform.position=new Vector3(50,2,0);unrelated.AddComponent<BoxCollider>();var unrelatedBody=unrelated.AddComponent<Rigidbody>();
                    bool contacted=false, movingAfterContact=false;
                    for(int frame=0;frame<900;frame++)
                    {
                        physics.Simulate(.02f);
                        typeof(VoxelCarController).GetMethod("TickDestroyedWreck",Private).Invoke(car,new object[]{.02f});
                        Call(follow,"UpdateFollow",.02f);
                        Check(collider.bounds.min.y>-.08f,"Rotating hull penetrated the ground: roll="+roll+", yaw="+yaw+", frame="+frame+", minY="+collider.bounds.min.y+", body="+body.position+", transform="+car.transform.position);
                        if(collider.bounds.min.y<.06f) contacted=true;
                        if(contacted && body.linearVelocity.sqrMagnitude>.25f) movingAfterContact=true;
                    }
                    Check(contacted && movingAfterContact && car.IsWreckResting,"Wreck must collide, continue bouncing/skidding and eventually settle.");
                    Check(unrelatedBody.position.y < -100f,"Wreck ground caught unrelated traffic/debris.");
                    Check(follow.DeathSequenceComplete,"Camera did not settle after the physics wreck.");
                    var viewport=camera.WorldToViewportPoint(car.WreckFocusPosition);
                    Check(Mathf.Abs(viewport.x-.5f)<.003f && Mathf.Abs(viewport.y-.5f)<.003f,"Real rotated wreck is not centred.");
                    Vector3 settled=body.position;Quaternion settledRotation=body.rotation;
                    for(int frame=0;frame<100;frame++) physics.Simulate(.02f);
                    Check(Vector3.Distance(settled,body.position)<.002f && Quaternion.Angle(settledRotation,body.rotation)<.05f,"Settled wreck continued jittering.");
                    var ground=(GameObject)typeof(VoxelCarController).GetField("wreckGround",Private).GetValue(car);
                    var material=(PhysicsMaterial)typeof(VoxelCarController).GetField("wreckMaterial",Private).GetValue(car);
                    released.Add(ground);released.Add(material);
                    Object.DestroyImmediate(root);Object.DestroyImmediate(cameraObject);Object.DestroyImmediate(unrelated);
                    Check(ground==null || !ground.activeSelf,"Wreck ground remained active while awaiting deferred destruction.");
                }
            }
            finally
            {
                UnityEngine.Random.state=random;SceneManager.UnloadSceneAsync(scene);
            }
            return released.ToArray();
        }

        private static void RenderDeathUi(GameObject test,CanvasGroup group)
        {
            var model=Object.Instantiate(Resources.Load<VoxelCarDefinition>("Cars/SpyCar2PlayerCar").visualPrefab,test.transform);
            foreach(var behaviour in model.GetComponentsInChildren<MonoBehaviour>()) behaviour.enabled=false;
            foreach(var renderer in model.GetComponentsInChildren<MeshRenderer>())
                if(renderer.GetComponentInParent<VoxelIndestructiblePart>()==null) renderer.enabled=false;
            model.transform.rotation=Quaternion.Euler(0,20,90);
            model.transform.position=Vector3.up*1.1f;
            var cameraObject=new GameObject("Death UI render camera");cameraObject.transform.SetParent(test.transform,false);
            var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;
            var bounds=new Bounds();bool found=false;
            foreach(var renderer in model.GetComponentsInChildren<MeshRenderer>()) if(renderer.enabled)
            {if(!found){bounds=renderer.bounds;found=true;}else bounds.Encapsulate(renderer.bounds);}
            camera.transform.position=bounds.center+new Vector3(-4.2f,2.5f,5.5f);camera.transform.LookAt(bounds.center);
            camera.fieldOfView=42;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.16f,.20f,.24f);camera.cullingMask=1<<31;
            var sun=new GameObject("Death UI render light");sun.transform.SetParent(test.transform,false);
            var light=sun.AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.5f;light.cullingMask=1<<31;sun.transform.rotation=Quaternion.Euler(35,120,0);
            foreach(var transform in test.GetComponentsInChildren<Transform>()) transform.gameObject.layer=31;
            var canvas=group.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1f;
            Directory.CreateDirectory("Temp/DeathCamera");
            foreach(int width in new[]{1440,1920,2400})
            {
                var texture=new Texture2D(width,1080,TextureFormat.RGB24,false);var rt=new RenderTexture(width,1080,24);
                var previous=RenderTexture.active;
                try
                {
                    camera.targetTexture=rt;Canvas.ForceUpdateCanvases();camera.Render();
                    // Runtime Update refits the title backdrop when the canvas changes size.
                    typeof(VoxelPlayerDeathScreen).GetMethod("TickDeathSequence",Private)
                        .Invoke(test.GetComponent<VoxelPlayerDeathScreen>(),new object[]{0f});
                    Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=rt;
                    texture.ReadPixels(new Rect(0,0,width,1080),0,0);texture.Apply();
                    File.WriteAllBytes("Temp/DeathCamera/FailureUI"+width+".png",texture.EncodeToPNG());
                    var title=group.GetComponentsInChildren<Text>()[0];
                    Check(!string.IsNullOrEmpty(title.text),"Failure title disappeared.");
                    var textMesh=title.canvasRenderer.GetMesh();
                    {
                        Check(textMesh.vertexCount>0,"Failure title has no rendered glyphs.");
                        var header=(RectTransform)group.transform.Find("Failure Header");
                        foreach(var vertex in textMesh.vertices)
                        {
                            var point=header.InverseTransformPoint(title.transform.TransformPoint(vertex));
                            Check(point.x>=header.rect.xMin+11f && point.x<=header.rect.xMax-11f &&
                                point.y>=header.rect.yMin+11f && point.y<=header.rect.yMax-11f,
                                "Failure backdrop does not cover every rendered glyph with padding at width "+width);
                        }
                        Check(Mathf.Approximately(header.GetComponent<Image>().color.a,.9125f),"Failure backdrop is not 50% darker.");
                    }
                }
                finally {camera.targetTexture=null;RenderTexture.active=previous;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(texture);}
            }
            Object.DestroyImmediate(model);Object.DestroyImmediate(cameraObject);Object.DestroyImmediate(sun);
        }
    }
}
