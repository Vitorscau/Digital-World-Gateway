using UnityEngine;

namespace Niantic.Lightship.Maps.SampleAssets.Player
{
    /// <summary>Liga o estado do jogador (andando/parado) ao Animator do Digimon.</summary>
    public class PlayerModel : MonoBehaviour
    {
        [SerializeField] private Animator _animator;

        private static readonly int IsMovingHash = Animator.StringToHash("isMoving");
        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private bool _hasIsMoving, _hasSpeed;

        private void Awake() => CacheParameters();

        public void SetAnimator(Animator animator)
        {
            _animator = animator;
            CacheParameters();
        }

        private void CacheParameters()
        {
            _hasIsMoving = _hasSpeed = false;
            if (_animator == null || _animator.runtimeAnimatorController == null) return;
            foreach (var p in _animator.parameters)
            {
                if (p.nameHash == IsMovingHash && p.type == AnimatorControllerParameterType.Bool) _hasIsMoving = true;
                if (p.nameHash == SpeedHash && p.type == AnimatorControllerParameterType.Float) _hasSpeed = true;
            }
        }

        public void UpdatePlayerState(bool isMoving, float speed = 0f)
        {
            if (_animator == null) return;
            if (_hasIsMoving) _animator.SetBool(IsMovingHash, isMoving);
            if (_hasSpeed) _animator.SetFloat(SpeedHash, speed);
        }
    }
}
