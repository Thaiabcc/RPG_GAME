using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Target")]
    public Transform target;                    

    [Header("Follow Settings")]
    public float smoothTime = 0.2f;             
    public Vector2 offset = new Vector2(0f, 1f); 

    [Header("Look Ahead")]
    public float lookAheadDistance = 3f;       
    public float lookAheadSmooth = 0.15f;     

    [Header("Vertical Bias")]
    public float verticalLookUp = 2.5f;        
    public float verticalLookDown = -1.5f;     
    public float verticalSmooth = 0.2f;

    [Header("Dead Zone")]
    public bool useDeadZone = true;
    public Vector2 deadZone = new Vector2(1.5f, 1f); 

    private Vector3 currentVelocity;
    private float currentLookAhead;
    private float lookAheadVelocity;
    private float currentVerticalBias;
    private float verticalVelocity;
    private Camera cam;

    void Start()
    {
        cam = GetComponent<Camera>();
        if (target == null)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) target = player.transform;
        }
    }

    void LateUpdate()
    {
        if (target == null) return;

        float targetLookAhead = 0f;
        float facing = Mathf.Sign(target.localScale.x); 
        targetLookAhead = facing * lookAheadDistance;

        currentLookAhead = Mathf.SmoothDamp(currentLookAhead, targetLookAhead, ref lookAheadVelocity, lookAheadSmooth);

        float targetVertical = 0f;
        Rigidbody2D rb = target.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            if (rb.linearVelocity.y > 1f)     
                targetVertical = verticalLookUp;
            else if (rb.linearVelocity.y < -1f)  
                targetVertical = verticalLookDown;
        }

        currentVerticalBias = Mathf.SmoothDamp(currentVerticalBias, targetVertical, ref verticalVelocity, verticalSmooth);

        Vector3 targetPos = target.position + new Vector3(currentLookAhead, offset.y + currentVerticalBias, -10f);

        if (useDeadZone)
        {
            Vector3 camPos = transform.position;

            float dx = targetPos.x - camPos.x;
            float dy = targetPos.y - camPos.y;

            if (Mathf.Abs(dx) < deadZone.x) targetPos.x = camPos.x;
            if (Mathf.Abs(dy) < deadZone.y) targetPos.y = camPos.y;
        }

        transform.position = Vector3.SmoothDamp(transform.position, targetPos, ref currentVelocity, smoothTime);
    }
}