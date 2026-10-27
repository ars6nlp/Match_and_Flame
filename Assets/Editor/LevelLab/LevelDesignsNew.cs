using UnityEngine;

/// <summary>Новые уровни 22–26 (экран «Уровень 23…27») — данные в том же формате, что 17–21 (центры видимых палочек).</summary>
public static partial class LevelDesigns
{
    // ---------------------------------------------------------------- 22. Солнце (отдых, зрелище, без механик)
    // Двенадцать лучей по кругу через 30° (исключение из правила 0/90/180/270: круговой рисунок), огонь в каждом луче идёт
    // наружу, пламя по левому краю поджигает соседний луч против часовой стрелки — огонь обходит солнце по кругу.
    // Нижнего луча нет: его место — слот для спички игрока. Верхний луч золотой.
    private static Level L22()
    {
        float r = Env("DESIGN_R", 1.625f);
        Vector2 c = new Vector2(0f, Env("DESIGN_CY", 0.5f));
        var l = new Level
        {
            index = 22, difficulty = 1, name = "Level_22", titleRU = "Солнце", titleEN = "Sun", free = false,
            idea = "Двенадцать лучей солнца, одного не хватает. Вставь свою спичку на место нижнего луча — огонь обойдёт солнце по кругу и вспыхнет золотой луч наверху.",
            player = S(-1f, -3.5f, 0f),
        };
        for (int i = 0; i < 12; i++)
        {
            float a = 30f * i;
            Vector2 p = c + r * Dir(a);
            Match m = S(p.x, p.y, a);
            if (i == 9) { l.slotCenter = m.center; l.slotRot = a; continue; } // 270° — место игрока
            if (i == 3) m.Gold();
            l.matches.Add(m);
        }
        l.solutionCenter = l.slotCenter; l.solutionRot = l.slotRot;
        l.solutionText = "Перетащить спичку в слот на месте нижнего луча.";
        return l;
    }

    // ---------------------------------------------------------------- 23. Лифт (иммунные + маятник)
    // Вход внизу справа. Над ним качается вверх-вниз золотая иммунная «кабина» (±0.75, период 3.5 с): в нижней точке она
    // в 0.5 над входом и загорается от него (лесенка), в верхней — в 0.5 под голубой башней и поджигает её.
    // Башня (иммунная) → флажок → перекладина → лесенка вниз по левой стороне. Своей спичкой кабину и башню не зажечь.
    private static Level L23()
    {
        var l = new Level
        {
            index = 23, difficulty = 2, name = "Level_23", titleRU = "Лифт", titleEN = "Lift", free = true,
            idea = "Голубую башню не зажечь своей спичкой — огонь туда отвезёт золотая кабина лифта. Зажги вход так, чтобы кабина была внизу, — наверху она подожжёт башню, и огонь обойдёт арку.",
            player = S(-0.75f, -3.5f, 0f),
            hintEN = "Light the entry\nwhen the lift is down", hintRU = "Зажги вход,\nкогда лифт внизу",
            hintPosition = new Vector2(-1f, -2.125f), hintWidth = 2.4f, hintFontSize = 2.1f,
        };
        l.matches.Add(S(1.25f, -2.5f, 0f));                                   // вход
        Match lift = S(1.25f, -1.25f, 0f).Gold().Immune();                    // кабина: ±0.5 по вертикали
        lift.Osc(new Vector3(0f, 1f, 0f), 0.75f, 2f * Mathf.PI / 3.5f);
        l.matches.Add(lift);
        foreach (float y in new[] { 0f, 0.5f, 1f }) l.matches.Add(S(1.25f, y, 0f).Immune()); // башня
        l.matches.Add(S(1.25f, 2.125f, 90f).Immune());                       // флажок: Т-стык к верху башни
        l.matches.Add(S(0.125f, 2.25f, 180f));                                   // перекладина
        l.matches.Add(S(-0.75f, 1.75f, 180f));                                 // лесенка вниз
        foreach (float y in new[] { 1.25f, 0.75f, 0.25f, -0.25f }) l.matches.Add(S(-1.25f, y, 180f));
        Match sol = S(1.25f, -3f, 0f);      // под входом, лесенка
        l.solutionCenter = sol.center; l.solutionRot = 0f; l.solutionPhase = 0f;
        l.solutionText = "Горизонтально под входом, огнём вправо, когда кабина опускается: вход загорится, пока она внизу.";
        return l;
    }

