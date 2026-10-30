using System;
using UnityEngine;

public enum MeleeHitMode
{
    Collider,
    Fan
}

public class PlayerMeleeAttack : MonoBehaviour
{
    private Player _player;
    private Animator _animator;
    private int _meleeLayerIndex;
    [SerializeField] private Collider _katanaCollider;
    [SerializeField] private MeleeHitMode _meleeHitMode = MeleeHitMode.Collider;

    [Header("부채꼴")] 
    [SerializeField] private float _range = 2f;
    [SerializeField] private float _angle = 90f;
    [SerializeField] private LayerMask _targetMask;
    

    private void Awake()
    {
        _player = GetComponentInParent<Player>();
        _animator = GetComponentInChildren<Animator>();
        _meleeLayerIndex = _animator.GetLayerIndex("Melee Layer");

        _katanaCollider.enabled = false;
    }

    public void FanAttack()
    {
        if (_meleeHitMode != MeleeHitMode.Fan) return;
        
        Vector3 origin = _player.transform.position;

        // Physics.OverlapSpere(원점, 반지름, 찾을 레이어들)
        Collider[] colliders = Physics.OverlapSphere(origin, _range, _targetMask);
        foreach (Collider col in colliders)
        {
            
            // 적의 타격점을 찾는다.
            Vector3 hitPoint = col.ClosestPoint(origin);
            
            // 방향을 판단한다.
            Vector3 direction = (hitPoint - origin);
            direction.y = 0f;
            float angle = Vector3.Angle(_player.transform.forward, direction);
            if (angle > _angle / 2f) continue;
            
            // 적과 나 사이에 벽이 있으면 때리지 않는다.
            if (Physics.Linecast(origin, hitPoint, LayerMask.NameToLayer("Wall"))) continue;
            
            
            if (col.TryGetComponent(out IDamageable damageable))
            {
                DamageInfo damageInfo = new DamageInfo();
                damageInfo.Value = 25f;
                damageInfo.HitPoint = hitPoint;
                damageInfo.HitNormal = (origin - hitPoint).normalized;
                damageable.TakeDamage(damageInfo);
            }
        }
        
        
    }
    
    private void OnDrawGizmosSelected()
    {
        // 플레이하지 않을 때도 불리므로, Awake에서 찾는 _player 대신 여기서 찾는다.
        Player player = GetComponentInParent<Player>();
        if (player == null) return;

        Vector3 origin = player.transform.position;
        Vector3 forward = player.transform.forward;
        Gizmos.color = Color.yellow;

        // 앞쪽을 y축으로 -Angle/2, +Angle/2만큼 돌린 두 방향이 부채꼴의 양 끝이다.
        Vector3 left = Quaternion.Euler(0f, -_angle / 2f, 0f) * forward;
        Vector3 right = Quaternion.Euler(0f, _angle / 2f, 0f) * forward;
        Gizmos.DrawLine(origin, origin + left * _range);
        Gizmos.DrawLine(origin, origin + right * _range);

        // 호: 10도마다 짧은 선을 이어 그린다.
        int count = Mathf.Max(1, Mathf.CeilToInt(_angle / 10f));
        Vector3 previous = origin + left * _range;
        for (int i = 1; i <= count; i++)
        {
            float angle = -_angle / 2f + _angle * i / count;
            Vector3 next = origin + Quaternion.Euler(0f, angle, 0f) * forward * _range;
            Gizmos.DrawLine(previous, next);
            previous = next;
        }
    }
    
    // 람다식을 활용한 함수 표현법
    public void ActivateCollider()
    {
        if (_meleeHitMode != MeleeHitMode.Collider) return;
        
        Debug.Log("카타나 콜라이더 활성화");
        _katanaCollider.enabled = true;
    }

    public void DeactivateCollider()
    {
        if (_meleeHitMode != MeleeHitMode.Collider) return;

        Debug.Log("카타나 콜라이더 비활성화");
        _katanaCollider.enabled = false;
    }
    

    private void Update()
    {
        if (GameManager.Instance.State != GameState.Playing) return;
        if (_player.WeaponMode != WeaponMode.Melee) return;

        if (Input.GetButtonDown("Fire2"))
        {
            _animator.SetTrigger("Slash");
        }
    }
}
