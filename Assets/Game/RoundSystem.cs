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
    public RectTransform IncomingWaveIndicator;
    [Header("Sfx")]
    public AudioClip RoundOverClip;
    RoundState State;
    enum RoundState { Playing, NewRound, SpawningWave, Restarting, }

    Vector3 WavePosition;

    // Start is called before the first frame update
    void Start()
    {
        Instance = this;
        State = RoundState.Playing;
        nEnemies = nEnemiesFirstRound;
    }

    private void FixedUpdate()
    {
        var Ships = ShipSystem.Instance.Ships;
        var nShips = ShipSystem.Instance.nShips;

        // If there's no ships, create player ship
        if (nShips == 0)
        {
            //Debug.Log("Creating player ship");
            var PlayerShip = ShipSystem.Instance.AddShip(Vector3.right, team.Allies); // Add player ship.
            PlayerShip.GetComponent<Ship>().RocketPower *= 4; // Give player a speed advantage.
            PlayerShip.GetComponent<Ship>().IsPlayer = true;
        }
        
        if (State == RoundState.Playing)
            if (ShipUtility.EnemyCount() == 0)
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
            RoundText.transform.localScale = Vector3.one * 3.0f;
        }
        else
        {
            RoundText.color = Color.red;
            RoundText.transform.localScale = Vector3.one;
        }

        IncomingWaveIndicator.gameObject.SetActive(false);
        if (State == RoundState.SpawningWave)
        {
            var Position = Camera.main.WorldToScreenPoint(WavePosition);
            if (Position.z > 0)
            {
                IncomingWaveIndicator.gameObject.SetActive(true);
                IncomingWaveIndicator.transform.position = Position;
                IncomingWaveIndicator.GetChild(0).GetComponent<Image>().color = new Color(1, 0, 0, Mathf.Sin(Time.time * Mathf.PI * 2.0f));
            }
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

        BulletSystem.Instance.Clear();
        PlayerController.Instance._Points = 0;
        Round = 0;                  // Reset round.
        State = RoundState.Playing; // Reset state.
    }

    IEnumerator StartNextRound()
    {
        State = RoundState.NewRound;
        Round++;

        yield return new WaitForSeconds(2.0f);

       
        if (MaxEnemies > 0)
        {
            RoundText.text = Round.ToString(); // Update UI.
            //nEnemies++;
            nEnemies = Mathf.Min(MaxEnemies, nEnemies);
            WavePosition = GetRandPosOnCircle(SpawnDistance);

            State = RoundState.SpawningWave;
            yield return new WaitForSeconds(3.0f);

            GetComponent<AudioSource>().PlayOneShot(RoundOverClip);
            SpawnWave(WavePosition, nEnemies, team.Enemies);

            State = RoundState.Playing;
        }
    }

    public void SpawnWave(Vector3 WavePosition, int nEnemies, team Team)
    {
        //Debug.LogFormat("Spawning {0} enemies.", nEnemies);

        if (nEnemies <= 0)
        {
            //Debug.LogErrorFormat("Warning: spawning {0} enemeies", nEnemies);
        }

        // Todo: put a blinking "incoming wave" sign, then 3 seconds later, spawn the wave.
        Vector3 right = Vector3.Cross(WavePosition, Vector3.up).normalized;

        for (int i = 0; i < nEnemies; i++)
        {
            //var Position = GetRandPosOnCircle(SpawnDistance);
            //var Offset = GetRandPosOnCircle(15.0f);
            var Offset = right * i * 30.0f;
            var Position = WavePosition + Offset;

            // Spawn the ship.
            var go = ShipSystem.Instance.AddShip(Position, team.Enemies);
        }
    }

    static Vector3 GetRandPosOnCircle(float Radius)
    {
        Vector2 RandPosInCircle = Random.insideUnitCircle;
        Vector3 Position = new Vector3(RandPosInCircle.x, 0, RandPosInCircle.y);
        Position += Position.normalized * Radius;
        return Position;
    }
}
