using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("追踪目标")]
    public Transform target; 
    
    [Header("平滑参数")]
    public float smoothing = 5f;
    public Vector3 offset = new Vector3(0, 2, -5); // 初始偏移坐标

    void LateUpdate()
    {
        // 关键逻辑：如果目标被 Destroy 了，立刻停止追踪，防止红字报错
        if (target == null) return; 

        // 经典的平滑插值算法，跨越空间维度的跟随
        Vector3 targetPosition = target.position + offset;
        transform.position = Vector3.Lerp(transform.position, targetPosition, smoothing * Time.deltaTime);
        
        // 始终注视着实验体
        transform.LookAt(target);
    }
}