using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AudioListenerPositioner : MonoBehaviour
{
    public GameObject Target;
    private GameObject AudioListenerObject;

    // Start is called before the first frame update
    void Start()
    {
        AudioListenerObject = new GameObject("Audio Listener");
        AudioListenerObject.AddComponent<AudioListener>();
    }

    // Update is called once per frame
    void Update()
    {
        // Position directly halfway between camera and target (I read somewhere that its the best placement for an audio listener).
        AudioListenerObject.transform.position = Target.transform.position + (transform.position - Target.transform.position) / 2;
    }
}
