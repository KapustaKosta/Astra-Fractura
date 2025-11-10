using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Research
{
    public enum ResearchNodeState
    {
        Locked,
        Available,
        Completed
    }

    public class ResearchNodeWidget : MonoBehaviour
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI costText;
        [SerializeField] private Button selectButton;
        [SerializeField] private Button researchButton;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private GameObject selectionHighlight;

        private ResearchTechnology technology;
        private Action<ResearchTechnology> onSelected;
        private Action<ResearchTechnology> onResearch;

        public ResearchTechnology Technology => technology;

        public void Initialize(ResearchTechnology tech, Action<ResearchTechnology> selectedCallback, Action<ResearchTechnology> researchCallback)
        {
            technology = tech;
            onSelected = selectedCallback;
            onResearch = researchCallback;

            if (nameText != null)
            {
                nameText.text = tech != null ? tech.DisplayName : string.Empty;
            }

            if (iconImage != null)
            {
                iconImage.sprite = tech != null ? tech.Icon : null;
                iconImage.enabled = tech != null && tech.Icon != null;
            }

            if (costText != null)
            {
                costText.text = tech != null ? tech.Cost.ToString() : string.Empty;
            }

            if (selectButton != null)
            {
                selectButton.onClick.AddListener(HandleSelect);
            }

            if (researchButton != null)
            {
                researchButton.onClick.AddListener(HandleResearch);
            }
        }

        private void OnDestroy()
        {
            if (selectButton != null)
            {
                selectButton.onClick.RemoveListener(HandleSelect);
            }

            if (researchButton != null)
            {
                researchButton.onClick.RemoveListener(HandleResearch);
            }
        }

        public void SetState(ResearchNodeState state, bool canAfford)
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = state == ResearchNodeState.Locked ? 0.4f : 1f;
            }

            if (researchButton != null)
            {
                researchButton.gameObject.SetActive(state != ResearchNodeState.Completed);
                researchButton.interactable = state == ResearchNodeState.Available && canAfford;
            }
        }

        public void SetSelected(bool isSelected)
        {
            if (selectionHighlight != null)
            {
                selectionHighlight.SetActive(isSelected);
            }
        }

        private void HandleSelect()
        {
            onSelected?.Invoke(technology);
        }

        private void HandleResearch()
        {
            onResearch?.Invoke(technology);
        }
    }
}
