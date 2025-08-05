using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public enum Sound
{
    BulletFlying
}

public class SoundSystem : MonoBehaviour
{
    public static SoundSystem Instance;

    public int MaxVoices = 32;
    AudioSource[] Voices;
    public AudioListener Listener;

    // Start is called before the first frame update
    void Start()
    {
        Instance = this;

        Voices = new AudioSource[MaxVoices];
        for (int i = 0; i < MaxVoices; i++)
        {
            Voices[i] = new GameObject("Voice").AddComponent<AudioSource>();
            Voices[i].transform.SetParent(transform, true);
        }
        Listener = new GameObject("Listener").AddComponent<AudioListener>();
    }

    public void PlaySound(Sound Sound, Vector3 Position)
    {

    }

    // Update is called once per frame
    void Update()
    {
        // Set listener position.
        var Ship = ShipSystem.Instance.Ships[0];
        if (Ship)
        {
            var ShipPos = Ship.transform.position;
            Listener.transform.position = (ShipPos + Camera.main.transform.position) * 0.5f;
        }
    }
}
