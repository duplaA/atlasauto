using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using AtlasAuto.Editor;
using System;
using System.Text.RegularExpressions;


#if UNITY_EDITOR
namespace AtlasAuto.Compiler
{
    public static class CarCompilerManager
    {
        public static string[] NECESSARY_PARTS = { "wheelfl", "wheelfr", "wheelrl", "wheelrr" };

        public static string DumbDownText(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            return Regex.Replace(input, "[^a-zA-Z]", "").ToLower();
        }
        public static bool CheckIntegrity(GameObject obj, out int reason, out Dictionary<string, GameObject> parts)
        {

            Dictionary<string, GameObject> carParts = new();
            var transform = obj.transform;
            for (int i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                var childObj = child.gameObject;

                var dumbName = DumbDownText(child.name);

                if (NECESSARY_PARTS.Contains(dumbName)) carParts.Add(dumbName, childObj);
            }

            parts = carParts;
            var hasProperties = HasPropertParts(carParts);

            if (!hasProperties)
            {
                reason = 0;
                return false;
            }

            reason = 0;
            return true;
        }

        static bool HasPropertParts(Dictionary<string, GameObject> parts)
        {
            foreach (var part in NECESSARY_PARTS)
            {
                System.Console.WriteLine(part);
                if (!parts.ContainsKey(part))
                {
                    return false;
                }
            }

            return true;
        }

        static Bounds GetBoundsInWheelLocal(Transform wheel)
        {
            var renderers = wheel.GetComponentsInChildren<Renderer>();
            if (renderers == null || renderers.Length == 0)
                return new Bounds(Vector3.zero, Vector3.zero);

            bool inited = false;
            Bounds b = default;

            foreach (var r in renderers)
            {
                var wb = r.bounds;
                var c = wb.center;
                var e = wb.extents;

                Span<Vector3> corners = stackalloc Vector3[8]
                {
            c + new Vector3(+e.x, +e.y, +e.z),
            c + new Vector3(+e.x, +e.y, -e.z),
            c + new Vector3(+e.x, -e.y, +e.z),
            c + new Vector3(+e.x, -e.y, -e.z),
            c + new Vector3(-e.x, +e.y, +e.z),
            c + new Vector3(-e.x, +e.y, -e.z),
            c + new Vector3(-e.x, -e.y, +e.z),
            c + new Vector3(-e.x, -e.y, -e.z),
        };

                for (int i = 0; i < corners.Length; i++)
                {
                    var pLocal = wheel.InverseTransformPoint(corners[i]);
                    if (!inited) { b = new Bounds(pLocal, Vector3.zero); inited = true; }
                    else b.Encapsulate(pLocal);
                }
            }

            return b;
        }

        static float ComputeWheelRadiusFromLocalBounds(Bounds localBounds)
        {
            var e = localBounds.extents;

            if (e.x <= e.y && e.x <= e.z) return Mathf.Max(e.y, e.z);
            if (e.y <= e.x && e.y <= e.z) return Mathf.Max(e.x, e.z);
            return Mathf.Max(e.x, e.y);
        }

