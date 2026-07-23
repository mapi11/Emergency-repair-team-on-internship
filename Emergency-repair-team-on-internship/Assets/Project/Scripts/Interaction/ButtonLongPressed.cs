using System.Collections;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class ButtonLongPressed : ButtonPress
{
    protected override IEnumerator AutoRelease(PlayerController player)
    {
        yield break;
    }
}
