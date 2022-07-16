using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode, ImageEffectAllowedInSceneView]
public class Cloud : MonoBehaviour
{
    // Start is called before the first frame update

    // Transform boxTransform;

    [Header("Raymarch Settings")]
    public bool raymarchByStepCount;
    public int stepCount = 30;

    [Range(1, 10)]
    public float stepSize = 10;

    [Header("Cloud Settings")]
    public Texture2D blueNoise; // used to randomly off set the ray origin to reduce layered artifact

    [Header("Base Noise")]
    public Vector3 baseNoiseOffset;
    public float baseNoiseScale = 1;

    [Header("Detail Noise")]
    public Vector3 detailNoiseOffset;
    public float detailNoiseScale = 1;

    [Header("Density Modifiers")]
    // [Range(0, 1)]
    // public float densityThreshold = 1;
    public float densityMultiplier = 1;
    [Range(0, 5)]
    public float globalCoverageMultiplier;
    // public float anvilBias = 1;



    [Header("Lighting")]
    public Light sunLight;
    [Range(0, 1)]
    public float darknessThreshold = 0;
    public float lightAbsorptionThroughCloud = 1;
    public float lightAbsorptionTowardSun = 1;
    [Range(0, 1)]
    public float forwardScattering = .83f;
    [Range(0, 1)]
    public float backScattering = .3f;
    [Range(0, 1)]
    public float baseBrightness = 0.5f;
    [Range(0, 1)]
    public float phaseFactor = .15f;

    [Header("Other")]
    public Shader shader;
    public GameObject boundingBox;
    public Material material;

    void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (material == null)
            material = new Material(shader);
        material.SetTexture("_MainTex", source);

        Transform transform = boundingBox.transform;
        material.SetVector("boundsMin", transform.position - transform.localScale / 2);
        material.SetVector("boundsMax", transform.position + transform.localScale / 2);
        material.SetInt("raymarchByCount", raymarchByStepCount ? 1 : 0);
        material.SetInt("raymarchStepCount", stepCount);
        material.SetFloat("raymarchStepSize", stepSize);
        material.SetTexture("BlueNoise", blueNoise);

        NoiseGenerator noiseGenerator = FindObjectOfType<NoiseGenerator>();
        if (noiseGenerator.shouldUpdateNoise) noiseGenerator.updateNoise();

        WeatherMapGenerator WMGenerator = FindObjectOfType<WeatherMapGenerator>();
        if (WMGenerator.shouldUpdateNoise) WMGenerator.updateNoise();

        OceanGeometry oceanGeometry = FindObjectOfType<OceanGeometry>();



        // values related to shaping the cloud
        material.SetFloat("time", Time.time);

        material.SetTexture("BaseNoise", noiseGenerator.baseRenderTexture);
        material.SetVector("baseNoiseOffset", baseNoiseOffset);
        material.SetFloat("baseNoiseScale", baseNoiseScale);

        material.SetTexture("DetailNoise", noiseGenerator.detailRenderTexture);
        material.SetVector("detailNoiseOffset", detailNoiseOffset);
        material.SetFloat("detailNoiseScale", detailNoiseScale);
        material.SetFloat("densityMultiplier", densityMultiplier);
        material.SetFloat("globalCoverage", globalCoverageMultiplier);

        material.SetTexture("WeatherMap", WMGenerator.WMRenderTexture);

        // values related to lighting the cloud
        material.SetVector("lightColor", sunLight.color);
        material.SetFloat("darknessThreshold", darknessThreshold);
        material.SetFloat("lightAbsorptionThroughCloud", lightAbsorptionThroughCloud);
        material.SetFloat("lightAbsorptionTowardSun", lightAbsorptionTowardSun);
        material.SetVector("phaseParams", new Vector4(forwardScattering, backScattering, baseBrightness, phaseFactor));

        // wave
        material.SetTexture("HeightMap", oceanGeometry.heightField);



        Graphics.Blit(source, destination, material);


    }
    // Update is called once per frame
    void OnValidate()
    {
        if (stepCount < 1)
        {
            stepCount = 1;
        }
    }
}

