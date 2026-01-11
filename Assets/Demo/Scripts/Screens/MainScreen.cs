using AngryKoala.Services;
using AngryKoala.UI;
using UnityEngine;
using Screen = AngryKoala.UI.Screen;

public class MainScreen : Screen
{
    [SerializeField] private Button _openTestScreenButton;
    [SerializeField] private Button _openTestScreenWithSubscreenButton;

    private IUIService _uiService;
    
    private void OnEnable()
    {
        _uiService = ServiceLocator.Get<IUIService>();
        
        _openTestScreenButton.OnClickEvent.AddListener(OpenTestScreen);
        _openTestScreenWithSubscreenButton.OnClickEvent.AddListener(OpenTestScreenWithSubscreen);
    }

    private void OnDisable()
    {
        _openTestScreenButton.OnClickEvent.RemoveListener(OpenTestScreen);
        _openTestScreenWithSubscreenButton.OnClickEvent.RemoveListener(OpenTestScreenWithSubscreen);
    }

    private void OpenTestScreen()
    {
        _uiService.ShowScreenAsync("TestScreen");
    }

    private async void OpenTestScreenWithSubscreen()
    {
        await _uiService.ShowScreenAsync("TestScreenWithSubscreen");
        await _uiService.ShowSubscreenAsync("TestScreenWithSubscreen", "TestSubscreen");
    }
}