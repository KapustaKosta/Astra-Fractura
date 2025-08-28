using UnityEngine;
using Unity.Entities;
using TMPro;
using UnityEngine.UI;
using Energy.Core;
using Unity.Transforms; // Parent

namespace Energy.UI
{
    public class GeneratorUI : MonoBehaviour
    {
        public static GeneratorUI Instance { get; private set; }

        [Header("Panel")]
        [SerializeField] private GameObject uiPanel;

        [Header("Text (basic)")]
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private TextMeshProUGUI powerText;   // "2.0 / 5.0 кВт"
        [SerializeField] private TextMeshProUGUI levelText;

        [Header("Ports (optional visuals)")]
        [SerializeField] private TextMeshProUGUI portsText;   // "In 0.0/2.0 | Out 2.0/2.0 кВт"
        [SerializeField] private Slider inPortSlider;         // 0..1
        [SerializeField] private Slider outPortSlider;        // 0..1

        [Header("Ramp & Hints (optional)")]
        [SerializeField] private TextMeshProUGUI rampText;    // "Ramp: ↑5.0 ↓10.0 кВт/с"
        [SerializeField] private TextMeshProUGUI hintsText;   // "Ограничение: PORT OUT / RATED"

        [Header("Buttons")]
        [SerializeField] private Button closeButton;
        [SerializeField] private Button toggleButton;

        private EntityManager _em;
        private bool _init;
        private Entity _target;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else { Destroy(gameObject); return; }

#if UNITY_EDITOR
            if (!uiPanel || !nameText || !statusText || !powerText || !levelText || !closeButton || !toggleButton)
            {
                Debug.LogError("[GeneratorUI] Назначи обязательные ссылки в инспекторе!", this);
                enabled = false;
            }
#endif
        }

        private void Start()
        {
            TryInit();
            if (_init)
            {
                uiPanel.SetActive(false);
                closeButton.onClick.AddListener(OnClosePressed);
                toggleButton.onClick.AddListener(OnTogglePressed);
            }
        }

        private void TryInit()
        {
            if (_init) return;
            var w = World.DefaultGameObjectInjectionWorld;
            if (w != null && w.IsCreated) { _em = w.EntityManager; _init = true; }
        }

        private void Update()
        {
            if (!_init) { TryInit(); return; }

            var gameStateQuery = _em.CreateEntityQuery(typeof(GameState));
            if (gameStateQuery.IsEmpty) return;
            var gameStateEntity = gameStateQuery.GetSingletonEntity();

            bool shouldBeOpen = _em.HasComponent<InUIMode>(gameStateEntity) &&
                                _em.HasComponent<UIState>(gameStateEntity) &&
                                _em.GetComponentData<UIState>(gameStateEntity).ActiveUIType == UIType.Generator;

            if (uiPanel.activeSelf != shouldBeOpen)
            {
                if (shouldBeOpen)
                {
                    var uiState = _em.GetComponentData<UIState>(gameStateEntity);
                    OpenFor(uiState.ActiveUITarget);
                }
                else
                {
                    Hide();
                }
            }
        }

        private void LateUpdate()
        {
            if (!uiPanel.activeSelf || _target == Entity.Null || !_em.Exists(_target)) return;
            Refresh();
        }

        public void OpenFor(Entity generatorEntity)
        {
            if (!_init) TryInit();
            _target = (!_em.Exists(generatorEntity)) ? Entity.Null : generatorEntity;
            uiPanel.SetActive(_target != Entity.Null);
            if (_target != Entity.Null) Refresh(true);
        }

        private void Hide()
        {
            uiPanel.SetActive(false);
            _target = Entity.Null;
        }

        private void OnClosePressed()
        {
            GameBridge.Instance?.HandleUICloseAction();
            Hide();
        }

        private void OnTogglePressed()
        {
            if (_target == Entity.Null) return;

            bool online = false;
            if (_em.HasComponent<GeneratorComponent>(_target))
            {
                var g = _em.GetComponentData<GeneratorComponent>(_target);
                online = g.IsOnline;
            }

            var req = _em.CreateEntity();
            _em.AddComponentData(req, new ToggleGeneratorRequest { Target = _target, DesiredOn = !online });
        }

