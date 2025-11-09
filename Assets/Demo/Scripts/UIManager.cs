using AngryKoala.Services;
using AngryKoala.UI;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    private IUIService _uiService;

    private void Start()
    {
        _uiService = ServiceLocator.Get<IUIService>();
        
        ShowHomeScreen();
    }

    private void ShowHomeScreen()
    {
        _uiService.ShowScreenAsync("Home");
        _uiService.LoadScreenAsync("Settings");
    }
}