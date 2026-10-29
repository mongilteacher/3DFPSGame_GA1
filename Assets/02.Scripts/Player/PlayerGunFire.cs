using System.Collections;
using UnityEngine;


// 마우스 왼쪽 버튼을 누르면 바라보는 방향으로 총알을 발사하고 싶다.
public class PlayerGunFire : MonoBehaviour
{
    [SerializeField] private ParticleSystem _hitEffectPrefab;

    [Header("스탯")] 
    [SerializeField] private int _maxBulletCount = 30;
    [SerializeField] private float _reloadingTime = 3f;
    [SerializeField] private float _range = 100f;            // 사거리
    [SerializeField] private float _damage = 10f;            // 공격력
    [SerializeField] private float _fireInterval = 0.1f;     // 발사 속도
    
    private float _fireTimer = 0f;
    private int _bulletCount = 0;
    private Coroutine _reloadCoroutine = null;
    private Animator _animator;

    public int BulletCount => _bulletCount;                 // 남은 총알 수 (밖에서는 읽기만 한다)
    public int MaxBulletCount => _maxBulletCount;           // 탄창에 들어가는 총알 수
    public event System.Action<int, int> OnBulletChanged;   // 총알 수가 바뀌면 (남은 수, 탄창 크기)를 알린다.

    private void Awake()
    {
        // UI가 Start에서 처음 값을 읽어 가므로, 총알은 Awake에서 채워 둔다.
        _bulletCount = _maxBulletCount;
    }

    private void Start()
    {
        _animator = GetComponentInChildren<Animator>();
    }
    
    
    private void Update()
    {
        if (GameManager.Instance.State != GameState.Playing) return;

        // 0. 발사 타이머를 지난 시간만큼 줄인다.
        _fireTimer -= Time.deltaTime;
        
        // 1. 마우스 왼쪽 버튼 입력 받기
        bool isMouseVisible = Cursor.visible;
        if (!isMouseVisible && Input.GetButton("Fire1") && _fireTimer <= 0f && _bulletCount > 0)
        {
            _bulletCount--;
            OnBulletChanged?.Invoke(_bulletCount, _maxBulletCount);  // 한 발 줄었다고 알린다.
            _fireTimer = _fireInterval;
            Shoot();
            
            
            _animator.SetBool("Fire", true);
            
            // 이 방식은: 이 모노비헤이비어가 등록한 모든 코루틴을 실행 취소
            // StopAllCoroutines();
            
            if(_reloadCoroutine != null)
            {
                StopCoroutine(_reloadCoroutine);
                Debug.Log("재장전 취소!");
                _reloadCoroutine = null;
            }
        }
        else
        {
            _animator.SetBool("Fire", false);
        }
        
        // 2. R키 누르면 총알 재장전 시작
        if (Input.GetKeyDown(KeyCode.R) && _reloadCoroutine == null)
        {
            _reloadCoroutine = StartCoroutine(Reloading_Coroutine());
        }
    }

    private IEnumerator Reloading_Coroutine()
    {
        Debug.Log("재장전 시작!");
        
        yield return new WaitForSeconds(_reloadingTime);
        
        _bulletCount = _maxBulletCount;
        OnBulletChanged?.Invoke(_bulletCount, _maxBulletCount);  // 탄창을 채웠다고 알린다.

        Debug.Log("재장전 완료!");
        _reloadCoroutine = null;
    }
    

    private void Shoot()
    {
        // Ray: 레이저(시작위치, 날아갈 방향)
        // Physics.RayCast: 레이저를 발사
        // RayCastHit: 레이저가 물체와 부딛히면 그 정보를 저장하는 구조체
        
        // 2. 레이(광선)를 생성하고 발사될 위치와 진행 방향 설정하기
        Transform cameraTransform = Camera.main.transform;
        Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);

        // 3. 레이가 부딛힌 대상의 정보를 저장할 변수 생성하기
        RaycastHit hitInfo = new RaycastHit();

        // 4. 레이를 발사하고, 만일 부딛힌 물체가 있다면 이펙트 표시하기
        bool isHit = Physics.Raycast(ray, out hitInfo, _range);
        if (isHit)
        {
            // Debug.Log($"{hitInfo.collider.name}이 총알을 맞았다. 대미지: {_damage}");
            
            // 맞은 자리에 총알 자국 이펙트 만들기
            ParticleSystem effect = Instantiate(_hitEffectPrefab);
            effect.transform.position = hitInfo.point; // 맞은 자리
            effect.transform.forward = hitInfo.normal; // 수직 위치로 회전
            effect.Play();
            
            // 대미지 구조체 만들기
            DamageInfo damageInfo = new DamageInfo();
            damageInfo.Value = _damage;
            damageInfo.HitPoint = hitInfo.point;
            damageInfo.HitNormal = hitInfo.normal;
            
            hitInfo.collider.GetComponent<IDamageable>()?.TakeDamage(damageInfo);
            
            /*
             hitInfo.collider.GetComponent<Zombie>()?.TakeDamage(_damage);
            hitInfo.collider.GetComponent<Drum>()?.TakeDamage(_damage);
            hitInfo.collider.GetComponent<Box>()?.TakeDamage(_damage);
            */
            
            // 황유찬 :         Player
            // 김민쉬:     Zombie, Boss
            // 박현서:  Drum, Box, Bird
        }
    }
}