        private void Refresh(bool force = false)
        {
            if (!_em.Exists(_target)) { Hide(); return; }

            string genName = "<Generator>";
#if UNITY_EDITOR
            try { genName = _em.GetName(_target); } catch { }
#endif
            if (_em.HasComponent<GeneratorComponent>(_target))
            {
                var g = _em.GetComponentData<GeneratorComponent>(_target);
                if (g.Name.Length > 0) genName = g.Name.ToString();

                nameText.text = genName;
                statusText.text = g.IsOnline ? "Статус: Включен" : "Статус: Выключен";
                powerText.text = $"Мощность: {FmtKW(g.CurrentKW)} / {FmtKW(g.RatedKW)}";
                levelText.text = $"Уровень линии: {g.Level}";

                // важная часть: читаем порты с узла (NetworkNode), а не обязательно с _target
                var nodeEnt = ResolveNodeOwner(_target);
                float inUsed = 0f, outUsed = 0f, cap = float.PositiveInfinity;

                if (nodeEnt != Entity.Null)
                {
                    if (_em.HasComponent<NetLinkUsage>(nodeEnt))
                    {
                        var u = _em.GetComponentData<NetLinkUsage>(nodeEnt);
                        inUsed = u.InUsedKW;
                        outUsed = u.OutUsedKW;
                    }
                    if (_em.HasComponent<NetLinkCapacity>(nodeEnt))
                    {
                        cap = _em.GetComponentData<NetLinkCapacity>(nodeEnt).MaxKW;
                    }
                }

                if (portsText)
                {
                    string capStr = float.IsPositiveInfinity(cap) ? "∞" : cap.ToString("0.0");
                    portsText.text = $"Порты: In {inUsed:0.0}/{capStr} | Out {outUsed:0.0}/{capStr} кВт";
                }
                if (inPortSlider)
                {
                    inPortSlider.minValue = 0f;
                    inPortSlider.maxValue = 1f;
                    inPortSlider.value = (float.IsPositiveInfinity(cap) || cap <= 1e-6f) ? 0f : Mathf.Clamp01(inUsed / cap);
                }
                if (outPortSlider)
                {
                    outPortSlider.minValue = 0f;
                    outPortSlider.maxValue = 1f;
                    outPortSlider.value = (float.IsPositiveInfinity(cap) || cap <= 1e-6f) ? 0f : Mathf.Clamp01(outUsed / cap);
                }

                // Рэмпы (если есть)
                if (rampText)
                {
                    if (_em.HasComponent<GeneratorRamp>(_target))
                    {
                        var r = _em.GetComponentData<GeneratorRamp>(_target);
                        rampText.text = $"Рэмп: ↑{r.UpKWps:0.0} ↓{r.DownKWps:0.0} кВт/с";
                        rampText.gameObject.SetActive(true);
                    }
                    else rampText.gameObject.SetActive(false);
                }

                // Подсказки
                if (hintsText)
                {
                    string hint = "";
                    if (!float.IsPositiveInfinity(cap) && outUsed >= cap - 1e-3f) hint = AddTag(hint, "PORT OUT");
                    if (g.CurrentKW >= g.RatedKW - 1e-3f) hint = AddTag(hint, "RATED");
                    hintsText.text = string.IsNullOrEmpty(hint) ? "" : $"Ограничение: {hint}";
                }

                toggleButton.GetComponentInChildren<TextMeshProUGUI>()?.SetText(g.IsOnline ? "Выключить" : "Включить");
            }
            else
            {
                nameText.text = genName;
                statusText.text = "Статус: неизвестно";
                powerText.text = "-";
                levelText.text = "-";
                if (portsText) portsText.text = "";
                if (rampText) rampText.text = "";
                if (hintsText) hintsText.text = "";
            }
        }

        // helpers 
        private static string FmtKW(float v) => $"{v:0.0} кВт";
        private static string AddTag(string src, string tag) => string.IsNullOrEmpty(src) ? tag : (src + " • " + tag);

        /// <summary> Поднимаемся по Parent, чтобы найти сущность-узел (носитель NetworkNode). </summary>
        private Entity ResolveNodeOwner(Entity e)
        {
            if (e == Entity.Null || !_em.Exists(e)) return Entity.Null;

            // если на самой сущности есть NetworkNode — отлично
            if (_em.HasComponent<NetworkNode>(e)) return e;

            // иначе поднимаемся по родителям пока не найдём NetworkNode
            int safety = 16;
            Entity cur = e;
            while (safety-- > 0 && _em.Exists(cur))
            {
                if (_em.HasComponent<NetworkNode>(cur)) return cur;
                if (!_em.HasComponent<Parent>(cur)) break;
                cur = _em.GetComponentData<Parent>(cur).Value;
            }
            return Entity.Null;
        }
    }
}
