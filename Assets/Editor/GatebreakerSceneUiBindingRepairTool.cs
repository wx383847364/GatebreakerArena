using System;
using System.Collections.Generic;
using App.AOT.Bootstrap;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Gatebreaker.Editor
{
    public static class GatebreakerSceneUiBindingRepairTool
    {
        private const string BootstrapScenePath = "Assets/Scenes/BootstrapScene.scene";

        [MenuItem("Gatebreaker/UI/Repair Scene UI Binding")]
        public static void RepairBootstrapSceneMenu()
        {
            RepairBootstrapScene();
            List<string> errors = ValidateBootstrapScene();
            if (errors.Count == 0)
            {
                EditorUtility.DisplayDialog("Gatebreaker UI Binding", "BootstrapScene UI binding repaired. Missing required bindings: 0.", "OK");
                return;
            }

            EditorUtility.DisplayDialog("Gatebreaker UI Binding", string.Join("\n", errors), "OK");
        }

        [MenuItem("Gatebreaker/UI/Validate Scene UI Binding")]
        public static void ValidateBootstrapSceneMenu()
        {
            List<string> errors = ValidateBootstrapScene();
            EditorUtility.DisplayDialog(
                "Gatebreaker UI Binding",
                errors.Count == 0 ? "BootstrapScene UI binding is valid. Missing required bindings: 0." : string.Join("\n", errors),
                "OK");
        }

        public static void RepairBootstrapSceneForBatch()
        {
            RepairBootstrapScene();
            ValidateBootstrapSceneForBatch();
        }

        public static void ValidateBootstrapSceneForBatch()
        {
            List<string> errors = ValidateBootstrapScene();
            if (errors.Count > 0)
            {
                throw new InvalidOperationException("BootstrapScene UI binding validation failed:\n" + string.Join("\n", errors));
            }

            Debug.Log("Gatebreaker UI binding validation passed.");
        }

        public static List<string> ValidateBootstrapScene()
        {
            Scene scene = OpenBootstrapScene();
            GatebreakerArenaSceneUiBinding binding = FindSceneBinding(scene);
            var errors = new List<string>();
            if (binding == null)
            {
                errors.Add("Missing GatebreakerArenaSceneUiBinding in BootstrapScene.");
                return errors;
            }

            var serializedBinding = new SerializedObject(binding);
            Require<Button>(serializedBinding, "_skillButton", errors);
            Require<TMP_Text>(serializedBinding, "_ballCountText", errors);
            Require<RectTransform>(serializedBinding, "_movementPad", errors);
            Require<RectTransform>(serializedBinding, "_movementHandle", errors);
            Require<RectTransform>(serializedBinding, "_movementLeftArrowInput", errors);
            Require<RectTransform>(serializedBinding, "_movementRightArrowInput", errors);
            Require<Graphic>(serializedBinding, "_movementLeftArrowHighlight", errors);
            Require<Graphic>(serializedBinding, "_movementRightArrowHighlight", errors);
            Require<TMP_Text>(serializedBinding, "_timeText", errors);
            RequireArray<TMP_Text>(serializedBinding, "_playerScoreTexts", 3, errors);
            RequireArray<TMP_Text>(serializedBinding, "_playerHitTexts", 3, errors);
            Require<GameObject>(serializedBinding, "_resultRoot", errors);
            Require<TMP_Text>(serializedBinding, "_resultTitleText", errors);
            RequireArray<TMP_Text>(serializedBinding, "_resultRankLabelTexts", 3, errors);
            RequireArray<TMP_Text>(serializedBinding, "_resultRankNameTexts", 3, errors);
            Require<Button>(serializedBinding, "_resultRestartButton", errors);
            Require<Button>(serializedBinding, "_resultBackButton", errors);
            Require<GameObject>(serializedBinding, "_gmRoot", errors);
            Require<Slider>(serializedBinding, "_gmHitOffsetSlider", errors);
            Require<TMP_Text>(serializedBinding, "_gmHitOffsetValueText", errors);
            Require<Slider>(serializedBinding, "_gmPaddleVelocitySlider", errors);
            Require<TMP_Text>(serializedBinding, "_gmPaddleVelocityValueText", errors);
            Require<Slider>(serializedBinding, "_gmMinimumOutwardSlider", errors);
            Require<TMP_Text>(serializedBinding, "_gmMinimumOutwardValueText", errors);
            Require<GameObject>(serializedBinding, "_lanRoot", errors);
            Require<GameObject>(serializedBinding, "_modeSelectRoot", errors);
            Require<Button>(serializedBinding, "_localBattleButton", errors);
            Require<Button>(serializedBinding, "_onlineBattleButton", errors);
            Require<GameObject>(serializedBinding, "_lanMenuRoot", errors);
            Require<GameObject>(serializedBinding, "_lanRoomInfoRoot", errors);
            Require<GameObject>(serializedBinding, "_lanStatusRoot", errors);
            Require<Button>(serializedBinding, "_lanBackButton", errors);
            Require<Button>(serializedBinding, "_lanCreateButton", errors);
            Require<Button>(serializedBinding, "_lanDiscoverButton", errors);
            Require<Button>(serializedBinding, "_lanJoinButton", errors);
            Require<Button>(serializedBinding, "_lanReadyButton", errors);
            Require<Button>(serializedBinding, "_lanStartButton", errors);
            Require<Button>(serializedBinding, "_lanLeaveButton", errors);
            Require<Button>(serializedBinding, "_lanAcknowledgeStartButton", errors);
            Require<TMP_InputField>(serializedBinding, "_lanPlayerNameInput", errors);
            Require<TMP_InputField>(serializedBinding, "_lanRoomCodeInput", errors);
            Require<TMP_Text>(serializedBinding, "_lanStateText", errors);
            Require<TMP_Text>(serializedBinding, "_lanRoomCodeText", errors);
            Require<TMP_Text>(serializedBinding, "_lanPlayerCountText", errors);
            Require<TMP_Text>(serializedBinding, "_lanLocalIpText", errors);
            Require<TMP_Text>(serializedBinding, "_lanRoomIpText", errors);
            Require<TMP_Text>(serializedBinding, "_lanErrorText", errors);
            RequireArray<TMP_Text>(serializedBinding, "_lanRoomPlayerInfoTexts", 3, errors);
            RequireArray<TMP_Text>(serializedBinding, "_lanRoomPlayerNameTexts", 3, errors);
            RequireArray<TMP_Text>(serializedBinding, "_lanRoomPlayerReadyTexts", 3, errors);
            Require<GameObject>(serializedBinding, "_startCountdownRoot", errors);
            Require<TMP_Text>(serializedBinding, "_startCountdownText", errors);

            return errors;
        }

        private static void RepairBootstrapScene()
        {
            Scene scene = OpenBootstrapScene();
            GatebreakerArenaSceneUiBinding binding = FindSceneBinding(scene);
            if (binding == null)
            {
                throw new InvalidOperationException("Missing GatebreakerArenaSceneUiBinding in BootstrapScene.");
            }

            Transform canvas = FindRequired(scene, "UI Camera/Canvas");
            Transform topPanel = FindRequired(canvas, "TopPanel");
            Transform downPanel = FindRequired(canvas, "DownPanel");
            Transform lanRoot = EnsureRectChild(canvas, "LanRoot", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Vector2.one);
            Transform lanPanel = FindFirstRequired("LanPanel", canvas, lanRoot);
            Transform roomInfoPanel = FindFirstRequired("RoomInfoPanel", canvas, lanRoot);
            ReparentPreserve(lanPanel, lanRoot);
            ReparentPreserve(roomInfoPanel, lanRoot);

            Transform gmPanel = EnsureGmPanel(canvas);
            Transform modeSelectPanel = EnsureModeSelectPanel(lanRoot);
            Transform lanBackButton = EnsureLanBackButton(lanRoot);
            Transform startCountdownPanel = EnsureStartCountdownPanel(canvas);
            Transform discoverButton = EnsureClonedButton(
                FindRequired(lanRoot, "LanPanel/JoinRoom/JoinBtn"),
                FindRequired(lanRoot, "LanPanel/JoinRoom"),
                "DiscoverBtn",
                "DISCOVER",
                new Vector2(0f, 30f));
            Transform acknowledgeStartButton = EnsureClonedButton(
                FindRequired(lanRoot, "RoomInfoPanel/StartBtn"),
                FindRequired(lanRoot, "RoomInfoPanel"),
                "AcknowledgeStartBtn",
                "ACK START",
                new Vector2(0f, -42f));
            acknowledgeStartButton.gameObject.SetActive(false);

            Transform lanStatusPanel = EnsureLanStatusPanel(lanRoot);
            HideOptionalRow(lanRoot, "LanPanel/CreateRoom/Password");
            HideOptionalRow(lanRoot, "LanPanel/JoinRoom/Password");
            SetTextIfExists(lanRoot, "LanPanel/CreateRoom/RoomName", "Player Name:");
            SetTextIfExists(lanRoot, "LanPanel/JoinRoom/RoomName", "Room Code:");
            FixInputField(FindRequired<TMP_InputField>(lanRoot, "LanPanel/CreateRoom/RoomName/InputField (TMP)"));
            FixInputField(FindRequired<TMP_InputField>(lanRoot, "LanPanel/JoinRoom/RoomName/InputField (TMP)"));

            var serializedBinding = new SerializedObject(binding);
            Set(serializedBinding, "_skillButton", FindRequired<Button>(downPanel, "Skill_btn"));
            Set(serializedBinding, "_ballCountText", FindRequired<TMP_Text>(downPanel, "Skill_btn/BallCount"));
            Set(serializedBinding, "_movementPad", FindRequired<RectTransform>(downPanel, "joystick_bg"));
            Set(serializedBinding, "_movementHandle", FindRequired<RectTransform>(downPanel, "joystick_bg/joystick"));
            Set(serializedBinding, "_movementLeftArrowInput", FindRequired<RectTransform>(downPanel, "joystick_bg/MovementLeftArrowInput"));
            Set(serializedBinding, "_movementRightArrowInput", FindRequired<RectTransform>(downPanel, "joystick_bg/MovementRightArrowInput"));
            Set(serializedBinding, "_movementLeftArrowHighlight", FindRequired<Graphic>(downPanel, "joystick_bg/MovementLeftArrowInput"));
            Set(serializedBinding, "_movementRightArrowHighlight", FindRequired<Graphic>(downPanel, "joystick_bg/MovementRightArrowInput"));
            Set(serializedBinding, "_hudRoot", null);
            Set(serializedBinding, "_hudTitleText", null);
            Set(serializedBinding, "_hudStatusText", null);
            Set(serializedBinding, "_hudScoreText", null);
            Set(serializedBinding, "_hudServeText", null);
            Set(serializedBinding, "_hudBallText", null);
            Set(serializedBinding, "_timeText", FindRequired<TMP_Text>(topPanel, "TimeImage/Time"));
            SetArray(serializedBinding, "_playerScoreTexts",
                FindRequired<TMP_Text>(downPanel, "Player1Info/ScoreInfo/Score"),
                FindRequired<TMP_Text>(topPanel, "Player2Info/ScoreInfo/Score"),
                FindRequired<TMP_Text>(topPanel, "Player3Info/ScoreInfo/Score"));
            SetArray(serializedBinding, "_playerHitTexts",
                FindRequired<TMP_Text>(downPanel, "Player1Info/HitInfo/Hit"),
                FindRequired<TMP_Text>(topPanel, "Player2Info/HitInfo/Hit"),
                FindRequired<TMP_Text>(topPanel, "Player3Info/HitInfo/Hit"));

            Transform resultPanel = FindRequired(canvas, "ResultPanel");
            Set(serializedBinding, "_resultRoot", resultPanel.gameObject);
            Set(serializedBinding, "_resultTitleText", FindRequired<TMP_Text>(resultPanel, "Result/title"));
            Set(serializedBinding, "_resultBodyText", null);
            Set(serializedBinding, "_resultScoreText", null);
            SetArray(serializedBinding, "_resultRankLabelTexts",
                FindRequired<TMP_Text>(resultPanel, "Result/Rankinfo_1"),
                FindRequired<TMP_Text>(resultPanel, "Result/Rankinfo_2"),
                FindRequired<TMP_Text>(resultPanel, "Result/Rankinfo_3"));
            SetArray(serializedBinding, "_resultRankNameTexts",
                FindRequired<TMP_Text>(resultPanel, "Result/Rankinfo_1/Name"),
                FindRequired<TMP_Text>(resultPanel, "Result/Rankinfo_2/Name"),
                FindRequired<TMP_Text>(resultPanel, "Result/Rankinfo_3/Name"));
            Set(serializedBinding, "_resultRestartButton", FindRequired<Button>(resultPanel, "RestartBtn"));
            Set(serializedBinding, "_resultBackButton", FindRequired<Button>(resultPanel, "BackBtn"));

            Set(serializedBinding, "_gmRoot", gmPanel.gameObject);
            Set(serializedBinding, "_gmHitOffsetSlider", FindRequired<Slider>(gmPanel, "HitOffsetRow/HitOffsetSlider"));
            Set(serializedBinding, "_gmHitOffsetValueText", FindRequired<TMP_Text>(gmPanel, "HitOffsetRow/HitOffsetValue"));
            Set(serializedBinding, "_gmPaddleVelocitySlider", FindRequired<Slider>(gmPanel, "PaddleVelocityRow/PaddleVelocitySlider"));
            Set(serializedBinding, "_gmPaddleVelocityValueText", FindRequired<TMP_Text>(gmPanel, "PaddleVelocityRow/PaddleVelocityValue"));
            Set(serializedBinding, "_gmMinimumOutwardSlider", FindRequired<Slider>(gmPanel, "MinimumOutwardRow/MinimumOutwardSlider"));
            Set(serializedBinding, "_gmMinimumOutwardValueText", FindRequired<TMP_Text>(gmPanel, "MinimumOutwardRow/MinimumOutwardValue"));

            Set(serializedBinding, "_lanRoot", lanRoot.gameObject);
            Set(serializedBinding, "_modeSelectRoot", modeSelectPanel.gameObject);
            Set(serializedBinding, "_localBattleButton", FindRequired<Button>(modeSelectPanel, "LocalBattleButton"));
            Set(serializedBinding, "_onlineBattleButton", FindRequired<Button>(modeSelectPanel, "OnlineBattleButton"));
            Set(serializedBinding, "_lanMenuRoot", lanPanel.gameObject);
            Set(serializedBinding, "_lanRoomInfoRoot", roomInfoPanel.gameObject);
            Set(serializedBinding, "_lanStatusRoot", lanStatusPanel.gameObject);
            Set(serializedBinding, "_lanBackButton", lanBackButton.GetComponent<Button>());
            Set(serializedBinding, "_lanCreateButton", FindRequired<Button>(lanRoot, "LanPanel/CreateRoom/CreateBtn"));
            Set(serializedBinding, "_lanDiscoverButton", discoverButton.GetComponent<Button>());
            Set(serializedBinding, "_lanJoinButton", FindRequired<Button>(lanRoot, "LanPanel/JoinRoom/JoinBtn"));
            Set(serializedBinding, "_lanReadyButton", FindRequired<Button>(lanRoot, "RoomInfoPanel/READYBtn"));
            Set(serializedBinding, "_lanStartButton", FindRequired<Button>(lanRoot, "RoomInfoPanel/StartBtn"));
            Set(serializedBinding, "_lanLeaveButton", FindRequired<Button>(lanRoot, "RoomInfoPanel/BackBtn"));
            Set(serializedBinding, "_lanAcknowledgeStartButton", acknowledgeStartButton.GetComponent<Button>());
            Set(serializedBinding, "_lanPlayerNameInput", FindRequired<TMP_InputField>(lanRoot, "LanPanel/CreateRoom/RoomName/InputField (TMP)"));
            Set(serializedBinding, "_lanRoomCodeInput", FindRequired<TMP_InputField>(lanRoot, "LanPanel/JoinRoom/RoomName/InputField (TMP)"));
            Set(serializedBinding, "_lanStateText", FindRequired<TMP_Text>(lanStatusPanel, "LanStateText"));
            Set(serializedBinding, "_lanRoomCodeText", FindRequired<TMP_Text>(lanStatusPanel, "LanRoomCodeText"));
            Set(serializedBinding, "_lanPlayerCountText", FindRequired<TMP_Text>(lanStatusPanel, "LanPlayerCountText"));
            Set(serializedBinding, "_lanLocalIpText", FindRequired<TMP_Text>(lanStatusPanel, "LanLocalIpText"));
            Set(serializedBinding, "_lanRoomIpText", FindRequired<TMP_Text>(lanStatusPanel, "LanRoomIpText"));
            Set(serializedBinding, "_lanErrorText", FindRequired<TMP_Text>(lanStatusPanel, "LanErrorText"));
            SetArray(serializedBinding, "_lanRoomPlayerInfoTexts",
                FindRequired<TMP_Text>(lanRoot, "RoomInfoPanel/Playerinfo_1"),
                FindRequired<TMP_Text>(lanRoot, "RoomInfoPanel/Playerinfo_2"),
                FindRequired<TMP_Text>(lanRoot, "RoomInfoPanel/Playerinfo_3"));
            SetArray(serializedBinding, "_lanRoomPlayerNameTexts",
                FindRequired<TMP_Text>(lanRoot, "RoomInfoPanel/Playerinfo_1/Name"),
                FindRequired<TMP_Text>(lanRoot, "RoomInfoPanel/Playerinfo_2/Name"),
                FindRequired<TMP_Text>(lanRoot, "RoomInfoPanel/Playerinfo_3/Name"));
            SetArray(serializedBinding, "_lanRoomPlayerReadyTexts",
                FindRequired<TMP_Text>(lanRoot, "RoomInfoPanel/Playerinfo_1/Status"),
                FindRequired<TMP_Text>(lanRoot, "RoomInfoPanel/Playerinfo_2/Status"),
                FindRequired<TMP_Text>(lanRoot, "RoomInfoPanel/Playerinfo_3/Status"));
            Set(serializedBinding, "_startCountdownRoot", startCountdownPanel.gameObject);
            Set(serializedBinding, "_startCountdownText", FindRequired<TMP_Text>(startCountdownPanel, "StartCountdownText"));
            serializedBinding.ApplyModifiedPropertiesWithoutUndo();

            modeSelectPanel.gameObject.SetActive(true);
            lanBackButton.gameObject.SetActive(false);
            lanPanel.gameObject.SetActive(false);
            roomInfoPanel.gameObject.SetActive(false);
            lanStatusPanel.gameObject.SetActive(false);
            startCountdownPanel.gameObject.SetActive(false);

            EditorUtility.SetDirty(binding);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Gatebreaker BootstrapScene UI binding repaired.");
        }

        private static Transform EnsureGmPanel(Transform canvas)
        {
            Transform panel = EnsureRectChild(canvas, "GmPanel", Vector2.one, Vector2.one, new Vector2(1f, 1f), new Vector2(300f, 166f), new Vector2(-18f, -112f));
            var image = EnsureComponent<Image>(panel.gameObject);
            image.color = new Color(0f, 0f, 0f, 0.78f);
            EnsureText(panel, "Title", "GM TUNING", 18, new Vector2(0f, 54f), new Vector2(260f, 24f), TextAlignmentOptions.Center);
            EnsureSliderRow(panel, "HitOffset", "HitOffsetSlider", "HitOffsetValue", new Vector2(0f, 22f));
            EnsureSliderRow(panel, "PaddleVelocity", "PaddleVelocitySlider", "PaddleVelocityValue", new Vector2(0f, -22f));
            EnsureSliderRow(panel, "MinimumOutward", "MinimumOutwardSlider", "MinimumOutwardValue", new Vector2(0f, -66f));
            panel.gameObject.SetActive(false);
            return panel;
        }

        private static void EnsureSliderRow(Transform parent, string label, string sliderName, string valueName, Vector2 anchoredPosition)
        {
            Transform row = EnsureRectChild(parent, label + "Row", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(270f, 34f), anchoredPosition);
            EnsureText(row, label + "Label", label, 12, new Vector2(-86f, 0f), new Vector2(92f, 22f), TextAlignmentOptions.Left);
            EnsureSlider(row, sliderName, new Vector2(36f, 0f), new Vector2(106f, 18f));
            EnsureText(row, valueName, "-", 10, new Vector2(84f, -14f), new Vector2(178f, 16f), TextAlignmentOptions.Right);
        }

        private static Transform EnsureLanStatusPanel(Transform lanRoot)
        {
            Transform panel = EnsureRectChild(lanRoot, "LanStatusPanel", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(330f, 112f), new Vector2(0f, 140f));
            var image = EnsureComponent<Image>(panel.gameObject);
            image.color = new Color(0f, 0f, 0f, 0.65f);
            EnsureText(panel, "LanStateText", "状态：-", 13, new Vector2(0f, 42f), new Vector2(300f, 18f), TextAlignmentOptions.Left);
            EnsureText(panel, "LanRoomCodeText", "房间号：-", 13, new Vector2(0f, 22f), new Vector2(300f, 18f), TextAlignmentOptions.Left);
            EnsureText(panel, "LanPlayerCountText", "人数：-", 13, new Vector2(0f, 2f), new Vector2(300f, 18f), TextAlignmentOptions.Left);
            EnsureText(panel, "LanLocalIpText", "本机 IP：-", 12, new Vector2(0f, -18f), new Vector2(300f, 16f), TextAlignmentOptions.Left);
            EnsureText(panel, "LanRoomIpText", "房间 IP：-", 12, new Vector2(0f, -36f), new Vector2(300f, 16f), TextAlignmentOptions.Left);
            EnsureText(panel, "LanErrorText", string.Empty, 12, new Vector2(0f, -54f), new Vector2(300f, 16f), TextAlignmentOptions.Left);
            return panel;
        }

        private static Transform EnsureModeSelectPanel(Transform lanRoot)
        {
            Transform panel = EnsureRectChild(lanRoot, "ModeSelectPanel", new Vector2(0.5f, 0.55f), new Vector2(0.5f, 0.55f), new Vector2(0.5f, 0.5f), new Vector2(420f, 240f), Vector2.zero);
            var image = EnsureComponent<Image>(panel.gameObject);
            image.color = new Color(0f, 0f, 0f, 0.76f);
            EnsureText(panel, "Title", "BATTLE MODE", 28, new Vector2(0f, 80f), new Vector2(380f, 40f), TextAlignmentOptions.Center);
            EnsureButton(panel, "LocalBattleButton", "人机对战", new Vector2(0f, 20f), new Vector2(260f, 48f), new Color(0.92f, 0.08f, 0.08f, 1f));
            EnsureButton(panel, "OnlineBattleButton", "联机对战", new Vector2(0f, -48f), new Vector2(260f, 48f), new Color(0.06f, 0.62f, 0.08f, 1f));
            return panel;
        }

        private static Transform EnsureStartCountdownPanel(Transform canvas)
        {
            Transform panel = EnsureRectChild(canvas, "StartCountdownPanel", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            CanvasGroup canvasGroup = EnsureComponent<CanvasGroup>(panel.gameObject);
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
            TMP_Text text = EnsureText(panel, "StartCountdownText", "5", 76, Vector2.zero, new Vector2(520f, 150f), TextAlignmentOptions.Center);
            text.fontStyle = FontStyles.Bold;
            text.enableAutoSizing = true;
            text.fontSizeMax = 96f;
            text.fontSizeMin = 42f;
            text.outlineWidth = 0.24f;
            text.outlineColor = Color.black;
            return panel;
        }

        private static Transform EnsureLanBackButton(Transform lanRoot)
        {
            Transform buttonTransform = EnsureRectChild(
                lanRoot,
                "LanBackButton",
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(112f, 40f),
                new Vector2(18f, -18f));
            var image = EnsureComponent<Image>(buttonTransform.gameObject);
            image.color = new Color(0.08f, 0.08f, 0.08f, 0.92f);
            Button button = EnsureComponent<Button>(buttonTransform.gameObject);
            button.targetGraphic = image;
            button.onClick.RemoveAllListeners();
            TMP_Text text = EnsureText(buttonTransform, "Text (TMP)", "返回", 20, Vector2.zero, new Vector2(112f, 40f), TextAlignmentOptions.Center);
            text.fontStyle = FontStyles.Bold;
            return buttonTransform;
        }

        private static Transform EnsureButton(Transform parent, string name, string label, Vector2 anchoredPosition, Vector2 sizeDelta, Color color)
        {
            Transform buttonTransform = EnsureRectChild(parent, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), sizeDelta, anchoredPosition);
            var image = EnsureComponent<Image>(buttonTransform.gameObject);
            image.color = color;
            Button button = EnsureComponent<Button>(buttonTransform.gameObject);
            button.targetGraphic = image;
            button.onClick.RemoveAllListeners();
            TMP_Text text = EnsureText(buttonTransform, "Text (TMP)", label, 24, Vector2.zero, sizeDelta, TextAlignmentOptions.Center);
            text.fontStyle = FontStyles.Bold;
            return buttonTransform;
        }

        private static Transform EnsureClonedButton(Transform sourceButton, Transform parent, string name, string label, Vector2 offsetFromSource)
        {
            Transform existing = parent.Find(name);
            if (existing == null)
            {
                GameObject clone = UnityEngine.Object.Instantiate(sourceButton.gameObject, parent);
                clone.name = name;
                existing = clone.transform;
                if (existing is RectTransform rect && sourceButton is RectTransform sourceRect)
                {
                    rect.anchorMin = sourceRect.anchorMin;
                    rect.anchorMax = sourceRect.anchorMax;
                    rect.pivot = sourceRect.pivot;
                    rect.sizeDelta = sourceRect.sizeDelta;
                    rect.anchoredPosition = sourceRect.anchoredPosition + offsetFromSource;
                    rect.localScale = sourceRect.localScale;
                }
            }

            Button button = existing.GetComponent<Button>() ?? existing.gameObject.AddComponent<Button>();
            button.onClick.RemoveAllListeners();
            SetFirstChildText(existing, label);
            return existing;
        }

        private static void FixInputField(TMP_InputField input)
        {
            if (input == null)
            {
                return;
            }

            input.caretColor = Color.white;
            input.selectionColor = new Color(0.25f, 0.55f, 1f, 0.38f);
            if (input.textComponent != null)
            {
                input.textComponent.color = Color.white;
                input.textComponent.fontStyle = FontStyles.Normal;
                input.textComponent.enableAutoSizing = true;
                input.textComponent.fontSizeMin = 10f;
                input.textComponent.fontSizeMax = Mathf.Max(14f, input.textComponent.fontSize);
            }

            if (input.placeholder is TMP_Text placeholder)
            {
                placeholder.color = new Color(1f, 1f, 1f, 0.42f);
                placeholder.fontStyle = FontStyles.Italic;
            }
        }

        private static void EnsureSlider(Transform parent, string name, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            Transform existing = parent.Find(name);
            if (existing == null)
            {
                existing = EnsureRectChild(parent, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), sizeDelta, anchoredPosition);
                Slider slider = existing.gameObject.AddComponent<Slider>();
                Transform background = EnsureGraphicChild(existing, "Background", Color.gray, Vector2.zero, sizeDelta);
                Transform fillArea = EnsureRectChild(existing, "Fill Area", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(-18f, 0f), Vector2.zero);
                Transform fill = EnsureGraphicChild(fillArea, "Fill", new Color(0.16f, 0.62f, 1f, 1f), Vector2.zero, Vector2.zero);
                Transform handleArea = EnsureRectChild(existing, "Handle Slide Area", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(-18f, 0f), Vector2.zero);
                Transform handle = EnsureGraphicChild(handleArea, "Handle", Color.white, Vector2.zero, new Vector2(16f, 22f));
                slider.targetGraphic = handle.GetComponent<Graphic>();
                slider.fillRect = fill as RectTransform;
                slider.handleRect = handle as RectTransform;
                slider.minValue = 0f;
                slider.maxValue = 100f;
                slider.value = 50f;
                if (background is RectTransform backgroundRect)
                {
                    backgroundRect.anchorMin = Vector2.zero;
                    backgroundRect.anchorMax = Vector2.one;
                    backgroundRect.sizeDelta = Vector2.zero;
                }
                if (fill is RectTransform fillRect)
                {
                    fillRect.anchorMin = Vector2.zero;
                    fillRect.anchorMax = Vector2.one;
                    fillRect.sizeDelta = Vector2.zero;
                }
            }
            else
            {
                EnsureComponent<Slider>(existing.gameObject);
            }
        }

        private static Transform EnsureGraphicChild(Transform parent, string name, Color color, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            Transform child = EnsureRectChild(parent, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), sizeDelta, anchoredPosition);
            var image = EnsureComponent<Image>(child.gameObject);
            image.color = color;
            return child;
        }

        private static TMP_Text EnsureText(Transform parent, string name, string value, int fontSize, Vector2 anchoredPosition, Vector2 sizeDelta, TextAlignmentOptions alignment)
        {
            Transform child = EnsureRectChild(parent, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), sizeDelta, anchoredPosition);
            var text = EnsureComponent<TextMeshProUGUI>(child.gameObject);
            text.text = value;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }

        private static Transform EnsureRectChild(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 sizeDelta, Vector2 anchoredPosition)
        {
            Transform child = parent.Find(name);
            if (child == null)
            {
                var gameObject = new GameObject(name, typeof(RectTransform));
                child = gameObject.transform;
                child.SetParent(parent, false);
            }

            var rect = child as RectTransform;
            if (rect == null)
            {
                throw new InvalidOperationException(name + " is not a RectTransform.");
            }

            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.sizeDelta = sizeDelta;
            rect.anchoredPosition = anchoredPosition;
            rect.localScale = Vector3.one;
            return child;
        }

        private static void ReparentPreserve(Transform child, Transform parent)
        {
            if (child.parent == parent)
            {
                return;
            }

            child.SetParent(parent, true);
        }

        private static void HideOptionalRow(Transform root, string path)
        {
            Transform row = root.Find(path);
            if (row != null)
            {
                row.gameObject.SetActive(false);
            }
        }

        private static void SetTextIfExists(Transform root, string path, string value)
        {
            Transform transform = root.Find(path);
            TMP_Text text = transform != null ? transform.GetComponent<TMP_Text>() : null;
            if (text != null)
            {
                text.text = value;
            }
        }

        private static void SetFirstChildText(Transform root, string value)
        {
            TMP_Text text = root.GetComponentInChildren<TMP_Text>(true);
            if (text != null)
            {
                text.text = value;
            }
        }

        private static T EnsureComponent<T>(GameObject gameObject) where T : Component
        {
            T component = gameObject.GetComponent<T>();
            if (component == null)
            {
                component = gameObject.AddComponent<T>();
            }

            return component;
        }

        private static Scene OpenBootstrapScene()
        {
            Scene scene = SceneManager.GetSceneByPath(BootstrapScenePath);
            if (scene.IsValid() && scene.isLoaded)
            {
                return scene;
            }

            return EditorSceneManager.OpenScene(BootstrapScenePath, OpenSceneMode.Single);
        }

        private static GatebreakerArenaSceneUiBinding FindSceneBinding(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                GatebreakerArenaSceneUiBinding binding = root.GetComponentInChildren<GatebreakerArenaSceneUiBinding>(true);
                if (binding != null)
                {
                    return binding;
                }
            }

            return null;
        }

        private static Transform FindRequired(Scene scene, string path)
        {
            int slash = path.IndexOf('/');
            string rootName = slash >= 0 ? path.Substring(0, slash) : path;
            string childPath = slash >= 0 ? path.Substring(slash + 1) : string.Empty;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name != rootName)
                {
                    continue;
                }

                Transform transform = string.IsNullOrEmpty(childPath) ? root.transform : root.transform.Find(childPath);
                if (transform != null)
                {
                    return transform;
                }
            }

            throw new InvalidOperationException("Missing scene path: " + path);
        }

        private static Transform FindRequired(Transform root, string path)
        {
            Transform transform = root.Find(path);
            if (transform == null)
            {
                throw new InvalidOperationException("Missing UI path: " + GetPath(root) + "/" + path);
            }

            return transform;
        }

        private static Transform FindFirstRequired(string path, params Transform[] roots)
        {
            for (int i = 0; i < roots.Length; i++)
            {
                Transform root = roots[i];
                Transform transform = root != null ? root.Find(path) : null;
                if (transform != null)
                {
                    return transform;
                }
            }

            throw new InvalidOperationException("Missing UI path in expected roots: " + path);
        }

        private static T FindRequired<T>(Transform root, string path) where T : Component
        {
            Transform transform = FindRequired(root, path);
            T component = transform.GetComponent<T>();
            if (component == null)
            {
                throw new InvalidOperationException("UI path has wrong component type: " + GetPath(transform) + " is not " + typeof(T).Name);
            }

            return component;
        }

        private static string GetPath(Transform transform)
        {
            var names = new List<string>();
            while (transform != null)
            {
                names.Add(transform.name);
                transform = transform.parent;
            }

            names.Reverse();
            return string.Join("/", names);
        }

        private static void Set(SerializedObject serializedObject, string propertyName, UnityEngine.Object value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException("Missing serialized property: " + propertyName);
            }

            property.objectReferenceValue = value;
        }

        private static void SetArray(SerializedObject serializedObject, string propertyName, params UnityEngine.Object[] values)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException("Missing serialized array property: " + propertyName);
            }

            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
        }

        private static void Require<T>(SerializedObject serializedObject, string propertyName, List<string> errors) where T : UnityEngine.Object
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            UnityEngine.Object value = property != null ? property.objectReferenceValue : null;
            if (value is T)
            {
                return;
            }

            errors.Add($"{propertyName} is missing or not a {typeof(T).Name}.");
        }

        private static void RequireArray<T>(SerializedObject serializedObject, string propertyName, int minimumCount, List<string> errors) where T : UnityEngine.Object
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null || !property.isArray || property.arraySize < minimumCount)
            {
                errors.Add($"{propertyName} has fewer than {minimumCount} bindings.");
                return;
            }

            for (int i = 0; i < minimumCount; i++)
            {
                UnityEngine.Object value = property.GetArrayElementAtIndex(i).objectReferenceValue;
                if (!(value is T))
                {
                    errors.Add($"{propertyName}[{i}] is missing or not a {typeof(T).Name}.");
                }
            }
        }
    }
}
