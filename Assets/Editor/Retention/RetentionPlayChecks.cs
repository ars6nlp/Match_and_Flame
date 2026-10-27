using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// «Очаг» в Play Mode (batchmode с графикой): подарок дня, меню, Очаг (3 вкладки), Дневник (2 вкладки), коллекция, переход стадии,
/// окно награды, главы на выборе уровня, уровень дня и его награда. Кадры → Builds/hearth-ui/{HEARTH_TAG}/{имя}_{язык}_{экран}.png
/// (RET_RES — разрешения через запятую, RET_LANGS — языки, по умолчанию ru,en). Аудит окон (раскладка, кегль ≥14 sp, буквы на буквах,
/// выход из карточки, контраст ≥4.5:1) — Builds/hearth-ui/audit-{HEARTH_TAG}.md.
/// Ключи PlayerPrefs, которые трогает проверка (и язык), восстанавливаются в конце.
/// </summary>
[InitializeOnLoad]
public static class RetentionPlayChecks
{
    private const string Key = "RetChecks.Mode";
    private static readonly string[] StringKeys = { "Ret_State", "Ret_StateBroken", "Lang" };
    private static readonly string[] IntKeys = { "Ret_SaveVersion", "Ret_DebugDayOffset", "GoldenMatchesCurrency" };

    static RetentionPlayChecks()
    {
        EditorApplication.playModeStateChanged += s =>
        {
            if (s == PlayModeStateChange.EnteredPlayMode && SessionState.GetString(Key, "") != "")
            {
                var go = new GameObject("RetChecksRunner");
                Object.DontDestroyOnLoad(go);
                go.AddComponent<Runner>();
            }
        };
    }

    public static void Shots()
    {
        foreach (string k in StringKeys) SessionState.SetString("RetChecks.S." + k, PlayerPrefs.HasKey(k) ? "1" + PlayerPrefs.GetString(k) : "0");
        foreach (string k in IntKeys) SessionState.SetString("RetChecks.I." + k, PlayerPrefs.HasKey(k) ? "1" + PlayerPrefs.GetInt(k) : "0");
        SessionState.SetString(Key, "shots");
        EditorSceneManager.OpenScene("Assets/Scenes/MainScene.unity");
        EditorApplication.isPlaying = true;
    }

    private static void RestorePrefs()
    {
        foreach (string k in StringKeys)
        {
            string v = SessionState.GetString("RetChecks.S." + k, "0");
            if (v[0] == '0') PlayerPrefs.DeleteKey(k); else PlayerPrefs.SetString(k, v.Substring(1));
        }
        foreach (string k in IntKeys)
        {
            string v = SessionState.GetString("RetChecks.I." + k, "0");
            if (v[0] == '0') PlayerPrefs.DeleteKey(k); else PlayerPrefs.SetInt(k, int.Parse(v.Substring(1)));
        }
        PlayerPrefs.Save();
    }

    private class Runner : MonoBehaviour
    {
        private readonly StringBuilder log = new StringBuilder();
        private UiAudit.Res[] resList;
        private string[] langs;
        private static string Tag => System.Environment.GetEnvironmentVariable("HEARTH_TAG") ?? "current";

