using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[ImageEffectAllowedInSceneView]
public class RayMarch : MonoBehaviour
{

    [Header("Height Multiplier")]
    public float heightMultiplier = 10;
    [Header("Height Field Scale")]
    public float heightFieldScale = 0.01f;

    [Header("Raymarch Settings")]
    public int stepCount = 30;
    [Range(1, 100)]
    public float rayMarchStepSize = 10;
    public float lightMarchStepSize = 10;
    public float cloudThickness = 1000;
    [Range(0, 1)]
    public float distanceDamping = 0.8f;

    [Header("Cloud Settings")]
    public Texture2D blueNoise; // used to randomly off set the ray origin to reduce layered artifact

    [Header("Base Noise")]
    public Vector3 baseNoiseOffset;
    public float baseNoiseScale = 1;

    [Header("Detail Noise")]
    public Vector3 detailNoiseOffset;
    public float detailNoiseScale = 1;

    [Header("Density Modifiers")]
    public float densityMultiplier = 1;
    [Range(0, 5)]
    public float globalCoverageMultiplier;
    

    [Header("Lighting")]
    public Light sunLight;
    [Range(0, 1)]
    public float darknessThreshold = 0;
    public float extinctionFactor = 1;
    public float scatteringFactor = 1;
    [Range(0, 1)]
    public float forwardScattering = .83f;
    [Range(0, 1)]
    public float backScattering = .3f;
    [Range(0, 1)]
    public float baseBrightness = 0.5f;
    [Range(0, 1)]
    public float phaseFactor = .15f;

    public Material material;

    void OnRenderImage(RenderTexture src, RenderTexture dest)
    {
        Graphics.Blit(src, dest);

        material.SetTexture("_MainTex", src);

        material.SetFloat("heightMultiplier", heightMultiplier);
        material.SetFloat("heightFieldScale", heightFieldScale);
        // raymarch settings
        material.SetInt("raymarchStepCount", stepCount);
        material.SetFloat("raymarchStepSize", rayMarchStepSize);
        material.SetFloat("lightmarchStepSize", lightMarchStepSize);
        material.SetFloat("cloudThickness", cloudThickness);
        material.SetFloat("distanceDampingFactor", distanceDamping); // reducing the effect of height field further away
        material.SetTexture("BlueNoise", blueNoise);

        NoiseGenerator noiseGenerator = FindObjectOfType<NoiseGenerator>();
        if (noiseGenerator.shouldUpdateNoise) noiseGenerator.updateNoise();

        WeatherMapGenerator WMGenerator = FindObjectOfType<WeatherMapGenerator>();
        if (WMGenerator.shouldUpdateNoise) WMGenerator.updateNoise();

        // values related to shaping the cloud
        material.SetFloat("time", Time.time);

        material.SetTexture("BaseNoise", noiseGenerator.baseRenderTexture);
        material.SetVector("baseNoiseOffset", baseNoiseOffset);
        material.SetFloat("baseNoiseScale", baseNoiseScale);

        material.SetTexture("DetailNoise", noiseGenerator.detailRenderTexture);
        material.SetVector("detailNoiseOffset", detailNoiseOffset);
        material.SetFloat("detailNoiseScale", detailNoiseScale);
        material.SetFloat("densityMultiplier", densityMultiplier);

        material.SetTexture("WeatherMap", WMGenerator.WMRenderTexture);

        // values related to lighting the cloud
        material.SetVector("lightColor", sunLight.color);
        material.SetFloat("darknessThreshold", darknessThreshold);
        material.SetFloat("extinctionFactor", extinctionFactor);
        material.SetFloat("scatteringFactor", scatteringFactor);
        material.SetVector("phaseParams", new Vector4(forwardScattering, backScattering, baseBrightness, phaseFactor));

        if(material)
            Graphics.Blit(src, dest, material);
        else Graphics.Blit(src, dest);

    }
    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }
    void OnValidate()
    {
        if (stepCount < 10)
        {
            stepCount = 10;
        }
    }
}
