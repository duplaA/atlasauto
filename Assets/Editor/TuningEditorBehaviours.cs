using System;
using System.Linq;
using System.Reflection;
using Unity.Mathematics;
using UnityEngine;

namespace AtlasAuto.Editor
{

    public class TuningEditorBehaviours
    {
        // =========================
        // ENGINE
        // =========================
        public class EngineSettings
        {
            public VehicleEngine.EngineType engineType = VehicleEngine.EngineType.InternalCombustion;

            [EditorRange(0f, 2000f)] public float horsepowerHP = 300f;
            [EditorRange(0f, 1500f)] public float maxPowerKW = 220f;

            [EditorRange(0f, 2000f)] public float peakTorqueNm = 400f;
            [EditorRange(0f, 12000f)] public float maxRPM = 7000f;
            [EditorRange(0f, 5f)] public float inertia = 0.15f;

            [EditorRange(0f, 12000f)] public float peakTorqueRPM = 4500f;
            [EditorRange(0f, 12000f)] public float peakPowerRPM = 6000f;
            [EditorRange(0f, 3000f)] public float idleRPM = 800f;

            [EditorRange(0f, 12000f)] public float evBaseRPM = 1000f;

            [EditorRange(0f, 200f)] public float frictionTorque = 20f;
            [EditorRange(0f, 300f)] public float brakingTorque = 50f;
        }

        // =========================
        // POWERTRAIN / CONTROLLER
        // =========================
        public class PowertrainSettings
        {
            public bool isElectricVehicle = false;

            public enum DrivetrainType { RWD, FWD, AWD }
            public DrivetrainType drivetrain = DrivetrainType.RWD;

            [EditorRange(50f, 500f)] public float topSpeedKMH = 250f;
            [EditorRange(0.1f, 1.0f)] public float physicsWheelRadius = 0.35f;
            [EditorRange(0.1f, 1.0f)] public float visualWheelRadius = 0.35f;

            [NameInEditor("Diff Accel Split (RWD)")]
            [EditorRange(0.25f, 1f)] public float differentialAccelSplitRWD = 0.5f;

            [NameInEditor("Diff Decel Split (RWD)")]
            [EditorRange(0.2f, 0.5f)] public float differentialDecelSplitRWD = 0.35f;

            [NameInEditor("Diff Accel Split (FWD)")]
            [EditorRange(0.15f, 0.35f)] public float differentialAccelSplitFWD = 0.22f;

            [NameInEditor("AWD Rear Bias")]
            [EditorRange(0.5f, 0.8f)] public float differentialAWDRearBias = 0.65f;
        }

        // =========================
        // TRANSMISSION
        // =========================
        public class TransmissionSettings
        {
            public VehicleTransmission.TransmissionMode mode = VehicleTransmission.TransmissionMode.Automatic;
            public bool isElectric = false;

            [EditorRange(1f, 10f)] public float finalDriveRatio = 3.5f;
            [EditorRange(1f, 15f)] public float reverseRatio = 3.2f;

            // EV
            [EditorRange(1f, 20f)] public float electricFixedRatio = 9.0f;

            // ICE Shift Logic
            [EditorRange(0.5f, 1f)] public float upshiftRPM = 0.85f;
            [EditorRange(0.2f, 0.7f)] public float downshiftRPM = 0.45f;
            [EditorRange(0f, 1f)] public float shiftDuration = 0.2f;
        }

        // =========================
        // HANDLING
        // =========================
        public class HandlingSettings
        {
            [EditorRange(500f, 5000f)] public float vehicleMass = 1500f;
            public Vector3 centerOfMassOffset = new Vector3(0f, -0.5f, 0.1f);

            [EditorRange(0f, 10000f)] public float maxBrakeTorque = 5000f;
            [EditorRange(0f, 1f)] public float frontBrakeBias = 0.65f;

            [NameInEditor("Brake Pressure Multiplier")]
            [EditorRange(1f, 1.2f)] public float brakePressureMultiplier = 1f;

            [EditorRange(0.5f, 5f)] public float corneringStiffness = 2.5f;
            [EditorRange(0f, 10f)] public float downforceFactor = 3f;

