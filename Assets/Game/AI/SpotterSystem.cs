using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpotterSystem : MonoBehaviour
{
    public float TickRate = 0.5f;
    public LayerMask Layer;

    // Update the list of enemies

    // Start is called before the first frame update
    void Start()
    {
        //StartCoroutine(Tick());
    }

    IEnumerator Tick()
    {
        var ShipSystem = global::ShipSystem.Instance;
        var TeamSystem = Teams.Instance;
        var Colliders = new Collider[5]; // Allocate once.

        // Each ship will look around for enemy ships, and report them to the central team data.
        while (true)
        {
            yield return new WaitForSeconds(TickRate);

            // Clear spotted enemies each time.
            for (int i = 0; i < TeamSystem.nTeams; i++)
            {
                TeamSystem.DataPerTeam[i].SpottedEnemies.Clear();
            }

            // Now every ship will look for spotted enemies.
            //Debug.LogFormat("Iterating {0} ships.", ShipSystem.nShips);
            for (int i = 0; i < ShipSystem.nShips; i++)
            {
                var go = ShipSystem.Ships[i];
                var Ship = go.GetComponent<Ship>(); // Ship data.

                // Spot enemies.
                int nSpotted = Physics.OverlapSphereNonAlloc(go.transform.position, Ship.SpottingRange, Colliders, Layer);
                if (nSpotted > 0)
                {
                    //Debug.LogFormat("Spotted {0} ships", nSpotted);
                    // Add enemies to spotted list. 
                    var TeamId = Ship.GetComponent<Team>().value;
                    for (int j = 0; j < nSpotted; j++)
                    {
                        var SpottedEnemy = Colliders[j];

                        if (SpottedEnemy.TryGetComponent(out Ship SpottedShip))
                        {
                            // Make sure its not our team (this will also avoid targetting self).
                            var Team = SpottedShip.GetComponent<Team>().value;
                            if (Team != TeamId && Team != team.Neutral)
                            {
                                var Payload = new SpottedEnemy()
                                {
                                    Position = SpottedEnemy.transform.position,
                                    Velocity = SpottedEnemy.GetComponent<Rigidbody>().velocity
                                };
                                //if(Ship.Team == Team.Enemy) Debug.LogFormat("(Team {0}) Spotted enemy at {1}", Ship.Team, Payload.Position);

                                TeamSystem.DataPerTeam[(int)TeamId].SpottedEnemies.TryAdd(SpottedEnemy.transform.position, Payload);
                            }
                        }
                        else Debug.LogError("Spotted enemy didn't have Ship component", SpottedEnemy);
                    }
                }
            }
        }
    }
}
