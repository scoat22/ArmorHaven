using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class RoundSystem : MonoBehaviour
{
    public static RoundSystem Instance;
    /// <summary>
    /// Radius.
    /// </summary>
    public float SpawnDistance = 300.0f;
    public int nEnemiesFirstRound = 4;
    public int MaxEnemies = 4;
    private int nEnemies = 0;

    public int Round = 0;
    [Header("UI")]
    public Text RoundText;
    public float FadeSpeed = 1.0f;
    [Header("Sfx")]
    public AudioClip RoundOverClip;
    RoundState State;
    enum RoundState { Playing, NewRound, Restarting, }

    // Start is called before the first frame update
    void Start()
    {
        Instance = this;
        State = RoundState.Playing;
        nEnemies = nEnemiesFirstRound - 1;
    }

    private void FixedUpdate()
    {
        var Ships = ShipSystem.Instance.Ships;
        var nShips = ShipSystem.Instance.nShips;

        // If there's no ships, create player ship
        if (nShips == 0)
        {
            //Debug.Log("Creating player ship");
            ShipSystem.Instance.AddShip(Vector3.right, team.Allies); // Add player ship.
        }

        if (State == RoundState.Playing)
            if (EnemyCount() == 0)
                StartCoroutine(StartNextRound());

        // If we aren't already restarting, and the player has no health.
        if (State != RoundState.Restarting && nShips > 0 && Ships[0].GetComponent<Ship>().Health <= 0)
        {
            // Player died.
            State = RoundState.Restarting;
            StartCoroutine(Restart());
        }

        if(State == RoundState.NewRound)
        {
            RoundText.color = new Color(1, 1, 1, Mathf.Sin(Time.time * FadeSpeed) * 0.5f + 0.5f);
        }
        else
        {
            RoundText.color = Color.red;
        }
    }

    IEnumerator Restart()
    {
        yield return new WaitForSeconds(4.7f);

        //Debug.Log("Restarting: Removing all ships");
        // Remove all ships.
        var ShipSystem = global::ShipSystem.Instance;
        var Ships = ShipSystem.Ships;
        for (int i = 0; i < ShipSystem.nShips; i++)
            if(Ships[i] != null)
                ShipSystem.Instance.RemoveShip(Ships[i]);

        Round = 0;                  // Reset round.
        State = RoundState.Playing; // Reset state.
    }

    IEnumerator StartNextRound()
    {
        State = RoundState.NewRound;
        Round++;

        yield return new WaitForSeconds(2.0f);

        GetComponent<AudioSource>().PlayOneShot(RoundOverClip);

        State = RoundState.Playing;
        RoundText.text = Round.ToString(); // Update UI.
        //nEnemies++;
        nEnemies = Mathf.Min(MaxEnemies, nEnemies);
        SpawnWave(nEnemies, team.Enemies);
    }

    public void SpawnWave(int nEnemies, team Team)
    {
        Debug.LogFormat("Spawning {0} enemies.", nEnemies);

        if (nEnemies <= 0)
        {
            //Debug.LogErrorFormat("Warning: spawning {0} enemeies", nEnemies);
        }
        
        for (int i = 0; i < nEnemies; i++)
        {
            // Determine position.
            /*Vector3 Position = new Vector3(Random.Range(-SpawnDistance, SpawnDistance), 0, Random.Range(-SpawnDistance, SpawnDistance));
            // Make clearing in the center.
            Position += Position.normalized * 30.0f;*/

            Vector2 RandPosInCircle = Random.insideUnitCircle;
            Vector3 Position = new Vector3(RandPosInCircle.x, 0, RandPosInCircle.y);
            Position += Position.normalized * SpawnDistance;            

            // Spawn the ship.
            var go = ShipSystem.Instance.AddShip(Position, team.Enemies);
        }
    }

    int EnemyCount()
    {
        var Ships = ShipSystem.Instance.Ships;
        var nShips = ShipSystem.Instance.nShips;

        // Count enemies.
        int nEnemies = 0;
        for (int i = 0; i < nShips; i++)
            if (Ships[i].GetComponent<Team>().value == team.Enemies)
                nEnemies++;

        return nEnemies;
    }
}