            [NameInEditor("Downforce Exponent")]
            [EditorRange(1.5f, 2.2f)] public float downforceExponent = 1.8f;

            [NameInEditor("Drag Coefficient")]
            [EditorRange(0.1f, 1f)] public float dragCoefficient = 0.3f;

            [NameInEditor("Frontal Area (m²)")]
            [EditorRange(1f, 4f)] public float frontalArea = 2.2f;

            [NameInEditor("Weight Transfer")]
            [EditorRange(0f, 1f)] public float weightTransferFactor = 0.4f;

            [NameInEditor("Spring Rate Avg (N/m)")]
            [EditorRange(10000f, 100000f)] public float springRateAvgNpm = 40000f;

            [NameInEditor("Load Sensitivity")]
            [EditorRange(0f, 2f)] public float loadSensitivity = 0.8f;

            [NameInEditor("Counter-Steer Assist")]
            [EditorRange(0f, 1f)] public float counterSteerAssist = 0.3f;

            [NameInEditor("Traction Control")]
            [EditorRange(0f, 1f)] public float tractionControl = 0.2f;
        }

        // =========================
        // STEERING
        // =========================
        public class SteeringSettings
        {
            [EditorRange(5f, 60f)] public float maxSteerAngle = 35f;

            [NameInEditor("Steering Response")]
            [EditorRange(0.5f, 3f)] public float steeringResponse = 1.5f;

            [NameInEditor("Return Speed")]
            [EditorRange(1f, 15f)] public float steeringReturnSpeed = 8f;

            public bool speedSensitiveSteering = true;
        }

        // =========================
        // DRIFT & HANDBRAKE
        // =========================
        public class DriftSettings
        {
            [NameInEditor("Enable Drift Assist")]
            public bool enableDriftAssist = true;

            [NameInEditor("Handbrake Slip Multiplier")]
            [EditorRange(0.1f, 1f)] public float handbrakeSlipMultiplier = 0.4f;

            [NameInEditor("Drift Spin Factor")]
            [EditorRange(0f, 2f)] public float driftSpinFactor = 0.5f;
        }

        // =========================
        // SHIFT KICK
        // =========================
        public class ShiftKickSettings
        {
            [NameInEditor("Shift Kick Strength")]
            [EditorRange(0f, 10f)] public float shiftKickStrength = 3f;

            [NameInEditor("Min Throttle to Trigger")]
            [EditorRange(0f, 1f)] public float shiftKickMinThrottle = 0.7f;

            [NameInEditor("Chirp Torque Multiplier")]
            [EditorRange(1f, 2f)] public float shiftChirpTorqueMultiplier = 1.3f;
        }

        // =========================
        // SUSPENSION
        // =========================
        public class SuspensionSettings
        {
            [EditorRange(0.05f, 0.5f)] public float suspensionDistance = 0.2f;

            public VehicleSuspension.SpringFrequency frequency = VehicleSuspension.SpringFrequency.Comfort;
            public VehicleSuspension.DampRatio damping = VehicleSuspension.DampRatio.Comfort;
            public VehicleSuspension.FrontRearBias bias = VehicleSuspension.FrontRearBias.FiftyFifty;
        }

        // =========================
        // ANTI ROLL BAR
        // =========================
        public class AntiRollSettings
        {
            [EditorRange(0f, 50000f)]
            public float antiRollForce = 5000f;
        }

        // =========================
        // BRAKES
        // =========================
        public class BrakeSettings
        {
            [EditorRange(0f, 10000f)]
            public float maxBrakeTorque = 3000f;
        }

        // =========================
        // AERODYNAMICS
        // =========================
        public class AerodynamicsSettings
        {
            [NameInEditor("Drag Coefficient")]
            [EditorRange(0.1f, 1f)] public float dragCoefficient = 0.3f;

            [NameInEditor("Downforce Coefficient")]
            [EditorRange(0f, 1f)] public float downforceCoefficient = 0.1f;

            [NameInEditor("Frontal Area (m²)")]
            [EditorRange(1f, 4f)] public float frontalArea = 2.2f;

