using AngryKoala.Services;
using AngryKoala.UI;
using UnityEngine;
using Screen = AngryKoala.UI.Screen;

public class TestScreenWithSubscreen : Screen
{
    [SerializeField] private Button _openSubscreenButton;
    [SerializeField] private Button _closeSubscreenButton;
    
    [SerializeField] private Button _closeButton;

    private IUIService _uiService;
    
    private void OnEnable()
    {
        _uiService = ServiceLocator.Get<IUIService>();
        
        _openSubscreenButton.OnClickEvent.AddListener(OpenSubscreen);
        _closeSubscreenButton.OnClickEvent.AddListener(CloseSubscreen);
        
        _closeButton.OnClickEvent.AddListener(CloseScreen);
    }

    private void OnDisable()
    {
        _openSubscreenButton.OnClickEvent.RemoveListener(OpenSubscreen);
        _closeSubscreenButton.OnClickEvent.RemoveListener(CloseSubscreen);
        
        _closeButton.OnClickEvent.RemoveListener(CloseScreen);
    }

    private void OpenSubscreen()
    {
        _uiService.ShowSubscreenAsync(ScreenKey, "TestSubscreen");
    }

    private void CloseSubscreen()
    {
        _uiService.HideSubscreenAsync("TestSubscreen");
    }
    
    private void CloseScreen()
    {
        _uiService.HideScreenAsync(ScreenKey);
    }
}
