using System;
using Dungonator.Client.Scripts;
using UnityEngine;
using UnityEngine.UIElements;

namespace Dungonator.Client.UI.Scripts
{
    public class MenuManager : MonoBehaviour
    {
        VisualElement _play, _settings, _mainMenu;

        void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;

            //Query each panel by name
            _play     = root.Q<VisualElement>("serverBrowserPanel");
            _settings = root.Q<VisualElement>("settingsPanel");
            _mainMenu = root.Q<VisualElement>("mainMenuPanel");

            //Hook up buttons and assert
            Button btnPlay     = root.Q<Button>("btnPlay");
            Button btnSettings = root.Q<Button>("btnSettings");
            Button btnQuit = root.Q<Button>("btnQuit");
            
            Button btnMenuInPlay     = _play.Q<Button>("btnMenu");
            Button btnMenuInSettings  = _settings.Q<Button>("btnMenu");

            //if (btnPlay != null) btnPlay.clicked += () => Show(_play); TODO: shows serverbrowser, change back later

            if (btnPlay != null) btnPlay.clicked += () => ClientConnector.Instance.Connect();
            
            if (btnSettings != null) btnSettings.clicked += () => Show(_settings);
            if (btnQuit != null) btnQuit.clicked += Application.Quit;
            
            if (btnMenuInPlay != null) btnMenuInPlay.clicked += () => Show(_mainMenu);
            if (btnMenuInSettings != null) btnMenuInSettings.clicked += () => Show(_mainMenu);

            //Start by showing main menu
            Show(_mainMenu);
        }

        void Show(VisualElement panelToShow)
        {
            // Remove both our class and explicit display styling
            void Hide(VisualElement panel)
            {
                if (panel == null) return;
                panel.RemoveFromClassList("panel-visible");
                panel.style.display = DisplayStyle.None;
            }

            Hide(_play);
            Hide(_settings);
            Hide(_mainMenu);

            if (panelToShow != null)
            {
                panelToShow.AddToClassList("panel-visible");
                // fallback in case USS isn't applied
                panelToShow.style.display = DisplayStyle.Flex;
            }
            else throw new Exception("panelToShow is null!");
        }
    }
}