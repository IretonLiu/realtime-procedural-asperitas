using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// [ExecuteInEditMode, ImageEffectAllowedInSceneView]
public class WaveGenerator
{
    // Start is called before the first frame update
    static int LOCAL_WORK_GROUP_X = 16;
    static int LOCAL_WORK_GROUP_Y = 16;

    //phillips
    float windSpeed;
    Vector2 windDirection;
    float A;
    [Header("Other")]
    public Texture2D gaussianNoise1;
    public Texture2D gaussianNoise2;
    ComputeShader initialSpectrumCompute; // h0(k) h0(-k)
    ComputeShader fourierAmplitudeCompute;
    ComputeShader butterflyTextureCompute;
    ComputeShader butterflyCompute;
    ComputeShader inversePermutationCompute;


    public RenderTexture h0k_RenderTexture;
    RenderTexture h0minusk_RenderTexture;

    public RenderTexture butterflyTexture;

    RenderTexture hktDy; // height change, this is the h0 from the paper
    RenderTexture hktDx; // directional change
    RenderTexture hktDz; // directional change

    RenderTexture pingpong0;
    RenderTexture pingpong1;
    public RenderTexture displacement;


    bool shouldUpdate;

    FourierGrid fourierGrid;

    public WaveGenerator(Texture2D guassianTex1, Texture2D guassianTex2, FourierGrid fourierGrid)
    {
        gaussianNoise1 = guassianTex1;
        gaussianNoise2 = guassianTex2;
        this.fourierGrid = fourierGrid;
    }

    public void setComputeShader(ComputeShader ISCompute, ComputeShader FACompute,
                                     ComputeShader butterflyTextureCompute, ComputeShader butterflyCompute, ComputeShader IPCompute)
    {
        initialSpectrumCompute = ISCompute;
        fourierAmplitudeCompute = FACompute;
        this.butterflyTextureCompute = butterflyTextureCompute;
        this.butterflyCompute = butterflyCompute;
        inversePermutationCompute = IPCompute;
    }

    public void setPhillipsParams(float windSpeed, Vector2 windDirection, float A)
    {
        this.A = A;
        this.windDirection = windDirection;
        this.windSpeed = windSpeed;
    }
    public void InitialSpectrum()
    {


        // if (h0k_RenderTexture == null)
        createTexture(ref h0k_RenderTexture, fourierGrid.N, fourierGrid.M);
        createTexture(ref h0minusk_RenderTexture, fourierGrid.N, fourierGrid.M);


        int initialSpectrumKernel = initialSpectrumCompute.FindKernel("CSInitialSpectrum");
        // int conjugateSpectrumKernel = fourierAmplitudeCompute.FindKernel("CSConjugateSpectrum");

        initialSpectrumCompute.SetInt("N", fourierGrid.N);
        initialSpectrumCompute.SetFloat("Lx", fourierGrid.Lx);

        initialSpectrumCompute.SetFloat("windSpeed", windSpeed);

        initialSpectrumCompute.SetFloats("windDirection", new float[] { windDirection.normalized.x, windDirection.normalized.y });
        initialSpectrumCompute.SetFloat("A", A);

        initialSpectrumCompute.SetTexture(initialSpectrumKernel, "GaussianNoise", gaussianNoise1);
        initialSpectrumCompute.SetTexture(initialSpectrumKernel, "GaussianNoise2", gaussianNoise2);

        initialSpectrumCompute.SetTexture(initialSpectrumKernel, "H0k", h0k_RenderTexture);
        initialSpectrumCompute.SetTexture(initialSpectrumKernel, "H0minusk", h0minusk_RenderTexture);

        initialSpectrumCompute.Dispatch(initialSpectrumKernel, fourierGrid.N / LOCAL_WORK_GROUP_X, fourierGrid.M / LOCAL_WORK_GROUP_Y, 1);



    }

    public void calcFourierAmplitude()
    {

        createTexture(ref hktDy, fourierGrid.N, fourierGrid.M);
        createTexture(ref hktDx, fourierGrid.N, fourierGrid.M);
        createTexture(ref hktDz, fourierGrid.N, fourierGrid.M);

        int fourierAmplitudeKernel = fourierAmplitudeCompute.FindKernel("CSFourierAmplitude");

        fourierAmplitudeCompute.SetInt("N", fourierGrid.N);
        fourierAmplitudeCompute.SetFloat("Lx", fourierGrid.Lx);
        fourierAmplitudeCompute.SetFloat("t", Time.time);



        fourierAmplitudeCompute.SetTexture(fourierAmplitudeKernel, "H0k", h0k_RenderTexture);
        fourierAmplitudeCompute.SetTexture(fourierAmplitudeKernel, "H0minusk", h0minusk_RenderTexture);

        fourierAmplitudeCompute.SetTexture(fourierAmplitudeKernel, "Hkt_dy", hktDy);
        fourierAmplitudeCompute.SetTexture(fourierAmplitudeKernel, "Hkt_dx", hktDx);
        fourierAmplitudeCompute.SetTexture(fourierAmplitudeKernel, "Hkt_dz", hktDz);

        fourierAmplitudeCompute.Dispatch(fourierAmplitudeKernel, fourierGrid.N / LOCAL_WORK_GROUP_X, fourierGrid.M / LOCAL_WORK_GROUP_Y, 1);
    }

