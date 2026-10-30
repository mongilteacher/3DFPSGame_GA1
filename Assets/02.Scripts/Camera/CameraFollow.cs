using System;
using UnityEngine;

// 카메라 모드에 따라 타겟을 따라다니는 위치를 바꾼다. [ / ] 키로 모드를 전환한다.
public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform _target;
    [SerializeField] private CameraMode _mode = CameraMode.FirstPerson;

    [Header("모드별 위치")]
    [SerializeField] private Vector3 _thirdPersonOffset = new Vector3(0.6f, 0.3f, -2.5f); // 어깨 너머
    [SerializeField] private Vector3 _backViewOffset = new Vector3(0f, 1f, -4.5f);        // 바로 뒤
    [SerializeField] private float _quarterViewDistance = 12f;                            // 내려다보는 거리
    [SerializeField] private Vector3 _quarterViewAngle = new Vector3(45f, 45f, 0f);       // 월드 기준 고정 각도

    public CameraMode Mode => _mode;
    public event Action<CameraMode> OnCameraModeChanged;

    private void Awake()
    {
        if (_target == null)
        {
            Debug.LogWarning("[CameraFollow] 카메라 타겟이 없습니다!!");
        }
    }

    private void Update()
    {
        if (GameManager.Instance.State != GameState.Playing) return;

        if (Input.GetKeyDown(KeyCode.LeftBracket))
        {
            ChangeMode(-1);
        }
        else if (Input.GetKeyDown(KeyCode.RightBracket))
        {
            ChangeMode(1);
        }
    }

    // 모드를 앞(1)이나 뒤(-1)로 한 칸 순환시킨다.
    private void ChangeMode(int step)
    {
        int count = Enum.GetValues(typeof(CameraMode)).Length;
        _mode = (CameraMode)(((int)_mode + step + count) % count);
        OnCameraModeChanged?.Invoke(_mode);
        Debug.Log($"카메라 모드 변경: {_mode}");
    }

    private void LateUpdate()
    {
        // 회전은 CameraRotate가 Update에서 먼저 적용해 둔다.
        switch (_mode)
        {
            case CameraMode.FirstPerson:
                transform.position = _target.position;
                break;

            case CameraMode.ThirdPerson:
                transform.position = _target.position + transform.rotation * _thirdPersonOffset;
                break;

            case CameraMode.BackView:
                transform.position = _target.position + transform.rotation * _backViewOffset;
                break;

            case CameraMode.QuarterView:
                transform.rotation = Quaternion.Euler(_quarterViewAngle);
                transform.position = _target.position - transform.forward * _quarterViewDistance;
                break;
        }
    }

    // 사격과 투척이 나갈 방향. 쿼터뷰는 카메라가 바닥을 보므로 플레이어 정면을 쓴다.
    public Ray GetAimRay()
    {
        if (_mode == CameraMode.QuarterView)
        {
            Vector3 forward = _target.forward;
            forward.y = 0f;
            return new Ray(_target.position, forward.normalized);
        }

        return new Ray(transform.position, transform.forward);
    }
}
