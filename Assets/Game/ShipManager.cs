using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShipManager : MonoBehaviour
{
    public static ShipManager Instance; 

    public GameObject ShipPrefab;
    public List<GameObject> Ships;

    // Start is called before the first frame update
    void Start()
    {
        Instance = this;
        Ships = new List<GameObject>();
        AddShip(Vector3.zero);
    }

    void AddShip(Vector3 Position)
    {
        Ships.Add(Instantiate(ShipPrefab, Position, Quaternion.identity));
    }

    // Update is called once per frame
    void Update()
    {
        for (int i = 0; i < Ships.Count; i++)
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
}
