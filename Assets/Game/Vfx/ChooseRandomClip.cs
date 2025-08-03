using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChooseRandomClip : MonoBehaviour
{
    public List<AudioClip> Clips;

    // Start is called before the first frame update
    void Start()
    {
        var AudioSource = GetComponent<AudioSource>();
        int Index = Random.Range(0, Clips.Count);
        AudioSource.clip = Clips[Index];
        AudioSource.Play();
    }
}
