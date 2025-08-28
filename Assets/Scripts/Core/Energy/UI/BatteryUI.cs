using UnityEngine;
using Unity.Entities;
using TMPro;
using UnityEngine.UI;
using Energy.Core;
using Unity.Mathematics;
using Unity.Transforms; // Parent
using Unity.Collections; // Temp arrays for queries

namespace Energy.UI
{
    public class BatteryUI : MonoBehaviour
    {
        public static BatteryUI Instance { get; private set; }

        [Header("Panel")]
        [SerializeField] private GameObject uiPanel;

        [Header("Text (basic)")]
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI socText;      // "SoC: 51% (1.0 кВт⋅ч)"
        [SerializeField] private TextMeshProUGUI powerText;    // "Поток: +1.0/-1.0"
        [SerializeField] private TextMeshProUGUI limitsText;   // "Charge≤X / Discharge≤Y"

        [Header("Ports (local flows)")]
        [SerializeField] private TextMeshProUGUI portsText;    // "In 1.0/4.0 | Out 0.0/4.0"
        [SerializeField] private Slider inPortSlider;          // 0..1
        [SerializeField] private Slider outPortSlider;         // 0..1
        [SerializeField] private Image inFillImage;            // цвет заливки In
        [SerializeField] private Image outFillImage;           // цвет заливки Out

        [Header("Transit (pass-through flow)")]
        [SerializeField] private TextMeshProUGUI transitText;  // "Транзит: 10.0 ↔ 10.0 кВт"

        [Header("Ramp & ETA (optional)")]
        [SerializeField] private TextMeshProUGUI rampText;     // "Ramp: charge 3.0 / discharge 6.0 кВт/с"
        [SerializeField] private TextMeshProUGUI etaText;      // "До 80%: 00:25" или "До 0%: 00:40"

        [Header("Deficit hint")]
        [SerializeField] private TextMeshProUGUI deficitText;  // "Недостаёт: 2,0 кВт" (если Load > Delivered)

        [Header("Targets")]
        [Range(0f, 1f)]
        [SerializeField] private float targetSoC = 0.80f;

        private EntityManager _em;
        private bool _init;
        private Entity _target;

