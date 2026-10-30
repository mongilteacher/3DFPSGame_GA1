using UnityEngine;

// 역할: 마우스를 움직이면 카메라를 그 방향으로 회전하고 싶다.
public class CameraRotate : MonoBehaviour
{
    [SerializeField] private float _rotationSpeed = 200f;

    [Header("3인칭/백뷰 위아래 회전 제한")]
    [SerializeField] private float _followMinY = -40f; // 올려다보는 한계
    [SerializeField] private float _followMaxY = 60f;  // 내려다보는 한계

    private float _accumulationX = 0f; // 좌우   회전 각도
    private float _accumulationY = 0f; // 위아래 회전 각도

    private CameraFollow _cameraFollow;

    private void Awake()
    {
        _cameraFollow = GetComponent<CameraFollow>();
    }

    private void Update()
    {
        if (GameManager.Instance.State != GameState.Playing) return;

        // 1. 마우스 입력을 받는다.
        float mouseX = Input.GetAxis("Mouse X");
        float mouseY = Input.GetAxis("Mouse Y");

        // 2. 마우스 입력 만큼 회전값을 누적한다.
        _accumulationX += mouseX * _rotationSpeed * Time.deltaTime;
        _accumulationY += -mouseY * _rotationSpeed * Time.deltaTime;

        // 3. 누적하기 전에 내 변수에서 먼저 -90 ~ 90도로 제한
        //    3인칭/백뷰에서는 카메라가 바닥을 뚫지 않도록 더 좁게 제한
        if (_cameraFollow.Mode == CameraMode.FirstPerson)
        {
            _accumulationY = Mathf.Clamp(_accumulationY, -90, 90);
        }
        else
        {
            _accumulationY = Mathf.Clamp(_accumulationY, _followMinY, _followMaxY);
        }

        // 4. 누적한 회전값을 카메라에 적용한다.
        transform.eulerAngles = new Vector3(_accumulationY, _accumulationX, 0f);


        /*// 3. 그 방향으로 회전 시킨다.
        // 새로운 각도 = 현재 각도 + 회전 속도 * 시간
        transform.eulerAngles += direction * _rotationSpeed * Time.deltaTime;

        // 4. 위 아래 회전값을 -90 ~ 90도로 제한한다.
        Vector3 rotation = transform.eulerAngles;
        // 정면: 0도
        // 하늘을 살짞 1도만큼 쳐다보면: -1 -> 359 -> 90
        // 유니티는 0~360 체계를 쓰기때문에 음수는 자동으로 변환되서 반환된다.
        rotation.x = Mathf.Clamp(rotation.x, -90, 90);
        transform.eulerAngles = rotation;*/
    }
}