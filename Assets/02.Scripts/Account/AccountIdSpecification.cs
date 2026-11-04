using System.Text.RegularExpressions;

public class AccountIdSpecification
{
    // 이메일: (영문, 숫자, ., _, -)@(영문, 숫자, -)(.영문, 숫자, -)를 1번 이상
    // 예) abc.def@gmail.com, user_01@mail.co.kr
    private static readonly Regex EmailRegex = new Regex(@"^[A-Za-z0-9._-]+@[A-Za-z0-9-]+(\.[A-Za-z0-9-]+)+$");

    public string ErrorMessage => "아이디는 이메일 형식이어야 합니다.";
    
    public bool IsSatisfiedBy(string email)
    {
        return EmailRegex.IsMatch(email);
    }
}