using UnityEngine;

namespace RosSharp.RosBridgeClient
{
    public class LinkCollisionSensor : MonoBehaviour
    {
        public bool IsColliding { get; private set; }
        
        // 어떤 물체와 부딪혔는지 확인하기 위한 용도 (디버그용)
        public string CollidedWith = "";

        private void OnCollisionEnter(Collision collision)
        {
            // 자기 자신(로봇의 다른 부품)과의 충돌은 레이어 설정으로 막는 것이 좋지만,
            // 여기서도 필터링할 수 있습니다.
            if (collision.gameObject.CompareTag("RobotLink")) return;

            IsColliding = true;
            CollidedWith = collision.gameObject.name;
        }

        private void OnCollisionStay(Collision collision)
        {
            if (collision.gameObject.CompareTag("RobotLink")) return;
            IsColliding = true;
        }

        private void OnCollisionExit(Collision collision)
        {
            IsColliding = false;
            CollidedWith = "";
        }
        
        // 트리거(IsTrigger=true)를 사용할 경우를 대비
        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("RobotLink")) return;
            IsColliding = true;
        }

        private void OnTriggerExit(Collider other)
        {
            IsColliding = false;
        }
    }
}