        public static GameObject BakeModel(GameObject baseModel, ref Dictionary<string, GameObject> parts)
        {
            Dictionary<GameObject, Bounds> wheelBounds = new();
            foreach (var key in parts.Keys)
            {
                var obj = parts[key];
                if (key.Contains("wheel"))
                {
                    // Bounds b = new Bounds(obj.transform.localPosition, Vector3.zero);
                    // foreach (var r in obj.GetComponentsInChildren<Renderer>())
                    // {
                    //     var localBounds = r.localBounds;
                    //     localBounds.center = obj.transform.InverseTransformPoint(
                    //         r.transform.TransformPoint(localBounds.center)
                    //     );
                    //     b.Encapsulate(localBounds);
                    // }
                    // wheelBounds.Add(obj, b);
                    wheelBounds.Add(obj, GetBoundsInWheelLocal(obj.transform));
                }
            }
            GameObject temporaryInstace = GameObject.Instantiate(baseModel);
            temporaryInstace.AddComponent<Rigidbody>();
            Collider collider = temporaryInstace.GetComponent<Collider>();

            if (collider) GameObject.DestroyImmediate(collider);

            var filter = temporaryInstace.GetComponent<MeshFilter>();
            if (filter)
            {
                MeshCollider collider2 = temporaryInstace.AddComponent<MeshCollider>();
                collider2.sharedMesh = filter.sharedMesh;
                collider2.convex = true;
            }
            else
            {
                Debug.LogError("Error the model does NOT contain a mesh filter in the ROOT of the vehicle. This is unacceptable and should NOT be the case. Make sure your model has a mesh filter in the root which isn't none either!");
                return null;
            }

            // Wheel calculation
            // Fuckass radius calculation would be too hard, so I lowk decided to use a couple of bounds
            // A bound ACTUALLY HAS A RADIUS
            // My job easy yall fuckers
            temporaryInstace.AddComponent<VehicleController>();
            temporaryInstace.AddComponent<VehicleEngine>();
            AntiRollBar antiRollBarFront = temporaryInstace.AddComponent<AntiRollBar>();
            AntiRollBar antiRollBarRear = temporaryInstace.AddComponent<AntiRollBar>();

            var partsToAdd = new Dictionary<string, GameObject>();
            foreach (var part in parts)
            {
                var name = part.Key;
                var obj = part.Value;

                if (name.Contains("wheel"))
                {
                    Bounds b = wheelBounds[obj];

                    GameObject wheel = new GameObject();
                    wheel.name = name + "_coll";
                    partsToAdd[wheel.name] = wheel;
                    var wheelColl = wheel.AddComponent<WheelCollider>();
                    wheelColl.center = wheel.transform.TransformPoint(b.center);
                    wheelColl.radius = ComputeWheelRadiusFromLocalBounds(b);

                    wheel.transform.SetParent(temporaryInstace.transform, false);
                    wheel.transform.position = obj.transform.position;
                    wheel.transform.rotation = obj.transform.rotation;

                    VehicleWheel wheelComponent = wheel.AddComponent<VehicleWheel>();
                    wheelComponent.wheelCollider = wheelColl;
                    wheelComponent.wheelVisual = obj.transform;
                    if (obj == parts["wheelfl"])
                    {
                        antiRollBarFront.wheelL = wheelColl;
                    }
                    else if (obj == parts["wheelfr"])
                    {
                        antiRollBarFront.wheelR = wheelColl;
                    }
                    else if (obj == parts["wheelrl"])
                    {
                        antiRollBarRear.wheelL = wheelColl;
                    }
                    else if (obj == parts["wheelrr"])
                    {
                        antiRollBarRear.wheelR = wheelColl;
                    }
                }
            }

            parts = parts.Concat(partsToAdd).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            return temporaryInstace;
        }