        private static readonly Color32 ColGrey = new Color32(150, 150, 150, 255);
        private static readonly Color32 ColGreen = new Color32(25, 170, 25, 255);
        private static readonly Color32 ColBlue = new Color32(40, 120, 255, 255);

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else { Destroy(gameObject); return; }

#if UNITY_EDITOR
            if (!uiPanel || !nameText || !socText || !powerText || !limitsText)
            {
                Debug.LogError("[BatteryUI] Назначи обязательные ссылки в инспекторе!", this);
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
                var closeBtn = GetComponentInChildren<Button>();
                if (closeBtn != null) closeBtn.onClick.AddListener(OnClosePressed);
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
                                _em.GetComponentData<UIState>(gameStateEntity).ActiveUIType == UIType.Battery;

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

        public void OpenFor(Entity batteryEntity)
        {
            if (!_init) TryInit();
            _target = (!_em.Exists(batteryEntity)) ? Entity.Null : batteryEntity;
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

        private void Refresh(bool force = false)
        {
            if (!_em.Exists(_target)) { Hide(); return; }

            string battName = "<Battery>";
#if UNITY_EDITOR
            try { battName = _em.GetName(_target); } catch { }
#endif
            if (_em.HasComponent<BatteryComponent>(_target))
            {
                var b = _em.GetComponentData<BatteryComponent>(_target);
                if (b.Name.Length > 0) battName = b.Name.ToString();

                nameText.text = battName;
                socText.text = $"SoC: {(b.SoC * 100f):0}%  (Ёмкость: {b.CapacityKWh:0.0} кВт⋅ч)";
                powerText.text = $"Поток: {FmtSignedKW(b.CurrentKW)}  (разряд + / заряд -)";
                limitsText.text = $"Лимиты: Charge≤{b.MaxChargeKW:0.0}  |  Discharge≤{b.MaxDischargeKW:0.0} кВт";

                // читаем порты и транзит у сущности-узла 
                var nodeEnt = ResolveNodeOwner(_target);
                float inUsed = 0f, outUsed = 0f, cap = float.PositiveInfinity;
                float tIn = 0f, tOut = 0f;

                if (nodeEnt != Entity.Null)
                {
                    if (_em.HasComponent<NetLinkUsage>(nodeEnt))
                    {
                        var u = _em.GetComponentData<NetLinkUsage>(nodeEnt);
                        inUsed = u.InUsedKW;
                        outUsed = u.OutUsedKW;
                        tIn = u.TransitInKW;
                        tOut = u.TransitOutKW;
                    }
                    if (_em.HasComponent<NetLinkCapacity>(nodeEnt))
                        cap = _em.GetComponentData<NetLinkCapacity>(nodeEnt).MaxKW;
                }

                if (portsText)
                {
                    string capStr = float.IsPositiveInfinity(cap) ? "∞" : cap.ToString("0.0");
                    portsText.text = $"Порты: In {inUsed:0.0}/{capStr} | Out {outUsed:0.0}/{capStr} кВт";
                }
                if (transitText)
                {
                    transitText.text = $"Транзит: {tIn:0.0} ↔ {tOut:0.0} кВт";
                    transitText.gameObject.SetActive((tIn > 0f) || (tOut > 0f));
                }

                // Слайдеры и подсветка активного потока
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
                // цвет заливки: заряд -> зелёный In; разряд -> синий Out; покой -> серые
                if (inFillImage) inFillImage.color = ColGrey;
                if (outFillImage) outFillImage.color = ColGrey;
                if (b.CurrentKW < -1e-4f) // заряд
                {
                    if (inFillImage) inFillImage.color = ColGreen;
                }
                else if (b.CurrentKW > 1e-4f) // разряд
                {
                    if (outFillImage) outFillImage.color = ColBlue;
                }

                // Рэмпы
                if (rampText)
                {
                    if (_em.HasComponent<BatteryRamp>(_target))
                    {
                        var r = _em.GetComponentData<BatteryRamp>(_target);
                        rampText.text = $"Рэмп: charge {r.ChargeKWps:0.0} / discharge {r.DischargeKWps:0.0} кВт/с";
                        rampText.gameObject.SetActive(true);
                    }
                    else rampText.gameObject.SetActive(false);
                }

                // ETA
                if (etaText)
                    etaText.text = CalcEtaText(b, targetSoC);

                // Подсказка о дефиците по сети
                if (deficitText)
                {
                    float deficit = CalcNetworkDeficit(b.NetworkId);
                    if (deficit > 0.05f)
                    {
                        deficitText.text = $"Недостаёт: {deficit:0.0} кВт";
                        deficitText.gameObject.SetActive(true);
                    }
                    else
                    {
                        deficitText.gameObject.SetActive(false);
                    }
                }
            }
            else
            {
                nameText.text = battName;
                socText.text = "SoC: n/a";
                powerText.text = "-";
                limitsText.text = "-";
                if (portsText) portsText.text = "";
                if (transitText) { transitText.text = ""; transitText.gameObject.SetActive(false); }
                if (rampText) rampText.text = "";
                if (etaText) etaText.text = "";
                if (deficitText) { deficitText.text = ""; deficitText.gameObject.SetActive(false); }
            }
        }

        // helpers 
        private static string FmtSignedKW(float v) => v >= 0f ? $"+{v:0.0} кВт" : $"{v:0.0} кВт";

        private static string ToHMS(float seconds)
        {
            if (!float.IsFinite(seconds) || seconds <= 0f) return "—";
            int s = Mathf.CeilToInt(seconds);
            int h = s / 3600;
            int m = (s % 3600) / 60;
            int sec = s % 60;
            if (h > 0) return $"{h:00}:{m:00}:{sec:00}";
            return $"{m:00}:{sec:00}";
        }

        private static string CalcEtaText(BatteryComponent b, float targetSoC)
        {
            float soc = math.saturate(b.SoC);
            float cap = Mathf.Max(1e-6f, b.CapacityKWh);
            float p = b.CurrentKW; // + разряд, - заряд

            if (p < -1e-4f)
            {
                float need = Mathf.Max(0f, targetSoC - soc) * cap;
                float secs = (need <= 1e-6f) ? 0f : (need / (-p)) * 3600f;
                return $"До {targetSoC * 100f:0}%: {ToHMS(secs)}";
            }

            if (p > 1e-4f)
            {
                float need = soc * cap;
                float secs = (need <= 1e-6f) ? 0f : (need / p) * 3600f;
                return $"До 0%: {ToHMS(secs)}";
            }

            return "ETA: —";
        }

        /// <summary> Поднимаемся по Parent до носителя NetworkNode — там и лежат порты. </summary>
        private Entity ResolveNodeOwner(Entity e)
        {
            if (e == Entity.Null || !_em.Exists(e)) return Entity.Null;
            if (_em.HasComponent<NetworkNode>(e)) return e;

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

        /// <summary>
        /// Подсчитывает дефицит по сети: sum(load.CurrentKW) - sum(usage.InUsedKW на узлах нагрузок).
        /// </summary>
        private float CalcNetworkDeficit(int netId)
        {
            float demand = 0f;
            float delivered = 0f;

            var q = _em.CreateEntityQuery(ComponentType.ReadOnly<ConsumerLoad>());
            using (var loads = q.ToComponentDataArray<ConsumerLoad>(Allocator.Temp))
            using (var ents = q.ToEntityArray(Allocator.Temp))
            {
                for (int i = 0; i < loads.Length; i++)
                {
                    var l = loads[i];
                    if (l.NetworkId != netId) continue;

                    demand += math.max(0f, l.CurrentKW);

                    var owner = ResolveNodeOwner(ents[i]);
                    if (owner != Entity.Null && _em.HasComponent<NetLinkUsage>(owner))
                        delivered += _em.GetComponentData<NetLinkUsage>(owner).InUsedKW;
                }
            }

            float deficit = demand - delivered;
            return (deficit > 0f) ? deficit : 0f;
        }
    }
}
