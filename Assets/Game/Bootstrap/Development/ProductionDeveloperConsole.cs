using System;
using System.Collections.Generic;
using System.Globalization;
using MSC.Home;
using MSC.Interaction.Carrying;
using MSC.Needs;
using MSC.Player;
using MSC.UI.Presentation;
using MSC.Weather.Domain;
using MSC.Weather.Production;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MSC.Bootstrap.Development
{
    /// <summary>
    /// Development-only button menu for repeatable Phase 1 playtests. The
    /// historical class name is retained so existing Bootstrap wiring and any
    /// serialized references remain compatible.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ProductionDeveloperConsole : MonoBehaviour
    {
        private const float MenuWidth = 1040f;
        private const float MenuHeight = 640f;
        private const float NavigationWidth = 176f;
        private const float StatusHeight = 32f;
        private const float NeedButtonWidth = 44f;
        private const float NeedValueWidth = 54f;
        private const float WeatherTransitionSeconds = 2f;

        private static readonly TeleportDestination[] TeleportDestinations =
        {
            // All canonical anchors below are project-owned destinations whose
            // coordinates are backed by the recorded donor evidence IDs.
            new TeleportDestination(
                "home",
                "Дом",
                "Основные места",
                new Vector3(157.7796f, 1.5841784f, -1026.6515f),
                "eaf0362179091c79da9f6728b3c5fdc1"),
            new TeleportDestination(
                "teimo",
                "Магазин Теймо",
                "Основные места",
                new Vector3(-1374.197f, 5.761583f, 141.97205f),
                "52287202f0ac2d9c79937d5f26a4a0e4"),
            new TeleportDestination(
                "fleetari",
                "Мастерская Флитари",
                "Основные места",
                new Vector3(1724.5306f, 6.757f, -301.77405f),
                "f4a1a1c8ca621a1360978da9675c45f0"),
            new TeleportDestination(
                "inspection",
                "Техосмотр",
                "Основные места",
                new Vector3(-1356.0469f, 5.757086f, 224.02539f),
                "400f84613a0cc31a961b8f240d702b6c"),
            new TeleportDestination(
                "landfill",
                "Свалка",
                "Основные места",
                new Vector3(-609.63f, 14.49f, -1688.729f),
                "161f1947ab0236ca3cc8f38f5eafd13f"),
            new TeleportDestination(
                "cottage",
                "Дача",
                "Основные места",
                new Vector3(-676.1919f, -1.0159999f, -535.8192f),
                "cc1ebee642bc6f1c46285b30093c5fa88e"),
            new TeleportDestination(
                "teimo-bike-road",
                "Маршрут Теймо",
                "NPC и события",
                new Vector3(-1041.1455f, 2.2f, 273.5314f),
                "route.teimo.bicycle-to-store:18"),
            new TeleportDestination(
                "jani-road",
                "Шоссе Яни",
                "NPC и события",
                new Vector3(-1173.0842f, 14.5f, 635.5f),
                "anchor.traffic.highway.1511"),
            new TeleportDestination(
                "petteri-road",
                "Шоссе Петтери",
                "NPC и события",
                new Vector3(-1166.3274f, 14.5f, 635.4f),
                "anchor.traffic.highway.1510"),
            new TeleportDestination(
                "farmer",
                "Фермер",
                "NPC и события",
                new Vector3(-659.5f, 3.8f, 309f),
                "6cf023b6a9a89f4493f892d8d895f971"),
            new TeleportDestination(
                "berryman",
                "Сборщик ягод",
                "NPC и события",
                new Vector3(-1031.5f, 2.8f, -1672f),
                "a0226eebad3f8e78da5136a8ff36611b"),
            new TeleportDestination(
                "uncle",
                "Дядя Кессели",
                "NPC и события",
                new Vector3(200.2f, 1.2f, -1075.7f),
                "anchor.uncle-kesseli.home"),
            new TeleportDestination(
                "grandmother",
                "Бабушка",
                "NPC и события",
                new Vector3(633.1f, 5.2f, -2369.8f),
                "anchor.grandmother.home"),
            new TeleportDestination(
                "jokke",
                "Йокке",
                "NPC и события",
                new Vector3(2114.1f, 9.3f, -1266.1f),
                "anchor.jokke.kilju-camp"),
            new TeleportDestination(
                "suski",
                "Суски",
                "NPC и события",
                new Vector3(-1374.8f, 5.1f, 139.5f),
                "anchor.suski.store-hiker"),
            new TeleportDestination(
                "sewage-1",
                "Септик 1",
                "Работы и услуги",
                new Vector3(2044.8f, -1.1f, -1826.9f),
                "anchor.sewage-client-1.home"),
            new TeleportDestination(
                "sewage-2",
                "Септик 2",
                "Работы и услуги",
                new Vector3(-1129.2f, 3.7f, 81.8f),
                "anchor.sewage-client-2.home"),
            new TeleportDestination(
                "sewage-3",
                "Септик 3",
                "Работы и услуги",
                new Vector3(-1183.7f, 4.7f, 174.6f),
                "anchor.sewage-client-3.home"),
            new TeleportDestination(
                "sewage-4",
                "Септик 4",
                "Работы и услуги",
                new Vector3(1711.1f, 6.9f, -322.1f),
                "anchor.sewage-client-4.home"),
            new TeleportDestination(
                "sewage-5",
                "Септик 5",
                "Работы и услуги",
                new Vector3(1755.9f, 6.1f, -384.8f),
                "anchor.sewage-client-5.home"),
            new TeleportDestination(
                "firewood",
                "Дрова",
                "Работы и услуги",
                new Vector3(2089.8f, 6.4f, -1461.8f),
                "anchor.firewood-customer.home"),
            new TeleportDestination(
                "wastewater",
                "Очистные сооружения",
                "Работы и услуги",
                new Vector3(-1348.3f, 7.9f, 305.5f),
                "anchor.wastewater-attendant.facility"),
            new TeleportDestination(
                "ventti",
                "Дом для Ventti",
                "Работы и услуги",
                new Vector3(5.6f, -1.7f, -19.9f),
                "anchor.ventti-pigman.cabin"),
        };

        private static readonly NeedDefinition[] NeedDefinitions =
        {
            new NeedDefinition("thirst", "Жажда", NeedKind.Thirst),
            new NeedDefinition("hunger", "Голод", NeedKind.Hunger),
            new NeedDefinition("stress", "Стресс", NeedKind.Stress),
            new NeedDefinition("urine", "Мочевой пузырь", NeedKind.Urine),
            new NeedDefinition("fatigue", "Усталость", NeedKind.Fatigue),
            new NeedDefinition(
                "dirtiness",
                "Загрязнение",
                NeedKind.Dirtiness),
            new NeedDefinition(
                "intoxication",
                "Опьянение",
                NeedKind.Intoxication),
            new NeedDefinition("hangover", "Похмелье", NeedKind.Hangover),
        };

        private static readonly WeatherOption[] WeatherOptions =
        {
            new WeatherOption(WeatherStateIds.Clear.Value, "Ясно"),
            new WeatherOption(
                WeatherStateIds.PartlyCloudy.Value,
                "Переменная облачность"),
            new WeatherOption(
                WeatherStateIds.BrightOvercast.Value,
                "Светлая облачность"),
            new WeatherOption(WeatherStateIds.Overcast.Value, "Пасмурно"),
            new WeatherOption(
                WeatherStateIds.HeavyOvercast.Value,
                "Плотная облачность"),
            new WeatherOption(WeatherStateIds.Drizzle.Value, "Морось"),
            new WeatherOption(WeatherStateIds.LightRain.Value, "Лёгкий дождь"),
            new WeatherOption(
                WeatherStateIds.SteadyRain.Value,
                "Дождь"),
            new WeatherOption(
                WeatherStateIds.HeavyRain.Value,
                "Сильный дождь"),
            new WeatherOption(
                WeatherStateIds.Thunderstorm.Value,
                "Гроза"),
            new WeatherOption(
                WeatherStateIds.MorningMist.Value,
                "Утренний туман"),
            new WeatherOption(WeatherStateIds.DenseFog.Value, "Густой туман"),
            new WeatherOption(
                WeatherStateIds.PostRainWet.Value,
                "После дождя"),
            new WeatherOption(
                WeatherStateIds.ClearingAfterRain.Value,
                "Прояснение после дождя"),
            new WeatherOption(
                WeatherStateIds.ColdClearEvening.Value,
                "Холодный ясный вечер"),
            new WeatherOption(WeatherStateIds.BlueHour.Value, "Синий час"),
        };

        private static readonly double[] TimeScales =
        {
            0.25d,
            0.5d,
            1d,
            2d,
            5d,
            10d,
            25d,
            100d,
        };

        private readonly List<Texture2D> generatedTextures =
            new List<Texture2D>(12);

        private Transform playerTransform;
        private CharacterController characterController;
        private PhysicalCarryController carryController;
        private PlayerInputRouter input;
        private PlayerNeedsRuntime needs;
        private ProductionEnvironmentController environment;
        private Rect windowRect;
        private Vector2 contentScroll;
        private DeveloperSection selectedSection;
        private string statusMessage =
            "Выберите раздел и действие. F10 или ` закрывает меню.";
        private bool statusIsError;
        private bool initialized;
        private bool menuOpen;
        private bool restoreGameplayInput;
        private bool closeAfterTeleport;
        private int lastScreenWidth;
        private int lastScreenHeight;
        private CursorLockMode previousCursorLockMode;
        private bool previousCursorVisible;

        private GUIStyle windowStyle;
        private GUIStyle titleStyle;
        private GUIStyle subtitleStyle;
        private GUIStyle closeButtonStyle;
        private GUIStyle navigationStyle;
        private GUIStyle selectedNavigationStyle;
        private GUIStyle sectionTitleStyle;
        private GUIStyle cardStyle;
        private GUIStyle rowLabelStyle;
        private GUIStyle valueStyle;
        private GUIStyle buttonStyle;
        private GUIStyle primaryButtonStyle;
        private GUIStyle dangerButtonStyle;
        private GUIStyle statusStyle;
        private GUIStyle errorStatusStyle;
        private Font uiFont;
        private Texture2D dimTexture;
        private Texture2D progressTrackTexture;
        private Texture2D progressFillTexture;

        public bool IsOpen => menuOpen;

        public void Initialize(
            GameObject configuredPlayer,
            PlayerInputRouter configuredInput,
            PlayerNeedsRuntime configuredNeeds,
            ProductionEnvironmentController configuredEnvironment,
            FirstPersonLifeActionPresenter configuredLifeActionPresenter)
        {
            Initialize(
                configuredPlayer,
                configuredInput,
                configuredNeeds,
                configuredEnvironment,
                configuredLifeActionPresenter,
                configuredHome: null);
        }

        public void Initialize(
            GameObject configuredPlayer,
            PlayerInputRouter configuredInput,
            PlayerNeedsRuntime configuredNeeds,
            ProductionEnvironmentController configuredEnvironment,
            FirstPersonLifeActionPresenter configuredLifeActionPresenter,
            HomeSystemRuntime configuredHome)
        {
            if (initialized)
            {
                throw new InvalidOperationException(
                    "Developer menu is already initialized.");
            }

            if (configuredPlayer == null)
            {
                throw new ArgumentNullException(nameof(configuredPlayer));
            }

            input = configuredInput ??
                throw new ArgumentNullException(nameof(configuredInput));
            needs = configuredNeeds ??
                throw new ArgumentNullException(nameof(configuredNeeds));
            environment = configuredEnvironment ??
                throw new ArgumentNullException(nameof(configuredEnvironment));

            // Retain the accepted initializer signature while the replacement
            // menu deliberately scopes its visible categories to the four
            // requested developer domains.
            _ = configuredLifeActionPresenter ??
                throw new ArgumentNullException(
                    nameof(configuredLifeActionPresenter));
            _ = configuredHome;

            characterController =
                configuredPlayer.GetComponentInChildren<CharacterController>(
                    true);
            carryController = configuredPlayer.GetComponentInChildren<
                PhysicalCarryController>(true);
            playerTransform = characterController != null
                ? characterController.transform
                : configuredPlayer.transform;
            initialized = true;
        }

        public void Open()
        {
            SetMenuOpen(true);
        }

        public void Close()
        {
            SetMenuOpen(false);
        }

        public void Toggle()
        {
            SetMenuOpen(!menuOpen);
        }

        private void Awake()
        {
            if (!Application.isEditor && !Debug.isDebugBuild)
            {
                enabled = false;
            }
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.backquoteKey.wasPressedThisFrame ||
                keyboard.f10Key.wasPressedThisFrame)
            {
                Toggle();
                return;
            }

            if (menuOpen && keyboard.escapeKey.wasPressedThisFrame)
            {
                Close();
            }
        }

        private void OnDisable()
        {
            Close();
        }

        private void OnDestroy()
        {
            Close();
            for (int index = 0; index < generatedTextures.Count; index++)
            {
                Texture2D texture = generatedTextures[index];
                if (texture == null)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Destroy(texture);
                }
                else
                {
                    DestroyImmediate(texture);
                }
            }

            generatedTextures.Clear();
        }

        private void OnGUI()
        {
            if (!menuOpen)
            {
                return;
            }

            EnsureStyles();
            RecenterWindowIfRequired();
            int previousDepth = GUI.depth;
            try
            {
                // IMGUI windows and ordinary controls do not share intuitive
                // call-order layering. Give the fullscreen veil an explicitly
                // deeper layer so it can darken only the world, never the menu.
                GUI.depth = 1000;
                GUI.DrawTexture(
                    new Rect(0f, 0f, Screen.width, Screen.height),
                    dimTexture,
                    ScaleMode.StretchToFill);

                GUI.depth = -1000;
                windowRect = GUI.Window(
                    GetInstanceID(),
                    windowRect,
                    DrawMenuWindow,
                    GUIContent.none,
                    windowStyle);
            }
            finally
            {
                GUI.depth = previousDepth;
            }
        }

        private void DrawMenuWindow(int windowId)
        {
            GUILayout.BeginVertical();
            DrawHeader();
            GUILayout.Space(10f);

            GUILayout.BeginHorizontal();
            DrawNavigation();
            GUILayout.Space(14f);
            contentScroll = GUILayout.BeginScrollView(
                contentScroll,
                false,
                true,
                GUILayout.ExpandWidth(true),
                GUILayout.ExpandHeight(true));
            DrawSelectedSection();
            GUILayout.EndScrollView();
            GUILayout.EndHorizontal();

            GUILayout.Space(8f);
            GUILayout.Label(
                statusMessage,
                statusIsError ? errorStatusStyle : statusStyle,
                GUILayout.Height(StatusHeight));
            GUILayout.EndVertical();
        }

        private void DrawHeader()
        {
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical();
            GUILayout.Label("МЕНЮ РАЗРАБОТЧИКА", titleStyle);
            GUILayout.Label(
                "PROJECT-OWNED DEVELOPMENT TOOLS • F10 / `",
                subtitleStyle);
            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("ЗАКРЫТЬ  ×", closeButtonStyle))
            {
                Close();
            }

            GUILayout.EndHorizontal();
        }

        private void DrawNavigation()
        {
            GUILayout.BeginVertical(GUILayout.Width(NavigationWidth));
            DrawNavigationButton(DeveloperSection.Teleport, "ТЕЛЕПОРТ");
            DrawNavigationButton(DeveloperSection.Needs, "ПОТРЕБНОСТИ");
            DrawNavigationButton(DeveloperSection.Time, "ВРЕМЯ");
            DrawNavigationButton(DeveloperSection.Weather, "ПОГОДА");
            GUILayout.FlexibleSpace();
            GUILayout.Label(
                "Меню доступно только в Editor и Development Build.",
                subtitleStyle);
            GUILayout.EndVertical();
        }

        private void DrawNavigationButton(
            DeveloperSection section,
            string label)
        {
            GUIStyle style = selectedSection == section
                ? selectedNavigationStyle
                : navigationStyle;
            if (GUILayout.Button(label, style, GUILayout.Height(44f)))
            {
                selectedSection = section;
                contentScroll = Vector2.zero;
            }
        }

        private void DrawSelectedSection()
        {
            switch (selectedSection)
            {
                case DeveloperSection.Teleport:
                    DrawTeleportSection();
                    break;
                case DeveloperSection.Needs:
                    DrawNeedsSection();
                    break;
                case DeveloperSection.Time:
                    DrawTimeSection();
                    break;
                case DeveloperSection.Weather:
                    DrawWeatherSection();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void DrawTeleportSection()
        {
            GUILayout.Label("ТЕЛЕПОРТ", sectionTitleStyle);
            GUILayout.Label(
                "Выберите безопасную проектную точку назначения.",
                subtitleStyle);
            GUILayout.Space(6f);
            GUILayout.BeginVertical(cardStyle);
            GUILayout.Label(
                "Текущая позиция: " + FormatVector(playerTransform.position),
                valueStyle);
            closeAfterTeleport = GUILayout.Toggle(
                closeAfterTeleport,
                " Закрывать меню после телепорта");
            GUILayout.EndVertical();
            GUILayout.Space(8f);

            string currentGroup = string.Empty;
            bool rowOpen = false;
            int buttonsInRow = 0;
            for (int index = 0; index < TeleportDestinations.Length; index++)
            {
                TeleportDestination destination = TeleportDestinations[index];
                if (!string.Equals(
                        currentGroup,
                        destination.Group,
                        StringComparison.Ordinal))
                {
                    if (rowOpen)
                    {
                        GUILayout.EndHorizontal();
                        rowOpen = false;
                    }

                    currentGroup = destination.Group;
                    buttonsInRow = 0;
                    GUILayout.Space(5f);
                    GUILayout.Label(currentGroup, rowLabelStyle);
                }

                if (!rowOpen)
                {
                    GUILayout.BeginHorizontal();
                    rowOpen = true;
                }

                if (GUILayout.Button(
                        destination.DisplayName,
                        buttonStyle,
                        GUILayout.Height(35f)))
                {
                    Teleport(in destination);
                }

                buttonsInRow++;
                if (buttonsInRow % 3 == 0)
                {
                    GUILayout.EndHorizontal();
                    rowOpen = false;
                }
            }

            if (rowOpen)
            {
                while (buttonsInRow % 3 != 0)
                {
                    GUILayout.Label(
                        GUIContent.none,
                        GUILayout.ExpandWidth(true));
                    buttonsInRow++;
                }

                GUILayout.EndHorizontal();
            }
        }

        private void DrawNeedsSection()
        {
            GUILayout.Label("ПОТРЕБНОСТИ", sectionTitleStyle);
            GUILayout.Label(
                "Значения ограничены диапазоном 0–100. Ручная настройка " +
                "работает даже при отключённом росте потребностей.",
                subtitleStyle);
            GUILayout.Space(6f);

            PlayerNeedsSnapshot snapshot = needs.Snapshot;
            for (int index = 0; index < NeedDefinitions.Length; index++)
            {
                NeedDefinition definition = NeedDefinitions[index];
                DrawNeedRow(
                    in definition,
                    GetNeedValue(snapshot, definition.Kind));
            }

            GUILayout.Space(8f);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(
                    "СБРОСИТЬ ВСЁ",
                    dangerButtonStyle,
                    GUILayout.Height(38f)))
            {
                ResetAllNeeds();
            }

            bool progressionDisabled = IsNeedsProgressionDisabled();
            string toggleLabel = progressionDisabled
                ? "ВКЛЮЧИТЬ ПОТРЕБНОСТИ"
                : "ОТКЛЮЧИТЬ ПОТРЕБНОСТИ";
            if (GUILayout.Button(
                    toggleLabel,
                    progressionDisabled
                        ? primaryButtonStyle
                        : buttonStyle,
                    GUILayout.Height(38f)))
            {
                SetNeedsProgressionDisabled(!progressionDisabled);
            }

            GUILayout.EndHorizontal();

            GUILayout.Space(5f);
            GUILayout.Label(
                progressionDisabled
                    ? "Автоматический рост и игровые эффекты потребностей отключены."
                    : "Потребности работают в обычном режиме.",
                progressionDisabled ? statusStyle : subtitleStyle);
        }

        private void DrawNeedRow(
            in NeedDefinition definition,
            float currentValue)
        {
            GUILayout.BeginHorizontal(cardStyle, GUILayout.Height(44f));
            GUILayout.Label(
                definition.DisplayName,
                rowLabelStyle,
                GUILayout.Width(132f));

            Rect progressRect = GUILayoutUtility.GetRect(
                110f,
                12f,
                GUILayout.ExpandWidth(true));
            progressRect.y += 10f;
            GUI.DrawTexture(progressRect, progressTrackTexture);
            Rect fillRect = progressRect;
            fillRect.width *= Mathf.Clamp01(currentValue * 0.01f);
            GUI.DrawTexture(fillRect, progressFillTexture);

            GUILayout.Label(
                currentValue.ToString("0", CultureInfo.InvariantCulture) + "%",
                valueStyle,
                GUILayout.Width(NeedValueWidth));
            DrawNeedDeltaButton(in definition, currentValue, -10f, "−10");
            DrawNeedDeltaButton(in definition, currentValue, -1f, "−");
            DrawNeedDeltaButton(in definition, currentValue, 1f, "+");
            DrawNeedDeltaButton(in definition, currentValue, 10f, "+10");
            GUILayout.EndHorizontal();
        }

        private void DrawNeedDeltaButton(
            in NeedDefinition definition,
            float currentValue,
            float delta,
            string label)
        {
            if (GUILayout.Button(
                    label,
                    buttonStyle,
                    GUILayout.Width(NeedButtonWidth),
                    GUILayout.Height(28f)))
            {
                SetNeedValue(
                    in definition,
                    Mathf.Clamp(currentValue + delta, 0f, 100f));
            }
        }

        private void DrawTimeSection()
        {
            GUILayout.Label("ВРЕМЯ", sectionTitleStyle);
            GUILayout.Label(
                "Настройте игровую дату, часы и скорость хода времени.",
                subtitleStyle);
            GUILayout.Space(8f);

            MSC.Core.Time.GameTimeSnapshot snapshot =
                environment.GameTime.Snapshot;
            GUILayout.BeginVertical(cardStyle);
            GUILayout.Label(
                FormatGameDateTime(snapshot),
                titleStyle);
            GUILayout.Label(
                "Скорость: ×" +
                snapshot.TimeScale.ToString("0.##", CultureInfo.InvariantCulture) +
                (snapshot.IsPaused ? "  •  ПАУЗА" : "  •  ИДЁТ"),
                valueStyle);
            GUILayout.EndVertical();

            GUILayout.Space(10f);
            GUILayout.Label("СДВИГ ВРЕМЕНИ", rowLabelStyle);
            GUILayout.BeginHorizontal();
            DrawTimeShiftButton("−1 ДЕНЬ", -24d);
            DrawTimeShiftButton("−6 Ч", -6d);
            DrawTimeShiftButton("−1 Ч", -1d);
            DrawTimeShiftButton("−10 МИН", -1d / 6d);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            DrawTimeShiftButton("+10 МИН", 1d / 6d);
            DrawTimeShiftButton("+1 Ч", 1d);
            DrawTimeShiftButton("+6 Ч", 6d);
            DrawTimeShiftButton("+1 ДЕНЬ", 24d);
            GUILayout.EndHorizontal();

            GUILayout.Space(12f);
            GUILayout.Label("СКОРОСТЬ ВРЕМЕНИ", rowLabelStyle);
            GUILayout.BeginHorizontal(cardStyle, GUILayout.Height(46f));
            if (GUILayout.Button(
                    "−",
                    buttonStyle,
                    GUILayout.Width(64f),
                    GUILayout.Height(30f)))
            {
                ChangeTimeScale(-1);
            }

            GUILayout.FlexibleSpace();
            GUILayout.Label(
                "×" + snapshot.TimeScale.ToString(
                    "0.##",
                    CultureInfo.InvariantCulture),
                titleStyle,
                GUILayout.Width(150f));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(
                    "+",
                    buttonStyle,
                    GUILayout.Width(64f),
                    GUILayout.Height(30f)))
            {
                ChangeTimeScale(1);
            }

            GUILayout.EndHorizontal();

            GUILayout.Space(8f);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(
                    snapshot.IsPaused ? "ПРОДОЛЖИТЬ" : "ПАУЗА",
                    snapshot.IsPaused ? primaryButtonStyle : buttonStyle,
                    GUILayout.Height(38f)))
            {
                SetClockPaused(!snapshot.IsPaused);
            }

            if (GUILayout.Button(
                    "СКОРОСТЬ ×1",
                    buttonStyle,
                    GUILayout.Height(38f)))
            {
                SetTimeScale(1d);
            }

            GUILayout.EndHorizontal();
            GUILayout.Space(5f);
            GUILayout.Label(
                "Отрицательный сдвиг корректирует только проектные часы: " +
                "он не откатывает уже произошедшие события мира.",
                subtitleStyle);
        }

        private void DrawTimeShiftButton(string label, double hours)
        {
            if (GUILayout.Button(label, buttonStyle, GUILayout.Height(36f)))
            {
                ShiftTime(hours);
            }
        }

        private void DrawWeatherSection()
        {
            GUILayout.Label("ПОГОДА", sectionTitleStyle);
            GUILayout.Label(
                "Кнопка создаёт временный project-owned override; картинку " +
                "рисует выбранный в Bootstrap backend (Native HDRP или Enviro 3).",
                subtitleStyle);
            GUILayout.Space(8f);

            WeatherState current = environment.Weather.CurrentState;
            bool frozen = environment.Weather.IsScheduleFrozen;
            GUILayout.BeginVertical(cardStyle);
            GUILayout.Label(
                "Текущая: " + WeatherDisplayName(current.Id.Value),
                titleStyle);
            GUILayout.Label(
                "Осадки: " +
                Mathf.RoundToInt(current.PrecipitationIntensity01 * 100f) +
                "%  •  Ветер: " +
                current.WindSpeedMetersPerSecond.ToString(
                    "0.0",
                    CultureInfo.InvariantCulture) +
                " м/с",
                valueStyle);
            GUILayout.EndVertical();

            GUILayout.Space(10f);
            for (int index = 0; index < WeatherOptions.Length; index += 3)
            {
                GUILayout.BeginHorizontal();
                for (int column = 0; column < 3; column++)
                {
                    int optionIndex = index + column;
                    if (optionIndex >= WeatherOptions.Length)
                    {
                        GUILayout.Label(
                            GUIContent.none,
                            GUILayout.ExpandWidth(true));
                        continue;
                    }

                    WeatherOption option = WeatherOptions[optionIndex];
                    bool selected = string.Equals(
                        current.Id.Value,
                        option.StateId,
                        StringComparison.Ordinal);
                    if (GUILayout.Button(
                            option.DisplayName,
                            selected ? primaryButtonStyle : buttonStyle,
                            GUILayout.Height(40f)))
                    {
                        ApplyWeather(in option);
                    }
                }

                GUILayout.EndHorizontal();
            }

            GUILayout.Space(10f);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(
                    "АВТОМАТИЧЕСКАЯ ПОГОДА",
                    buttonStyle,
                    GUILayout.Height(40f)))
            {
                RestoreAutomaticWeather();
            }

            if (GUILayout.Button(
                    frozen ? "РАЗМОРОЗИТЬ РАСПИСАНИЕ" : "ЗАМОРОЗИТЬ РАСПИСАНИЕ",
                    frozen ? primaryButtonStyle : buttonStyle,
                    GUILayout.Height(40f)))
            {
                SetWeatherScheduleFrozen(!frozen);
            }

            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(
                    "МОЛНИЯ РЯДОМ С ИГРОКОМ",
                    buttonStyle,
                    GUILayout.Height(36f)))
            {
                TriggerLightning();
            }

            GUILayout.EndHorizontal();
        }

        private void Teleport(in TeleportDestination destination)
        {
            bool controllerWasEnabled =
                characterController != null && characterController.enabled;
            if (controllerWasEnabled)
            {
                characterController.enabled = false;
            }

            playerTransform.position = destination.Position;
            carryController?.SynchronizeAfterOwnerTeleport();
            Physics.SyncTransforms();

            if (controllerWasEnabled)
            {
                characterController.enabled = true;
            }

            SetStatus(
                "Телепорт: " + destination.DisplayName + " [" +
                destination.Id + "] • evidence " + destination.EvidenceId,
                isError: false);
            if (closeAfterTeleport)
            {
                Close();
            }
        }

        private void SetNeedValue(
            in NeedDefinition definition,
            float value)
        {
            if (!needs.TrySetNeed(definition.Id, value, out string failure))
            {
                SetStatus(failure, isError: true);
                return;
            }

            SetStatus(
                definition.DisplayName + ": " +
                value.ToString("0", CultureInfo.InvariantCulture) + "%",
                isError: false);
        }

        private void ResetAllNeeds()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!needs.DevTryResetAll(out string failure))
            {
                SetStatus(failure, isError: true);
                return;
            }

            SetStatus(
                "Все потребности и отложенные эффекты сброшены.",
                isError: false);
