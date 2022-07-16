using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class OceanGeometry : MonoBehaviour
{
    [Header("FourierGrid Settings")]
    public bool isSquare;
    // number of points on each side
    public int N;
    public int M;
    // width and length of the mesh
    public float Lx;
    public float Lz;
    // Start is called before the first frame update
    [Header("Phillips Spectrum")]
    public float windSpeed;
    public Vector2 windDirection;
    public float A;

    [Header("Compute Shaders")]
    public ComputeShader initialSpectrumCompute; // h0(k) h0(-k)
    public ComputeShader fourierAmplitudeCompute;
    public ComputeShader butterflyTextureCompute;
    public ComputeShader butterflyCompute;
    public ComputeShader inversePermutationCompute;

    [Header("Other")]
    // public Material matVis;
    public Material heightFieldMaterial;
    public Texture2D gaussianNoiseTexture1;
    public Texture2D gaussianNoiseTexture2;

    public RenderTexture heightField;

    bool shouldUpdateStatic = false;
    FourierGrid fourierGrid;
    WaveGenerator waveGenerator;

    void Start()
    {
        updateFourierGrid();

        updateWaveGenerator();


    }

    // Update is called once per frame
    void Update()
    {

        if (shouldUpdateStatic)
        {
            updateFourierGrid();
            updateWaveGenerator();

            shouldUpdateStatic = false;
        }
        waveGenerator.calcFourierAmplitude();
        waveGenerator.fft();
        // matVis.SetTexture("_MainTex", waveGenerator.displacement);
        heightField = waveGenerator.displacement;
        heightFieldMaterial.SetTexture("_HeightMap", waveGenerator.displacement);

        //     Texture2D tex2 = new Texture2D(h0minusk_RenderTexture.width, h0minusk_RenderTexture.height, TextureFormat.RGB24, false);
        //     RenderTexture.active = h0minusk_RenderTexture;
        //     tex2.ReadPixels(new Rect(0, 0, h0minusk_RenderTexture.width, h0minusk_RenderTexture.height), 0, 0);
        //     Color[] pixels2 = tex2.GetPixels();

    }

    void OnValidate()
    {
        // update mesh settings
        if (N < 256) N = 256;
        if (M < 256) M = 256;
        if (Lx < 1) Lx = 1;
        if (Lz < 1) Lz = 1;

        if (isSquare)
        {
            M = N;
            Lz = Lx;
        }
        shouldUpdateStatic = true;
    }

    void updateFourierGrid()
    {
        // Transform transform = GetComponent<Transform>();
        // transform.localScale = new Vector3(Lx, 1, Lz);

        // initialize mesh
        fourierGrid = new FourierGrid(N, M, Lx, Lz);

    }
    void updateWaveGenerator()
    {
        GaussianNoiseTexture gnt = new GaussianNoiseTexture();

        gaussianNoiseTexture1 = gnt.generateGaussianTexture(256, 256);
        gaussianNoiseTexture2 = gnt.generateGaussianTexture(256, 256);
        Color[] pixels1 = gaussianNoiseTexture1.GetPixels();
        Color[] pixels2 = gaussianNoiseTexture2.GetPixels();
        waveGenerator = new WaveGenerator(gaussianNoiseTexture1, gaussianNoiseTexture2, fourierGrid);
        waveGenerator.setComputeShader(initialSpectrumCompute, fourierAmplitudeCompute,
                                        butterflyTextureCompute, butterflyCompute, inversePermutationCompute);
        waveGenerator.setPhillipsParams(windSpeed, windDirection, A);
        waveGenerator.InitialSpectrum();
        waveGenerator.PrecomputeTwiddleFactorsAndInputIndices();

    }
}