        public static void ApplySettings(GameObject carRoot, TuningEditorBehaviours t, Dictionary<string, GameObject> parts)
        {
            if (carRoot == null || t == null)
            {
                Debug.LogError("[Tuning] Invalid carRoot or tuning data");
                return;
            }

            // =========================
            // FIND COMPONENTS (structure-agnostic)
            // =========================
            var controller = carRoot.GetComponentInChildren<VehicleController>();
            var engine = carRoot.GetComponentInChildren<VehicleEngine>();
            var transmission = carRoot.GetComponentInChildren<VehicleTransmission>();
            var suspension = carRoot.GetComponentInChildren<VehicleSuspension>();
            var brakes = carRoot.GetComponentInChildren<VehicleBrakes>();
            var antiRollBars = carRoot.GetComponentsInChildren<AntiRollBar>();
            var wheels = carRoot.GetComponentsInChildren<VehicleWheel>();
            var rb = carRoot.GetComponentInChildren<Rigidbody>();

            if (controller == null)
            {
                Debug.LogError("[Tuning] VehicleController not found");
                return;
            }

            // =========================
            // ENGINE
            // =========================
            if (engine != null && t.engine != null)
            {
                engine.engineType = t.engine.engineType;
                engine.horsepowerHP = t.engine.horsepowerHP;
                engine.maxPowerKW = t.engine.maxPowerKW;
                engine.peakTorqueNm = t.engine.peakTorqueNm;
                engine.maxRPM = t.engine.maxRPM;
                engine.inertia = t.engine.inertia;
                engine.peakTorqueRPM = t.engine.peakTorqueRPM;
                engine.peakPowerRPM = t.engine.peakPowerRPM;
                engine.idleRPM = t.engine.idleRPM;
                engine.evBaseRPM = t.engine.evBaseRPM;
                engine.frictionTorque = t.engine.frictionTorque;
                engine.brakingTorque = t.engine.brakingTorque;
            }

            // =========================
            // POWERTRAIN / CONTROLLER
            // =========================
            if (t.powertrain != null)
            {
                controller.isElectricVehicle = t.powertrain.isElectricVehicle;
                controller.topSpeedKMH = t.powertrain.topSpeedKMH;
                controller.physicsWheelRadius = t.powertrain.physicsWheelRadius;
                controller.drivetrain =
                    (VehicleController.DrivetrainType)t.powertrain.drivetrain;

                if (transmission != null)
                    transmission.isElectric = t.powertrain.isElectricVehicle;
            }

            // =========================
            // TRANSMISSION
            // =========================
            if (transmission != null && t.transmission != null)
            {
                transmission.mode = t.transmission.mode;
                transmission.isElectric = t.transmission.isElectric;
                transmission.finalDriveRatio = t.transmission.finalDriveRatio;
                transmission.reverseRatio = t.transmission.reverseRatio;
                transmission.electricFixedRatio = t.transmission.electricFixedRatio;
                transmission.upshiftRPM = t.transmission.upshiftRPM;
                transmission.downshiftRPM = t.transmission.downshiftRPM;
                transmission.shiftDuration = t.transmission.shiftDuration;
            }

            // =========================
            // HANDLING
            // =========================
            if (t.handling != null)
            {
                controller.vehicleMass = t.handling.vehicleMass;
                controller.centerOfMassOffset = t.handling.centerOfMassOffset;
                controller.maxBrakeTorque = t.handling.maxBrakeTorque;
                controller.frontBrakeBias = t.handling.frontBrakeBias;
                controller.corneringStiffness = t.handling.corneringStiffness;
                controller.downforceFactor = t.handling.downforceFactor;

                if (rb != null)
                {
                    rb.mass = t.handling.vehicleMass;
                    rb.centerOfMass = t.handling.centerOfMassOffset;
                }
            }

            // =========================
            // STEERING
            // =========================
            if (t.steering != null)
            {
                controller.maxSteerAngle = t.steering.maxSteerAngle;
                controller.speedSensitiveSteering = t.steering.speedSensitiveSteering;
            }

            // =========================
            // SUSPENSION
            // =========================
            if (suspension != null && t.suspension != null)
            {
                suspension.Configure(
                    controller.vehicleMass,
                    t.suspension.suspensionDistance,
                    t.suspension.frequency,
                    t.suspension.damping,
                    t.suspension.bias
                );

                suspension.UpdateSuspension(wheels);
            }

            // =========================
            // ANTI-ROLL BARS
            // =========================
            if (t.antiRoll != null)
            {
                foreach (var arb in antiRollBars)
                {
                    arb.antiRollForce = t.antiRoll.antiRollForce;
                }
            }

            // =========================
            // BRAKES
            // =========================
            if (brakes != null && t.brakes != null)
            {
                brakes.maxBrakeTorque = t.brakes.maxBrakeTorque;
            }

            // =========================
            // WHEELS (explicit mapping)
            // =========================
            ApplyWheel(t.wheelfl, parts, true, true);
            ApplyWheel(t.wheelfr, parts, true, false);
            ApplyWheel(t.wheelrl, parts, false, true);
            ApplyWheel(t.wheelrr, parts, false, false);

            // =========================
            // FINAL SYNC
            // =========================

            Debug.Log("[Tuning] All settings applied successfully");
        }

        static void ApplyWheel(
            TuningEditorBehaviours.Wheel src,
            Dictionary<string, GameObject> parts,
            bool front,
            bool left
        )
        {
            if (src == null || parts == null) return;

            var w = parts["wheel" + (front ? "f" : "r") + (left ? "l" : "r") + "_coll"].GetComponent<VehicleWheel>();

            if (w == null) return;

            w.isFront = src.isFront;
            w.isSteer = src.isSteer;
            w.isMotor = src.isMotor;
            w.steerSpeed = src.steerSpeed;
        }
    }
}
#endif