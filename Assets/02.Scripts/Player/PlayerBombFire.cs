using UnityEngine;

// 마우스 올느쪽 버튼을 누르면 시선이 바라보는 방향으로 수류탄을 던지고 싶다.
public class PlayerBombFire : MonoBehaviour
{
    [SerializeField] private Transform _firePoint;
    [SerializeField] private Bomb _bombPrefab;
    [SerializeField] private float _throwPower = 15f;
    [SerializeField] private float _spinPower = 0.4f;   // 던질 때 주는 회전력 (폭탄이 가벼워서 작은 값으로도 빙글빙글 돈다)

    [SerializeField] private int _maxBombCount = 3;   // 가질 수 있는 폭탄 수
    private int _bombCount;                           // 남은 폭탄 수

    public int BombCount => _bombCount;               // 남은 폭탄 수 (밖에서는 읽기만 한다)
    public event System.Action<int> OnBombChanged;    // 폭탄 수가 바뀌면 남은 수를 알린다.

    [SerializeField] private CameraFollow _cameraFollow;   // 조준 방향을 알려주는 카메라
    [SerializeField] private float _quarterViewLift = 0.3f; // 쿼터뷰에서 포물선으로 던지도록 위로 올려주는 양

    private Player _player;

    private void Awake()
    {
        _player = GetComponent<Player>();
        _bombCount = _maxBombCount;  // 폭탄을 가득 가지고 시작한다.

        if (_cameraFollow == null)
        {
            _cameraFollow = Camera.main.GetComponent<CameraFollow>();
        }
    }

    private void Update()
    {
        if (GameManager.Instance.State != GameState.Playing) return;

        // 1. 마우스 오른쪽 버튼 입력 받기 (일반 모드이고 폭탄이 남아 있을 때만)
        if (Input.GetButtonDown("Fire2") && _player.WeaponMode == WeaponMode.Normal && _bombCount > 0)
        {
            Shoot();

            _bombCount--;
            OnBombChanged?.Invoke(_bombCount);  // 하나 줄었다고 알린다.
        }

    }

    private void Shoot()
    {
        // 2. 발사 위치에서 수류탄 생성하기
        Bomb bomb = Instantiate(_bombPrefab, _firePoint.position, Quaternion.identity);

        // 3. 생성된 수류탄을 시선 방향으로 던지기
        Rigidbody rigidbody = bomb.GetComponent<Rigidbody>();
        //    쿼터뷰의 조준 방향은 수평이므로 살짝 위로 올려 던진다.
        Vector3 direction = _cameraFollow.GetAimRay().direction;
        if (_cameraFollow.Mode == CameraMode.QuarterView)
        {
            direction = (direction + Vector3.up * _quarterViewLift).normalized;
        }
        rigidbody.AddForce(direction * _throwPower, ForceMode.Impulse);

        // 4. 날아가는 동안 무작위 방향으로 빙글빙글 돌도록 토크를 준다.
        rigidbody.AddTorque(Random.onUnitSphere * _spinPower, ForceMode.Impulse);
    }

}