            [NameInEditor("High-Speed Threshold (km/h)")]
            [EditorRange(100f, 250f)] public float highSpeedThresholdKMH = 150f;

            [NameInEditor("High-Speed Full (km/h)")]
            [EditorRange(150f, 350f)] public float highSpeedFullKMH = 200f;

            [NameInEditor("High-Speed Downforce Mult")]
            [EditorRange(1f, 2f)] public float highSpeedDownforceMultiplier = 1.3f;

            [NameInEditor("Enable Body Weave")]
            public bool enableBodyWeave = true;

            [NameInEditor("Weave Min Speed (km/h)")]
            [EditorRange(100f, 300f)] public float weaveMinSpeedKMH = 200f;

            [NameInEditor("Weave Torque Strength")]
            [EditorRange(0f, 500f)] public float weaveTorqueStrength = 150f;

            [NameInEditor("Weave Frequency")]
            [EditorRange(0.5f, 3f)] public float weaveFrequency = 1.5f;

            [NameInEditor("Enable Active Aero")]
            public bool enableActiveAero = false;

            [NameInEditor("Max Aero Angle")]
            [EditorRange(0f, 30f)] public float maxAeroAngle = 15f;
        }

        // =========================
        // TYRES
        // =========================
        public class TyresSettings
        {
            [NameInEditor("Longitudinal Peak Slip")]
            [EditorRange(0.05f, 0.2f)] public float longitudinalPeakSlip = 0.08f;

            [NameInEditor("Longitudinal Peak Value")]
            [EditorRange(1f, 2.5f)] public float longitudinalPeakValue = 1.5f;

            [NameInEditor("Longitudinal Asymptote")]
            [EditorRange(0.5f, 1.2f)] public float longitudinalAsymptoteValue = 0.9f;

            [NameInEditor("Longitudinal Stiffness")]
            [EditorRange(1f, 3f)] public float longitudinalStiffness = 2f;

            [NameInEditor("Peak Slip Angle (deg)")]
            [EditorRange(3f, 8f)] public float peakSlipAngleDeg = 4f;

            [NameInEditor("Lateral Peak Value")]
            [EditorRange(1.2f, 2.2f)] public float lateralPeakValue = 1.8f;

            [NameInEditor("Lateral Asymptote")]
            [EditorRange(0.7f, 1.3f)] public float lateralAsymptoteValue = 1f;

            [NameInEditor("Lateral Stiffness")]
            [EditorRange(1.5f, 3.5f)] public float lateralStiffness = 2.5f;

            [NameInEditor("RWD Rear Slip Multiplier")]
            [EditorRange(1f, 1.5f)] public float rwdRearSlipMultiplier = 1.2f;

            [NameInEditor("Drift Grip Multiplier")]
            [EditorRange(0.1f, 1f)] public float driftGripMultiplier = 0.5f;

            [NameInEditor("Enable Tire Temperature")]
            public bool enableTireTemperature = false;

            [NameInEditor("Optimal Temp (°C)")]
            [EditorRange(60f, 140f)] public float optimalTempC = 95f;

            [NameInEditor("Ambient Temp (°C)")]
            [EditorRange(10f, 40f)] public float ambientTempC = 25f;

            [NameInEditor("Heat Rate Per Slip")]
            [EditorRange(0f, 10f)] public float heatRatePerSlip = 2f;

            [NameInEditor("Cooling Rate")]
            [EditorRange(0.001f, 0.1f)] public float coolingRate = 0.02f;

            [NameInEditor("Enable Tire Pressure")]
            public bool enableTirePressure = false;

            [NameInEditor("Tire Pressure (PSI)")]
            [EditorRange(24f, 40f)] public float tirePressurePsi = 32f;

            [NameInEditor("Enable Front Steering Tug")]
            public bool enableFrontSteeringTug = false;

            [NameInEditor("Front Tug Slip Threshold")]
            [EditorRange(0.1f, 0.5f)] public float frontTugSlipThreshold = 0.2f;

            [NameInEditor("Front Tug Stiffness Reduction")]
            [EditorRange(0.7f, 1f)] public float frontTugStiffnessReduction = 0.9f;
        }

