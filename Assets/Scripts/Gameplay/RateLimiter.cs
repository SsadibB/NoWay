using System.Collections.Generic;
using UnityEngine;

namespace TrickyPictures
{
    public class RateLimiter
    {
        readonly int maxEvents;
        readonly float windowSeconds;
        readonly Queue<float> timestamps = new Queue<float>();

        public RateLimiter(int maxEvents, float windowSeconds)
        {
            this.maxEvents = maxEvents;
            this.windowSeconds = windowSeconds;
        }

        public bool TryConsume()
        {
            float now = Time.unscaledTime;
            while (timestamps.Count > 0 && now - timestamps.Peek() > windowSeconds) timestamps.Dequeue();
            if (timestamps.Count >= maxEvents) return false;
            timestamps.Enqueue(now);
            return true;
        }
    }
}
