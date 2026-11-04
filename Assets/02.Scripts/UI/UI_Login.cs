using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class UI_Login : MonoBehaviour
{
    private enum Mode
    {
        Login,     // 로그인
        Register,  // 회원가입
    }

    [Header("입력 칸")]
    [SerializeField] private TMP_InputField _idInput;
    [SerializeField] private TMP_InputField _passwordInput;
    [SerializeField] private TMP_InputField _passwordConfirmInput;  // 회원가입 모드에서만 보인다.

    [Header("버튼")]
    [SerializeField] private Button _loginButton;         // 로그인 모드: 로그인
    [SerializeField] private Button _gotoRegisterButton;  // 로그인 모드: 회원가입 모드로
    [SerializeField] private Button _registerButton;      // 회원가입 모드: 가입하기
    [SerializeField] private Button _gotoLoginButton;     // 회원가입 모드: 로그인 모드로
    
    [Header("기타")]
    [SerializeField] private TextMeshProUGUI _messageText;        // 안내 글자

    
    private Mode _mode = Mode.Login;

    private PlayerPrefsAccountRepository _repository = new PlayerPrefsAccountRepository();

    // 이메일: (영문, 숫자, ., _, -)@(영문, 숫자, -)(.영문, 숫자, -)를 1번 이상
    // 예) abc.def@gmail.com, user_01@mail.co.kr
    private static readonly Regex EmailRegex = new Regex(@"^[A-Za-z0-9._-]+@[A-Za-z0-9-]+(\.[A-Za-z0-9-]+)+$");

    // 비밀번호: 전방탐색 (?=...)으로 대문자, 소문자, 숫자, 기호가 각각 하나 이상 있는지 검사하고,
    //           전체는 공백을 제외한 ASCII 출력 문자([!-~]) 8~30 글자
    // 기호 = [!-/] [:-@] [\[-`] [{-~] (ASCII 출력 문자 중 영문/숫자가 아닌 범위)
    private static readonly Regex PasswordRegex = new Regex(@"^(?=.*[A-Z])(?=.*[a-z])(?=.*[0-9])(?=.*[!-/:-@\[-`{-~])[!-~]{8,30}$");
    
    
    private void Start()
    { 
        Refresh();
    }

    private void Refresh()
    {
        bool isLoginMode = _mode == Mode.Login;
        
        _passwordConfirmInput.gameObject.SetActive(!isLoginMode);
        
        _loginButton.gameObject.SetActive(isLoginMode);
        _gotoRegisterButton.gameObject.SetActive(isLoginMode);
        
        _registerButton.gameObject.SetActive(!isLoginMode);
        _gotoLoginButton.gameObject.SetActive(!isLoginMode);
    }

    // 로그인 모드로
    public void GotoLogin()
    {
        _mode = Mode.Login;
        _messageText.text = string.Empty;
        Refresh();
    }

    // 회원가입 모드로
    public void GotoRegister()
    {
        _mode = Mode.Register;
        _messageText.text = string.Empty;
        Refresh();
    }
    
    public void Login()
    {
        string id = _idInput.text;
        string password = _passwordInput.text;

        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(password))
        {
            _messageText.text = "아이디와 비밀번호를 입력해주세요.";
            return;
        }

        if (_repository.CheckPassword(id, password))
        {
            SceneManager.LoadScene("GameScene");
        }
        else
        {
            _messageText.text = "아이디와 비밀번호를 확인해주세요.";
            return;
        }
        
        
    }

    public void Register()
    {
        string id = _idInput.text;
        string password = _passwordInput.text;
        string passwordConfirm = _passwordConfirmInput.text;
        
        // 정규표현식(정규식)
        // 문자열에서 특정한 규칙이나 패턴을 찾기 위해 사용하는 문법
        // - 데이터 검증(이메일 형식, 주민번호 형식, 비밀번호 형식)
        // - 문자열 검색(ToDo라는게 있냐?, 한국어가 있냐?)
        // - 문자열 치환(규칙에 맞는 문자열을 찾아서 다른 문자열로 변경)
        // - 웹 크롤링 & 파싱
        
        
        // 규칙이 여기서만 쓰일까?
        // 정리 과제
        //   - 정규표현식
        //   - 명세 패턴
        // \.text\s*=\s*\$?"[^"]*[가-힣][^"]*"
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(password))
        {
            _messageText.text = "아이디와 비밀번호를 입력해주세요.";
            return;
        }

        // ID 규칙: 이메일
        if (!EmailRegex.IsMatch(id))
        {
            _messageText.text = "아이디는 이메일 형식이어야 합니다.";
            return;
        }

        // 비밀번호 규칙: 대문자, 소문자, 숫자, 기호를 모두 포함한 8~30 글자
        if (!PasswordRegex.IsMatch(password))
        {
            _messageText.text = "비밀번호는 대문자, 소문자, 숫자, 기호를 모두 포함한 8~30 글자여야 합니다.";
            return;
        }

        if (password != passwordConfirm)
        {
            _messageText.text = "비밀번호 확인이 맞지 않습니다.";
            return;
        }
        
        if (_repository.Exists(id))
        {
            _messageText.text = "이미 존재하는 아이디입니다.";
            return;
        }
        
        _repository.Register(id, password);
        
        GotoLogin();
        _messageText.text = "가입했습니다. 로그인 해주세요.";
    }
}
