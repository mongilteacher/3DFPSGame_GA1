using System.Text.RegularExpressions;

public class AccountPasswordSpecification
{
    // 비밀번호: 전방탐색 (?=...)으로 소문자, 숫자, 기호가 각각 하나 이상 있는지 검사하고,
    //           전체는 공백을 제외한 ASCII 출력 문자([!-~]) 10~30 글자
    // 기호 = [!-/] [:-@] [\[-`] [{-~] (ASCII 출력 문자 중 영문/숫자가 아닌 범위)
    // 예) abcdef123!, my_pass_2026
    private static readonly Regex PasswordRegex = new Regex(@"^(?=.*[a-z])(?=.*[0-9])(?=.*[!-/:-@\[-`{-~])[!-~]{10,30}$");

    public string ErrorMessage => "비밀번호는 소문자, 숫자, 기호를 모두 포함한 10~30 글자여야 합니다.";

    public bool IsSatisfiedBy(string password)
    {
        return PasswordRegex.IsMatch(password);
    }
}
