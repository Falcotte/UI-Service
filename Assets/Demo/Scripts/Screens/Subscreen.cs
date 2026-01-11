using System;
using AngryKoala.Services;
using AngryKoala.UI;
using UnityEngine;
using Screen = AngryKoala.UI.Screen;

public class Subscreen : Screen
{
    [SerializeField] private Button _closeButton;
   
    private IUIService _uiService;

    private void OnEnable()
    {
        _uiService = ServiceLocator.Get<IUIService>();
        
        _closeButton.OnClickEvent.AddListener(CloseScreen);
    }

    private void OnDisable()
    {
        _closeButton.OnClickEvent.RemoveListener(CloseScreen);
    }

    private void CloseScreen()
    {
        _uiService.HideSubscreenAsync(ScreenKey);
    }
}
