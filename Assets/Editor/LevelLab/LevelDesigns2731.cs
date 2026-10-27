using UnityEngine;

/// <summary>Новые уровни 27–31 (экран «Уровень 28…32») — та же таблица, что 17–26 (центры видимых палочек, поворот — куда идёт огонь).</summary>
public static partial class LevelDesigns
{
    // ---------------------------------------------------------------- 27. Мельница (новая глава, без механик)
    // Четыре лопасти по две спички — вертушка, где огонь идёт по кругу от лопасти к лопасти. Одной спички
    // в нижней лопасти нет: круг разомкнут, у цепочки один вход (левая нижняя лопасть). С неё огонь обходит все
    // лопасти и спускается по «башне» из трёх перекладин. Вторая спичка цепочки — золотая и голубая: своей спичкой
    // её не зажечь, она горит, только если начать с входа (тогда сгорает всё). Зажжёшь лопасть в середине — часть
    // мельницы останется.
    private static Level L27()
    {
        var l = new Level
        {
            index = 27, difficulty = 2, name = "Level_27", titleRU = "Мельница", titleEN = "Windmill", free = true,
            idea = "Вертушка из семи лопастей и башня из перекладин — огонь обходит лопасти по кругу и спускается вниз. Круг разомкнут: найди лопасть, с которой начинается цепочка, — тогда загорится и золотая.",
            player = S(0.75f, -3.625f, 0f),
        };
        l.matches.Add(S(-1.375f, -0.625f, 0f));                 // вход: левая нижняя лопасть
        l.matches.Add(S(-1.25f, 0.5f, 270f).Gold().Immune());   // золотая: только от входа
        l.matches.Add(S(-0.75f, 1.5f, 270f));
        l.matches.Add(S(0.375f, 1.375f, 180f));
        l.matches.Add(S(1.375f, 0.875f, 180f));
        l.matches.Add(S(1.25f, -0.25f, 90f));
        l.matches.Add(S(0.75f, -1.25f, 90f));
        foreach (float y in new[] { -1.75f, -2.25f, -2.75f }) l.matches.Add(S(-0.5f, y, 180f)); // башня
        Match sol = S(-1.375f, -1.125f, 0f);   // под входом, лесенка
        l.solutionCenter = sol.center; l.solutionRot = 0f;
        l.solutionText = "Горизонтально под левой нижней лопастью (входом), огнём вправо.";
        return l;
    }

