using UnityEngine;
using UnityEngine.InputSystem;

public class VehicleController : MonoBehaviour
{
    [Header("Engine & Transmission")]
    public bool isElectric = false;
    public float peakTorqueNm = 450f;
    public float maxRPM = 7000f;
    public float idleRPM = 800f;
    public float[] gearRatios = { 3.5f, 2.1f, 1.4f, 1.0f, 0.75f };
    public float finalDriveRatio = 3.4f;

    [Header("Physics Settings")]
    public float maxBrakeTorque = 10000f;
    public float tyreStiffness = 2.0f;
    public float gripMultiplier = 1.5f;

    // Belső állapotok
    public float currentRPM;
    public int currentGear = 0; // -1: R, 0: N, 1-5: D
    private float speedKMH;
    private Vector2 moveInput;
    private Rigidbody rb;
    private VehicleWheel[] wheels;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb) {
            rb.mass = 1600f;
            rb.centerOfMass = new Vector3(0, -0.7f, 0.1f);
        }
        DiscoverWheels();
    }

    public void OnMove(InputValue value) => moveInput = value.Get<Vector2>();

    void FixedUpdate()
    {
        if (wheels == null || wheels.Length == 0) return;

        speedKMH = rb.linearVelocity.magnitude * 3.6f;
        float localVelZ = transform.InverseTransformDirection(rb.linearVelocity).z;
        float inputY = moveInput.y;

        // 1. FÉK ÉS IRÁNYVÁLTÁS LOGIKA
        float brakeInput = 0;
        if (localVelZ > 0.5f && inputY < -0.1f) brakeInput = Mathf.Abs(inputY);
        else if (localVelZ < -0.5f && inputY > 0.1f) brakeInput = Mathf.Abs(inputY);

        if (speedKMH < 3f && brakeInput < 0.1f) {
            if (inputY > 0.1f) currentGear = 1;
            else if (inputY < -0.1f) currentGear = -1;
            else currentGear = 0;
        }

        float throttle = (brakeInput > 0.1f) ? 0 : Mathf.Abs(inputY);

        // 2. VÁLTÓ ÉS RPM SZINKRON
        float totalRatio = GetTotalRatio();
        float avgWheelRPM = 0; int mCount = 0;
        foreach (var w in wheels) if (w.isMotor) { avgWheelRPM += w.GetRPM(); mCount++; }
        avgWheelRPM = (mCount > 0) ? avgWheelRPM / mCount : 0;

        UpdateEngineAndGear(throttle, avgWheelRPM, brakeInput);

        // 3. ERŐK KISZÁMÍTÁSA
        float engineTorque = GetEngineTorque(throttle);
        // A hajtott kerekekre jutó nyomaték (differenciálmű szimulációval)
        float wheelTorque = (engineTorque * totalRatio * 15f) / (mCount > 0 ? mCount : 1);

        // 4. KERÉK IMPLEMENTÁCIÓ (Fizika + Tapadás)
        foreach (var w in wheels)
        {
            // Tapadás beállítása (Jég-effekt ellen)
            UpdateWheelFriction(w.wheelCollider);

            // Hajtás és Fék alkalmazása
            if (w.isMotor) w.ApplyTorque(brakeInput > 0.1f ? 0 : wheelTorque);
            w.ApplyBrake(brakeInput * maxBrakeTorque);
            
            // Kormányzás
            if (w.isSteer) w.ApplySteer(moveInput.x * 35f);
        }

        // Végsebesség korlátozás (leszabályzáskor fizikai ellenállás)
        rb.linearDamping = (currentRPM >= maxRPM - 100) ? 1.0f : 0.05f;
    }

    void UpdateEngineAndGear(float throttle, float wheelRPM, float brakeInput)
    {
        float minRPM = isElectric ? 0 : idleRPM;
        float physicalRPM = Mathf.Abs(wheelRPM * GetTotalRatio());

        // Fordulatszám esés váltáskor és követés
        currentRPM = Mathf.Lerp(currentRPM, physicalRPM, Time.fixedDeltaTime * 8f);
        if (currentRPM < minRPM) currentRPM = Mathf.MoveTowards(currentRPM, minRPM, 2000 * Time.fixedDeltaTime);
        if (throttle > 0.1f && currentRPM < maxRPM) currentRPM += throttle * 500 * Time.fixedDeltaTime;

        // Automata váltás (ICE esetén)
        if (!isElectric && currentGear > 0) {
            if (currentRPM > 5800 && currentGear < gearRatios.Length && speedKMH > currentGear * 20) currentGear++;
            else if (currentRPM < 2200 && currentGear > 1) currentGear--;
        }
    }

    float GetEngineTorque(float throttle) {
        if (currentRPM >= maxRPM) return 0;
        float factor = isElectric ? 1.0f : Mathf.Clamp01(currentRPM / 3000f);
        return peakTorqueNm * throttle * factor;
    }

    float GetTotalRatio() {
        if (currentGear == 0) return 0;
        float ratio = isElectric ? 8.0f : (currentGear == -1 ? -3.2f : gearRatios[currentGear - 1]);
        return ratio * finalDriveRatio;
    }

    void UpdateWheelFriction(WheelCollider wc) {
        WheelFrictionCurve fwd = wc.forwardFriction;
        fwd.stiffness = tyreStiffness;
        fwd.extremumValue = gripMultiplier;
        wc.forwardFriction = fwd;

        WheelFrictionCurve side = wc.sidewaysFriction;
        side.stiffness = tyreStiffness;
        side.extremumValue = gripMultiplier;
        wc.sidewaysFriction = side;
    }

    void DiscoverWheels() {
        var colliders = GetComponentsInChildren<WheelCollider>();
        wheels = new VehicleWheel[colliders.Length];
        for (int i = 0; i < colliders.Length; i++) {
            var wc = colliders[i];
            var vw = wc.gameObject.GetComponent<VehicleWheel>() ?? wc.gameObject.AddComponent<VehicleWheel>();
            vw.wheelCollider = wc;
            vw.isMotor = transform.InverseTransformPoint(wc.transform.position).z < 0;
            vw.isSteer = !vw.isMotor;
            wheels[i] = vw;
        }
    }

    void Update() { if (wheels != null) foreach (var w in wheels) w.SyncVisuals(); }

    void OnGUI() {
        GUI.Box(new Rect(10, 10, 200, 100), "ULTIMATE VEHICLE");
        GUI.Label(new Rect(20, 30, 180, 20), $"Speed: {speedKMH:F1} km/h");
        GUI.Label(new Rect(20, 50, 180, 20), $"RPM: {currentRPM:F0}");
        GUI.Label(new Rect(20, 70, 180, 20), $"Gear: {currentGear} | EV: {isElectric}");
    }
}