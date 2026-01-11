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

    private async void ShowHomeScreen()
    {
        await _uiService.ShowScreenAsync("Main");
    }
}