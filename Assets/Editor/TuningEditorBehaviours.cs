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

            [EditorRange(0.5f, 5f)] public float corneringStiffness = 2.5f;
            [EditorRange(0f, 10f)] public float downforceFactor = 3f;

            [NameInEditor("Weight Transfer")]
            [EditorRange(0f, 1f)] public float weightTransferFactor = 0.4f;

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
        // DEBUG (INTERNAL - NOT IN SIDEBAR)
        // =========================
        public class DebugSettings
        {
            [EditorRange(0f, 400f)] public float speedKMH;
            [EditorRange(0f, 120f)] public float speedMS;
            [EditorRange(0f, 2000f)] public float engineTorque;
            [EditorRange(0f, 2000f)] public float driveTorque;
            [EditorRange(0f, 400f)] public float expectedSpeedKMH;
            [EditorRange(0f, 1f)] public float slipRatio;
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
        [ExportToSidebar("Suspension")] public SuspensionSettings suspension;
        [ExportToSidebar("Anti Roll")] public AntiRollSettings antiRoll;
        [ExportToSidebar("Brakes")] public BrakeSettings brakes;
        // Debug settings are internal, not exported to sidebar
        public DebugSettings debug;

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
