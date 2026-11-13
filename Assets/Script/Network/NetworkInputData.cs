using Fusion;
using UnityEngine;

public struct NetworkInputData : INetworkInput
{
    public Vector3 direction;
    public NetworkBool jump;
    public float yaw;   // ¶‰E‚Ì‰ñ“]Šp“x
    public float pitch; // ã‰º‚Ì‰ñ“]Šp“x
}