using UnityEngine;

namespace Combat
{
    public static class Knockback
    {
        public static void Apply(Rigidbody2D rb, Vector2 damageSourcePosition, float force, float upwardBoost = 1f)
        {
            if (rb == null) return;
            float direction = Mathf.Sign(rb.transform.position.x - damageSourcePosition.x);
            rb.linearVelocity = new Vector2(direction * force, upwardBoost);
        }
    }
}