        // =========================
        // BODY DYNAMICS
        // =========================
        public class BodyDynamicsSettings
        {
            [NameInEditor("Pitch Torque Gain")]
            [EditorRange(0f, 1f)] public float pitchTorqueGain = 0.35f;

            [NameInEditor("Pitch Input Multiplier")]
            [EditorRange(0f, 0.5f)] public float pitchInputMultiplier = 0.2f;

            [NameInEditor("Vertical Force Couple")]
            [EditorRange(0f, 2000f)] public float verticalForceCoupleStrength = 400f;

            [NameInEditor("Force Couple Distance")]
            [EditorRange(0.3f, 0.7f)] public float forceCoupleDistance = 0.5f;

            [NameInEditor("Roll Torque Gain")]
            [EditorRange(0f, 2f)] public float rollTorqueGain = 1.2f;

            [NameInEditor("Roll Speed Factor")]
            [EditorRange(0f, 1f)] public float rollSpeedFactor = 0.3f;

            [NameInEditor("Roll Min Speed (km/h)")]
            [EditorRange(0f, 50f)] public float rollMinSpeedKMH = 10f;

            [NameInEditor("Auto-Tune By Drivetrain")]
            public bool autoTuneByDrivetrain = true;

            [NameInEditor("Squat/Dive Strength")]
            [EditorRange(0.5f, 2f)] public float squatDiveStrength = 1f;

            [NameInEditor("Roll Intensity")]
            [EditorRange(0.3f, 1.5f)] public float rollIntensity = 1f;
        }

        // =========================
        // WHEELS (NOT SIDEBAR)
        // =========================
        public class Wheel
        {
            public bool isFront = false;
            public bool isSteer = false;
            public bool isMotor = true;

            [EditorRange(0f, 360f)] public float steerSpeed = 120f;
        }

        // =========================
        // SIDEBAR EXPORTS
        // =========================
        [ExportToSidebar("Engine")] public EngineSettings engine;
        [ExportToSidebar("Powertrain")] public PowertrainSettings powertrain;
        [ExportToSidebar("Transmission")] public TransmissionSettings transmission;
        [ExportToSidebar("Handling")] public HandlingSettings handling;
        [ExportToSidebar("Steering")] public SteeringSettings steering;
        [ExportToSidebar("Drift & Handbrake")] public DriftSettings drift;
        [ExportToSidebar("Shift Kick")] public ShiftKickSettings shiftKick;
        [ExportToSidebar("Suspension")] public SuspensionSettings suspension;
        [ExportToSidebar("Anti Roll")] public AntiRollSettings antiRoll;
        [ExportToSidebar("Brakes")] public BrakeSettings brakes;
        [ExportToSidebar("Aerodynamics")] public AerodynamicsSettings aerodynamics;
        [ExportToSidebar("Tyres")] public TyresSettings tyres;
        [ExportToSidebar("Body Dynamics")] public BodyDynamicsSettings bodyDynamics;

        public Wheel wheelfl;
        public Wheel wheelfr;
        public Wheel wheelrl;
        public Wheel wheelrr;

        public TuningEditorBehaviours()
        {
            foreach (var f in GetType().GetFields())
            {
                f.SetValue(this, Activator.CreateInstance(f.FieldType));
            }
        }

