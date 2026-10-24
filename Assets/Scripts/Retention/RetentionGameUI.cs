using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// «Очаг» в игровой сцене: после уровня дня — сводка (награды, путь недели, что даст очагу) с добровольным «Удвоить угольки»,
/// «В Очаг» и «В меню» (кнопка «Новый уровень» для уровня дня скрыта — она вела в меню). Короткие всплывашки о заданиях.
/// Поле до конца уровня ничем не перекрывается.
/// </summary>
public class RetentionGameUI : MonoBehaviour
{
    private static RetentionGameUI instance;
    private RectTransform root, card, toast;
    private TextMeshProUGUI cardTitle, cardText, weekText, toastText;
    private Image weekBar;
    private Button doubleButton;
    private float toastUntil, showCardAt = -1f, cardShownAt;
    private int dailyEmbers, dailyCoins;

    private static RetentionSettings C => RetentionSettings.Current;

    public static void Attach()
    {
        if (!Retention.Active || instance != null) return;
        var go = new GameObject("RetentionGameUI");
        instance = go.AddComponent<RetentionGameUI>();
        try { instance.Build(); }
        catch (System.Exception e) { RetSafe.Report("game ui build", e); Destroy(go); instance = null; }
    }

    private void Build()
    {
        Canvas c = RetUi.Overlay("RetentionGameOverlay", 30);
        c.transform.SetParent(transform, false);
        RectTransform safe = RetUi.Fill(c.transform, "Safe");
        safe.gameObject.AddComponent<SafeArea>();
        root = RetUi.Fill(safe, "Content");
        root.gameObject.AddComponent<BannerReserve>();

        Image cardImg = RetUi.Panel(root, "DailyCard", new Vector2(0.5f, 0f), new Vector2(0f, 330f), new Vector2(960f, 640f));
        card = cardImg.rectTransform;
        RetUi.Img(card, "Icon", RetUi.Art("ic_ember"), Color.white, new Vector2(0f, 1f), new Vector2(110f, -110f), new Vector2(130f, 130f));
        cardTitle = RetUi.Text(card, "Title", "Уровень дня пройден!", "Daily level complete!", 52f, C.title, new Vector2(0.5f, 1f), new Vector2(70f, -70f), new Vector2(700f, 70f), bold: true);
        cardText = RetUi.Text(card, "Text", "", "", 36f, C.textLight, new Vector2(0.5f, 1f), new Vector2(70f, -150f), new Vector2(700f, 90f));
        weekBar = RetUi.Bar(card, "Week", new Vector2(0.5f, 1f), new Vector2(0f, -250f), new Vector2(820f, 34f), new Color(1f, 0.8f, 0.3f));
        weekText = RetUi.Text(card, "WeekText", "", "", 28f, C.textLight, new Vector2(0.5f, 1f), new Vector2(0f, -300f), new Vector2(860f, 50f));
        doubleButton = RetUi.Button(card, "Double", "Удвоить угольки (видео)", "Double embers (video)", new Vector2(0.5f, 0f), new Vector2(0f, 200f), new Vector2(620f, 110f),
            () => Retention.Instance.DoubleDailyWithAd(), false, 34f);
        RetUi.Button(card, "ToHearth", "В Очаг", "To the Hearth", new Vector2(0.5f, 0f), new Vector2(-220f, 74f), new Vector2(400f, 128f), () => Leave(true), true, 42f);
        RetUi.Button(card, "ToMenu", "В меню", "Menu", new Vector2(0.5f, 0f), new Vector2(220f, 74f), new Vector2(400f, 128f), () => Leave(false), false, 42f);
        card.gameObject.SetActive(false);

        Image t = RetUi.Img(root, "Toast", C.pill, new Color(0.18f, 0.07f, 0.03f, 0.95f), new Vector2(0.5f, 1f), new Vector2(0f, -200f), new Vector2(880f, 100f), sliced: true);
        t.preserveAspect = false;
        toast = t.rectTransform;
        toastText = RetUi.Text(toast, "Text", "", "", 32f, C.textLight, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(840f, 90f));
        toast.gameObject.SetActive(false);

        Retention.Raised += OnEvent;
        Retention.Changed += OnChanged;
    }

    private void OnDestroy()
    {
        Retention.Raised -= OnEvent;
        Retention.Changed -= OnChanged;
        if (instance == this) instance = null;
    }

    private void Leave(bool hearth)
    {
        RetentionMenuUI.OpenHearthOnEnter = hearth;
        Retention.PendingDaily = -1;
        GameManager.RestartPressed = false;
        AdsManager.OnLevelCompleted();
        Time.timeScale = 1f;
        SceneLoadGuard.Load("MainScene");
    }

