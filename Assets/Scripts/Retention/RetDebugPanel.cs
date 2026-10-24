#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;

/// <summary>Отладка «Очага» (только редактор и Development Build): сдвиг даты, сброс, события.</summary>
public static class RetDebugPanel
{
    private static RectTransform panel;

    public static void Open(RectTransform root)
    {
        if (panel == null) Build(root);
        panel.gameObject.SetActive(true);
        panel.SetAsLastSibling();
    }

    private static void Build(RectTransform root)
    {
        panel = RetUi.Screen(root, "RetDebug", "Отладка Очага", "Hearth debug", () => panel.gameObject.SetActive(false), out RectTransform body, new Vector2(960f, 1500f));
        var items = new (string, System.Action)[]
        {
            ("День −1", () => Retention.Instance.DebugShiftDays(-1)),
            ("День +1", () => Retention.Instance.DebugShiftDays(1)),
            ("Неделя +7", () => Retention.Instance.DebugShiftDays(7)),
            ("Месяц +30", () => Retention.Instance.DebugShiftDays(30)),
            ("Сбросить дату", () => { DayClock.DebugOffset = 0; Retention.Instance.CheckDay(); }),
            ("+500 угольков", () => Retention.Instance.DebugEmbers(500)),
            ("Коробок", () => Retention.Instance.DebugBox()),
            ("Уровень дня на 3 звезды", () => Retention.Instance.DebugWinDaily(3)),
            ("Секретный — сейчас", () => Retention.Instance.DebugSecretNow()),
            ("Стадия +1", () => Retention.Instance.DebugStageUp()),
            ("Следующий праздник", () => Retention.Instance.DebugNextEvent()),
            ("Подарок снова", () => Retention.Instance.DebugGiftAgain()),
            ("Знакомство заново", () => Retention.Instance.DebugIntroAgain()),
            ("Сбросить Очаг", () => Retention.Instance.DebugReset()),
        };
        for (int i = 0; i < items.Length; i++)
        {
            var (label, act) = items[i];
            float x = i % 2 == 0 ? -220f : 220f, y = -40f - (i / 2) * 132f;
            RetUi.Button(body, "B" + i, label, label, new Vector2(0.5f, 1f), new Vector2(x, y), new Vector2(400f, 126f), () => act(), false, 32f);
        }
        var info = RetUi.Text(body, "Info", "", "", 26f, Color.white, new Vector2(0.5f, 0f), new Vector2(0f, 110f), new Vector2(880f, 150f));
        Retention.Changed += () =>
        {
            if (info == null || !Retention.Active) return;
            RetState s = Retention.Instance.S;
            string t = $"Дата: {DayClock.DateOf(Retention.Instance.core.Today):dd.MM.yyyy} (сдвиг {DayClock.DebugOffset}), серия {s.streak}, щиты {s.shields}, угольки {s.embers}, тепло {s.warmth}, стадия {s.stage + 1}, коробков {Retention.Instance.core.BoxesOwned}, уровень дня #{Retention.Instance.TodayDailyIndex}";
            RetUi.Set(info, t, t);
        };
    }
}
#endif
