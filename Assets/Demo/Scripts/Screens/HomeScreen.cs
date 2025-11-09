using AngryKoala.Services;
using AngryKoala.UI;
using UnityEngine;
using UnityEngine.UI;

public sealed class HomeScreen : BaseScreen
{
    [SerializeField] private Button _goToSettingsButton;

    private IUIService _uiService;

    private void Awake()
    {
        _uiService = ServiceLocator.Get<IUIService>();

        if (_goToSettingsButton != null)
        {
            _goToSettingsButton.onClick.AddListener(OnGoToSettingsClicked);
        }
    }

    private async void OnGoToSettingsClicked()
    {
        if (_uiService != null)
        {
            await _uiService.ShowScreenAsync("Settings");
            await _uiService.HideScreenAsync("Home");
        }
    }
}