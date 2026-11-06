using UnityEngine;

namespace MMO.Prototype
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class TowerGate : MonoBehaviour
    {
        private BoxCollider barrier;
        private Vector3 closedPosition;
        public bool Open { get; private set; }

        private void Awake()
        {
            barrier = GetComponent<BoxCollider>();
            closedPosition = transform.localPosition;
        }

        public void Raise() { Open = true; }

        public void Close()
        {
            Open = false;
            transform.localPosition = closedPosition;
            barrier.enabled = true;
        }

        private void Update()
        {
            if (!Open) return;
            var goal = closedPosition + Vector3.up * 5.25f;
            transform.localPosition = Vector3.MoveTowards(transform.localPosition, goal, 6.2f * Time.deltaTime);
            barrier.enabled = transform.localPosition.y - closedPosition.y < 2.12f;
        }
    }
}
