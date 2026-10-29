using UnityEngine;

// 저격 모드에서 마우스 오른쪽 버튼을 누르고 있는 동안 시야각 좁혀 확대한다.
public class PlayerZoom : MonoBehaviour
{
    private Player _player;


    private void Awake()
    {
        _player = GetComponent<Player>();
    }

    private void Update()
    {
        if (GameManager.Instance.State != GameState.Playing)
        {
            return;
        }

        bool isZooming = _player.WeaponMode == WeaponMode.Sniper && Input.GetMouseButton(1);
        Camera.main.fieldOfView = isZooming ? 15f : 60f;
    }

}
