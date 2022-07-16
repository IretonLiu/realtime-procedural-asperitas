using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class FourierGrid
{
    // Start is called before the first frame update

    bool isSquare;
    // number of points on each side
    public int N;
    public int M;
    // width and length of the mesh
    public float Lx;
    public float Lz;
    public FourierGrid(int N, int M, float Lx, float Lz)
    {
        this.N = N;
        this.M = M;
        this.Lx = Lx;
        this.Lz = Lz;
    }



}