        /// <summary>
        /// Populate all editor settings from an existing VehicleController in the scene.
        /// </summary>
        public void PopulateFrom(VehicleController controller)
        {
            if (controller == null) return;

            // Engine
            var eng = controller.engine;
            if (eng != null)
            {
                engine.engineType = eng.engineType;
                engine.horsepowerHP = eng.horsepowerHP;
                engine.maxPowerKW = eng.maxPowerKW;
                engine.peakTorqueNm = eng.peakTorqueNm;
                engine.maxRPM = eng.maxRPM;
                engine.inertia = eng.inertia;
                engine.peakTorqueRPM = eng.peakTorqueRPM;
                engine.peakPowerRPM = eng.peakPowerRPM;
                engine.idleRPM = eng.idleRPM;
                engine.evBaseRPM = eng.evBaseRPM;
                engine.frictionTorque = eng.frictionTorque;
                engine.brakingTorque = eng.brakingTorque;
            }

            // Powertrain
            powertrain.isElectricVehicle = controller.isElectricVehicle;
            powertrain.drivetrain = (PowertrainSettings.DrivetrainType)controller.drivetrain;
            powertrain.topSpeedKMH = controller.topSpeedKMH;
            powertrain.physicsWheelRadius = controller.physicsWheelRadius;
            powertrain.visualWheelRadius = controller.visualWheelRadius;
            powertrain.differentialAccelSplitRWD = controller.differentialAccelSplitRWD;
            powertrain.differentialDecelSplitRWD = controller.differentialDecelSplitRWD;
            powertrain.differentialAccelSplitFWD = controller.differentialAccelSplitFWD;
            powertrain.differentialAWDRearBias = controller.differentialAWDRearBias;

            // Transmission
            var trans = controller.transmission;
            if (trans != null)
            {
                transmission.mode = trans.mode;
                transmission.isElectric = trans.isElectric;
                transmission.finalDriveRatio = trans.finalDriveRatio;
                transmission.reverseRatio = trans.reverseRatio;
                transmission.electricFixedRatio = trans.electricFixedRatio;
                transmission.upshiftRPM = trans.upshiftRPM;
                transmission.downshiftRPM = trans.downshiftRPM;
                transmission.shiftDuration = trans.shiftDuration;
            }

            // Handling
            handling.vehicleMass = controller.vehicleMass;
            handling.centerOfMassOffset = controller.centerOfMassOffset;
            handling.maxBrakeTorque = controller.maxBrakeTorque;
            handling.frontBrakeBias = controller.frontBrakeBias;
            handling.brakePressureMultiplier = controller.brakePressureMultiplier;
            handling.corneringStiffness = controller.corneringStiffness;
            handling.downforceFactor = controller.downforceFactor;
            handling.downforceExponent = controller.downforceExponent;
            handling.dragCoefficient = controller.dragCoefficient;
            handling.frontalArea = controller.frontalArea;
            handling.weightTransferFactor = controller.weightTransferFactor;
            handling.springRateAvgNpm = controller.springRateAvgNpm;
            handling.loadSensitivity = controller.loadSensitivity;
            handling.counterSteerAssist = controller.counterSteerAssist;
            handling.tractionControl = controller.tractionControl;

            // Steering
            steering.maxSteerAngle = controller.maxSteerAngle;
            steering.steeringResponse = controller.steeringResponse;
            steering.steeringReturnSpeed = controller.steeringReturnSpeed;
            steering.speedSensitiveSteering = controller.speedSensitiveSteering;

            // Drift & Handbrake
            drift.enableDriftAssist = controller.enableDriftAssist;
            drift.handbrakeSlipMultiplier = controller.handbrakeSlipMultiplier;
            drift.driftSpinFactor = controller.driftSpinFactor;

            // Shift Kick
            shiftKick.shiftKickStrength = controller.shiftKickStrength;
            shiftKick.shiftKickMinThrottle = controller.shiftKickMinThrottle;
            shiftKick.shiftChirpTorqueMultiplier = controller.shiftChirpTorqueMultiplier;

            // Suspension
            var susp = controller.suspension;
            if (susp != null)
            {
                suspension.suspensionDistance = susp.suspensionDistance;
                suspension.frequency = susp.frequency;
                suspension.damping = susp.damping;
                suspension.bias = susp.bias;
            }

            // Anti-Roll
            var arbs = controller.GetComponentsInChildren<AntiRollBar>();
            if (arbs != null && arbs.Length > 0)
            {
                antiRoll.antiRollForce = arbs[0].antiRollForce;
            }

            // Brakes
            var brakesComp = controller.GetComponent<VehicleBrakes>();
            if (brakesComp != null)
            {
                brakes.maxBrakeTorque = brakesComp.maxBrakeTorque;
            }

            // Aerodynamics
            var aero = controller.aerodynamics;
            if (aero != null)
            {
                aerodynamics.dragCoefficient = aero.dragCoefficient;
                aerodynamics.downforceCoefficient = aero.downforceCoefficient;
                aerodynamics.frontalArea = aero.frontalArea;
                aerodynamics.highSpeedThresholdKMH = aero.highSpeedThresholdKMH;
                aerodynamics.highSpeedFullKMH = aero.highSpeedFullKMH;
                aerodynamics.highSpeedDownforceMultiplier = aero.highSpeedDownforceMultiplier;
                aerodynamics.enableBodyWeave = aero.enableBodyWeave;
                aerodynamics.weaveMinSpeedKMH = aero.weaveMinSpeedKMH;
                aerodynamics.weaveTorqueStrength = aero.weaveTorqueStrength;
                aerodynamics.weaveFrequency = aero.weaveFrequency;
                aerodynamics.enableActiveAero = aero.enableActiveAero;
                aerodynamics.maxAeroAngle = aero.maxAeroAngle;
            }

            // Tyres
            var tyresComp = controller.tyres;
            if (tyresComp != null)
            {
                tyres.longitudinalPeakSlip = tyresComp.longitudinalPeakSlip;
                tyres.longitudinalPeakValue = tyresComp.longitudinalPeakValue;
                tyres.longitudinalAsymptoteValue = tyresComp.longitudinalAsymptoteValue;
                tyres.longitudinalStiffness = tyresComp.longitudinalStiffness;
                tyres.peakSlipAngleDeg = tyresComp.peakSlipAngleDeg;
                tyres.lateralPeakValue = tyresComp.lateralPeakValue;
                tyres.lateralAsymptoteValue = tyresComp.lateralAsymptoteValue;
                tyres.lateralStiffness = tyresComp.lateralStiffness;
                tyres.rwdRearSlipMultiplier = tyresComp.rwdRearSlipMultiplier;
                tyres.driftGripMultiplier = tyresComp.driftGripMultiplier;
                tyres.enableTireTemperature = tyresComp.enableTireTemperature;
                tyres.optimalTempC = tyresComp.optimalTempC;
                tyres.ambientTempC = tyresComp.ambientTempC;
                tyres.heatRatePerSlip = tyresComp.heatRatePerSlip;
                tyres.coolingRate = tyresComp.coolingRate;
                tyres.enableTirePressure = tyresComp.enableTirePressure;
                tyres.tirePressurePsi = tyresComp.tirePressurePsi;
                tyres.enableFrontSteeringTug = tyresComp.enableFrontSteeringTug;
                tyres.frontTugSlipThreshold = tyresComp.frontTugSlipThreshold;
                tyres.frontTugStiffnessReduction = tyresComp.frontTugStiffnessReduction;
            }

            // Body Dynamics
            var bd = controller.bodyDynamics;
            if (bd != null)
            {
                bodyDynamics.pitchTorqueGain = bd.pitchTorqueGain;
                bodyDynamics.pitchInputMultiplier = bd.pitchInputMultiplier;
                bodyDynamics.verticalForceCoupleStrength = bd.verticalForceCoupleStrength;
                bodyDynamics.forceCoupleDistance = bd.forceCoupleDistance;
                bodyDynamics.rollTorqueGain = bd.rollTorqueGain;
                bodyDynamics.rollSpeedFactor = bd.rollSpeedFactor;
                bodyDynamics.rollMinSpeedKMH = bd.rollMinSpeedKMH;
                bodyDynamics.autoTuneByDrivetrain = bd.autoTuneByDrivetrain;
                bodyDynamics.squatDiveStrength = bd.squatDiveStrength;
                bodyDynamics.rollIntensity = bd.rollIntensity;
            }
        }
    }


    [AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = true)]
    internal class ExportToSidebarAttribute : Attribute
    {
        readonly string name;

        public ExportToSidebarAttribute(string name)
        {
            this.name = name;

        }

        public string Name
        {
            get { return name; }
        }
    }

    [AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = true)]
    internal class EditorRangeAttribute : Attribute
    {
        readonly float min, max;

        public EditorRangeAttribute(float min, float max)
        {
            this.min = min;
            this.max = max;
        }

        public float Min => min;
        public float Max => max;
    }

    [AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = true)]
    public class NameInEditor : Attribute
    {
        readonly string name;

        public NameInEditor(string name)
        {
            this.name = name;
        }

        public string Name => name;

    }
}
