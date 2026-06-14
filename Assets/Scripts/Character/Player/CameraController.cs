using System;
using UnityEngine;
using UnityEngine.UIElements;

public class CameraController : MonoBehaviour
{
    [SerializeField] private Transform camera;
    [SerializeField] private Vector3 offset;
    
    public void SetCameraPosition(Vector2 position)
    {
        camera.position = new Vector3(position.x, position.y, camera.position.z) + offset;
    }


}