    // ---------------------------------------------------------------- 28. Волна (иммунные + вода)
    // Справа внизу — вход, от него «мост» из трёх голубых спичек поднимается лесенкой влево к золотой стойке под краном.
    // Голубые своей спичкой не зажечь: начать можно только со входа. Стойка горит, только если огонь пришёл к ней между
    // каплями (кран каждые 4 с), и поджигает «волну» из семи стоек. Волну можно зажечь и с моста, но без золота.
    private static Level L28()
    {
        var l = new Level
        {
            index = 28, difficulty = 2, name = "Level_28", titleRU = "Волна", titleEN = "Wave", free = true,
            idea = "Голубой мост не зажечь своей спичкой — огонь пойдёт по нему только от входа справа внизу. На вершине моста золотая стойка под краном: подожги вход так, чтобы огонь дошёл до неё сразу после капли, и он побежит по волне.",
            player = S(-1f, -3.625f, 0f),
        };
        l.matches.Add(S(0.5f, -2.5f, 0f));                       // вход
        l.matches.Add(S(-0.5f, -2f, 0f).Immune());               // мост
        l.matches.Add(S(-1.25f, -1.5f, 0f).Immune());
        l.matches.Add(S(-2.125f, -0.375f, 270f).Immune());
        l.matches.Add(S(-1.625f, 0.5f, 270f).Gold().Immune());   // золотая стойка под краном
        float[] fx = { -1.125f, -0.625f, -0.125f, 0.375f, 0.875f, 1.375f, 1.875f };
        float[] fy = { -0.375f, 0.125f, 0.375f, -0.125f, -0.625f, -0.375f, -0.125f };
        for (int i = 0; i < fx.Length; i++) l.matches.Add(S(fx[i], fy[i], 270f)); // волна
        l.mechs.Add(Dripper(-1.625f, 1.95f).Set("interval", Env("DESIGN_I", 4f)).Set("startDelay", Env("DESIGN_D", 1f)).Set("wetDuration", 0.4f));
        Match sol = S(0.5f, -3f, 0f);   // под входом, лесенка
        l.solutionCenter = sol.center; l.solutionRot = 0f; l.solutionPhase = Env("DESIGN_PH", 2f);
        l.solutionText = "Горизонтально под входом, огнём вправо — так, чтобы огонь дошёл по голубому мосту до золотой стойки сразу после капли.";
        return l;
    }
    // ---------------------------------------------------------------- 29. Подъёмник (ветер + маятник)
    // Справа «труба»: вход и голубая спичка соосно, зазор 0.2 — огонь перелетает его только на ветру вентилятора.
    // Голубая поджигает голубую кабину-подъёмник, только когда та проходит середину своего хода (качается
    // вверх-вниз). Внизу хода кабина передаёт огонь золотой перекладине, та — стойке и «забору» внизу.
    // Поджог должен совпасть и с ветром, и с положением кабины.
    private static Level L29()
    {
        var l = new Level
        {
            index = 29, difficulty = 3, name = "Level_29", titleRU = "Подъёмник", titleEN = "Hoist", free = true,
            idea = "Огонь перелетает зазор в трубе только на ветру. Наверху его ловит качающаяся золотая кабина — только когда она проходит мимо голубой спички. Внизу хода кабина передаёт огонь забору.",
            player = S(-0.75f, -3.625f, 0f),
            hintEN = "The cabin catches fire\nonly when passing by", hintRU = "Кабина ловит огонь,\nкогда проезжает мимо",
            hintPosition = new Vector2(-1f, 1.625f), hintWidth = 2f, hintFontSize = 1.9f,
        };
        l.mechs.Add(FanAt(1.25f, -2.8f, 90f).Set("mode", Fan.Mode.Cycle).Set("onDuration", Env("DESIGN_AON", 3f)).Set("offDuration", Env("DESIGN_AOFF", 1f))
            .Set("cyclePhase", Env("DESIGN_APH", 0f)).Set("strength", 6f).Set("farStrength", 0.5f).Set("zoneLength", 2.2f).Set("zoneWidth", 0.6f)
            .Set("extinguishStrength", 50f).Set("extinguishTime", 0.35f));
        l.matches.Add(S(1.25f, -1.125f, 90f));                    // труба: вход
        l.matches.Add(S(1.25f, 0.625f, 90f).Immune());            // труба: верх, зазор 0.2 — только по ветру
        Match cabin = S(0.125f, 0.625f, 180f).Immune();           // кабина: качается вверх-вниз
        cabin.Osc(new Vector3(0f, 1f, 0f), Env("DESIGN_AMP", 0.75f), 2f * Mathf.PI / Env("DESIGN_SP", 4f));
        l.matches.Add(cabin);
        l.matches.Add(S(-0.625f, Env("DESIGN_R1", -0.375f), 180f).Gold().Immune()); // золотая перекладина: огонь к ней — только от кабины внизу хода
        l.matches.Add(S(0.25f, Env("DESIGN_R1", -0.375f) - 1.125f, 90f)); // стойка
        foreach (float x in new[] { -0.25f, -0.75f, -1.25f, -1.75f }) l.matches.Add(S(x, Env("DESIGN_R1", -0.375f) - 1.625f, 90f)); // забор
        Match sol = S(1.75f, -1.125f, 90f);   // справа от входа, вне потока
        l.solutionCenter = sol.center; l.solutionRot = 90f; l.solutionPhase = Env("DESIGN_PH", 2f);
        l.solutionText = "Вертикально справа от входа (вне потока), огнём вверх — чтобы вход догорел на ветру, а кабина в этот момент проходила мимо голубой спички.";
        return l;
    }
    // ---------------------------------------------------------------- 30. Карусель (вода + ветер + вертушка)
    // Внизу «карусель»: шесть спичек вертушки (половина фигуры, которую поворот на 90° переводит в себя) и спица внутри —
    // огонь обходит её по кругу и уходит внутрь.
    // Вход слева — вертикальная спичка; она поджигает золотую перекладину (голубая: своей спичкой не зажечь), а та — карусель.
    // Перекладина горит справа налево: сначала под вентилятором (дует вниз и задувает пламя, пока работает),
    // потом под краном (капля гасит). Подожги вход так, чтобы перекладина прошла вентилятор, пока он стоит, и кран — между каплями.
    private static Level L30()
    {
        var l = new Level
        {
            index = 30, difficulty = 3, name = "Level_30", titleRU = "Карусель", titleEN = "Carousel", free = true,
            idea = "Золотая перекладина горит сначала под вентилятором, потом под краном. Подожги вход так, чтобы вентилятор в это время стоял, а капля упала до или после — и огонь побежит по карусели.",
            player = S(0.75f, -3.625f, 0f),
            hintEN = "Wind and drops\nput out the fire", hintRU = "Ветер и капли\nгасят огонь",
            hintPosition = new Vector2(Env("DESIGN_HX", 1.375f), Env("DESIGN_HY", 0.875f)), hintWidth = 1.9f, hintFontSize = 1.9f,
        };
        l.matches.Add(S(-1.75f, -0.25f, 270f));                   // вход
        l.matches.Add(S(-0.625f, 0.5f, 180f).Gold().Immune());     // золотая перекладина: под вентилятором и краном
        l.matches.Add(S(0f, 0f, 180f));                            // карусель: верх
        l.matches.Add(S(0.75f, -0.5f, 180f));
        l.matches.Add(S(1.875f, -0.875f, 90f));                    // правый край
        l.matches.Add(S(1.375f, -1.625f, 90f));
        l.matches.Add(S(1f, -2.75f, 0f));                          // низ
        l.matches.Add(S(0.25f, -2.25f, 0f));
        l.matches.Add(S(-0.625f, -1.75f, 0f));                     // спица внутрь
        l.mechs.Add(Dripper(-1.25f, Env("DESIGN_TY", 1.3f)).Set("interval", Env("DESIGN_I", 4f)).Set("startDelay", Env("DESIGN_D", 0.5f)).Set("wetDuration", 0.4f));
        l.mechs.Add(FanAt(-0.125f, Env("DESIGN_FY", 1.625f), 270f).Set("mode", Fan.Mode.Cycle).Set("onDuration", Env("DESIGN_BON", 1.5f)).Set("offDuration", Env("DESIGN_BOFF", 2.5f))
            .Set("cyclePhase", Env("DESIGN_BPH", 0f)).Set("strength", 8f).Set("farStrength", 0f).Set("zoneLength", 1.2f).Set("zoneWidth", 0.6f)
            .Set("extinguishStrength", Env("DESIGN_BT", 3.3f)).Set("extinguishTime", 0.35f));
        Match sol = S(-2.25f, 0.625f, 270f);   // слева от входа, вплотную
        l.solutionCenter = sol.center; l.solutionRot = 270f; l.solutionPhase = Env("DESIGN_PH", 0.4f);
        l.solutionText = "Вертикально слева от входа, огнём вниз — в момент, чтобы перекладина горела под вентилятором, пока он стоит, а под краном — между каплями.";
        return l;
    }
    // ---------------------------------------------------------------- 31. Финал (всё вместе: ветер, маятник, вода, иммунные, два золота)
    // Справа «труба»: вход и голубая спичка соосно, зазор 0.2 — огонь перелетает его только на ветру вентилятора.
    // Голубая поджигает голубую кабину-маятник, когда та проходит мимо; внизу хода кабина передаёт огонь золотой
    // перекладине под краном. Перекладина горит справа налево: правый конец поджигает стойку со столбиком, левый (под краном) —
    // вторую золотую с забором. Кран капает раз в 8 с — на один из двух проходов кабины; капля гасит перекладину до левого конца,
    // и вторая золотая не загорается. Оба золота голубые: своей спичкой их не зажечь, они горят, только если совпали ветер,
    // кабина и капли — то есть в 3★.
    private static Level L31()
    {
        var l = new Level
        {
            index = 31, difficulty = 3, name = "Level_31", titleRU = "Финал", titleEN = "Finale", free = true,
            idea = "Всё вместе: ветер перебрасывает огонь через зазор в трубе, кабина подхватывает его, проезжая мимо, а золотая перекладина под краном должна сгореть между каплями. Тогда загорятся оба золота и забор.",
            player = S(-0.75f, -3.625f, 0f),
            hintEN = "Wind, cabin and drops:\ncatch the moment", hintRU = "Ветер, кабина и капли:\nпоймай момент",
            hintPosition = new Vector2(Env("DESIGN_HX", -1.125f), Env("DESIGN_HY", 2f)), hintWidth = 2.2f, hintFontSize = 1.9f,
        };
        float r1 = Env("DESIGN_R1", -0.375f), bx = Env("DESIGN_BX", -0.625f);
        l.mechs.Add(FanAt(1.25f, -2.8f, 90f).Set("mode", Fan.Mode.Cycle).Set("onDuration", Env("DESIGN_AON", 3f)).Set("offDuration", Env("DESIGN_AOFF", 1f))
            .Set("cyclePhase", Env("DESIGN_APH", 0f)).Set("strength", 6f).Set("farStrength", 0.5f).Set("zoneLength", 2.2f).Set("zoneWidth", 0.6f)
            .Set("extinguishStrength", 50f).Set("extinguishTime", 0.35f));
        l.matches.Add(S(1.25f, -1.125f, 90f));                    // труба: вход
        l.matches.Add(S(1.25f, 0.625f, 90f).Immune());            // труба: верх, зазор 0.2 — только по ветру
        Match cabin = S(0.125f, 0.625f, 180f).Immune();           // кабина: качается вверх-вниз
        cabin.Osc(new Vector3(0f, 1f, 0f), Env("DESIGN_AMP", 0.75f), 2f * Mathf.PI / Env("DESIGN_SP", 4f));
        l.matches.Add(cabin);
        l.matches.Add(S(bx, r1, 180f).Gold().Immune());           // золотая перекладина под краном: горит справа налево
        l.matches.Add(S(bx + 0.875f, r1 - 1.125f, 90f));          // стойка: от правого конца перекладины
        l.matches.Add(S(bx + 0.375f, r1 - 1.625f, 90f));          // её столбик
        l.matches.Add(S(bx - 0.625f, r1 - 1.125f, 90f).Gold().Immune()); // вторая золотая: только от левого конца перекладины (после крана)
        l.matches.Add(S(bx - 1.125f, r1 - 1.625f, 90f));          // её забор
        l.matches.Add(S(bx - 1.625f, r1 - 1.625f, 90f));
        l.mechs.Add(Dripper(bx - 0.75f, r1 + 0.785f).Set("interval", Env("DESIGN_I", 8f)).Set("startDelay", Env("DESIGN_D", 4f)).Set("wetDuration", 0.4f));
        Match sol = S(1.75f, -1.125f, 90f);   // справа от входа, вне потока
        l.solutionCenter = sol.center; l.solutionRot = 90f; l.solutionPhase = Env("DESIGN_PH", 2.5f);
        l.solutionText = "Вертикально справа от входа (вне потока), огнём вверх — чтобы вход догорел на ветру, кабина в этот момент проходила мимо голубой спички, а перекладина горела между каплями.";
        return l;
    }
}
