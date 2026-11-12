using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace VoxelRacer.Editor
{
    public static class VoxelCarrierTruckBuilder
    {
        private const string Folder = "Assets/Prefabs/Vehicles/CarrierTruck";
        private const string PrefabPath = Folder + "/CarrierTruck.prefab";
        private const string ScenePath = "Assets/Scenes/CarrierTruckTest.unity";
        private const string PreviewFolder = "Temp/CarrierTruck";
        private const string Menu = "Tools/Voxel Racer/Carrier Truck/";

        [MenuItem(Menu + "Open Driving Test")]
        public static void OpenTest()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem(Menu + "Build Truck and Test Scene")]
        public static void Build()
        {
            if (Application.isPlaying)
                throw new InvalidOperationException("Build the carrier in Edit Mode.");
            Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();
            var definition = Resources.Load<VoxelCarDefinition>("Cars/SpyCar2PlayerCar");
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                var car = (GameObject)PrefabUtility.InstantiatePrefab(definition.visualPrefab, scene);
                var bounds = Measure(car.transform);
                var wheels = car.GetComponentsInChildren<Transform>().Where(t => t.name == "Voxel Wheel").ToArray();
                if (wheels.Length != 4)
                    throw new InvalidOperationException("Expected the four original Spy Car 2 wheel pivots.");
                float rear = wheels.Min(w => w.position.z);
                float front = wheels.Max(w => w.position.z);
                var tyres = Measure(wheels.OrderBy(w => w.position.z).First());
                float ground = tyres.min.y;
                float radius = tyres.extents.y;
                Debug.Log($"Spy Car 2 bounds: {bounds}; axles {rear:F3}/{front:F3}; tyre bottom {ground:F3}, radius {radius:F3}.");

                var model = BuildTruck();
                var prefab = PrefabUtility.SaveAsPrefabAsset(model.gameObject, PrefabPath);
                Object.DestroyImmediate(model.gameObject);
                var truck = ((GameObject)PrefabUtility.InstantiatePrefab(prefab, scene)).GetComponent<VoxelCarrierTruck>();
                Camera camera = BuildTestLot();
                var drive = new GameObject("Carrier driving test").AddComponent<VoxelCarrierDrive>();
                drive.Configure(truck, car.transform, camera, bounds, new Vector2(rear, front), ground, wheels, radius);
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                SceneManager.SetActiveScene(previous);
            }
        }

        private static VoxelCarrierTruck BuildTruck()
        {
            const float deck = 1.08f;
            const float middleLength = 2.16f;
            const float tipLength = 1.70f;
            const float cabLengthScale = .74f;
            const float cabExtension = .27f;
            // The fully upgraded Spy Car 2 measures 3.427 m across the spikes and 6.092 m with the plough.
            const float clearWidth = 3.66f;
            const float clearHeight = 2.41f;
            const float length = 6.45f;
            const float cabOuter = 1.655f;
            float rear = -length * .5f;
            float front = -rear;
            float half = clearWidth * .5f;
            float outer = half + .115f;
            float roof = deck + clearHeight;
            float hood = front + 2.55f;
            float bumper = hood + .43f;
            float cab = cabOuter - .16f;
            float rearAxle = rear + 1.38f;
            float CabZ(float z)
            {
                float offset = z - front;
                return front + offset * cabLengthScale + cabExtension * Mathf.Clamp01(offset / 1.10f);
            }

            float frontAxle = CabZ(hood - .53f);
            float rearWheelOffset = outer - .20f;
            float frontWheelOffset = cabOuter - .20f;
            const float glassBottom = 1.91f;
            const float glassTop = 2.51f;
            float glassBottomZ = CabZ(front + 1.12f);
            float glassTopZ = glassBottomZ - .23f;
            float cabRear = CabZ(front + .025f);
            float cabRoofFront = glassTopZ + .093f;
            const float stepInset = .24f;
            const float stepWidth = .49f;
            float tankRadius = (TankHalfRows.Max() + .5f) * FuelTankCell;
            float tankX = cabOuter - stepInset + stepWidth * .5f - tankRadius;
            float tankZ = CabZ(front - .055f) - .61f;

            Material white = Material("Porcelain White", 0xF1F3F4, .06f, .43f);
            Material panel = Material("Cool White", 0xE1E7EB, .05f, .35f);
            Material steel = Material("Satin Silver", 0xA7B5C3, .32f, .47f);
            Material tread = Material("Ramp and Deck", 0x4C5864, .12f, .33f);
            Material black = Material("Chassis Rubber", 0x242C34, 0f, .24f);
            Material rubberLug = Material("Tyre Tread Blocks", 0x323D48, 0f, .18f);
            Material glass = Material("Cab Glass", 0x30485D, .17f, .53f);
            Material blue = Material("Carrier Blue", 0x49789E, .14f, .42f);
            Material whiteLamp = Material("Cool Lamp Lens", 0xD4F3FD, .05f, .39f, .42f);
            Material red = Material("Red Lamp Lens", 0xC32836, .07f, .41f, .3f);
            Material amber = Material("Amber Lamp Lens", 0xEEA344, .07f, .41f, .25f);

            var truck = new GameObject("Carrier Truck").AddComponent<VoxelCarrierTruck>();
            var body = new Blocks(truck.transform, "Truck body");
            // Stretch the cab ahead of the box; translate the bonnet and front axle together.
            void CabBlock(Material material, float x, float y, float z, float w, float h, float d)
            {
                float back = CabZ(z - d * .5f);
                float nose = CabZ(z + d * .5f);
                body.Box(material, x, y, (back + nose) * .5f, w, h, nose - back);
            }

            body.Box(black, 0, .70f, .81f + cabExtension * .5f, half * 1.16f, .27f, length + 1.43f + cabExtension);
            for (int side = -1; side <= 1; side += 2)
                body.Box(tread, side * .57f, .875f, -.02f, .19f, .23f, length - .25f);
            body.Box(tread, 0, deck - .063f, 0, clearWidth, .126f, length);
            body.Box(steel, 0, deck - .037f, rear + .045f, clearWidth, .074f, .09f);
            body.Box(white, 0, roof + .065f, 0, outer * 2f, .13f, length + .08f);
            body.Box(panel, 0, deck + clearHeight * .5f, front + .057f, clearWidth, clearHeight, .114f);
            BuildDriveline(body, black, tread, steel, rearWheelOffset, frontWheelOffset, rearAxle, frontAxle);

            // The bumper ends below the lamps are welded to a transverse bar and the chassis rails.
            body.Box(tread, 0, deck - .29f, rear + .215f, (outer - .028f) * 2f, .16f, .28f);
            body.Box(steel, 0, deck - .26f, rear + .064f, (outer - .17f) * 2f, .061f, .027f);

            for (int side = -1; side <= 1; side += 2)
            {
                float wall = side * (half + .048f);
                float rim = side * (outer - .023f);
                body.Box(white, wall, deck + clearHeight * .5f, 0, .096f, clearHeight, length);
                body.Box(steel, rim, deck + .025f, 0, .046f, .16f, length + .15f);
                body.Box(steel, rim, roof - .025f, 0, .046f, .12f, length + .14f);
                body.Box(steel, rim, deck + clearHeight * .5f, rear - .018f, .046f, clearHeight, .13f);
                body.Box(steel, rim, deck + clearHeight * .5f, front, .046f, clearHeight, .11f);
                body.Box(panel, side * (outer - .017f), deck + .32f, .02f, .018f, .37f, length - .27f);
                body.Box(blue, side * (outer - .006f), deck + .39f, .24f, .013f, .054f, length - .76f);
                body.Box(steel, side * (outer - .006f), deck + .48f, rear + .49f, .013f, .024f, .65f);
                for (int section = 1; section <= 5; section++)
                    body.Box(panel, side * (outer - .019f), deck + 1.22f,
                        rear + section * length / 6f, .013f, 1.27f, .040f);
                for (int end = -1; end <= 1; end += 2)
                {
                    body.Box(panel, rim, end < 0 ? deck + .15f : roof - .15f,
                        rear + .035f, .056f, .19f, .25f);
                    body.Box(panel, rim, end < 0 ? deck + .15f : roof - .15f,
                        front - .035f, .056f, .19f, .25f);
                }
                body.Box(whiteLamp, side * (half + .012f), roof - .18f, -.02f, .024f, .036f, length - .50f);
                body.Box(steel, side * (half + .009f), deck + .25f, 0, .018f, .064f, length - .28f);
                for (int rib = 1; rib < 6; rib++)
                    body.Box(panel, side * (half + .005f), deck + clearHeight * .53f,
                        rear + rib * length / 6f, .01f, clearHeight * .74f, .041f);

                body.Box(amber, rim, roof - .017f, rear + .24f, .048f, .046f, .14f);
                body.Box(amber, rim, roof - .017f, front - .24f, .048f, .046f, .14f);
                body.Box(red, rim, deck + .18f, rear - .086f, .095f, .13f, .046f);
                body.Box(red, rim, deck + .35f, rear - .086f, .095f, .13f, .046f);
                body.Box(black, rim, deck - .27f, rear + .20f, .22f, .25f, .41f);
                body.Box(tread, side * (outer - .075f), deck - .117f, rear + .18f, .125f, .27f, .24f);
                body.Box(black, side * .57f, deck - .19f, rear + .24f, .19f, .22f, .43f);
                CabBlock(black, side * (cabOuter - .26f), .45f, front + .43f, .45f, .13f, .97f);
                CabBlock(steel, side * (cabOuter - stepInset), .52f, front + .43f, stepWidth, .071f, .94f);
                CabBlock(black, side * (cabOuter - .24f), .66f, front + .43f, .45f, .10f, .90f);
                CabBlock(steel, side * (cabOuter - stepInset), .72f, front + .43f, stepWidth, .066f, .94f);

                float rearWheelX = side * rearWheelOffset;
                float frontWheelX = side * frontWheelOffset;
                Wheel(body, black, rubberLug, tread, steel, rearWheelX, rearAxle, side);
                Wheel(body, black, rubberLug, tread, steel, side * (rearWheelOffset - .38f), rearAxle, -side);
                Wheel(body, black, rubberLug, tread, steel, frontWheelX, frontAxle, side);
                body.Box(black, side * (rearWheelOffset - .19f), .50f, rearAxle - .60f, .74f, .82f, .052f);
                body.Box(steel, rearWheelX, deck + .018f, rearAxle, .46f, .064f, 1.21f);
                body.Box(panel, frontWheelX, 1.153f, frontAxle, .49f, .12f, .99f);
                body.Box(white, frontWheelX, 1.247f, frontAxle, .43f, .085f, .79f);
                body.Box(panel, frontWheelX, 1.044f, frontAxle + .487f, .46f, .105f, .20f);
                body.Box(panel, frontWheelX, 1.044f, frontAxle - .487f, .46f, .105f, .20f);
                CabBlock(whiteLamp, side * (cab - .11f), 1.22f, hood + .117f, .29f, .18f, .076f);
                CabBlock(amber, side * (cab - .11f), 1.075f, hood + .113f, .29f, .063f, .065f);
                CabBlock(steel, side * (cab + .053f), 2.08f, front + 1.03f, .145f, .058f, .053f);
                CabBlock(black, side * (cabOuter - .053f), 2.19f, front + 1.03f, .088f, .29f, .16f);
                CabBlock(steel, side * (cabOuter - .054f), 2.19f, front + .94f, .072f, .244f, .024f);

                CabBlock(panel, side * cab, 1.35f, front + .57f, .08f, .78f, .93f);
                CabBlock(steel, side * (cab + .048f), 1.69f, front + .32f, .026f, .049f, .19f);
                Vector3[] SideGlass(float x) => new[]
                {
                    new Vector3(x, glassBottom - .03f, cabRear + .065f),
                    new Vector3(x, glassBottom - .03f, glassBottomZ - .028f),
                    new Vector3(x, glassTop - .022f, glassTopZ - .025f),
                    new Vector3(x, glassTop - .022f, cabRear + .065f)
                };
                body.Loft(glass, SideGlass(side * (cab - .018f)), SideGlass(side * (cab + .036f)));
                body.Box(white, side * cab, 2.20f, cabRear + .050f, .093f, .66f, .10f);
                body.Box(blue, side * (cab + .04f), glassBottom + .017f, (cabRear + glassBottomZ) * .5f,
                    .012f, .023f, glassBottomZ - cabRear - .19f);
                StorageChest(body, black, steel, panel, outer, rearAxle + 1.10f, side);
                FuelTank(body, steel, tread, black, side * tankX, tankZ, side);
            }

            CabBlock(white, 0, 1.29f, front + .61f, cab * 2f, 1.11f, 1.15f);
            body.Box(panel, 0, 2.18f, (cabRear + glassTopZ) * .5f - .04f,
                (cab - .063f) * 2f, .64f, glassTopZ - cabRear - .08f);
            body.Box(white, 0, 2.555f, (cabRear + cabRoofFront) * .5f,
                cab * 2f + .12f, .15f, cabRoofFront - cabRear);
            body.Box(panel, 0, 2.636f, (cabRear + cabRoofFront) * .5f - .018f,
                cab * 2f - .13f, .052f, cabRoofFront - cabRear - .098f);

            float glassRise = glassTop - glassBottom;
            float glassSetback = glassBottomZ - glassTopZ;
            float glassLength = new Vector2(glassRise, glassSetback).magnitude;
            float rake = -Mathf.Atan2(glassSetback, glassRise) * Mathf.Rad2Deg;
            float glassCentreY = (glassBottom + glassTop) * .5f;
            float glassCentreZ = (glassBottomZ + glassTopZ) * .5f;
            body.Box(glass, 0f, glassCentreY, glassCentreZ, cab * 2f - .038f, glassLength, .064f, rake);
            for (int side = -1; side <= 1; side += 2)
            {
                body.Box(white, side * (cab - .021f), glassCentreY, glassCentreZ,
                    .094f, glassLength + .040f, .105f, rake);
                body.Box(black, side * .31f, glassBottom + .047f, glassBottomZ + .031f,
                    .40f, .022f, .025f, rake, side * -7f);
            }
            body.Box(white, 0f, glassCentreY, glassCentreZ + .036f, .042f, glassLength, .021f, rake);
            body.Box(panel, 0f, glassBottom - .075f, glassBottomZ - .040f, cab * 1.90f, .16f, .26f);
            BuildRoofFairing(body, white, front, glassTopZ - .10f, roof + .13f, cab, outer);
            CabBlock(white, 0, 1.20f, (front + 1.11f + hood) * .5f, cab * 1.64f, .72f, hood - front - 1.03f);
            CabBlock(white, 0, 1.57f, front + 1.83f, cab * 1.48f, .12f, 1.34f);
            CabBlock(panel, 0, 1.65f, front + 1.74f, cab * 1.22f, .07f, 1.15f);
            CabBlock(white, 0, 1.70f, front + 1.65f, cab * .98f, .047f, .94f);
            CabBlock(panel, 0, 1.31f, hood - .02f, cab * 1.7f, .52f, .26f);
            CabBlock(steel, 0, 1.33f, hood + .128f, cab * 1.27f, .48f, .078f);
            CabBlock(black, 0, 1.33f, hood + .173f, cab * 1.17f, .40f, .036f);
            for (int slat = -3; slat <= 3; slat++)
                CabBlock(steel, 0, 1.33f + slat * .051f, hood + .198f, cab * 1.13f, .015f, .028f);
            CabBlock(blue, 0, 1.39f, hood + .218f, .105f, .089f, .036f);
            CabBlock(steel, 0, .819f, bumper - .122f, (cabOuter - .05f) * 2f, .24f, .30f);
            CabBlock(black, 0, .793f, bumper + .034f, cab * 1.34f, .092f, .027f);
            for (int marker = -1; marker <= 1; marker++)
                body.Box(amber, marker * cab * .52f, 2.642f, cabRoofFront - .053f, .098f, .049f, .077f);
            body.Box(steel, 0, roof - .035f, rear - .038f, outer * 2f, .11f, .138f);
            for (int marker = -1; marker <= 1; marker++)
                body.Box(red, marker * .22f, roof - .035f, rear - .115f, .124f, .043f, .031f);

            float leaf = clearHeight - .065f;
            float rampWidth = clearWidth - .048f;
            var door = new Blocks(truck.transform, "Rear door hinge");
            door.Root.localPosition = new Vector3(0f, deck, rear);
            door.Box(tread, 0, leaf * .5f, -.054f, rampWidth, leaf, .108f);
            door.Box(white, 0, leaf * .5f, -.119f, rampWidth - .13f, leaf - .14f, .026f);
            door.Box(steel, 0, .071f, -.075f, rampWidth, .142f, .096f);
            door.Box(steel, 0, leaf - .047f, -.075f, rampWidth, .094f, .096f);
            door.Box(blue, 0, .37f, -.138f, rampWidth - .18f, .061f, .013f);
            door.Box(panel, 0, .30f, -.138f, rampWidth - .18f, .027f, .013f);
            var extension = new Blocks(door.Root, "Folding ramp extension");
            extension.Root.localPosition = new Vector3(0f, leaf, 0f);
            extension.Box(tread, 0, middleLength * .5f, -.05f, rampWidth, middleLength, .10f);
            var tip = new Blocks(extension.Root, "Sliding ramp tip");
            float tipWidth = rampWidth - .062f;
            tip.Box(tread, 0, (tipLength - .16f) * .5f, -.024f, tipWidth, tipLength - .16f, .048f);
            // Taper the last section under the driving face so it sits flush on the road.
            tip.Box(steel, 0, tipLength - .13f, -.017f, tipWidth, .099f, .034f);
            tip.Box(steel, 0, tipLength - .043f, -.006f, tipWidth, .086f, .012f);
            for (int side = -1; side <= 1; side += 2)
            {
                float edge = side * (rampWidth * .5f - .036f);
                door.Box(steel, edge, leaf * .5f, -.061f, .072f, leaf, .126f);
                door.Box(steel, side * rampWidth * .31f, leaf * .50f, -.142f, .040f, leaf - .23f, .034f);
                door.Box(blue, edge, leaf * .5f, .006f, .027f, leaf - .16f, .012f);
                extension.Box(steel, edge, middleLength * .5f, -.041f, .067f, middleLength, .085f);
                extension.Box(blue, edge, middleLength * .5f, .006f, .027f, middleLength - .16f, .012f);
                float tipEdge = side * (tipWidth * .5f - .036f);
                tip.Box(steel, tipEdge, (tipLength - .35f) * .5f, -.029f, .065f, tipLength + .05f, .054f);
                tip.Box(blue, tipEdge, (tipLength - .17f) * .5f, .006f, .025f, tipLength - .25f, .012f);
            }
            for (int rib = 1; rib <= 10; rib++)
                door.Box(steel, 0, leaf * rib / 11f, .004f, rampWidth - .19f, .025f, .008f);
            for (int rib = 1; rib <= 9; rib++)
                extension.Box(steel, 0, middleLength * rib / 10f, .004f, rampWidth - .19f, .025f, .008f);
            for (int rib = 1; rib <= 6; rib++)
                tip.Box(steel, 0, (tipLength - .15f) * rib / 7f, .004f, tipWidth - .19f, .025f, .008f);

            body.Bake("Body");
            door.Bake("Door");
            extension.Bake("Extension");
            tip.Bake("Tip");
            AddCollider(body.Root, new Vector3(0f, deck - .063f, 0f), new Vector3(clearWidth, .126f, length));
            AddCollider(body.Root, new Vector3(0f, roof + .065f, 0f), new Vector3(outer * 2f, .13f, length));
            AddCollider(body.Root, new Vector3(0f, deck + clearHeight * .5f, front + .057f), new Vector3(clearWidth, clearHeight, .114f));
            for (int side = -1; side <= 1; side += 2)
                AddCollider(body.Root, new Vector3(side * (half + .048f), deck + clearHeight * .5f, 0f),
                    new Vector3(.096f, clearHeight, length));
            AddCollider(door.Root, new Vector3(0f, leaf * .5f, -.054f), new Vector3(rampWidth, leaf, .108f));
            AddCollider(extension.Root, new Vector3(0f, middleLength * .5f, -.05f), new Vector3(rampWidth, middleLength, .10f));
            AddCollider(tip.Root, new Vector3(0f, tipLength * .5f, -.008f), new Vector3(tipWidth, tipLength, .016f));
            truck.Configure(door.Root, extension.Root, tip.Root, leaf, middleLength, tipLength, front, clearWidth, clearHeight);
            return truck;
        }

        private static void BuildRoofFairing(Blocks body, Material paint, float boxFront, float leadingZ,
            float boxRoofY, float cabHalfWidth, float boxHalfWidth)
        {
            const float baseY = 2.613f;
            float trailingZ = boxFront + .038f;

            Vector3[] Section(float z, float width, float top)
            {
                float shoulderY = Mathf.Max(baseY + .015f, top - .13f);
                return new[]
                {
                    new Vector3(-width, baseY, z),
                    new Vector3(width, baseY, z),
                    new Vector3(width, shoulderY, z),
                    new Vector3(width - .145f, top, z),
                    new Vector3(-width + .145f, top, z),
                    new Vector3(-width, shoulderY, z)
                };
            }

            float leadingWidth = cabHalfWidth - .27f;
            float trailingWidth = boxHalfWidth - .09f;
            body.Loft(paint,
                Section(leadingZ, leadingWidth, 2.671f),
                Section(Mathf.Lerp(leadingZ, trailingZ, .31f), Mathf.Lerp(leadingWidth, trailingWidth, .25f), 2.969f),
                Section(Mathf.Lerp(leadingZ, trailingZ, .67f), Mathf.Lerp(leadingWidth, trailingWidth, .65f), 3.352f),
                Section(trailingZ, trailingWidth, boxRoofY));
        }

        private static void BuildDriveline(Blocks body, Material black, Material metal, Material steel,
            float rearWheelOffset, float frontWheelOffset, float rear, float front)
        {
            float hubY = TyreHalfRows.Length * WheelCell * .5f;
            float gearbox = front - .44f;
            body.Box(metal, 0f, hubY, front, (frontWheelOffset + .07f) * 2f, .15f, .19f);
            body.Box(metal, 0f, hubY, rear, (rearWheelOffset + .07f) * 2f, .18f, .21f);

            // Low-mounted beam, gearbox and differential make a continuous path to the rear hubs.
            body.Box(black, 0f, hubY + .064f, gearbox, .40f, .29f, .58f);
            body.Box(metal, 0f, hubY, (rear + gearbox) * .5f, .14f, .14f, gearbox - rear);
            body.Box(steel, 0f, hubY, rear + .38f, .22f, .22f, .10f);
            body.Box(steel, 0f, hubY, gearbox - .30f, .22f, .22f, .10f);
            body.Box(metal, 0f, hubY, rear, .50f, .24f, .39f);
            body.Box(metal, 0f, hubY, rear, .37f, .35f, .44f);
            body.Box(steel, 0f, hubY, rear - .223f, .28f, .24f, .052f);
            body.Box(black, 0f, hubY + .10f, (rear + gearbox) * .5f, .24f, .20f, .13f);

            for (int side = -1; side <= 1; side += 2)
            {
                body.Box(black, side * (frontWheelOffset - .16f), hubY, front, .20f, .25f, .25f);
                body.Box(black, side * .64f, hubY + .14f, rear, .18f, .30f, .32f);
                body.Box(steel, side * (rearWheelOffset - .19f), hubY, rear, .21f, .21f, .21f);
            }
        }

        private static void StorageChest(Blocks body, Material black, Material steel, Material panel, float outer, float z, int side)
        {
            const float lift = .22f;
            body.Box(black, side * (outer - .40f), .66f + lift, z, .70f, .17f, .72f);
            body.Box(steel, side * (outer - .265f), .395f + lift, z, .40f, .38f, .86f);
            body.Box(black, side * (outer - .063f), .394f + lift, z, .027f, .32f, .78f);
            body.Box(panel, side * (outer - .041f), .394f + lift, z, .033f, .277f, .732f);
            body.Box(steel, side * (outer - .024f), .506f + lift, z, .027f, .045f, .75f);
            body.Box(steel, side * (outer - .022f), .274f + lift, z - .267f, .031f, .063f, .095f);
            body.Box(steel, side * (outer - .022f), .274f + lift, z + .267f, .031f, .063f, .095f);
            body.Box(black, side * (outer - .017f), .448f + lift, z, .030f, .076f, .119f);
            body.Box(steel, side * (outer - .002f), .46f + lift, z, .023f, .025f, .084f);
        }

        private const float FuelTankCell = .064f;
        private static readonly int[] TankHalfRows = { 1, 2, 3, 4, 4, 4, 3, 2, 1 };

        private static void FuelTank(Blocks body, Material steel, Material strap, Material black,
            float x, float z, int side)
        {
            const float centerY = .58f;
            const float cell = FuelTankCell;

            // Run the stepped barrel profile along the chassis, with raised bands and smaller end caps.
            void Profile(Material material, float centerZ, float length, float rim)
            {
                for (int row = 0; row < TankHalfRows.Length; row++)
                {
                    int rowOffset = row - TankHalfRows.Length / 2;
                    body.Box(material, x, centerY + rowOffset * (cell + rim), centerZ,
                        (2 * TankHalfRows[row] + 1) * cell + 2f * rim, cell + rim, length);
                }
            }

            Profile(steel, z, .98f, 0f);
            for (int end = -1; end <= 1; end += 2)
            {
                float strapZ = z + end * .285f;
                Profile(strap, strapZ, .080f, .004f);
                Profile(steel, z + end * .495f, .070f, -.007f);
                Profile(strap, z + end * .468f, .034f, .004f);
                body.Box(black, x - side * .19f, .89f, strapZ, .54f, .17f, .102f);
                body.Box(steel, x + side * .301f, centerY + .08f, strapZ, .024f, .081f, .059f);
            }
            body.Box(black, x + side * .145f, .817f, z + .13f, .071f, .073f, .072f);
            body.Box(steel, x + side * .145f, .86f, z + .13f, .105f, .034f, .100f);
        }

        private const float WheelCell = .081f;
        private static readonly int[] TyreHalfRows = { 2, 4, 5, 5, 6, 6, 6, 6, 6, 5, 5, 4, 2 };
        private static readonly int[] RimHalfRows = { 1, 2, 3, 3, 3, 2, 1 };

        private static void Wheel(Blocks body, Material tyre, Material lug, Material recess, Material silver,
            float x, float z, int side)
        {
            const float cell = WheelCell;
            float hubY = TyreHalfRows.Length * cell * .5f;
            for (int row = 0; row < TyreHalfRows.Length; row++)
            {
                float y = (row + .5f) * cell;
                int halfRow = TyreHalfRows[row];
                body.Box(tyre, x, y, z, .32f, cell, (2 * halfRow + 1) * cell - .012f);
                for (int edge = -1; edge <= 1; edge += 2)
                {
                    float edgeZ = z + edge * (halfRow + .32f) * cell;
                    float treadOffset = row % 2 == 0 ? .08f : -.08f;
                    body.Box(tyre, x - treadOffset, y, edgeZ, .16f, cell, .36f * cell);
                    body.Box(lug, x + treadOffset, y, edgeZ, .16f, cell * .74f, .36f * cell);
                    if (row % 2 == 0)
                        body.Box(lug, x + side * .162f, y, z + edge * (halfRow - .32f) * cell,
                            .014f, cell * .77f, cell * .81f);
                }
            }

            for (int row = 0; row < RimHalfRows.Length; row++)
            {
                float y = hubY + (row - 3) * cell;
                int halfRow = RimHalfRows[row];
                body.Box(silver, x + side * .17f, y, z, .027f, cell, (2 * halfRow + 1) * cell);
                if (row > 0 && row < RimHalfRows.Length - 1)
                    body.Box(recess, x + side * .185f, y, z, .019f, cell, (2 * halfRow - 1) * cell);
            }
            body.Box(silver, x + side * .208f, hubY, z, .037f, cell * 2f, cell * 2f);
            for (int vertical = -1; vertical <= 1; vertical += 2)
                for (int longitudinal = -1; longitudinal <= 1; longitudinal += 2)
                    body.Box(silver, x + side * .2f, hubY + vertical * cell * 1.72f,
                        z + longitudinal * cell * 1.72f, .020f, .032f, .032f);
        }

        private static Camera BuildTestLot()
        {
            var lot = new Blocks(null, "Straight test road");
            lot.Box(Material("Test Asphalt", 0x48515B, 0f, .13f), 0f, -.107f, 3f, 16f, .20f, 130f);
            lot.Box(Material("Test Gravel", 0xA99884, 0f, .10f), 0f, -.26f, 0f, 150f, .24f, 150f);
            var stripe = Material("Test Road Lines", 0xE5E0D5, 0f, .19f);
            for (int side = -1; side <= 1; side += 2)
            {
                lot.Box(stripe, side * 7.83f, -.004f, 2f, .11f, .006f, 126f);
                for (int segment = -8; segment <= 8; segment++)
                    lot.Box(stripe, side * (16f / 6f), -.004f, segment * 6f, .095f, .006f, 2.9f);
            }
            lot.Bake("TestRoad");
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, .96f, .90f);
            sun.intensity = 1.23f;
            sun.shadows = LightShadows.Hard;
            sun.transform.rotation = Quaternion.Euler(43f, -36f, 0f);
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.56f, .63f, .72f);

            var camera = new GameObject("Carrier Camera").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.62f, .72f, .81f);
            camera.fieldOfView = 44f;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 170f;
            camera.gameObject.AddComponent<AudioListener>();
            return camera;
        }

        private static void AddCollider(Transform root, Vector3 center, Vector3 size)
        {
            var collider = root.gameObject.AddComponent<BoxCollider>();
            collider.center = center;
            collider.size = size;
        }

        private static Bounds Measure(Transform root)
        {
            // Build and measure at the origin in the test scene before posing the car.
            var renderers = root.GetComponentsInChildren<Renderer>();
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers)
                bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        private static Material Material(string name, uint rgb, float metallic, float smoothness, float glow = 0f)
        {
            string path = $"{Folder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;
            var color = new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name, color = color };
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            if (glow > 0f)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", (Color)color * glow);
            }
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private sealed class Blocks
        {
            private readonly Mesh cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            private readonly Dictionary<Material, List<CombineInstance>> batches = new();
            private readonly List<Mesh> lofts = new();
            public Transform Root { get; }

            public Blocks(Transform parent, string name)
            {
                Root = new GameObject(name).transform;
                Root.SetParent(parent, false);
            }

            public void Box(Material material, float x, float y, float z, float w, float h, float d,
                float pitch = 0f, float roll = 0f)
                => Add(material, cube, new Vector3(x, y, z), Quaternion.Euler(pitch, 0f, roll), new Vector3(w, h, d));

            public void Loft(Material material, params Vector3[][] sections)
            {
                int edgeCount = sections[0].Length;
                var vertices = new List<Vector3>();
                var indices = new List<int>();
                Vector3 center = Vector3.zero;
                foreach (var section in sections)
                    foreach (Vector3 point in section)
                        center += point;
                center /= sections.Length * edgeCount;

                // Separate face vertices preserve the hard edges of each flat panel.
                void Triangle(Vector3 a, Vector3 b, Vector3 c)
                {
                    bool outward = Vector3.Dot(Vector3.Cross(b - a, c - a), a - center) > 0f;
                    int first = vertices.Count;
                    vertices.Add(a);
                    vertices.Add(b);
                    vertices.Add(c);
                    indices.Add(first);
                    indices.Add(first + (outward ? 1 : 2));
                    indices.Add(first + (outward ? 2 : 1));
                }

                for (int section = 1; section < sections.Length; section++)
                {
                    Vector3[] previous = sections[section - 1];
                    Vector3[] next = sections[section];
                    for (int edge = 0; edge < edgeCount; edge++)
                    {
                        int adjacent = (edge + 1) % edgeCount;
                        Triangle(previous[edge], previous[adjacent], next[adjacent]);
                        Triangle(previous[edge], next[adjacent], next[edge]);
                    }
                }
                Vector3[] start = sections[0];
                Vector3[] end = sections[sections.Length - 1];
                for (int edge = 1; edge < edgeCount - 1; edge++)
                {
                    Triangle(start[0], start[edge], start[edge + 1]);
                    Triangle(end[0], end[edge], end[edge + 1]);
                }

                var mesh = new Mesh();
                mesh.SetVertices(vertices);
                mesh.SetTriangles(indices, 0);
                mesh.RecalculateNormals();
                lofts.Add(mesh);
                Add(material, mesh, Vector3.zero, Quaternion.identity, Vector3.one);
            }

            private void Add(Material material, Mesh mesh, Vector3 position, Quaternion rotation, Vector3 scale)
            {
                if (!batches.TryGetValue(material, out var instances))
                    batches.Add(material, instances = new List<CombineInstance>());
                instances.Add(new CombineInstance { mesh = mesh, transform = Matrix4x4.TRS(position, rotation, scale) });
            }

            public void Bake(string name)
            {
                foreach (var batch in batches)
                {
                    string meshName = name + " - " + batch.Key.name;
                    string path = $"{Folder}/{meshName}.asset";
                    var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                    if (saved != null)
                    {
                        saved.Clear();
                        saved.CombineMeshes(batch.Value.ToArray());
                        EditorUtility.SetDirty(saved);
                        AssetDatabase.SaveAssetIfDirty(saved);
                    }
                    else
                    {
                        saved = new Mesh { name = meshName };
                        saved.CombineMeshes(batch.Value.ToArray());
                        AssetDatabase.CreateAsset(saved, path);
                    }
                    var group = new GameObject(batch.Key.name);
                    group.transform.SetParent(Root, false);
                    group.AddComponent<MeshFilter>().sharedMesh = saved;
                    group.AddComponent<MeshRenderer>().sharedMaterial = batch.Key;
                }
                foreach (Mesh mesh in lofts)
                    Object.DestroyImmediate(mesh);
            }
        }
    }
}