    // ---------------------------------------------------------------- 24. Полки (два крана не в такт, зонтики)
    // Верхняя балка (вход) под краном 1 лесенкой поджигает золотую балку (иммунную) под краном 2, та — две «этажерки»
    // полок с боковыми стойками. Краны капают с одним интервалом, но со сдвигом фазы: балки должны догореть каждая
    // между своими каплями. Когда балки сгорят, капли пролетают сквозь них — полки под струями прикрывают зонтики.
    // Если вход потушит капля уже после передачи огня, золото попадает под каплю крана 2 (фазы подобраны) — золото
    // без 3★ не сгорает.
    private static Level L24()
    {
        var l = new Level
        {
            index = 24, difficulty = 2, name = "Level_24", titleRU = "Полки", titleEN = "Shelves", free = true,
            idea = "Два крана капают с одним ритмом, но не в такт. Зажги верхнюю балку так, чтобы и она, и золотая балка под вторым краном сгорели между каплями. Полки внизу прикрыты зонтиками.",
            player = S(1f, -3.625f, 0f),
        };
        l.matches.Add(S(0f, 1.5f, 180f));                        // балка-вход под краном 1
        l.matches.Add(S(-0.875f, 1f, 180f).Gold().Immune());     // золотая балка под краном 2: лесенка от входа
        foreach (float y in new[] { 0.5f, 0f, -0.5f }) l.matches.Add(S(0.125f, y, 180f));                  // верхняя этажерка
        l.matches.Add(S(1.25f, -0.125f, 90f));                                                             // её стойка
        foreach (float y in new[] { -1f, -1.5f, -2f, -2.5f }) l.matches.Add(S(-0.75f, y, 180f));           // нижняя этажерка
        l.matches.Add(S(-1.25f, -3f, 180f));                                                               // нижняя полка — шаг влево
        l.matches.Add(S(0.375f, -2.125f, 90f));                                                            // стойка нижней этажерки
        float interval = Env("DESIGN_I", 4f), tapB = Env("DESIGN_B", 2.4f);
        l.mechs.Add(Dripper(0.25f, 2.2f).Set("interval", interval).Set("startDelay", 1f).Set("wetDuration", 0.4f));
        l.mechs.Add(Dripper(-1.375f, 2.2f).Set("interval", interval).Set("startDelay", tapB).Set("wetDuration", 0.4f));
        l.mechs.Add(Shelter(0.5f, 1f));         // под балкой-входом: над верхней этажеркой
        l.mechs.Add(Shelter(-1.375f, 0f));      // под золотой балкой: над нижней этажеркой
        Match sol = S(1.125f, 1.75f, 90f);      // справа от начала балки-входа, вне струй
        l.solutionCenter = sol.center; l.solutionRot = 90f; l.solutionPhase = Env("DESIGN_PH", 2.2f);
        l.solutionText = "Вертикально справа от правого конца верхней балки (огнём вверх), сразу после капли крана 1 — тогда и золотая балка успевает сгореть до капли крана 2.";
        return l;
    }