#else
            SetStatus(
                "Сброс потребностей недоступен в release build.",
                isError: true);
#endif
        }

        private bool IsNeedsProgressionDisabled()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            return needs.DevProgressionDisabled;
#else
            return false;
#endif
        }

        private void SetNeedsProgressionDisabled(bool disabled)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!needs.DevTrySetProgressionDisabled(
                    disabled,
                    out string failure))
            {
                SetStatus(failure, isError: true);
                return;
            }

            SetStatus(
                disabled
                    ? "Потребности отключены и заморожены."
                    : "Потребности включены.",
                isError: false);
#else
            SetStatus(
                "Управление потребностями недоступно в release build.",
                isError: true);
#endif
        }

        private void ShiftTime(double hours)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!double.IsFinite(hours) || Math.Abs(hours) < 0.000001d)
            {
                return;
            }

            if (hours > 0d)
            {
                if (!environment.DevTryAdvanceGameSeconds(
                        hours * 3600d,
                        out string advanceFailure))
                {
                    SetStatus(advanceFailure, isError: true);
                    return;
                }

                SetStatus(
                    "Время сдвинуто вперёд на " + FormatHours(hours) + ".",
                    isError: false);
                return;
            }

            MSC.Core.Time.GameTimeSnapshot snapshot =
                environment.GameTime.Snapshot;
            double requestedSeconds = snapshot.SecondsOfDay + hours * 3600d;
            long dayOffset = (long)Math.Floor(requestedSeconds / 86400d);
            double secondsOfDay = requestedSeconds - dayOffset * 86400d;
            string failure = string.Empty;
            if (!snapshot.Date.TryAddDays(dayOffset, out MSC.Core.Time.GameDate date) ||
                !environment.DevTrySetDateAndTime(
                    date,
                    secondsOfDay,
                    out failure))
            {
                SetStatus(
                    string.IsNullOrWhiteSpace(failure)
                        ? "Запрошенное время находится вне допустимого календаря."
                        : failure,
                    isError: true);
                return;
            }

            SetStatus(
                "Часы сдвинуты назад на " + FormatHours(-hours) +
                "; события мира не откатывались.",
                isError: false);
