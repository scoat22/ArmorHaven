using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShipSystem : MonoBehaviour
{
    public static ShipSystem Instance; 

    public GameObject ShipPrefab;
    public List<GameObject> Ships;
    public int nShips => Ships.Count;
    public bool UseRotationStabilizers;
    public bool UsePositionStabilizers;
    public float StabilizerTheshold = 0.01f;

    // Start is called before the first frame update
    void Start()
    {
        Instance = this;
        Ships = new List<GameObject>();
        AddShip(Vector3.right, team.Allies);
    }

    public GameObject AddShip(Vector3 Position, team Team)
    {
        var go = Instantiate(ShipPrefab, Position, Quaternion.identity);
        go.GetComponent<Ship>().Team = Team;
        // Assign the team to all armor and turret subcomponents. (so that AIs have an easier time targetting pieces of the ship).
        foreach (var team in go.GetComponentsInChildren<Team>()) team.value = Team;
        Ships.Add(go);
        return go;
    }

    // Update is called once per frame
    void Update()
    {
        for (int i = 0; i < nShips; i++)
        {
            Ships[i].name = i.ToString();
        }

        // Test (bad practice...) Todo: fix.
        foreach (var Ship in Ships)
        {
            foreach (var Thruster in Ship.GetComponentsInChildren<Thruster>())
            {
                Thruster.transform.GetChild(0).gameObject.SetActive(false);
            }
        }
    }

    private void FixedUpdate()
    {
        float ThresholdSq = StabilizerTheshold * StabilizerTheshold;
        for (int i = 0; i < nShips; i++)
        {
            var Ship = Ships[i].GetComponent<Ship>();

            Ship.Accelerate(Ship.transform, Ship.Controls.DesiredDirection, Ship.Controls.ThrusterPower);

            var rb = Ship.GetComponent<Rigidbody>();

            
            // Auto stabilize (counteract rotational velocity).
            if (UseRotationStabilizers && rb.angularVelocity.sqrMagnitude > ThresholdSq)
            {
                Ship.AngularAccelerate(Ship.transform, -rb.angularVelocity, rb.angularVelocity.magnitude);
            }
            if(UsePositionStabilizers && rb.velocity.sqrMagnitude > ThresholdSq && Ship.Controls.DesiredDirection == Vector3.zero)
            {
                Ship.Accelerate(Ship.transform, -rb.velocity, rb.velocity.magnitude);
            }
        }
    }
}
