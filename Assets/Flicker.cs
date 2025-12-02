using UnityEngine;

[RequireComponent(typeof(Light))]
public class Flicker : MonoBehaviour
{
    public float minIntensity = 0.5f;
    public float maxIntensity = 1.5f;
    public float flickerSpeed = 2f;

    Light targetLight;
    float baseIntensity;
    float noiseOffset;

    void Awake()
    {
        targetLight = GetComponent<Light>();
        baseIntensity = targetLight.intensity;
        noiseOffset = Random.Range(0f, 100f);
    }

    void Update()
    {
        float noise = Mathf.PerlinNoise(Time.time * flickerSpeed, noiseOffset);
        float flicker = Mathf.Lerp(minIntensity, maxIntensity, noise);
        targetLight.intensity = baseIntensity * flicker;
    }
}
