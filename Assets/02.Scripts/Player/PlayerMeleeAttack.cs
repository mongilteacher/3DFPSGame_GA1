using UnityEngine;

public class PlayerMeleeAttack : MonoBehaviour
{
    private Player _player;
    private Animator _animator;
    private int _meleeLayerIndex;
    [SerializeField] private Collider _katanaCollider;

    private void Awake()
    {
        _player = GetComponentInParent<Player>();
        _animator = GetComponentInChildren<Animator>();
        _meleeLayerIndex = _animator.GetLayerIndex("Melee Layer");
    }
    
    // 람다식을 활용한 함수 표현법
    public void ActivateCollider()
    {
        Debug.Log("카타나 콜라이더 활성화");
        _katanaCollider.enabled = true;
    }

    public void DeactivateCollider()
    {
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