    private void OnEvent(RetEvent e)
    {
        switch (e.kind)
        {
            case RetEventKind.DailyWin: dailyEmbers += e.a; dailyCoins += e.b; showCardAt = Time.unscaledTime + 2.2f; break;
            case RetEventKind.Daily3Stars: dailyEmbers += e.a; dailyCoins += e.b; showCardAt = Time.unscaledTime + 2.2f; break;
            case RetEventKind.DailyDoubled: dailyEmbers += e.a; UpdateCard(); break;
            case RetEventKind.QuestDone:
                RetQuest q = C.questPool[e.a];
                Toast($"Задание выполнено: {string.Format(q.textRU, q.target)} (+{e.b})", $"Task done: {string.Format(q.textEN, q.target)} (+{e.b})");
                break;
            case RetEventKind.WeekStep: Toast($"Неделя: {RetText.WinsRu(e.a)} — +{RetText.EmbersRu(e.b)}", $"Week: {RetText.WinsEn(e.a)} — +{RetText.EmbersEn(e.b)}"); break;
            case RetEventKind.QuestsAll: Toast($"Все задания дня! +{RetText.EmbersRu(e.a)}", $"All daily tasks! +{RetText.EmbersEn(e.a)}"); break;
            case RetEventKind.WeekGoal: Toast("Недельная цель выполнена! Подарок ждёт в меню", "Weekly goal complete! A gift awaits in the menu"); break;
            case RetEventKind.Milestone: Toast($"Серия {RetText.DaysRu(e.a)}! Подарок ждёт в меню", $"{e.a}-day streak! A gift awaits in the menu"); break;
        }
    }

    private void OnChanged() { if (card.gameObject.activeSelf) UpdateCard(); }

    private void UpdateCard()
    {
        if (!Retention.Active) return;
        RetentionCore core = Retention.Instance.core;
        RetState s = core.s;
        string coins = dailyCoins > 0 ? $", +{dailyCoins} {RetText.Plural(dailyCoins, "монета", "монеты", "монет")}" : "";
        int next = core.NextStageWarmth;
        string stageRU = next > 0 ? $"\nВ очаге ждут угольки: {s.embers} — до «{C.stages[s.stage + 1].nameRU}» ещё {next - s.warmth}" : "";
        string stageEN = next > 0 ? $"\nEmbers waiting: {s.embers} — {next - s.warmth} more to «{C.stages[s.stage + 1].nameEN}»" : "";
        RetUi.Set(cardText, $"+{RetText.EmbersRu(dailyEmbers)}{coins}{stageRU}", $"+{RetText.EmbersEn(dailyEmbers)}{(dailyCoins > 0 ? $", +{RetText.CoinsEn(dailyCoins)}" : "")}{stageEN}");
        RetUi.SetBar(weekBar, (float)s.weekWins / C.weeklyGoalWins);
        RetUi.Set(weekText, s.weekGoalClaimed ? "Неделя выполнена!" : $"Неделя: {s.weekWins} из {C.weeklyGoalWins} побед · серия {s.streak}",
                  s.weekGoalClaimed ? "Week complete!" : $"Week: {s.weekWins} of {C.weeklyGoalWins} wins · streak {s.streak}");
        doubleButton.gameObject.SetActive(core.CanDoubleDaily);
    }

    private void Toast(string ru, string en)
    {
        RetUi.Set(toastText, ru, en);
        toast.gameObject.SetActive(true);
        toastUntil = Time.unscaledTime + 2.6f;
    }

    private void Update()
    {
        try { Tick(); }
        catch (System.Exception e) { RetSafe.Report("game ui update", e); enabled = false; }
    }

    private void Tick()
    {
        if (toast.gameObject.activeSelf && Time.unscaledTime >= toastUntil) toast.gameObject.SetActive(false);
        if (showCardAt > 0f && Time.unscaledTime >= showCardAt)
        {
            showCardAt = -1f;
            card.gameObject.SetActive(true);
            cardShownAt = Time.unscaledTime;
            UpdateCard();
        }
        if (card.gameObject.activeSelf)
        {
            float k = Mathf.Clamp01((Time.unscaledTime - cardShownAt) / 0.35f);
            float sc = k < 1f ? Mathf.Lerp(0.8f, 1f, 1f - (1f - k) * (1f - k)) : 1f;
            card.localScale = new Vector3(sc, sc, 1f);
        }
    }
}
