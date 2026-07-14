using UnityEngine;

public class PlayerSpawnPoint : MonoBehaviour
{
    [SerializeField] private int index;

    public int Index => index;

    //private void OnDrawGizmos()
    //{
    //    Gizmos.color = Color.blue;
    //    Gizmos.DrawSphere(transform.position, 0.1f);
    //    Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 1f);
    //}
}