#else
            SetStatus(
                "Настройка времени недоступна в release build.",
                isError: true);
#endif
        }

        private void ChangeTimeScale(int direction)
        {
            double current = environment.GameTime.Snapshot.TimeScale;
            int targetIndex = direction < 0 ? 0 : TimeScales.Length - 1;
            if (direction < 0)
            {
                for (int index = TimeScales.Length - 1; index >= 0; index--)
                {
                    if (TimeScales[index] < current - 0.0001d)
                    {
                        targetIndex = index;
                        break;
                    }
                }
            }
            else
            {
                for (int index = 0; index < TimeScales.Length; index++)
                {
                    if (TimeScales[index] > current + 0.0001d)
                    {
                        targetIndex = index;
                        break;
                    }
                }
            }

            SetTimeScale(TimeScales[targetIndex]);
        }

        private void SetTimeScale(double timeScale)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!environment.DevTrySetTimeScale(
                    timeScale,
                    out string failure))
            {
                SetStatus(failure, isError: true);
                return;
            }

            SetStatus(
                "Скорость времени: ×" +
                timeScale.ToString("0.##", CultureInfo.InvariantCulture) + ".",
                isError: false);
#else
            SetStatus(
                "Настройка скорости недоступна в release build.",
                isError: true);
#endif
        }

        private void SetClockPaused(bool paused)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            environment.DevSetPaused(paused);
            SetStatus(
                paused ? "Игровые часы приостановлены." : "Игровые часы запущены.",
                isError: false);
