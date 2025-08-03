using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct TeamData
{
    public Dictionary<Vector3, SpottedEnemy> SpottedEnemies;
}

// Message created by spotters. Can be used by gunners to shoot enemies. (Maybe different gunners can actually shoot different enemies). 
public struct SpottedEnemy
{
    public float Time; // Time of sighting (so that we can extrapolate current position). 
    public Vector3 Position;
    public Vector3 Velocity;
}

/// <summary>
/// Data per team, eg. shared information over the radio (like spotted enemies, etc). Kind of unrealistic cuz what if radio blockers? For now dont worry. 
/// </summary>
public class Teams : MonoBehaviour
{
    public static Teams Instance;

    public TeamData[] DataPerTeam;
    public int nTeams;

    // Start is called before the first frame update
    void Awake()
    {
        Instance = this;

        // 1 data per team
        nTeams = typeof(team).GetEnumNames().Length;
        DataPerTeam = new TeamData[nTeams];
        for (int i = 0; i < nTeams; i++)
        {
            DataPerTeam[i] = new TeamData()
            {
                SpottedEnemies = new Dictionary<Vector3, SpottedEnemy>()
            };
        }
    }
}
