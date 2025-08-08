using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShipSystem : MonoBehaviour
{
    public static ShipSystem Instance;
    [Header("Prefabs")]
    public GameObject ShipPrefab;
    public GameObject ExplosionPrefab;
    [Header("Runtime")]
    public GameObject[] Ships;
    int MaxShips = 15;
    public int nShips = 0;
    public bool UseRotationStabilizers;
    public bool UsePositionStabilizers;
    public float StabilizerTheshold = 0.01f;
    public float ExplosionPower = 100000.0f;

    void Start()
    {
        Instance = this;
        Ships = new GameObject[MaxShips];
    }

    public GameObject AddShip(Vector3 Position, team Team)
    {
        var go = Instantiate(ShipPrefab, Position, Quaternion.identity);
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

                foreach (var Thruster in Ships[i].GetComponentsInChildren<Thruster>())
                {
                    Thruster.transform.GetChild(0).gameObject.SetActive(false);
                }
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
    }

    private void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        var NewShips = new GameObject[MaxShips];
        int nNewShips = 0;
        float ThresholdSq = StabilizerTheshold * StabilizerTheshold;
        for (int i = 0; i < nShips; i++)
        {
            var ShipGo = Ships[i];
            if (ShipGo != null)
            {
                var Ship = ShipGo.GetComponent<Ship>();
                var rb = Ship.GetComponent<Rigidbody>();

                if(Ship.Health == 0)
                {
                    // It's not a threat to anyone anymore, so switch to team Neutral.
                    Ship.GetComponent<Team>().value = team.Neutral;
                    Explode(ShipGo); // For now just have it explode when it dies cuz its satisfying. 
                    //Destroy(Ship); // Remove ship component.
                }

                if (Ship.Health > 0)
                {
                    // Accelerate in desired direction.
                    Ship.Accelerate(Ship.transform, Ship.Controls.DesiredDirection, Ship.Controls.ThrusterPower);

                    // Auto stabilize Rotational velocity. (counteracts rotational velocity).
                    if (UseRotationStabilizers && rb.angularVelocity.sqrMagnitude > ThresholdSq)
                    {
                        Ship.AngularAccelerate(Ship.transform, -rb.angularVelocity, rb.angularVelocity.magnitude);
                    }
                    // Auto stabilize Translational velocity.
                    if (UsePositionStabilizers && rb.velocity.sqrMagnitude > ThresholdSq && Ship.Controls.DesiredDirection == Vector3.zero)
                    {
                        Ship.Accelerate(Ship.transform, -rb.velocity, rb.velocity.magnitude);
                    }
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
