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
            var hasProperties = HasProperParts(carParts);

            if (!hasProperties)
            {
                reason = 0;
                return false;
            }

            reason = 0;
            return true;
        }

        static bool HasProperParts(Dictionary<string, GameObject> parts)
        {
            foreach (var part in NECESSARY_PARTS)
            {
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

            // Find or add components
            var controller = carRoot.GetComponentInChildren<VehicleController>();
            var engine = carRoot.GetComponentInChildren<VehicleEngine>();
            var transmission = carRoot.GetComponentInChildren<VehicleTransmission>();
            if (transmission == null) transmission = carRoot.AddComponent<VehicleTransmission>();
            var suspension = carRoot.GetComponentInChildren<VehicleSuspension>();
            if (suspension == null) suspension = carRoot.AddComponent<VehicleSuspension>();
            var brakes = carRoot.GetComponentInChildren<VehicleBrakes>();
            var antiRollBars = carRoot.GetComponentsInChildren<AntiRollBar>();
            var wheels = carRoot.GetComponentsInChildren<VehicleWheel>();
            var rb = carRoot.GetComponentInChildren<Rigidbody>();

            // Add optional components if settings exist
            var tyresComp = carRoot.GetComponentInChildren<VehicleTyres>();
            if (tyresComp == null && t.tyres != null) tyresComp = carRoot.AddComponent<VehicleTyres>();
            var aeroComp = carRoot.GetComponentInChildren<VehicleAerodynamics>();
            if (aeroComp == null && t.aerodynamics != null) aeroComp = carRoot.AddComponent<VehicleAerodynamics>();
            var bodyDynComp = carRoot.GetComponentInChildren<VehicleBodyDynamics>();
            if (bodyDynComp == null && t.bodyDynamics != null) bodyDynComp = carRoot.AddComponent<VehicleBodyDynamics>();

            if (controller == null)
            {
                Debug.LogError("[Tuning] VehicleController not found");
                return;
            }

            ApplyAllSettings(controller, engine, transmission, suspension, brakes, antiRollBars, wheels, rb, tyresComp, aeroComp, bodyDynComp, t);

            // Wheels (explicit mapping for baked models)
            if (parts != null)
            {
                ApplyWheel(t.wheelfl, parts, true, true);
                ApplyWheel(t.wheelfr, parts, true, false);
                ApplyWheel(t.wheelrl, parts, false, true);
                ApplyWheel(t.wheelrr, parts, false, false);
            }

            Debug.Log("[Tuning] All settings applied successfully");
        }

        /// <summary>
        /// Apply settings directly to an existing scene vehicle (no bake step).
        /// </summary>
        public static void ApplySettingsToExisting(GameObject carRoot, TuningEditorBehaviours t)
        {
            if (carRoot == null || t == null)
            {
                Debug.LogError("[Tuning] Invalid carRoot or tuning data");
                return;
            }

            Undo.RecordObject(carRoot, "Apply Vehicle Tuning");

            var controller = carRoot.GetComponentInChildren<VehicleController>();
            var engine = carRoot.GetComponentInChildren<VehicleEngine>();
            var transmission = carRoot.GetComponentInChildren<VehicleTransmission>();
            var suspension = carRoot.GetComponentInChildren<VehicleSuspension>();
            var brakes = carRoot.GetComponentInChildren<VehicleBrakes>();
            var antiRollBars = carRoot.GetComponentsInChildren<AntiRollBar>();
            var wheels = carRoot.GetComponentsInChildren<VehicleWheel>();
            var rb = carRoot.GetComponentInChildren<Rigidbody>();
            var tyresComp = carRoot.GetComponentInChildren<VehicleTyres>();
            var aeroComp = carRoot.GetComponentInChildren<VehicleAerodynamics>();
            var bodyDynComp = carRoot.GetComponentInChildren<VehicleBodyDynamics>();

            if (controller == null)
            {
                Debug.LogError("[Tuning] VehicleController not found on selected object");
                return;
            }

            // Record all components for undo
            if (engine != null) Undo.RecordObject(engine, "Apply Vehicle Tuning");
            if (transmission != null) Undo.RecordObject(transmission, "Apply Vehicle Tuning");
            if (suspension != null) Undo.RecordObject(suspension, "Apply Vehicle Tuning");
            if (brakes != null) Undo.RecordObject(brakes, "Apply Vehicle Tuning");
            if (rb != null) Undo.RecordObject(rb, "Apply Vehicle Tuning");
            if (tyresComp != null) Undo.RecordObject(tyresComp, "Apply Vehicle Tuning");
            if (aeroComp != null) Undo.RecordObject(aeroComp, "Apply Vehicle Tuning");
            if (bodyDynComp != null) Undo.RecordObject(bodyDynComp, "Apply Vehicle Tuning");
            Undo.RecordObject(controller, "Apply Vehicle Tuning");
            foreach (var arb in antiRollBars) Undo.RecordObject(arb, "Apply Vehicle Tuning");

            ApplyAllSettings(controller, engine, transmission, suspension, brakes, antiRollBars, wheels, rb, tyresComp, aeroComp, bodyDynComp, t);

            EditorUtility.SetDirty(carRoot);
            Debug.Log("[Tuning] Settings applied to existing vehicle");
        }

        /// <summary>
        /// Core settings application shared by both new and existing vehicle flows.
        /// </summary>
        static void ApplyAllSettings(
            VehicleController controller,
            VehicleEngine engine,
            VehicleTransmission transmission,
            VehicleSuspension suspension,
            VehicleBrakes brakes,
            AntiRollBar[] antiRollBars,
            VehicleWheel[] wheels,
            Rigidbody rb,
            VehicleTyres tyresComp,
            VehicleAerodynamics aeroComp,
            VehicleBodyDynamics bodyDynComp,
            TuningEditorBehaviours t)
        {
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
                controller.visualWheelRadius = t.powertrain.visualWheelRadius;
                controller.drivetrain = (VehicleController.DrivetrainType)t.powertrain.drivetrain;

                // Differential settings
                controller.differentialAccelSplitRWD = t.powertrain.differentialAccelSplitRWD;
                controller.differentialDecelSplitRWD = t.powertrain.differentialDecelSplitRWD;
                controller.differentialAccelSplitFWD = t.powertrain.differentialAccelSplitFWD;
                controller.differentialAWDRearBias = t.powertrain.differentialAWDRearBias;

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
                controller.brakePressureMultiplier = t.handling.brakePressureMultiplier;
                controller.corneringStiffness = t.handling.corneringStiffness;
                controller.downforceFactor = t.handling.downforceFactor;
                controller.downforceExponent = t.handling.downforceExponent;
                controller.dragCoefficient = t.handling.dragCoefficient;
                controller.frontalArea = t.handling.frontalArea;
                controller.weightTransferFactor = t.handling.weightTransferFactor;
                controller.springRateAvgNpm = t.handling.springRateAvgNpm;
                controller.loadSensitivity = t.handling.loadSensitivity;
                controller.counterSteerAssist = t.handling.counterSteerAssist;
                controller.tractionControl = t.handling.tractionControl;

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
                controller.steeringResponse = t.steering.steeringResponse;
                controller.steeringReturnSpeed = t.steering.steeringReturnSpeed;
                controller.speedSensitiveSteering = t.steering.speedSensitiveSteering;
            }

            // =========================
            // DRIFT & HANDBRAKE
            // =========================
            if (t.drift != null)
            {
                controller.enableDriftAssist = t.drift.enableDriftAssist;
                controller.handbrakeSlipMultiplier = t.drift.handbrakeSlipMultiplier;
                controller.driftSpinFactor = t.drift.driftSpinFactor;
            }

            // =========================
            // SHIFT KICK
            // =========================
            if (t.shiftKick != null)
            {
                controller.shiftKickStrength = t.shiftKick.shiftKickStrength;
                controller.shiftKickMinThrottle = t.shiftKick.shiftKickMinThrottle;
                controller.shiftChirpTorqueMultiplier = t.shiftKick.shiftChirpTorqueMultiplier;
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
            // AERODYNAMICS
            // =========================
            if (aeroComp != null && t.aerodynamics != null)
            {
                aeroComp.dragCoefficient = t.aerodynamics.dragCoefficient;
                aeroComp.downforceCoefficient = t.aerodynamics.downforceCoefficient;
                aeroComp.frontalArea = t.aerodynamics.frontalArea;
                aeroComp.highSpeedThresholdKMH = t.aerodynamics.highSpeedThresholdKMH;
                aeroComp.highSpeedFullKMH = t.aerodynamics.highSpeedFullKMH;
                aeroComp.highSpeedDownforceMultiplier = t.aerodynamics.highSpeedDownforceMultiplier;
                aeroComp.enableBodyWeave = t.aerodynamics.enableBodyWeave;
                aeroComp.weaveMinSpeedKMH = t.aerodynamics.weaveMinSpeedKMH;
                aeroComp.weaveTorqueStrength = t.aerodynamics.weaveTorqueStrength;
                aeroComp.weaveFrequency = t.aerodynamics.weaveFrequency;
                aeroComp.enableActiveAero = t.aerodynamics.enableActiveAero;
                aeroComp.maxAeroAngle = t.aerodynamics.maxAeroAngle;
            }

            // =========================
            // TYRES
            // =========================
            if (tyresComp != null && t.tyres != null)
            {
                tyresComp.longitudinalPeakSlip = t.tyres.longitudinalPeakSlip;
                tyresComp.longitudinalPeakValue = t.tyres.longitudinalPeakValue;
                tyresComp.longitudinalAsymptoteValue = t.tyres.longitudinalAsymptoteValue;
                tyresComp.longitudinalStiffness = t.tyres.longitudinalStiffness;
                tyresComp.peakSlipAngleDeg = t.tyres.peakSlipAngleDeg;
                tyresComp.lateralPeakValue = t.tyres.lateralPeakValue;
                tyresComp.lateralAsymptoteValue = t.tyres.lateralAsymptoteValue;
                tyresComp.lateralStiffness = t.tyres.lateralStiffness;
                tyresComp.rwdRearSlipMultiplier = t.tyres.rwdRearSlipMultiplier;
                tyresComp.driftGripMultiplier = t.tyres.driftGripMultiplier;
                tyresComp.enableTireTemperature = t.tyres.enableTireTemperature;
                tyresComp.optimalTempC = t.tyres.optimalTempC;
                tyresComp.ambientTempC = t.tyres.ambientTempC;
                tyresComp.heatRatePerSlip = t.tyres.heatRatePerSlip;
                tyresComp.coolingRate = t.tyres.coolingRate;
                tyresComp.enableTirePressure = t.tyres.enableTirePressure;
                tyresComp.tirePressurePsi = t.tyres.tirePressurePsi;
                tyresComp.enableFrontSteeringTug = t.tyres.enableFrontSteeringTug;
                tyresComp.frontTugSlipThreshold = t.tyres.frontTugSlipThreshold;
                tyresComp.frontTugStiffnessReduction = t.tyres.frontTugStiffnessReduction;
            }

            // =========================
            // BODY DYNAMICS
            // =========================
            if (bodyDynComp != null && t.bodyDynamics != null)
            {
                bodyDynComp.pitchTorqueGain = t.bodyDynamics.pitchTorqueGain;
                bodyDynComp.pitchInputMultiplier = t.bodyDynamics.pitchInputMultiplier;
                bodyDynComp.verticalForceCoupleStrength = t.bodyDynamics.verticalForceCoupleStrength;
                bodyDynComp.forceCoupleDistance = t.bodyDynamics.forceCoupleDistance;
                bodyDynComp.rollTorqueGain = t.bodyDynamics.rollTorqueGain;
                bodyDynComp.rollSpeedFactor = t.bodyDynamics.rollSpeedFactor;
                bodyDynComp.rollMinSpeedKMH = t.bodyDynamics.rollMinSpeedKMH;
                bodyDynComp.autoTuneByDrivetrain = t.bodyDynamics.autoTuneByDrivetrain;
                bodyDynComp.squatDiveStrength = t.bodyDynamics.squatDiveStrength;
                bodyDynComp.rollIntensity = t.bodyDynamics.rollIntensity;
            }
        }

        static void ApplyWheel(
            TuningEditorBehaviours.Wheel src,
            Dictionary<string, GameObject> parts,
            bool front,
            bool left
        )
        {
            if (src == null || parts == null) return;

            string key = "wheel" + (front ? "f" : "r") + (left ? "l" : "r") + "_coll";
            if (!parts.ContainsKey(key)) return;

            var w = parts[key].GetComponent<VehicleWheel>();
            if (w == null) return;

            w.isFront = src.isFront;
            w.isSteer = src.isSteer;
            w.isMotor = src.isMotor;
            w.steerSpeed = src.steerSpeed;
        }
    }
}
#endif