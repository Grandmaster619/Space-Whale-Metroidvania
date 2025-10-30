using System;
using System.Threading;
using Unity.Cinemachine;
using UnityEngine;

public abstract class Sensor : MonoBehaviour {
    public CreatureMemory Memory { get; set; }

    public LayerMask targetMask;                // which layers are targets
    public LayerMask occluderMask;              // optional layers that block vision

}