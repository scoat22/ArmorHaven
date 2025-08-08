using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PointsUI : MonoBehaviour
{
    public Text PointsText;
    Text[] PointTexts = new Text[10];
    Vector3[] Velocities = new Vector3[10];
    int _idx; //When you add points, choose this one (wrap around PointTexts.length)

    private void Start()
    {
        for (int i = 0; i < PointTexts.Length; i++)
        {
            PointTexts[i] = transform.GetChild(i).GetComponent<Text>();
        }

        // Test
        //for (int i = 0; i < 10; i++) AddPoints(10);
    }
    
    // Call this once per frame to make sure its always updated. (completely syncing state will reduce bugs, vs. summing errors over time).
    public void SetPoints(int points)
    {
        PointsText.text = points.ToString();
    }

    public void AddPoints(int points)
    {
        PointTexts[_idx].text = "+" + points.ToString();
        PointTexts[_idx].transform.localPosition = -Vector3.right * 70.0f;
        //Velocities[_idx] = -Vector3.right * 200f;// Random.rotation * Vector3.right;
        Velocities[_idx] = Random.rotation * Vector3.right * 200.0f;
        // Clamp to left side
        Velocities[_idx].x = -Mathf.Abs(Velocities[_idx].x);

        // Reset alpha
        PointTexts[_idx].color = new Color(PointTexts[_idx].color.r, PointTexts[_idx].color.g, PointTexts[_idx].color.b, 1.0f);

        _idx = (_idx + 1) % PointTexts.Length;
    }

    public void RemovePoints(int points)
    {
       
    }

    // Update is called once per frame
    void Update()
    {
        float dt = Time.deltaTime;

        if (Input.GetKeyDown(KeyCode.Space))
            AddPoints(10);

        for (int i = 0; i < Velocities.Length; i++)
        {
            PointTexts[i].transform.localPosition += Velocities[i] * dt;
            Velocities[i] -= Velocities[i] * dt;

            PointTexts[i].color = new Color(PointTexts[i].color.r, PointTexts[i].color.g, PointTexts[i].color.b, PointTexts[i].color.a - dt);
        }
    }
}
