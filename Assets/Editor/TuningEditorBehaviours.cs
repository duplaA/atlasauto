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
            public VehicleEngine.EngineType engineType;

            [EditorRange(0f, 2000f)] public float horsepowerHP;
            [EditorRange(0f, 1500f)] public float maxPowerKW;

            [EditorRange(0f, 2000f)] public float peakTorqueNm;
            [EditorRange(0f, 12000f)] public float maxRPM;
            [EditorRange(0f, 5f)] public float inertia;

            [EditorRange(0f, 12000f)] public float peakTorqueRPM;
            [EditorRange(0f, 12000f)] public float peakPowerRPM;
            [EditorRange(0f, 3000f)] public float idleRPM;

            [EditorRange(0f, 12000f)] public float evBaseRPM;

            [EditorRange(0f, 200f)] public float frictionTorque;
            [EditorRange(0f, 300f)] public float brakingTorque;
        }

        // =========================
        // POWERTRAIN / CONTROLLER
        // =========================
        public class PowertrainSettings
        {
            public bool isElectricVehicle;

            public enum DrivetrainType { RWD, FWD, AWD }
            public DrivetrainType drivetrain;

            [EditorRange(50f, 500f)] public float topSpeedKMH;
            [EditorRange(0.1f, 1.0f)] public float physicsWheelRadius;
        }

        // =========================
        // TRANSMISSION
        // =========================
        public class TransmissionSettings
        {
            public VehicleTransmission.TransmissionMode mode;
            public bool isElectric;

            [EditorRange(1f, 10f)] public float finalDriveRatio;
            [EditorRange(1f, 15f)] public float reverseRatio;

            // EV
            [EditorRange(1f, 20f)] public float electricFixedRatio;

            // ICE Shift Logic
            [EditorRange(0.5f, 1f)] public float upshiftRPM;
            [EditorRange(0f, 0.8f)] public float downshiftRPM;
            [EditorRange(0f, 1f)] public float shiftDuration;
        }

        // =========================
        // HANDLING
        // =========================
        public class HandlingSettings
        {
            [EditorRange(500f, 5000f)] public float vehicleMass;
            public Vector3 centerOfMassOffset;

            [EditorRange(0f, 10000f)] public float maxBrakeTorque;
            [EditorRange(0f, 1f)] public float frontBrakeBias;

            [EditorRange(0.5f, 10f)] public float corneringStiffness;
            [EditorRange(0f, 20f)] public float downforceFactor;
        }

        // =========================
        // STEERING
        // =========================
        public class SteeringSettings
        {
            [EditorRange(5f, 60f)] public float maxSteerAngle;
            public bool speedSensitiveSteering;
        }

        // =========================
        // SUSPENSION
        // =========================
        public class SuspensionSettings
        {
            [EditorRange(0.05f, 0.5f)] public float suspensionDistance;

            public VehicleSuspension.SpringFrequency frequency;
            public VehicleSuspension.DampRatio damping;
            public VehicleSuspension.FrontRearBias bias;
        }

        // =========================
        // ANTI ROLL BAR
        // =========================
        public class AntiRollSettings
        {
            [EditorRange(0f, 50000f)]
            public float antiRollForce;
        }

        // =========================
        // BRAKES
        // =========================
        public class BrakeSettings
        {
            [EditorRange(0f, 10000f)]
            public float maxBrakeTorque;
        }

        // =========================
        // DEBUG (READONLY INTENT)
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
            public bool isFront;
            public bool isSteer;
            public bool isMotor;

            [EditorRange(0f, 360f)] public float steerSpeed;
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
        [ExportToSidebar("Debug")] public DebugSettings debug;

        public Wheel wheelFl;
        public Wheel wheelFr;
        public Wheel wheelRl;
        public Wheel wheelRr;

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
