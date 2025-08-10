using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShipSystem : MonoBehaviour
{
    public static ShipSystem Instance;
    [Header("Prefabs")]
    public GameObject[] ShipPrefabs;
    public GameObject ExplosionPrefab;
    [Header("Runtime")]
    public GameObject[] Ships;
    int MaxShips = 15;
    public int nShips = 0;
    public bool UseRotationStabilizers;
    public bool UsePositionStabilizers;
    public float StabilizerTheshold = 0.01f;
    public float PositionStabilizerTheshold = 1.0f;
    public float ExplosionPower = 100000.0f;
    int _idx;

    public enum ShipType
    {
        Heavy,
        Light,
    }

    void Start()
    {
        Instance = this;
        Ships = new GameObject[MaxShips];
    }

    public GameObject AddShip(Vector3 Position, team Team, ShipType type = ShipType.Heavy)
    {
        var go = Instantiate(ShipPrefabs[(int)type], Position, Quaternion.identity);
        go.GetComponent<Team>().value = Team;
        // Assign the team to all armor and turret subcomponents. (so that AIs have an easier time targetting pieces of the ship).
        foreach (var team in go.GetComponentsInChildren<Team>()) team.value = Team;
        Ships[nShips] = go;
        nShips++;
        return go;
    }

    public void RemoveShip(GameObject Ship)
    {
        Ship.GetComponent<Ship>().Health = -10;
    }

    // Update is called once per frame
    void Update()
    {
        // Test (bad practice...) Todo: fix.
        for (int i = 0; i < nShips; i++)
        {
            if (Ships[i] != null)
            {
                Ships[i].name = i.ToString();

                // Idk what this was for?
                /*foreach (var Thruster in Ships[i].GetComponentsInChildren<Thruster>())
                {
                    Thruster.transform.GetChild(0).gameObject.SetActive(false);
                }*/
            }
        }

        // Test
        if (Input.GetKeyDown(KeyCode.Alpha1))
            Ships[0].GetComponent<Ship>().Health = 0;

        // Kill all enemies
        if (Input.GetKeyDown(KeyCode.Alpha2))
            for (int i = 0; i < nShips; i++)
                if (Ships[i].GetComponent<Team>().value == team.Enemies)
                    Ships[i].GetComponent<Ship>().Health = 0;

        if (Input.GetKeyDown(KeyCode.Alpha3))
            if(nShips > 1)
                Ships[1].GetComponent<Ship>().Health = 0;

        if (Input.GetKeyDown(KeyCode.Mouse2))
            Debug.Break();
    }

    void Explode(GameObject Ship)
    {
        // Disable all thrusters
        foreach (var thruster in Ship.GetComponentsInChildren<Thruster>())
            Destroy(thruster);
        // Disable all turrets
        foreach (var turret in Ship.GetComponentsInChildren<TurretComponent>())
            turret.Skip = true;

        // Explode
        Vector3 ExplositonPosition = Ship.transform.position + Vector3.left;
        float ExplositonRadius = 150.0f;

        // Make sub-pieces fly off.
        var ShipRb = Ship.GetComponent<Rigidbody>();
        var armors = Ship.GetComponentsInChildren<Armor>();
        float SubMass = ShipRb.mass / (float)armors.Length;
        foreach (var armor in armors)
        {
            // Unparent
            armor.transform.SetParent(null, worldPositionStays: true);
            armor.gameObject.AddComponent<DestroyTimer>().Seconds = 5.0f;
            var rb = armor.gameObject.TryAddComponent<Rigidbody>();
            rb.mass = SubMass;
            rb.useGravity = false;
            rb.AddExplosionForce(ExplosionPower, ExplositonPosition, ExplositonRadius, 0.0f, ForceMode.Impulse);
        }

        // Vfx
        var Explosion = Instantiate(ExplosionPrefab, Ship.transform);
        Explosion.transform.position = Ship.transform.position;
        Explosion.SetActive(true);
        // Sfx
        //SoundSystem.Instance.PlaySound(Sound.Explosion, Ship.transform.position, 30000.0f);
    }

    void CalculateIdeal(int idx)
    {
        var Ship = Ships[idx].GetComponent<Ship>();
        Ship.IdealDirection = Ship.transform.worldToLocalMatrix * ShipUtility.GetIdealDirection(Ship);
    }

    private void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;

        if (nShips > 0)
        {
            var NewShips = new GameObject[MaxShips];
            int nNewShips = 0;

            // Calculate the ideal direction for one ship at a time
            CalculateIdeal(_idx);

            float RotationThresholdSq = StabilizerTheshold * StabilizerTheshold;
            float PositionThresholdSq = PositionStabilizerTheshold * PositionStabilizerTheshold;
            for (int i = 0; i < nShips; i++)
            {
                var ShipGo = Ships[i];
                if (ShipGo != null)
                {
                    var Ship = ShipGo.GetComponent<Ship>();
                    
                    if (Ship.Health == 0)
                    {
                        // It's not a threat to anyone anymore, so switch to team Neutral.
                        Ship.GetComponent<Team>().value = team.Neutral;
                        Explode(ShipGo); // For now just have it explode when it dies cuz its satisfying. 
                    }
                    else if (Ship.Health > 0)
                    {
                        var rb = Ship.GetComponent<Rigidbody>();

                        _Torque = Ship.Torque.magnitude / rb.mass;
                        _Velocity = rb.angularVelocity.magnitude; // We know this is radians/sec.
                        _DistanceToStop = Mathf.Abs(ShipUtility.DistanceToReachVelocity(0, _Velocity, _Torque));

                        // Auto stabilize Translational velocity.
                        if (UsePositionStabilizers && rb.velocity.sqrMagnitude > PositionThresholdSq && Ship.Controls.DesiredDirection == Vector3.zero)
                        {
                            Ship.Controls.DesiredDirection = -rb.velocity;
                            Ship.Controls.DirectionPower = rb.velocity.magnitude;
                        }

                        // Rotate the ship so that its facing the optimal direction in terms of thrusters and ship controls.
                        // (Only if the ideal direction is actually significant.)
                        if (Ship.IdealDirection.magnitude >= 2.0f)
                        {
                            //Debug.Log("Rotating towards ideal direction");
                            // Rotate so that our desired direction equals our ideal direction.
                            Vector3 Ideal = Ship.transform.localToWorldMatrix * Ship.IdealDirection;
                            // Use negative because our ideal direction actual refers to the direction of thrust, but desired refers to the direction we want to move.
                            Vector3 Desired = -Ship.Controls.DesiredDirection;
                            // Get the axis
                            Vector3 RotateAxis = Vector3.Cross(Ideal, Desired).normalized;

                            DistanceToIdeal = Mathf.Abs(Vector3.Angle(Desired, Ideal)) * Mathf.Deg2Rad;

                            // Prediction.
                            bool IsGoingRightWay = Vector3.Dot(RotateAxis, rb.angularVelocity) > 0;
                            if (IsGoingRightWay && _DistanceToStop >= DistanceToIdeal) RotateAxis *= -1.0f;

                            // Now rotate
                            if (DistanceToIdeal > 0.01f)
                            {
                                Ship.Controls.DesiredRotation = RotateAxis; // * Mathf.Clamp01(DistanceToIdeal); // Micro adjustment factor.
                                _ThrustAmount = Ship.Controls.DirectionPower;
                            }

                            // If we've reached the desired direction, start moving forwards
                            // (Don't move forwards until reached desired direction.
                            if (DistanceToIdeal > 0.5f)
                            {
                                Ship.Controls.DesiredDirection = Vector3.zero;
                                Ship.Controls.DirectionPower = 0.0f;
                                _ReachedIdeal = false;
                            }
                            else
                            {
                                Ship.Controls.DirectionPower = 1.0f;
                                _ReachedIdeal = true;
                            }
                        }

                        // Auto stabilize Rotational velocity. (counteracts rotational velocity).
                        if (UseRotationStabilizers && rb.angularVelocity.sqrMagnitude > RotationThresholdSq && Ship.Controls.DesiredRotation == Vector3.zero)
                        {
                            Ship.Controls.DesiredRotation = -rb.angularVelocity;
                        }

                        // Finally, apply translational/rotational thrust
                        if (Ship.Controls.DesiredRotation != Vector3.zero)
                        {
                            Ship.AngularAccelerate(Ship.transform, Ship.Controls.DesiredRotation);
                        }

                        if (Ship.Controls.DesiredDirection != Vector3.zero)
                        {
                            Ship.Accelerate(Ship.transform, Ship.Controls.DesiredDirection, Ship.Controls.DirectionPower);
                        }

                        Ship.Controls.DesiredRotation = Vector3.zero; // Reset.

                        // Cache torque.
                        Ship.Torque = rb.GetAccumulatedTorque();
                    }

                    if (Ship.Health <= 0)
                    {
                        Ship.Health -= dt;
                    }

                    // After 5 seconds, actually remove the ship.
                    if (Ship.Health > -5)
                    {
                        // Survive the ship.
                        NewShips[nNewShips] = Ships[i];
                        nNewShips++;
                    }
                    else
                    {
                        // Destroy it
                        Destroy(Ships[i].gameObject);
                    }
                }
            }
            Ships = NewShips;
            nShips = nNewShips;
        }
    }

    float _DistanceToStop;
    float DistanceToIdeal;
    float _Velocity;
    float _Torque;
    float _ThrustAmount;
    bool _ReachedIdeal;
    /*private void OnGUI()
    {
        if (nShips > 0)
        {
            var ShipPos = Instance.Ships[0];
            var ScreenPos = Camera.main.WorldToScreenPoint(ShipPos.transform.position);
            ScreenPos.y += 60;
            var w = 300;
            var h = 20;
            float Velocity = Mathf.Round(_Velocity * Mathf.Rad2Deg);
            float Torque = Mathf.Round(_Torque * Mathf.Rad2Deg);
            //float Answer = Mathf.Round(ShipUtility.DistanceToReachVelocity(0, _Velocity, _Torque) * Mathf.Rad2Deg);
            float Answer = Mathf.Round(ShipUtility.DistanceToReachVelocity(0, Velocity, Torque));
            // Display as degrees for readability
            GUI.Label(new Rect(ScreenPos.x, ScreenPos.y, w, h), string.Format("ToStop: {0}°", Mathf.Round(_DistanceToStop * Mathf.Rad2Deg)));
            ScreenPos.y += h;
            GUI.Label(new Rect(ScreenPos.x, ScreenPos.y, w, h), string.Format("ToIdeal: {0}°", Mathf.Round(DistanceToIdeal * Mathf.Rad2Deg)));
            ScreenPos.y += h;
            GUI.Label(new Rect(ScreenPos.x, ScreenPos.y, w, h), string.Format("Velocity: {0}°", Velocity));
            ScreenPos.y += h;
            GUI.Label(new Rect(ScreenPos.x, ScreenPos.y, w, h), string.Format("Torque: {0}°", Torque));
            ScreenPos.y += h;
            GUI.Label(new Rect(ScreenPos.x, ScreenPos.y, w, h), string.Format("({0}² - {1}²) / 2 * {2} = {3}", 0, Velocity, Torque, Answer));
            ScreenPos.y += h;
            GUI.Label(new Rect(ScreenPos.x, ScreenPos.y, w, h), string.Format("Thrust Amount: {0}%", Mathf.Round(_ThrustAmount * 100)));
            if (_ReachedIdeal)
            {
                ScreenPos.y += h;
                GUI.Label(new Rect(ScreenPos.x, ScreenPos.y, w, h), "Reached ideal");
            }
        }
    }*/
}
