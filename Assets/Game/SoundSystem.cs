using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using Unity.Mathematics;

public enum Sound
{
    HeavyTurret,
    MachineGun,
    Ricochet,
    Explosion
}

public class SoundSystem : MonoBehaviour
{
    public static SoundSystem Instance;

    public int MaxVoices = 31;
    public AudioSource[] Sources;
    public NativeArray<AudioData> Voices; // When will the source end? (if it ended, we can reuse it).
    public AudioData[] VisibleVoices; // Show in inspector
    int _idx; // The current voice index to play.
    public int nActiveSounds;
    public AudioListener Listener;
    Vector3 ListenerPosition;
    public AudioClip[] Clips;
    // Determine how many of each sounds are being played (each integer is an index into Voices[])
    //Dictionary<Sound, HashSet<int>> SoundIndex = new Dictionary<Sound, HashSet<int>>();
    public bool UseTestHotkeys = false;

    public AudioClip[] MachineGunClips;
    public AudioSource[] SustainSources;

    // Sustain
    float LastBulletFiredTime = -100;
    float SustainMaxVolume;
    float SustainChannelVolume; // A master volume on
    float LastLoudBulletTime = -100; // The time since a bullet was fired which had a volume over MinChannelVolume.
    public float SustainChannelRestPeriod = 1.5f; // (Seconds) 5 Seconds until we can reach max volume again.
    public float SustainChannelDecaySpeed = 1.0f;
    public float SustainMinChannelVolume = 0.1f;
    public float nNoises = 0.0f;

    [System.Serializable]
    public struct AudioData
    {
        public Sound Sound;
        public float EndTime;
        public float Weight;
        public float FalloffSpeed;
    }

    public struct SoundComparer : IComparer<AudioData>
    {
        public int Compare(AudioData x, AudioData y)
        {
            //return x.Weight.CompareTo(y.Weight);
            return y.Weight.CompareTo(x.Weight);
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        Instance = this;
        
        Sources = new AudioSource[MaxVoices];
        Voices = new NativeArray<AudioData>(MaxVoices, Allocator.Persistent);
        VisibleVoices = new AudioData[MaxVoices];
        for (int i = 0; i < MaxVoices; i++)
        {
            Sources[i] = new GameObject("Voice").AddComponent<AudioSource>();
            Sources[i].transform.SetParent(transform, true);
            Sources[i].dopplerLevel = 0.0f;
            Sources[i].priority = 0;
        }
        Listener = new GameObject("Listener").AddComponent<AudioListener>();
        SustainSources = new AudioSource[MachineGunClips.Length];
        for (int i = 0; i < MachineGunClips.Length; i++)
        {
            SustainSources[i] = new GameObject("Machinegun Clip " + i).AddComponent<AudioSource>();
            SustainSources[i].transform.SetParent(transform);
            SustainSources[i].priority = 0; // Test
            SustainSources[i].loop = true;
            SustainSources[i].volume = 0.0f;
            SustainSources[i].spatialBlend = 0.0f;
            SustainSources[i].clip = MachineGunClips[i];
            SustainSources[i].Play();
        }
    }

    private void OnDestroy()
    {
        Voices.Dispose();
    }

    public void PlaySound(Sound Sound, Vector3 Position, float Weight = 1.0f)
    {
        // Is there a source available? (just grab the current one)
        if (Weight > Voices[_idx].Weight)
        {
            // Todo: let's modify the weight by distance. (an explosion far away doesn't matter as much.
            float ClipLength = Clips[(int)Sound].length;
            // Modulate weight by distance
            Weight *= 1.0f / Vector3.Distance(ListenerPosition, Position);
            Voices[_idx] = new AudioData()
            {
                Sound = Sound,
                EndTime = Time.time + ClipLength,
                Weight = Weight,
                FalloffSpeed = 1.0f / ClipLength * Weight
            };
            Sources[_idx].Stop();
            Sources[_idx].PlayOneShot(Clips[(int)Sound]);
            _idx = (_idx + 1) % MaxVoices;
        }
        else if (Sound == Sound.Explosion) Debug.LogError("Explosion didn't make the cut.");
        //Debug.LogErrorFormat("No audio sources were available");

        // Model speed of sound (delay sound if further away)
        // Decrease pitch if the sound is further away. (do low pass, is that possible?)
    }

    // PlaySoundDelayed() -> it'll just add to a List of timers, and each one will just call PlaySound() when the time comes. 

    public void PlaySustained(float ClipTime, Vector3 Position)
    {
        // Todo: however many requests were made in the last 0.133 seconds, is the number of guns we want to play (choose the correct clip)
        const float MinDistance = 10.0f;
        //SustainMaxVolume = Mathf.Max(SustainMaxVolume, MinDistance / Vector3.Distance(ListenerPosition, Position));
        SustainMaxVolume = 1.0f; // Test.

        // Reset channel volume back to default if it hasn't been played in a while.

        //else Debug.LogFormat("Time since last loud bullet {0} wasn't greater than SustainChannelRestPeriod {1}", Time.time - LastLoudBulletTime, SustainChannelRestPeriod);
        if (SustainMaxVolume > SustainMinChannelVolume) LastLoudBulletTime = Time.time;

        LastBulletFiredTime = Time.time;
        nNoises++;
    }

