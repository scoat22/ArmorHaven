using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DestroyTimer : MonoBehaviour
{
    public float Seconds;

    // Start is called before the first frame update
    private void OnEnable()
    {
        StartCoroutine(DestroyIn(Seconds));
    }

    IEnumerator DestroyIn(float Seconds)
    {
        yield return new WaitForSeconds(Seconds);
        Destroy(this.gameObject);
    }
}