    // ---------------------------------------------------------------- 25. Сквозняк (два вентилятора: тяга и задув)
    // Справа «труба»: вход и голубая спичка соосно, зазор 0.2 — огонь перелетает его только на ветру вентилятора A
    // (дует вверх по трубе, циклами). Голубая поджигает золотую перекладину (Т-стык к середине). Над серединой
    // перекладины вентилятор B дует вниз: пока он работает, пламя перекладины под ним задувает (сила 124% порога),
    // а полкам ниже достаётся меньше 40% — их огонь только клонит. От перекладины огонь спускается «ёлочкой» полок.
    private static Level L25()
    {
        var l = new Level
        {
            index = 25, difficulty = 3, name = "Level_25", titleRU = "Сквозняк", titleEN = "Draft", free = true,
            idea = "Вентилятор внизу перебрасывает огонь через зазор в трубе, вентилятор над золотой перекладиной её задувает. Подожги вход так, чтобы труба догорела на ветру нижнего, а перекладина — пока верхний стоит.",
            player = S(0.75f, -3.625f, 0f),
        };
        float aOn = Env("DESIGN_AON", 2f), aOff = Env("DESIGN_AOFF", 2f), aPh = Env("DESIGN_APH", 0f);
        float bOn = Env("DESIGN_BON", 2f), bOff = Env("DESIGN_BOFF", 2f), bPh = Env("DESIGN_BPH", 1.5f);
        l.mechs.Add(FanAt(1.375f, -2.8f, 90f).Set("mode", Fan.Mode.Cycle).Set("onDuration", aOn).Set("offDuration", aOff).Set("cyclePhase", aPh)   // сопло (1.375, −2.11)
            .Set("strength", Env("DESIGN_AS", 6f)).Set("farStrength", 0.5f).Set("zoneLength", 2.2f).Set("zoneWidth", 0.6f)
            .Set("extinguishStrength", 50f).Set("extinguishTime", 0.35f));
        l.matches.Add(S(1.375f, -1.125f, 90f));                    // труба: вход
        l.matches.Add(S(1.375f, 0.625f, 90f).Immune());            // труба: верх, зазор 0.2 — только по ветру
        l.matches.Add(S(0.25f, 0.625f, 180f).Gold().Immune());     // золотая перекладина: Т-стык к середине верха трубы
        l.mechs.Add(FanAt(Env("DESIGN_BX", 0.5f), 1.75f, 270f).Set("mode", Fan.Mode.Cycle).Set("onDuration", bOn).Set("offDuration", bOff).Set("cyclePhase", bPh)   // сопло (0.5, 1.06)
            .Set("strength", 8f).Set("farStrength", 0f).Set("zoneLength", 1.2f).Set("zoneWidth", 0.6f)
            .Set("extinguishStrength", Env("DESIGN_BT", 3.3f)).Set("extinguishTime", 0.35f));
        float[] ys = { 0.125f, -0.375f, -0.875f, -1.375f, -1.875f, -2.375f, -2.875f };
        for (int i = 0; i < ys.Length; i++) l.matches.Add(S(i % 2 == 0 ? -0.25f : -1f, ys[i], 180f)); // «ёлочка» полок
        Match sol = S(1.875f, -1.125f, 90f);   // справа от входа, вне потока
        l.solutionCenter = sol.center; l.solutionRot = 90f; l.solutionPhase = Env("DESIGN_PH", 2f);
        l.solutionText = "Вертикально справа от входа (вне потока), огнём вверх — так, чтобы вход догорел, пока нижний вентилятор дует, а перекладина горела, пока верхний стоит.";
        return l;
    }
    // ---------------------------------------------------------------- 26. Финал (капля, маятник-зонтик, ветер, иммунные, два золота)
    // Внизу горизонтальная «труба»: вход и голубая спичка соосно, зазор 0.2 — огонь перелетает его только на ветру
    // вентилятора слева (циклами). Над входом кран, между ними качается зонтик: вход должен сгореть целиком между
    // каплями, пока зонтик в стороне, — иначе капля гасит его, и вспышки на конце не будет (огонь дальше трубы не уходит).
    // Голубая поджигает две золотые стойки (иммунные, своей спичкой не зажечь), от них огонь лесенкой поднимается
    // вверх-влево и уходит в стопку полок наверху. Золото горит только если вход догорел на ветру — то есть в 3★.
    private static Level L26()
    {
        var l = new Level
        {
            index = 26, difficulty = 3, name = "Level_26", titleRU = "Финал", titleEN = "Finale", free = true,
            idea = "Всё вместе: вход под краном, над ним качается зонтик, огонь перелетает зазор трубы только на ветру. Подожги вход, когда зонтик над ним и вентилятор вот-вот подует: вход догорит, вспышка перелетит к голубой, и огонь поднимется через два золота наверх.",
            player = S(1f, -3.625f, 0f),
        };
        float aOn = Env("DESIGN_AON", 1.5f), aOff = Env("DESIGN_AOFF", 2.5f), aPh = Env("DESIGN_APH", 0f);
        l.mechs.Add(FanAt(-1.875f, -2.25f, 0f).Set("mode", Fan.Mode.Cycle).Set("onDuration", aOn).Set("offDuration", aOff).Set("cyclePhase", aPh)   // сопло (−1.185, −2.25)
            .Set("strength", Env("DESIGN_AS", 6f)).Set("farStrength", 0.5f).Set("zoneLength", 2.4f).Set("zoneWidth", 0.6f)
            .Set("extinguishStrength", 50f).Set("extinguishTime", 0.35f));
        l.matches.Add(S(-0.125f, -2.25f, 0f));                     // труба: вход (огонь вправо)
        l.matches.Add(S(1.625f, -2.25f, 0f).Immune());             // труба: голубая, зазор 0.2 — только по ветру
        l.matches.Add(S(1.625f, -1.125f, 90f).Gold().Immune());    // первая золотая: Т-стык над серединой голубой
        l.matches.Add(S(1.125f, -0.5f, 90f).Gold().Immune());      // вторая золотая: лесенка
        l.matches.Add(S(0.625f, 0.125f, 90f));                     // лесенка вверх-влево
        l.matches.Add(S(0.125f, 0.75f, 90f));
        foreach (Vector2 p in new[] { new Vector2(-1f, 1.375f), new Vector2(-1.5f, 1.875f), new Vector2(-1f, 2.375f) })
            l.matches.Add(S(p.x, p.y, 0f));                        // стопка полок наверху
        float tapX = -0.75f;
        l.mechs.Add(Dripper(tapX, -0.25f).Set("interval", Env("DESIGN_I", 4f)).Set("startDelay", Env("DESIGN_D", 0f)).Set("wetDuration", 0.4f));
        // Зонтик качается со смещением вправо: над струёй он полцикла подряд (4 с из 8) и ловит каждую вторую каплю
        l.mechs.Add(Shelter(tapX + Env("DESIGN_SB", 0.4f), -1.25f).Osc(new Vector3(1f, 0f, 0f), Env("DESIGN_SW", 0.6f), 2f * Mathf.PI / Env("DESIGN_SP", 8f)));
        Match sol = S(-0.125f, -2.75f, 0f);    // под входом, лесенка
        l.solutionCenter = sol.center; l.solutionRot = 0f; l.solutionPhase = Env("DESIGN_PH", 2.2f);
        l.solutionText = "Горизонтально под входом, огнём вправо — в момент, когда зонтик закрывает вход от капель, а нижний вентилятор включится к концу его горения.";
        return l;
    }

    private static Level Stub(int index) => new Level { index = index, name = "Level_" + index, titleRU = "—", titleEN = "-", free = true, player = S(0f, -3.5f, 0f) };

    private static float Env(string name, float fallback)
    {
        string v = System.Environment.GetEnvironmentVariable(name);
        return string.IsNullOrEmpty(v) ? fallback : float.Parse(v, System.Globalization.CultureInfo.InvariantCulture);
    }
}