    public void computeButterflyTexture()
    {
        createTexture(ref butterflyTexture, (int)Mathf.Log(fourierGrid.N, 2), fourierGrid.M);
        int butterflyTextureKernel = butterflyTextureCompute.FindKernel("CSButterflyTexture");

        butterflyTextureCompute.SetInt("N", fourierGrid.N);
        butterflyTextureCompute.SetTexture(butterflyTextureKernel, "butterflyTexture", butterflyTexture);

        butterflyTextureCompute.Dispatch(butterflyTextureKernel, fourierGrid.N / LOCAL_WORK_GROUP_X, fourierGrid.M / LOCAL_WORK_GROUP_Y, 1);
        return;
    }

    public void PrecomputeTwiddleFactorsAndInputIndices()
    {
        int size = fourierGrid.N;
        int logSize = (int)Mathf.Log(size, 2);
        RenderTexture rt = new RenderTexture(logSize, size, 0,
            RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
        rt.filterMode = FilterMode.Point;
        rt.wrapMode = TextureWrapMode.Repeat;
        rt.enableRandomWrite = true;
        rt.Create();

        int precomputeKernel = butterflyCompute.FindKernel("PrecomputeTwiddleFactorsAndInputIndices");

        butterflyCompute.SetInt("Size", size);
        butterflyCompute.SetTexture(precomputeKernel, "ButterflyBuffer", rt);
        butterflyCompute.Dispatch(precomputeKernel, logSize, size / LOCAL_WORK_GROUP_Y, 1);
        butterflyTexture = rt;
    }

    public void fft()
    {

        pingpong0 = hktDy;
        createTexture(ref pingpong1, fourierGrid.N, fourierGrid.M);
        createTexture(ref displacement, fourierGrid.N, fourierGrid.M);

        int hButterflyKernel = butterflyCompute.FindKernel("CSHorizontalButterflies");
        int vButterflyKernel = butterflyCompute.FindKernel("CSVerticalButterflies");
        bool pingpong = false;

        butterflyCompute.SetTexture(hButterflyKernel, "pingpong0", pingpong0);
        butterflyCompute.SetTexture(hButterflyKernel, "pingpong1", pingpong1);
        butterflyCompute.SetTexture(hButterflyKernel, "ButterflyTexture", butterflyTexture);
        // horizontal fft
        for (int i = 0; i < (int)Mathf.Log(fourierGrid.N, 2); i++)
        {
            pingpong = !pingpong;
            butterflyCompute.SetInt("stage", i);
            butterflyCompute.SetInt("direction", 0);
            butterflyCompute.SetBool("pingpong", pingpong);
            butterflyCompute.Dispatch(hButterflyKernel, fourierGrid.N / LOCAL_WORK_GROUP_X, fourierGrid.M / LOCAL_WORK_GROUP_Y, 1);

        }

        pingpong0 = hktDy;
        butterflyCompute.SetTexture(vButterflyKernel, "pingpong0", pingpong0);
        butterflyCompute.SetTexture(vButterflyKernel, "pingpong1", pingpong1);
        butterflyCompute.SetTexture(vButterflyKernel, "ButterflyTexture", butterflyTexture);
        // vertical fft
        // pingpong = 0;

        for (int i = 0; i < (int)Mathf.Log(fourierGrid.M, 2); i++)
        {
            pingpong = !pingpong;
            butterflyCompute.SetInt("stage", i);
            butterflyCompute.SetInt("direction", 1);
            butterflyCompute.SetBool("pingpong", pingpong);

            butterflyCompute.Dispatch(vButterflyKernel, fourierGrid.N / LOCAL_WORK_GROUP_X, fourierGrid.M / LOCAL_WORK_GROUP_Y, 1);
        }


        int invPermKernel = inversePermutationCompute.FindKernel("CSMain");

        inversePermutationCompute.SetBool("pingpong", pingpong);
        inversePermutationCompute.SetInt("N", fourierGrid.N);
        inversePermutationCompute.SetTexture(invPermKernel, "displacement", displacement);
        inversePermutationCompute.SetTexture(invPermKernel, "pingpong0", pingpong0);
        inversePermutationCompute.SetTexture(invPermKernel, "pingpong1", pingpong1);
        inversePermutationCompute.Dispatch(invPermKernel, fourierGrid.N / LOCAL_WORK_GROUP_X, fourierGrid.M / LOCAL_WORK_GROUP_Y, 1);

    }
    void createTexture(ref RenderTexture renderTexture, int xResolution, int yResolution)
    {
        if (renderTexture == null || !renderTexture.IsCreated() || renderTexture.width != xResolution || renderTexture.height != yResolution)
        {
            if (renderTexture != null)
            {
                renderTexture.Release();
            }
            renderTexture = new RenderTexture(xResolution, yResolution, 0, RenderTextureFormat.ARGBFloat);
            renderTexture.enableRandomWrite = true;
            renderTexture.wrapMode = TextureWrapMode.Repeat;
            renderTexture.filterMode = FilterMode.Trilinear;
            renderTexture.useMipMap = false;
            renderTexture.autoGenerateMips = false;
            renderTexture.anisoLevel = 6;

            renderTexture.Create();
        }

    }

}
