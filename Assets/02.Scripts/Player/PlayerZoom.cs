using UnityEngine;

// 저격 모드에서 마우스 오른쪽 버튼을 누르고 있는 동안 시야각 좁혀 확대한다.
public class PlayerZoom : MonoBehaviour
{
    [SerializeField] private GameObject _sniperZoom;
    [SerializeField] private float _normalPov = 60f;
    [SerializeField] private float _sniperPov = 15f;

    [SerializeField] private CameraFollow _cameraFollow;

    private Player _player;



    private void Awake()
    {
        _player = GetComponent<Player>();

        if (_cameraFollow == null)
        {
            _cameraFollow = Camera.main.GetComponent<CameraFollow>();
        }
    }

    private void Update()
    {
        if (GameManager.Instance.State != GameState.Playing)
        {
            return;
        }

        // 저격 줌은 1인칭에서만 쓴다.
        bool isZooming = _player.WeaponMode == WeaponMode.Sniper
                         && _cameraFollow.Mode == CameraMode.FirstPerson
                         && Input.GetMouseButton(1);
        _sniperZoom.SetActive(isZooming);
        Camera.main.fieldOfView = isZooming ? _sniperPov : _normalPov;
    }

}
