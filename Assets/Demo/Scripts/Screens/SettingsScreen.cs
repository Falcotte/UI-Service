using System.Threading;
using System.Threading.Tasks;
using AngryKoala.Extensions;
using AngryKoala.Services;
using AngryKoala.UI;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public sealed class SettingsScreen : BaseScreen
{
    [SerializeField] private CanvasGroup _canvasGroup;
    
    [SerializeField] private Button _backButton;

    private IUIService _uiService;

    private void Awake()
    {
        _uiService = ServiceLocator.Get<IUIService>();

        if (_backButton != null)
        {
            _backButton.onClick.AddListener(OnBackClicked);
        }
    }

    protected override Task OnShowAsync(CancellationToken cancellationToken)
    {
        _canvasGroup.alpha = 0f;
        return _canvasGroup.DOFade(1f, 1f)
            .SetEase(Ease.OutCubic)
            .AwaitCompletionAsync(cancellationToken);
    }
    
    protected override Task OnHideAsync(CancellationToken cancellationToken)
    {
        return _canvasGroup.DOFade(0f, 1f)
            .SetEase(Ease.InCubic)
            .AwaitCompletionAsync(cancellationToken);
    }

    private async void OnBackClicked()
    {
        await _uiService.HideScreenAsync("Settings");
        await _uiService.ShowScreenAsync("Home");
    }
}