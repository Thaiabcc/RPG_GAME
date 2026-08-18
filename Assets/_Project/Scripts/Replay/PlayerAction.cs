using UnityEngine;

namespace PlayerReplay
{
    [System.Serializable]
    public struct PlayerAction
    {
        public float time;
        public Vector2 position;
        public float moveX;
        public float moveY;
        public bool isRunning;
        public bool jumpPressed;
        public bool jumpHeld;
        public bool dashPressed;
        public bool attack1Pressed;
        public bool attack2Pressed;
        public bool attack3Pressed;
        public bool isFacingRight;
    }
}