#else
            SetStatus(
                "Пауза часов недоступна в release build.",
                isError: true);
#endif
        }

        private void ApplyWeather(in WeatherOption option)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!environment.DevTryApplyWeatherOverride(
                    option.StateId,
                    WeatherTransitionSeconds,
                    out string failure))
            {
                SetStatus(failure, isError: true);
                return;
            }

            SetStatus(
                "Погода: " + option.DisplayName + ".",
                isError: false);
#else
            SetStatus(
                "Настройка погоды недоступна в release build.",
                isError: true);
#endif
        }

        private void RestoreAutomaticWeather()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            environment.DevRemoveWeatherOverride();
            environment.DevSetScheduleFrozen(false);
            SetStatus(
                "Восстановлены автоматическое расписание и переходы погоды.",
                isError: false);
#else
            SetStatus(
                "Настройка погоды недоступна в release build.",
                isError: true);
#endif
        }

        private void SetWeatherScheduleFrozen(bool frozen)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            environment.DevSetScheduleFrozen(frozen);
            SetStatus(
                frozen
                    ? "Расписание погоды заморожено."
                    : "Расписание погоды снова работает.",
                isError: false);
#else
            SetStatus(
                "Настройка погоды недоступна в release build.",
                isError: true);
#endif
        }

        private void TriggerLightning()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!environment.DevTryTriggerAmbientLightningAtListener(
                    out string failure))
            {
                SetStatus(failure, isError: true);
                return;
            }

            SetStatus("Создана тестовая молния рядом с игроком.", false);