    /*float Volume;
    private void OnGUI()
    {
        var ScreenPos = new Vector3(Screen.width / 2, Screen.height / 2, 0);
        var w = 300;
        var h = 20;
        GUI.Label(new Rect(ScreenPos.x, ScreenPos.y, w, h), "Volume: " + Volume); ScreenPos.y += h;
        GUI.Label(new Rect(ScreenPos.x, ScreenPos.y, w, h), "SustainChannelVolume: " + SustainChannelVolume); ScreenPos.y += h;
        GUI.Label(new Rect(ScreenPos.x, ScreenPos.y, w, h), "Time since last loud bullet: " + (Time.time - LastLoudBulletTime)); ScreenPos.y += h;
    }*/

    // Update is called once per frame
    void Update()
    {
        float dt = Time.deltaTime;

        // Sustain code (Todo: Put all the sustain data in a struct to make it modular, so we can use it for ricochet/hit sound effects.
        {
            int SustainIdx = Mathf.Clamp(Mathf.RoundToInt(nNoises), 0, SustainSources.Length - 1);
            //if (Input.GetKey(KeyCode.Space)) SustainIdx = 0; // Compare with old version that only uses one machine gun.
            float TimeSinceLastBullet = Time.time - LastBulletFiredTime;
            float FadeTime = 0.266f; // 0.133f;
            SustainSources[SustainIdx].volume = math.remap(0, FadeTime, 1.0f, 0.0f, TimeSinceLastBullet) * SustainChannelVolume;

            // Decrease volume of all clips except the current one/
            for (int i = 0; i < SustainSources.Length; i++)
            {
                if (i != SustainIdx) SustainSources[i].volume = 0.0f;
            }
            // Decay nNoises
            nNoises = Mathf.Max(0, nNoises - nNoises * dt / 0.133f);

            // Decay the channel if its currently being played, otherwise slowly raise it back up.
            float velocity = (Time.time - LastLoudBulletTime > SustainChannelRestPeriod) ? -SustainChannelDecaySpeed : SustainChannelDecaySpeed;
            SustainChannelVolume = Mathf.Clamp(SustainChannelVolume - velocity * dt, SustainMinChannelVolume, 1.0f);
        }

        ListenerPosition = Listener.transform.position; // Cache.

        if (ShipSystem.Instance.nShips > 0)
        {
            // Set listened position (midpoint between ship and camera).
            ListenerPosition = (ShipSystem.Instance.Ships[0].transform.position + Camera.main.transform.position) * 0.5f;
        }

        if (MaxVoices > 0)
        {
            // Set listener position.
            /*var Ship = ShipSystem.Instance.Ships[0];
            if (Ship)
            {
                var ShipPos = Ship.transform.position;
                Listener.transform.position = (ShipPos + Camera.main.transform.position) * 0.5f;
            }*/

            // Rebuild the array so that free ones appear at the top.
            AudioSource[] NewSources = new AudioSource[MaxVoices];
            NativeArray<AudioData> NewVoices = new NativeArray<AudioData>(MaxVoices, Allocator.Persistent);
            int TopIdx = MaxVoices - 1; // Backfill free sources from the top.
            _idx = 0;
            nActiveSounds = 0;
            float TotalWeight = 0;
            for (int i = 0; i < MaxVoices; i++)
            {
                // If the sound isn't done yet, add it to the new array
                if (Voices[i].EndTime > Time.time)
                {
                    NewSources[_idx] = Sources[i];
                    NewVoices[_idx] = Voices[i];
                    TotalWeight += Voices[i].Weight;
                    _idx++;
                    nActiveSounds++;
                }
                else
                {
                    NewSources[TopIdx] = Sources[i];
                    Voices[TopIdx] = new AudioData() // Reset the slot.
                    {
                        Weight = 0.0f,
                    };
                    TopIdx--;
                }
            }
            _idx = Mathf.Min(_idx, MaxVoices - 1);
            Sources = NewSources;
            var temp = Voices;
            Voices = NewVoices;
            temp.Dispose();

            // Now that we know how many active sounds there are, we can adjust volume
            for (int i = 0; i < nActiveSounds; i++)
            {
                // Fade the clip out
                var voice = Voices[i];

                voice.Weight -= dt * voice.FalloffSpeed;
                Sources[i].volume = voice.Weight / TotalWeight;

                Voices[i] = voice;
            }

            // Sort by weight
            Voices.Sort(new SoundComparer());

            // Debug show
            for (int i = 0; i < MaxVoices; i++)
            {
                VisibleVoices[i] = Voices[i];
            }

            if (UseTestHotkeys)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1)) PlaySound(Sound.HeavyTurret, Vector3.zero, 1);
                if (Input.GetKeyDown(KeyCode.Alpha2)) PlaySound(Sound.HeavyTurret, Vector3.zero, 5);
                if (Input.GetKeyDown(KeyCode.Alpha3)) PlaySound(Sound.Explosion, Vector3.zero, 10);
            }
            // Test
            /*for (int i = 0; i < 9; i++)
            {
                if(Input.GetKeyDown(i.ToString()))
                {
                    // Play i sounds at once
                    for (int j = 0; j < i; j++)
                    {
                        PlaySound(Sound.HeavyTurret, Vector3.zero);
                        //Sources[j].PlayOneShot(Clips[0]);
                    }
                }
            }*/
        }
    }
}
