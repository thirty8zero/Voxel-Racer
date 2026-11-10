using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VoxelRacer.Editor
{
    public static class VoxelMineLayerValidation
    {
        [MenuItem("Tools/Voxel Racer/Validate Mine Layer")]
        public static void Run()
        {
            var tuning=Resources.Load<VoxelEnemyVehicleTuning>("EnemyVehicles/BI_MineLayerTuning");
            if(tuning==null || tuning.mineLayer==null || tuning.mineLayer.minePrefab==null) throw new Exception("Missing mine layer assets");
            if(tuning.modelPrefab.GetComponentsInChildren<Collider>().Length!=0) throw new Exception("Unexpected visual colliders");
            if(!VoxelRoadMine.CrossesMine(new Vector2(0,-20),new Vector2(0,20),Vector2.zero,Vector2.one)) throw new Exception("Fast crossing missed");
            if(VoxelRoadMine.CrossesMine(new Vector2(3,-20),new Vector2(3,20),Vector2.zero,Vector2.one)) throw new Exception("Adjacent lane false hit");
            if(VoxelRoadMine.CrossesMine(new Vector2(0,-20),new Vector2(0,-10),Vector2.zero,Vector2.one)) throw new Exception("Distant false hit");
            if(!VoxelRoadMine.CrossesMine(Vector2.zero,Vector2.zero,Vector2.zero,Vector2.one)) throw new Exception("Stationary overlap missed");
            var preview=new PreviewRenderUtility();
            try
            {
                preview.AddSingleGO(UnityEngine.Object.Instantiate(tuning.modelPrefab));
                preview.camera.transform.position=new Vector3(5.7f,3.4f,-7.5f);
                preview.camera.transform.LookAt(new Vector3(0,.65f,-.25f));
                preview.camera.fieldOfView=34; preview.camera.nearClipPlane=.1f; preview.camera.farClipPlane=50;
                preview.camera.clearFlags=CameraClearFlags.SolidColor; preview.camera.backgroundColor=new Color(.17f,.20f,.23f);
                preview.lights[0].intensity=1.8f; preview.lights[0].transform.rotation=Quaternion.Euler(40,160,0);
                preview.lights[1].intensity=1.3f; preview.lights[1].transform.rotation=Quaternion.Euler(30,20,0);
                preview.ambientColor=new Color(.45f,.45f,.45f);
                preview.BeginStaticPreview(new Rect(0,0,1280,800)); preview.Render(true);
                var image=preview.EndStaticPreview(); File.WriteAllBytes("Temp/BI_MineLayerEnemyCar.png",image.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(image);
            }
            finally { preview.Cleanup(); }
            RenderWarningLights();
            File.WriteAllText("Temp/MineLayerValidation.txt","PASS: asset references, no visual colliders, fast crossing, adjacent lane, distant exclusion, stationary overlap and warning flashes on traffic, Vandito and El Bicho Loco mines; previews rendered.");
            Debug.Log("Mine layer validation passed.");
        }

        public static void RenderWarningLights()
        {
            var settings = new[]
            {
                Resources.Load<VoxelMineLayerTuning>("EnemyVehicles/MineLayerAttackTuning"),
                Resources.Load<VoxelMineLayerTuning>("Bosses/VanditoBossMineTuning"),
                Resources.Load<VoxelMineLayerTuning>("Bosses/BichoLocoMineTuning")
            };
            var lit = Resources.Load<Material>("CarMaterials/CarBrakeLights");
            var dim = Resources.Load<Material>("Bosses/BichoLocoArt/LampOff");
            if (lit == null || dim == null || !lit.IsKeywordEnabled("_EMISSION") || dim.IsKeywordEnabled("_EMISSION"))
                throw new Exception("Missing lit or dim mine materials");

            var preview = new PreviewRenderUtility();
            var roadRoot = new GameObject("Temporary mine preview road");
            var floorMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            var random = UnityEngine.Random.state;
            try
            {
                VoxelRacerBootstrap.ReloadGeneratedMaterials();
                var road = roadRoot.AddComponent<EndlessVoxelRoad>();
                road.enabled = false;
                road.segmentCount = 3;
                road.turnChancePerSegment = 0f;
                road.minimumCactiPerSegment = road.maximumCactiPerSegment = 0;
                var player = roadRoot.AddComponent<VoxelCarController>();
                player.enabled = false;
                var flash = typeof(VoxelRoadMine).GetMethod("UpdateWarningLamp", BindingFlags.Instance | BindingFlags.NonPublic);
                var mines = new VoxelRoadMine[settings.Length];
                for (int i = 0; i < settings.Length; i++)
                {
                    var mineRoot = new GameObject("Dropped mine preview");
                    preview.AddSingleGO(mineRoot);
                    var mine = mineRoot.AddComponent<VoxelRoadMine>();
                    mine.Configure(player, road, settings[i], 10f, i == 0 ? -1.55f : .9f);
                    mines[i] = mine;
                    MeshRenderer lamp = null;
                    foreach (var renderer in mine.GetComponentsInChildren<MeshRenderer>())
                    {
                        if (renderer.name != "Armed indicator") continue;
                        if (lamp != null) throw new Exception(settings[i].name + " has more than one mine lamp");
                        lamp = renderer;
                    }
                    if (lamp == null || lamp.sharedMaterial != lit)
                        throw new Exception(settings[i].name + " did not start with a lit red lamp");
                    Vector3 lampOffset = mine.transform.InverseTransformPoint(lamp.transform.position);
                    if (Mathf.Abs(lampOffset.x) > .01f || Mathf.Abs(lampOffset.z) > .01f || lampOffset.y <= 0f)
                        throw new Exception(settings[i].name + " lamp is not at the top centre");
                    flash.Invoke(mine, new object[] { .23f });
                    if (lamp.sharedMaterial != lit) throw new Exception("Mine stopped flashing too soon");
                    flash.Invoke(mine, new object[] { .25f });
                    if (lamp.sharedMaterial != dim) throw new Exception("Mine stayed lit through the dim interval");
                    flash.Invoke(mine, new object[] { .59f });
                    if (lamp.sharedMaterial != dim) throw new Exception("Mine relit before its next flash");
                    flash.Invoke(mine, new object[] { .61f });
                    if (lamp.sharedMaterial != lit) throw new Exception("Mine did not flash again");
                }
                // Both boss tunings use the same authored visual.
                mines[2].gameObject.SetActive(false);
                var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                preview.AddSingleGO(floor);
                floor.transform.position = new Vector3(0f, -.045f, 10f);
                floor.transform.localScale = new Vector3(20f, .08f, 20f);
                floorMaterial.SetColor("_BaseColor", new Color(.14f, .17f, .21f));
                floor.GetComponent<MeshRenderer>().sharedMaterial = floorMaterial;

                var camera = preview.camera;
                camera.orthographic = true;
                camera.orthographicSize = 1.88f;
                camera.transform.position = new Vector3(.18f, 5.3f, 4.25f);
                camera.transform.LookAt(new Vector3(.18f, .16f, 10f));
                camera.nearClipPlane = .1f;
                camera.farClipPlane = 40f;
                preview.lights[0].intensity = 2.2f;
                preview.lights[0].transform.rotation = Quaternion.Euler(55f, -35f, 0f);
                preview.lights[1].intensity = .85f;
                preview.ambientColor = new Color(.49f, .49f, .49f);
                Directory.CreateDirectory("Temp/MineLights");
                Capture("Lit", 0f);
                Capture("Dim", .30f);

                void Capture(string name, float age)
                {
                    foreach (var mine in mines) flash.Invoke(mine, new object[] { age });
                    preview.BeginStaticPreview(new Rect(0f, 0f, 940f, 480f));
                    preview.Render(true);
                    var image = preview.EndStaticPreview();
                    File.WriteAllBytes("Temp/MineLights/" + name + ".png", image.EncodeToPNG());
                    Object.DestroyImmediate(image);
                }
                Debug.Log("PASS top-centre warning flash on traffic, Vandito and El Bicho Loco mines. Lit and dim preview frames saved.");
            }
            finally
            {
                preview.Cleanup();
                Object.DestroyImmediate(floorMaterial);
                Object.DestroyImmediate(roadRoot);
                UnityEngine.Random.state = random;
            }
        }
    }
}
