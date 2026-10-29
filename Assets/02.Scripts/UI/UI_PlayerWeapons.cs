using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 오른쪽 아래에 지금 무기의 아이콘, 남은 총알, 남은 폭탄을 보여 준다.
// 무기 모드, 총알 수, 폭탄 수가 바뀌었다는 알림이 올 때만 고친다. (옵저버 패턴)
public class UI_PlayerWeapons : MonoBehaviour
{
    [SerializeField] private Image _weaponIcon;            // 무기 아이콘
    [SerializeField] private Sprite[] _modeIcons;          // 모드마다 보여 줄 그림 (WeaponMode에 적은 순서대로)
    [SerializeField] private TextMeshProUGUI _bulletText;  // 남은 총알 / 탄창 크기
    [SerializeField] private TextMeshProUGUI _bombText;    // 남은 폭탄

    [SerializeField] private Player _player;
    [SerializeField] private PlayerGunFire _gunFire;
    [SerializeField] private PlayerBombFire _bombFire;

    private void OnEnable()
    {
        // 구독: 바뀌면 불러 달라고 등록한다.
        _player.OnWeaponModeChanged += RefreshIcon;
        _gunFire.OnBulletChanged += RefreshBullet;
        _bombFire.OnBombChanged += RefreshBomb;
    }

    private void OnDisable()
    {
        // 구독 취소: 꺼질 때 목록에서 뺀다.
        _player.OnWeaponModeChanged -= RefreshIcon;
        _gunFire.OnBulletChanged -= RefreshBullet;
        _bombFire.OnBombChanged -= RefreshBomb;
    }

    private void Start()
    {
        // 처음에는 아직 알림이 오지 않았으므로 지금 값을 한 번 읽어 와 보여 준다.
        RefreshIcon(_player.WeaponMode);
        RefreshBullet(_gunFire.BulletCount, _gunFire.MaxBulletCount);
        RefreshBomb(_bombFire.BombCount);
    }

    // 모드가 바뀌었다는 알림이 오면 불리는 함수(콜백)
    private void RefreshIcon(WeaponMode mode)
    {
        _weaponIcon.sprite = _modeIcons[(int)mode];  // Normal은 0번 칸, Sniper는 1번 칸
    }

    // 총알 수가 바뀌었다는 알림이 오면 불리는 함수(콜백)
    private void RefreshBullet(int count, int maxCount)
    {
        _bulletText.text = $"{count} / {maxCount}";
    }

    // 폭탄 수가 바뀌었다는 알림이 오면 불리는 함수(콜백)
    private void RefreshBomb(int count)
    {
        _bombText.text = $"폭탄 {count}";
    }
}
