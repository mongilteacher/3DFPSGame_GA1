using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class UI_Loading : MonoBehaviour
{
    // allowSceneActivation이 false면 progress는 0.9에서 멈춘다. (0.9 = 로딩 완료)
    private const float LoadedProgress = 0.9f;

    [SerializeField] private TextMeshProUGUI _progressText;
    [SerializeField] private Slider _progressSlider;

    // 화면에 보이는 진행률이 실제 진행률을 따라가는 속도(초당). 1이면 0% → 100%에 최소 1초
    [SerializeField] private float _fillSpeed = 1f;

    private void Start()
    {
        Refresh(0f);
        StartCoroutine(Load_Coroutine());
    }

    private IEnumerator Load_Coroutine()
    {
        Application.backgroundLoadingPriority = ThreadPriority.Low;

        // 비동기 씬 로드!
        // 반환: 씬의 비동기 진행사항을 확인할 수 있는 객체
        AsyncOperation operation = SceneManager.LoadSceneAsync(SceneLoader.NextSceneName);
        // 로딩이 끝나도 바로 넘어가지 않게 막는다.
        operation.allowSceneActivation = false;

        // 실제 progress는 0에서 0.9로 한 번에 뛰기도 하므로,
        // 보여주는 값(displayProgress)을 따로 두고 실제 값을 천천히 따라가게 한다.
        float displayProgress = 0f;
        while (displayProgress < 1f)
        {
            // 0에서 1사이의 값으로 보간
            float targetProgress = Mathf.Clamp01(operation.progress / LoadedProgress);
            displayProgress = Mathf.MoveTowards(displayProgress, targetProgress, _fillSpeed * Time.unscaledDeltaTime);

            Refresh(displayProgress);

            yield return null;
        }

        // 다음 씬의 비동기 로딩에 영향을 주지 않도록 원래대로 돌려놓는다.
        Application.backgroundLoadingPriority = ThreadPriority.Normal;

        // 화면의 진행률이 100%가 된 뒤에 씬을 넘긴다.
        operation.allowSceneActivation = true;
    }

    private void Refresh(float progress)
    {
        _progressSlider.value = progress;
        _progressText.text = $"불러오는 중 {Mathf.RoundToInt(progress * 100)}%";
    }
}
