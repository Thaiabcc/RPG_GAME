using System.Collections;
using UnityEngine;

namespace CameraSystem
{
    public class CameraShake : MonoBehaviour
    {
        public static CameraShake Instance { get; private set; }

        void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        public void Shake(float duration, float magnitude)
        {
            StartCoroutine(ShakeRoutine(duration, magnitude));
        }

        private IEnumerator ShakeRoutine(float duration, float magnitude)
        {
            Vector3 originalPosition = transform.localPosition;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                float x = Random.Range(-1f, 1f) * magnitude;
                float y = Random.Range(-1f, 1f) * magnitude;
                transform.localPosition = new Vector3(originalPosition.x + x, originalPosition.y + y, originalPosition.z);

                elapsed += Time.deltaTime;
                yield return null;
            }

            transform.localPosition = originalPosition;
        }
    }
}