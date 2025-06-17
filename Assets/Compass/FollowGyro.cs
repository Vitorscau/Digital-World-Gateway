using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FollowGyro : MonoBehaviour
{
    public enum RotationAxis
    {
        X,
        Y,
        Z
    }

    [Header("Tweaks")]
    [SerializeField] private float rotationSpeed = 1.0f; // Adjust the rotation speed as needed
    [SerializeField] private float axisOffset = 0.0f;    // Offset for the chosen axis
    [SerializeField] private RotationAxis rotationAxis = RotationAxis.Z; // Selected rotation axis
    [SerializeField] private Vector3 positionOffset;     // Offset for the position

    // Start is called before the first frame update
    void Start()
    {
        GyroManager.Instance.EnanbleGyro();
    }

    void Update()
    {
        Quaternion gyroRotation = GyroManager.Instance.GetGyroRotation();

        // Extract only the rotation around the specified axis
        Quaternion axisRotation = Quaternion.identity;

        switch (rotationAxis)
        {
            case RotationAxis.X:
                axisRotation = Quaternion.Euler(gyroRotation.eulerAngles.x + axisOffset, 0, 0);
                break;
            case RotationAxis.Y:
                axisRotation = Quaternion.Euler(0, -gyroRotation.eulerAngles.y - axisOffset, 0);
                break;
            case RotationAxis.Z:
                axisRotation = Quaternion.Euler(0, 0, gyroRotation.eulerAngles.z + axisOffset);
                break;
        }

        // Apply the rotation to the object
        transform.localRotation = Quaternion.Slerp(transform.localRotation, axisRotation, rotationSpeed * Time.deltaTime);

        // Apply position offset
        transform.position = transform.position + positionOffset;
    }
}