        private IEnumerator Start()
        {
            string res = System.Environment.GetEnvironmentVariable("RET_RES") ?? "1080x1920,1080x2400,1200x1600";
            resList = UiAudit.Resolutions.Where(r => res.Split(',').Contains(r.name)).ToArray();
            langs = (System.Environment.GetEnvironmentVariable("RET_LANGS") ?? "ru,en").Split(',');
            yield return new WaitForSecondsRealtime(2f);
            Retention ret = Retention.Instance;
            RetentionMenuUI ui = RetentionMenuUI.Instance;
            if (ret == null || ui == null) { log.AppendLine($"Retention {(ret != null)}, меню {(ui != null)}"); Finish(); yield break; }
            ret.DebugReset();
            yield return new WaitForSecondsRealtime(0.5f);
            ui.Popups.CloseCurrent();
            ret.core.s.giftDay = -1;
            ret.core.s.embersEarned = 25;   // Очаг «открыт» (после сброса состояния он закрыт до первых уровней) — иначе подарок не предлагается
            ret.core.s.streak = 4; ret.core.s.lastCountedDay = ret.core.Today - 1;
            ui.Popups.Startup();
            yield return Shot("whatsnew", "Popup");
            ui.Popups.CloseCurrent();
            // Подарок закрывается сам через 4.5 с, а кадры одного окна на 12 раскладках идут дольше — держим окно открытым
            typeof(RetPopups).GetField("autoCloseAt", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(ui.Popups, -1f);
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Shot("gift", "Popup");
            ui.Popups.CloseCurrent();
            ui.Popups.CloseCurrent();
            yield return new WaitForSecondsRealtime(1.6f);
            yield return Shot("menu", "Retention");

            // Состояние «через пару недель»: угольки, декор, облики, коробки
            ret.DebugEmbers(1100);
            RetState s = ret.S;
            foreach (int i in new[] { 0, 1, 2, 3, 6, 7, 9 }) s.decorMask |= 1 << i;   // часть декора (монеты не трогаем)
            foreach (int o in new[] { 0, 1, 2, 3, 15 }) s.AddOutfit(o);
            s.aroma[0] = 40; s.aroma[1] = 15; s.aroma[2] = 10;
            ret.Feed(800);
            s.equippedOutfit = 3;
            for (int i = 0; i < 9; i++) ret.DebugBox();
            Retention.Unseen.Clear();
            ret.Flush();
            ui.Popups.CloseCurrent();
            HearthVisualSettings.DebugHour = 13;
            s.introDone = false;
            ui.Hearth.Open();
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Shot("hearth_intro", "HearthScreen");
            s.introDone = true;
            ui.Hearth.Close();
            ui.Hearth.Open();
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Shot("hearth_day", "HearthScreen");
            // Покупка декора: подтверждение (хватает) и подсказка (не хватает)
            var tapSlot = typeof(HearthScreen).GetMethod("TapSlot", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            int affordable = -1, expensive = -1;
            for (int i = 0; i < RetentionSettings.Current.decor.Count; i++)
            {
                RetDecor d = RetentionSettings.Current.decor[i];
                if (s.HasDecor(i) || !d.enabled) continue;
                bool can = d.priceCoins > 0 ? CurrencyManager.GetGoldenMatches() >= d.priceCoins : s.embers >= d.priceEmbers;
                if (can && affordable < 0) affordable = i;
                if (!can && expensive < 0) expensive = i;
            }
            log.AppendLine($"декор для кадров: по карману #{affordable}, не по карману #{expensive}");
            var okField0 = typeof(RetPopups).GetField("popupOk", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (affordable >= 0)
            {
                tapSlot.Invoke(ui.Hearth, new object[] { affordable });
                yield return Shot("hearth_item", "Popup");
                yield return new WaitForSecondsRealtime(0.4f);
                ((UnityEngine.UI.Button)okField0.GetValue(ui.Popups)).onClick.Invoke();   // «Купить» → подтверждение
                yield return Shot("hearth_confirm", "Popup");
                bool noBuy = RetentionMenuUI.HandleBack() && !ui.Popups.Busy && !s.HasDecor(affordable);
                log.AppendLine($"Карточка предмета → «Купить» → подтверждение; «Назад» = «Нет»: предмет не куплен {noBuy}");
            }
            if (expensive >= 0)
            {
                tapSlot.Invoke(ui.Hearth, new object[] { expensive });
                yield return Shot("hearth_item_low", "Popup");
                ui.Popups.CloseCurrent();
            }
            ui.Popups.Toast("Цилиндр: за стадию «Факел»", "Top hat: for the «Torch» stage");
            var until = typeof(RetPopups).GetField("toastUntil", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            until?.SetValue(ui.Popups, Time.unscaledTime + 999f);   // кадры на 12 раскладках дольше 2.5 с показа
            yield return Shot("hearth_toast", "Toast");
            until?.SetValue(ui.Popups, 0f);
            yield return null;
            // «Назад» по стеку: лист → Очаг → меню
            ui.Hearth.OpenSheet(0);
            bool b1 = RetentionMenuUI.HandleBack(), sheetClosed = !ui.Hearth.SheetOpen && ui.Hearth.IsOpen;
            bool b2 = RetentionMenuUI.HandleBack(), hearthClosed = !ui.Hearth.IsOpen;
            bool b3 = RetentionMenuUI.HandleBack();
            log.AppendLine($"«Назад» по стеку: лист закрыт {b1 && sheetClosed}, Очаг закрыт {b2 && hearthClosed}, дальше обработка меню {!b3}");
            // «Назад» на окне подтверждения = «Нет»; двойной тап по «Да» срабатывает один раз
            int yes = 0, no = 0;
            ui.Popups.Confirm("t", "t", "x", "x", () => yes++);
            bool backHandled = RetentionMenuUI.HandleBack();
            log.AppendLine($"«Назад» на подтверждении: обработано {backHandled}, окно закрыто {!ui.Popups.Busy}, «Да» выполнено {yes} раз (ожидается 0)");
            var okField = typeof(RetPopups).GetField("popupOk", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var okButton = (UnityEngine.UI.Button)okField.GetValue(ui.Popups);
            ui.Popups.Confirm("t", "t", "x", "x", () => yes++);
            okButton.onClick.Invoke();   // сразу после появления — игнорируется
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 0.45f) yield return null;
            okButton.onClick.Invoke(); okButton.onClick.Invoke();   // двойной тап
            log.AppendLine($"Двойной тап по «Да»: выполнено {yes} раз (ожидается 1), окно закрыто {!ui.Popups.Busy}, «Нет» {no}");
            // Оценка нагрузки экрана Очага
            ui.Hearth.Open();
            yield return null;
            var hearthRoot = GameObject.Find("HearthScreen");
            if (hearthRoot != null)
            {
                var graphics = hearthRoot.GetComponentsInChildren<UnityEngine.UI.Graphic>(false);
                var textures = new System.Collections.Generic.HashSet<Texture>();
                foreach (var g in graphics) if (g.mainTexture != null) textures.Add(g.mainTexture);
                int particles = 0; foreach (var pf in hearthRoot.GetComponentsInChildren<UIParticleField>(false)) particles += pf.maxParticles;
                log.AppendLine($"Экран Очага: {graphics.Length} графических элементов, {textures.Count} разных текстур (атлас, шрифт, glow, UI), Canvas: {hearthRoot.GetComponentsInChildren<Canvas>(false).Length}, частиц не больше {particles}");
                // Оценка draw calls сверху: смены (материал, текстура) по порядку отрисовки внутри каждого Canvas (батчер может объединить больше)
                int breaks = 0;
                foreach (var cv in hearthRoot.GetComponentsInChildren<Canvas>(false))
                {
                    Material lm = null; Texture lt = null; bool first = true;
                    foreach (var g in cv.GetComponentsInChildren<UnityEngine.UI.Graphic>(false))
                    {
                        if (g.canvas != cv || !g.enabled || g.color.a <= 0.001f) continue;
                        if (first || g.materialForRendering != lm || g.mainTexture != lt) { breaks++; first = false; lm = g.materialForRendering; lt = g.mainTexture; }
                    }
                }
                log.AppendLine($"Экран Очага: оценка draw calls (верхняя граница, без объединения батчером): {breaks}");
            }
            HearthVisualSettings.DebugHour = 23;
            ui.Hearth.Refresh(true);
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Shot("hearth_night", "HearthScreen");
            HearthVisualSettings.DebugHour = 19;
            ret.DebugShiftDays(9);   // Хэллоуин (25.10–02.11)
            ui.Popups.CloseCurrent();
            ui.Hearth.Refresh(true);
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Shot("hearth_event", "HearthScreen");
            ret.DebugShiftDays(-9);
            ui.Popups.CloseCurrent();
            HearthVisualSettings.DebugHour = 13;
            ui.Hearth.Refresh(true);
            ui.Hearth.OpenSheet(0); yield return Shot("hearth_outfits", "OutfitsSheet");
            ui.Hearth.OpenSheet(1); yield return Shot("hearth_atlas", "OutfitsSheet");
            ui.Hearth.CloseSheet();
            ui.Hearth.Close();
            ui.Popups.CloseCurrent();
            // Огонёк по стадиям: 8 стадий (линии по кругу, облики), второй ряд — настроения на «Костре»
            {
                var lab = RetUi.Overlay("FlameLab", 90);
                var bgImg = RetUi.Fill(lab.transform, "Bg").gameObject.AddComponent<UnityEngine.UI.Image>();
                bgImg.color = new Color(0.27f, 0.14f, 0.08f, 1f);
                var flames = new System.Collections.Generic.List<RetFlame>();
                int[] outfitsByStage = { -1, 0, -1, 3, -1, 5, -1, 6 };
                for (int st = 0; st < 8; st++)
                {
                    float x = ((st % 4) - 1.5f) * 250f, y = st < 4 ? 420f : 20f;
                    RetFlame f = RetFlame.Create(lab.transform, new Vector2(x, y), 1100f);
                    f.Setup(st, st % 3, outfitsByStage[st], 0.3f);
                    RetUi.Text(lab.transform, "N" + st, RetentionSettings.Current.stages[st].nameRU, RetentionSettings.Current.stages[st].nameEN, 34f, Color.white, new Vector2(0.5f, 0.5f), new Vector2(x, y - 170f), new Vector2(240f, 50f));
                    flames.Add(f);
                }
                FlameMood[] moods = { FlameMood.Idle, FlameMood.Happy, FlameMood.Surprised, FlameMood.Sleepy };
                for (int k = 0; k < moods.Length; k++)
                {
                    float x = (k - 1.5f) * 250f;
                    RetFlame f = RetFlame.Create(lab.transform, new Vector2(x, -400f), 1100f);
                    f.Setup(3, 1, -1, 0.3f);
                    f.SetMood(moods[k], 60f);
                    RetUi.Text(lab.transform, "M" + k, moods[k].ToString(), moods[k].ToString(), 34f, Color.white, new Vector2(0.5f, 0.5f), new Vector2(x, -570f), new Vector2(240f, 50f));
                }
                yield return new WaitForSecondsRealtime(1.2f);
                yield return Shot("flame_stages");
                Object.Destroy(lab.gameObject);
                yield return null;
            }
            // Одно задание выполнено (награда уже начислена) — на карточке «Забрать»
            if (s.questIds.Length > 0)
            {
                s.questProgress[0] = RetentionCore.Needed(RetentionSettings.Current.questPool[s.questIds[0]]);
                s.questClaimed[0] = 1;
                s.questsShownDay = ret.core.Today; s.questsShownMask = 0;
            }
            ui.Diary.Open(0); yield return Shot("diary_today", "DiaryScreen");
            ui.Diary.Open(1); yield return Shot("diary_week", "DiaryScreen");
            ui.Diary.Open(DiaryScreen.CalendarTab); yield return Shot("diary_calendar", "DiaryScreen");
            {
                var dsr = GameObject.Find("DiaryScreen").GetComponentsInChildren<ScrollRect>(false).FirstOrDefault();
                if (dsr != null) { Canvas.ForceUpdateCanvases(); dsr.verticalNormalizedPosition = 0f; }
                yield return Shot("diary_calendar_end", "DiaryScreen");
                if (dsr != null) dsr.verticalNormalizedPosition = 1f;
            }
            ui.Diary.Close();
            ui.Popups.CloseCurrent();
            ui.Collection.Open(); yield return Shot("collection", "CollectionScreen");
            {
                var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                typeof(CollectionScreen).GetMethod("ShowOdds", flags).Invoke(ui.Collection, null);
                yield return Shot("collection_odds", "Popup");
                ui.Popups.CloseCurrent();
                typeof(CollectionScreen).GetMethod("ToggleHoliday", flags).Invoke(ui.Collection, null);
                var sr = GameObject.Find("CollectionScreen").GetComponentInChildren<ScrollRect>();
                Canvas.ForceUpdateCanvases();
                sr.verticalNormalizedPosition = 0f;
                yield return Shot("collection_holiday", "CollectionScreen");
                typeof(CollectionScreen).GetMethod("ToggleHoliday", flags).Invoke(ui.Collection, null);
                sr.verticalNormalizedPosition = 1f;
            }
            ui.Collection.Close();
            ui.Popups.CloseCurrent();
            ui.Daily.Open(); yield return Shot("daily_entry", "DailyEntry");
            ui.Daily.Close();
            ui.Popups.CloseCurrent();
            yield return Shot("menu_extras", "Retention");   // меню с открытыми событиями и коллекцией
            ui.Popups.ShowStage(4, 0); yield return Shot("stage_up", "Popup");
            ui.Popups.CloseCurrent();
            Retention.Unseen.Clear();
            Retention.Unseen.Enqueue(new RetEvent(RetEventKind.Box, 6, 0));
            ui.Popups.DrainUnseen();
            yield return new WaitForSecondsRealtime(1.2f);   // текст появляется после раскрытия коробка
            yield return Shot("reveal_box", "Popup");
            ui.Popups.CloseCurrent();
            Retention.Unseen.Clear();
            Retention.Unseen.Enqueue(new RetEvent(RetEventKind.Milestone, 7, 60));
            ui.Popups.DrainUnseen(); yield return Shot("milestone", "Popup");
            ui.Popups.CloseCurrent();
            Retention.Unseen.Clear();
            HearthVisualSettings.DebugHour = -1;

            // Главы на выборе уровня
            SceneManager.LoadScene("LevelSelectionScene");
            yield return new WaitForSecondsRealtime(2.5f);
            yield return Shot("levels");

            // Уровень дня
            int daily = ret.TodayDailyIndex;
            Retention.PendingDaily = daily;
            log.AppendLine($"уровень дня сегодня: #{daily} ({(daily >= 0 ? DailyLevelPool.Current.entries[daily].prefab : "нет")})");
            SceneManager.LoadScene("GameScene");
            yield return new WaitForSecondsRealtime(3f);
            yield return Shot("daily_hud");
            var gm = Object.FindFirstObjectByType<GameManager>();
            log.AppendLine($"GameManager.IsDaily = {gm != null && gm.IsDaily}");
            ret.DebugWinDaily(3);
            if (gm != null) gm.DebugWin(3);
            yield return new WaitForSecondsRealtime(4f);
            yield return Shot("daily_win");
            Finish();
        }

        private void Finish()
        {
            WriteAudit();
            log.AppendLine($"аудит окон: {audit.Count} записей (Builds/hearth-ui/audit-{Tag}.md)");
            File.WriteAllText("Builds/retention-play.txt", log.ToString());
            Debug.Log("[Polish] RetentionPlayChecks\n" + log);
            SessionState.EraseString(Key);
            RestorePrefs();
            EditorApplication.Exit(0);
        }

        private static readonly System.Collections.Generic.List<UiAudit.Issue> audit = new System.Collections.Generic.List<UiAudit.Issue>();

        private IEnumerator Shot(string name, string modal = null)
        {
            string only = System.Environment.GetEnvironmentVariable("RET_ONLY");   // быстрый прогон: только эти кадры (через запятую)
            if (!string.IsNullOrEmpty(only) && System.Array.IndexOf(only.Split(','), name) < 0) yield break;
            foreach (string lang in langs)
            {
                LanguageInit.SetLanguage(lang);
                yield return null;
                yield return ShotLang(name, modal, lang);
            }
            log.AppendLine("кадр " + name);
        }

        private IEnumerator ShotLang(string name, string modal, string lang)
        {
            yield return null;
            yield return new WaitForSecondsRealtime(0.45f);
            Scene scene = SceneManager.GetActiveScene();
            string lt = "/" + lang;
            // Аудит раскладки — на всех 6 разрешениях; кадры — на выбранных (RET_RES)
            foreach (UiAudit.Res r in UiAudit.Resolutions)
            {
                bool render = System.Array.IndexOf(resList, r) >= 0;
                var canvases = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Canvas>(true))
                    .Where(c => c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay).ToList();
                var orders = canvases.Select(c => c.sortingOrder).ToList();
                var reserves = Object.FindObjectsByType<BannerReserve>(FindObjectsSortMode.None);
                foreach (var br in reserves) br.enabled = false;   // иначе в Play Mode пересчитает отступ по экрану batchmode
                UiAudit.Layout lay = UiAudit.Apply(scene, r);
                // В Play Mode Canvas после смены режима пересчитывает размер в следующем кадре — ждём кадр и ставим размер снова
                yield return null;
                float sc0 = Mathf.Min(r.w / 1080f, r.h / 1920f);
                lay.canvasSize = new Vector2(r.w / sc0, r.h / sc0);
                lay.scale = sc0;
                foreach (var br in reserves)
                {
                    var brt = (RectTransform)br.transform;
                    brt.offsetMin = new Vector2(brt.offsetMin.x, (VisualTheme.Current.bannerReserveDp + VisualTheme.Current.bannerGapDp) * r.density / sc0);
                }
                foreach (Canvas c in lay.roots)
                {
                    var scaler = c.GetComponent<CanvasScaler>();
                    if (scaler != null && scaler.referenceResolution != new Vector2(1080f, 1920f)) continue;
                    var rt = (RectTransform)c.transform;
                    rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.sizeDelta = lay.canvasSize;
                    rt.localScale = Vector3.one;
                }
                foreach (var f in Object.FindObjectsByType<AspectRatioFitter>(FindObjectsSortMode.None)) { f.enabled = false; f.enabled = true; }
                Canvas.ForceUpdateCanvases();
                // RetFit вписывает карточки в LateUpdate — здесь кадров нет, вписываем сразу
                foreach (var fit in Object.FindObjectsByType<RetFit>(FindObjectsSortMode.None)) fit.SendMessage("LateUpdate", SendMessageOptions.DontRequireReceiver);
                Canvas.ForceUpdateCanvases();
                var menuUi = Object.FindFirstObjectByType<RetentionMenuUI>();
                if (menuUi != null && menuUi.Hearth != null && menuUi.Hearth.IsOpen)
                {
                    // На телефоне сцена вписывается каждый кадр; здесь размер сменили вручную — вписываем сразу
                    menuUi.Hearth.Refresh(true);
                    Canvas.ForceUpdateCanvases();
                    // Сцена (не кнопки — общий аудит её не видит) не должна заходить на нижние панели
                    var host = Object.FindObjectsByType<RectTransform>(FindObjectsSortMode.None).FirstOrDefault(t => t.name == "SceneHost" && t.gameObject.activeInHierarchy);
                    var hs = host != null ? host.GetComponentsInParent<RectTransform>(true).FirstOrDefault(t => t.name == "HearthScreen") : null;
                    var cont = hs != null ? hs.Find("Safe/Content") : null;
                    if (host != null && cont != null)
                    {
                        // Видимая полоса комнаты (SceneHost) не должна заходить под карточку сверху и под панели снизу
                        float bottom = WorldRect(host).yMin, topY = WorldRect(host).yMax;
                        foreach (string part in new[] { "Top", "Info" })
                        {
                            var p = cont.Find(part);
                            if (p == null || !p.gameObject.activeInHierarchy) continue;
                            float low = WorldRect((RectTransform)p).yMin;
                            if (low < topY - 1f)
                                audit.Add(new UiAudit.Issue { state = name, element = "HearthScreen/SceneHost × " + part, res = r.name + lt, kind = "SCENE_OVER", detail = $"«{part}» заходит на комнату на {topY - low:0} px", severity = 2 });
                        }
                        foreach (string part in new[] { "Intro", "Bottom", "Nav" })
                        {
                            var p = cont.Find(part);
                            if (p == null || !p.gameObject.activeInHierarchy) continue;
                            float top = WorldRect((RectTransform)p).yMax;
                            if (top > bottom + 1f)
                                audit.Add(new UiAudit.Issue { state = name, element = "HearthScreen/SceneHost × " + part, res = r.name + lt, kind = "SCENE_OVER", detail = $"сцена заходит на «{part}» на {top - bottom:0} px", severity = 2 });
                        }
                    }
                }
                List<UiAudit.TextSample> samples = null;
                var st = new UiAudit.State { scene = scene.path, name = name, modal = modal, banner = true };
                bool Own(UiAudit.Issue i) => modal != null && i.element.Split(new[] { " × ", " ← ", " ⊄ " }, System.StringSplitOptions.None).All(part => part.Contains(modal));
                if (modal != null)
                {
                    // Рекламный тестовый баннер (свой Canvas) из проверки исключаем — место под него держит BannerReserve
                    lay.roots.RemoveAll(c => c.name.StartsWith("ADAPTIVE"));
                    foreach (var i in UiAudit.Check(scene, st, lay).Where(Own)) { i.res += lt; audit.Add(i); }
                    foreach (var i in UiAudit.CheckTexts(scene, st, lay).Where(Own)) { i.res += lt; audit.Add(i); }
                    audit.AddRange(UiAudit.CheckReadability(scene, st, lay, RetentionSettings.Current.card, lt).Where(Own));
                    if (render) samples = UiAudit.TextSamples(lay).Where(t => t.path.Contains(modal)).ToList();
                }
                if (render)
                {
                    UiAudit.Render(scene, lay, $"Builds/hearth-ui/{Tag}/{name}_{lang}_{r.name}.png");
                    if (samples != null && samples.Count > 0)
                    {
                        // Фон под буквами: тот же кадр без текста
                        var texts = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<TMPro.TMP_Text>(false)).Where(t => t.enabled).ToList();
                        foreach (var t in texts) t.enabled = false;
                        Texture2D bg = UiAudit.RenderToTexture(scene, lay);
                        foreach (var t in texts) t.enabled = true;
                        if (bg != null) { audit.AddRange(UiAudit.CheckContrast(st, lay, samples, bg, lt)); Object.DestroyImmediate(bg); }
                    }
                }
                foreach (var br in reserves) if (br != null) br.enabled = true;
                for (int i = 0; i < canvases.Count; i++)
                {
                    canvases[i].renderMode = RenderMode.ScreenSpaceOverlay;
                    canvases[i].sortingOrder = orders[i];
                    var scl = canvases[i].GetComponent<CanvasScaler>();
                    if (scl != null) scl.enabled = true;
                }
            }
        }

        private static void WriteAudit()
        {
            var sb = new StringBuilder($"# Аудит окон «Очага» в Play Mode ({Tag}): 6 разрешений, RU и EN\n\n");
            sb.AppendLine("Проверки: перекрытия кнопок и текста, выход за экран и безопасную зону, касание < 48 dp, зона баннера, переполнение текста, " +
                          "кегль < 14 sp (SMALL_FONT), буквы на буквах (TEXT_OVERLAP), выход из карточки (OUT_OF_CARD), контраст < 4.5:1 (CONTRAST, на разрешениях с кадрами), сцена Очага на нижних панелях (SCENE_OVER).\n");
            var unique = audit.GroupBy(i => (i.state, i.element, i.kind)).OrderByDescending(g => g.Max(i => i.severity)).ThenBy(g => g.Key.state).ThenBy(g => g.Key.kind).ToList();
            sb.AppendLine($"Записей: {audit.Count}, уникальных: {unique.Count}\n");
            sb.AppendLine("| Вид | Записей | Уникальных |\n|---|---|---|");
            foreach (var k in audit.GroupBy(i => i.kind).OrderBy(g => g.Key)) sb.AppendLine($"| {k.Key} | {k.Count()} | {k.Select(i => (i.state, i.element)).Distinct().Count()} |");
            sb.AppendLine("\n| Крит. | Окно | Вид | Элемент | Экраны | Подробно |\n|---|---|---|---|---|---|");
            foreach (var g in unique) sb.AppendLine($"| {g.Max(i => i.severity)} | {g.Key.state} | {g.Key.kind} | {g.Key.element} | {string.Join(", ", g.Select(i => i.res).Distinct())} | {g.First().detail.Replace("|", "/")} |");
            Directory.CreateDirectory("Builds/hearth-ui");
            File.WriteAllText($"Builds/hearth-ui/audit-{Tag}.md", sb.ToString());
        }
    }

    private static Rect WorldRect(RectTransform rt)
    {
        var c = new Vector3[4];
        rt.GetWorldCorners(c);
        return Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y);
    }
}
