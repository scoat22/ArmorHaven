using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class RoundSystem : MonoBehaviour
{
    /// <summary>
    /// Radius.
    /// </summary>
    public float SpawnRange = 100.0f;
    public int nEnemiesFirstRound = 4;
    public int MaxEnemies = 4;
    private int nEnemies = 0;

    public int Round = 0;
    public Text RoundText;
    public AudioClip RoundOverClip;

    // Start is called before the first frame update
    void Start()
    {
        nEnemies = nEnemiesFirstRound - 1;
        //SpawnWave(nEnemiesFirstRound, Team.Enemies);
        //SpawnWave(nShipsFirstRound, Team.Allies);

        StartNextRound();
    }

    private void FixedUpdate()
    {
        var Ships = ShipSystem.Instance.Ships;

        int nEnemies = 0;
        for (int i = 0; i < Ships.Count; i++)
            if (Ships[i].GetComponent<Ship>().Team == team.Enemies)
                nEnemies++;

        if (nEnemies == 0)
        {
            RoundOver();
            StartNextRound();
        }
    }

    void RoundOver()
    {
        GetComponent<AudioSource>().PlayOneShot(RoundOverClip);
    }

    void StartNextRound()
    {
        Round++;
        RoundText.text = Round.ToString();

        nEnemies++;
        nEnemies = Mathf.Min(MaxEnemies, nEnemies);
        SpawnWave(nEnemies, team.Enemies);
    }

    public void SpawnWave(int nEnemies, team Team)
    {
        for (int i = 0; i < nEnemies; i++)
        {
            // Determine position.
            Vector3 Position = new Vector3(
                Random.Range(-SpawnRange, SpawnRange),
                0,
                Random.Range(-SpawnRange, SpawnRange));

            // Make clearing in the center.
            Position += Position.normalized * 30.0f;

            // Spawn the ship.
            var go = ShipSystem.Instance.AddShip(Position, team.Enemies);
        }
    }
}