#else
            SetStatus(
                "Тестовая молния недоступна в release build.",
                isError: true);
#endif
        }

        private void SetMenuOpen(bool value)
        {
            if (menuOpen == value)
            {
                return;
            }

            if (value)
            {
                if (!initialized || !enabled)
                {
                    return;
                }

                restoreGameplayInput = input.IsGameplayInputEnabled;
                input.SetGameplayInputEnabled(false);
                previousCursorLockMode = Cursor.lockState;
                previousCursorVisible = Cursor.visible;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                RecenterWindow(force: true);
            }
            else
            {
                if (initialized && restoreGameplayInput && input != null)
                {
                    input.SetGameplayInputEnabled(true);
                }

                if (menuOpen)
                {
                    Cursor.lockState = previousCursorLockMode;
                    Cursor.visible = previousCursorVisible;
                }

                restoreGameplayInput = false;
            }

            menuOpen = value;
        }

        private void SetStatus(string message, bool isError)
        {
            statusMessage = string.IsNullOrWhiteSpace(message)
                ? "Готово."
                : message;
            statusIsError = isError;
        }

        private void RecenterWindowIfRequired()
        {
            if (lastScreenWidth != Screen.width ||
                lastScreenHeight != Screen.height)
            {
                RecenterWindow(force: false);
            }
        }

        private void RecenterWindow(bool force)
        {
            if (!force &&
                lastScreenWidth == Screen.width &&
                lastScreenHeight == Screen.height)
            {
                return;
            }

            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;
            float width = Mathf.Min(MenuWidth, Mathf.Max(640f, Screen.width - 32f));
            float height = Mathf.Min(MenuHeight, Mathf.Max(480f, Screen.height - 32f));
            windowRect = new Rect(
                Mathf.Max(16f, (Screen.width - width) * 0.5f),
                Mathf.Max(16f, (Screen.height - height) * 0.5f),
                width,
                height);
        }

        private void EnsureStyles()
        {
            if (windowStyle != null)
            {
                return;
            }

            uiFont = Resources.Load<Font>("Fonts/HelveticaNeueRoman");

            Texture2D panel = CreateTexture(UiThemeTokens.Panel);
            Texture2D card = CreateTexture(UiThemeTokens.Card);
            Texture2D row = CreateTexture(UiThemeTokens.Row);
            Texture2D rowHover = CreateTexture(
                Color.Lerp(UiThemeTokens.Row, UiThemeTokens.Accent, 0.18f));
            Texture2D accent = CreateTexture(UiThemeTokens.Accent);
            Texture2D accentPressed = CreateTexture(
                Color.Lerp(UiThemeTokens.Accent, Color.black, 0.28f));
            Texture2D danger = CreateTexture(
                Color.Lerp(UiThemeTokens.Destructive, Color.black, 0.18f));
            dimTexture = CreateTexture(new Color(0f, 0f, 0f, 0.58f));
            progressTrackTexture = CreateTexture(UiThemeTokens.SliderTrack);
            progressFillTexture = CreateTexture(UiThemeTokens.Accent);

            windowStyle = new GUIStyle(GUI.skin.window)
            {
                font = uiFont,
                normal = { background = panel },
                padding = new RectOffset(20, 20, 16, 14),
            };
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                font = uiFont,
                fontSize = 21,
                fontStyle = FontStyle.Bold,
                normal = { textColor = UiThemeTokens.TextPrimary },
            };
            subtitleStyle = new GUIStyle(GUI.skin.label)
            {
                font = uiFont,
                fontSize = 11,
                wordWrap = true,
                normal = { textColor = UiThemeTokens.TextMuted },
            };
            closeButtonStyle = CreateButtonStyle(
                row,
                rowHover,
                accentPressed,
                UiThemeTokens.TextPrimary,
                12);
            closeButtonStyle.fixedWidth = 108f;
            closeButtonStyle.fixedHeight = 32f;
            navigationStyle = CreateButtonStyle(
                card,
                rowHover,
                accentPressed,
                UiThemeTokens.TextPrimary,
                14);
            navigationStyle.alignment = TextAnchor.MiddleLeft;
            navigationStyle.padding = new RectOffset(14, 10, 0, 0);
            selectedNavigationStyle = CreateButtonStyle(
                accent,
                accent,
                accentPressed,
                UiThemeTokens.PrimaryControlForeground,
                14);
            selectedNavigationStyle.alignment = TextAnchor.MiddleLeft;
            selectedNavigationStyle.padding = new RectOffset(14, 10, 0, 0);
            sectionTitleStyle = new GUIStyle(titleStyle)
            {
                fontSize = 26,
            };
            cardStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = card },
                padding = new RectOffset(10, 10, 7, 7),
                margin = new RectOffset(0, 0, 2, 2),
            };
            rowLabelStyle = new GUIStyle(GUI.skin.label)
            {
                font = uiFont,
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = UiThemeTokens.TextPrimary },
            };
            valueStyle = new GUIStyle(GUI.skin.label)
            {
                font = uiFont,
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = UiThemeTokens.Accent },
            };
            buttonStyle = CreateButtonStyle(
                row,
                rowHover,
                accentPressed,
                UiThemeTokens.TextPrimary,
                12);
            primaryButtonStyle = CreateButtonStyle(
                accent,
                accent,
                accentPressed,
                UiThemeTokens.PrimaryControlForeground,
                12);
            dangerButtonStyle = CreateButtonStyle(
                danger,
                danger,
                accentPressed,
                UiThemeTokens.TextPrimary,
                12);
            statusStyle = new GUIStyle(GUI.skin.box)
            {
                font = uiFont,
                normal =
                {
                    background = card,
                    textColor = UiThemeTokens.Positive,
                },
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(10, 10, 0, 0),
                fontSize = 12,
            };
            errorStatusStyle = new GUIStyle(statusStyle);
            errorStatusStyle.normal.textColor = UiThemeTokens.Destructive;
        }

        private GUIStyle CreateButtonStyle(
            Texture2D normal,
            Texture2D hover,
            Texture2D active,
            Color textColor,
            int fontSize)
        {
            return new GUIStyle(GUI.skin.button)
            {
                font = uiFont,
                normal = { background = normal, textColor = textColor },
                hover = { background = hover, textColor = textColor },
                active = { background = active, textColor = textColor },
                focused = { background = hover, textColor = textColor },
                fontSize = fontSize,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                margin = new RectOffset(2, 2, 2, 2),
                padding = new RectOffset(8, 8, 4, 4),
            };
        }

        private Texture2D CreateTexture(Color color)
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                name = "MSC Developer Menu Runtime Style",
                hideFlags = HideFlags.HideAndDontSave,
            };
            texture.SetPixel(0, 0, color);
            texture.Apply(updateMipmaps: false, makeNoLongerReadable: true);
            generatedTextures.Add(texture);
            return texture;
        }

        private static float GetNeedValue(
            PlayerNeedsSnapshot snapshot,
            NeedKind kind)
        {
            return kind switch
            {
                NeedKind.Thirst => snapshot.Thirst,
                NeedKind.Hunger => snapshot.Hunger,
                NeedKind.Stress => snapshot.Stress,
                NeedKind.Urine => snapshot.Urine,
                NeedKind.Fatigue => snapshot.Fatigue,
                NeedKind.Dirtiness => snapshot.Dirtiness,
                NeedKind.Intoxication => snapshot.Intoxication,
                NeedKind.Hangover => snapshot.Hangover,
                _ => 0f,
            };
        }

        private static string FormatGameDateTime(
            MSC.Core.Time.GameTimeSnapshot snapshot)
        {
            int totalMinutes = Mathf.FloorToInt(
                (float)(snapshot.SecondsOfDay / 60d));
            int hours = totalMinutes / 60;
            int minutes = totalMinutes % 60;
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0:D2}:{1:D2}  •  {2}",
                hours,
                minutes,
                snapshot.Date);
        }

        private static string FormatHours(double hours)
        {
            if (hours < 1d)
            {
                return Math.Round(hours * 60d).ToString(
                           CultureInfo.InvariantCulture) + " мин";
            }

            return hours.ToString("0.##", CultureInfo.InvariantCulture) + " ч";
        }

        private static string FormatVector(Vector3 value) =>
            string.Format(
                CultureInfo.InvariantCulture,
                "({0:0.###}, {1:0.###}, {2:0.###})",
                value.x,
                value.y,
                value.z);

        private static string WeatherDisplayName(string stateId)
        {
            for (int index = 0; index < WeatherOptions.Length; index++)
            {
                if (string.Equals(
                        WeatherOptions[index].StateId,
                        stateId,
                        StringComparison.Ordinal))
                {
                    return WeatherOptions[index].DisplayName;
                }
            }

            return stateId;
        }

        private enum DeveloperSection
        {
            Teleport = 0,
            Needs = 1,
            Time = 2,
            Weather = 3,
        }

        private enum NeedKind
        {
            Thirst = 0,
            Hunger = 1,
            Stress = 2,
            Urine = 3,
            Fatigue = 4,
            Dirtiness = 5,
            Intoxication = 6,
            Hangover = 7,
        }

        private readonly struct NeedDefinition
        {
            public NeedDefinition(string id, string displayName, NeedKind kind)
            {
                Id = id;
                DisplayName = displayName;
                Kind = kind;
            }

            public string Id { get; }
            public string DisplayName { get; }
            public NeedKind Kind { get; }
        }

        private readonly struct WeatherOption
        {
            public WeatherOption(string stateId, string displayName)
            {
                StateId = stateId;
                DisplayName = displayName;
            }

            public string StateId { get; }
            public string DisplayName { get; }
        }

        private readonly struct TeleportDestination
        {
            public TeleportDestination(
                string id,
                string displayName,
                string group,
                Vector3 position,
                string evidenceId)
            {
                Id = id;
                DisplayName = displayName;
                Group = group;
                Position = position;
                EvidenceId = evidenceId;
            }

            public string Id { get; }
            public string DisplayName { get; }
            public string Group { get; }
            public Vector3 Position { get; }
            public string EvidenceId { get; }
        }
    }
}
