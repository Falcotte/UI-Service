using AngryKoala.Services;
using AngryKoala.UI;
using UnityEngine;
using UnityEngine.UI;

public sealed class SettingsScreen : BaseScreen
{
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

    private async void OnBackClicked()
    {
        await _uiService.HideScreenAsync("Settings");
    }
}