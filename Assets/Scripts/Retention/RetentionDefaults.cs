using System.Collections.Generic;
using UnityEngine;

/// <summary>Контент «Очага» по умолчанию. Вызывается при создании RetentionSettings.asset и из его контекстного меню.</summary>
public static class RetentionDefaults
{
    public static void Fill(RetentionSettings s)
    {
        s.stages = new List<RetStage>
        {
            Stage("Искра", "Spark", 0, 0, -1, 0.42f, "Привет! Я совсем маленький, но очень тёплый.", "Hi! I'm tiny, but very warm."),
            Stage("Язычок", "Flicker", 120, 2, 0, 0.52f, "Смотри, я подрос! Спасибо за угольки.", "Look, I've grown! Thanks for the embers."),
            Stage("Огонёк", "Little flame", 360, 2, 1, 0.62f, "Теперь мне хватает сил согреть всю комнату.", "Now I can warm the whole room."),
            Stage("Костёр", "Campfire", 800, 3, 2, 0.72f, "Я трещу, как настоящий костёр!", "I crackle like a real campfire!"),
            Stage("Факел", "Torch", 1450, 3, 3, 0.82f, "С факелом не страшны никакие тёмные коридоры.", "No dark corridor can scare a torch."),
            Stage("Маяк", "Lighthouse", 2300, 4, 4, 0.92f, "Мой свет видно с другого берега.", "My light reaches the far shore."),
            Stage("Звезда", "Star", 3400, 4, 5, 1.02f, "Я сияю, как звезда над крышей.", "I shine like a star above the roof."),
            Stage("Солнце", "Sun", 4800, 5, 6, 1.12f, "Я — маленькое солнце этого дома!", "I'm the little sun of this home!"),
        };

        s.outfits = new List<RetOutfit>
        {
            Outfit("Бантик", "Bow", "out_bow", "стадия Язычок", "Flicker stage"),
            Outfit("Шарфик", "Scarf", "out_scarf", "стадия Огонёк", "Little flame stage", 0.05f, 0.4f),
            Outfit("Очки", "Glasses", "out_glasses", "стадия Костёр", "Campfire stage", 0.16f, 0.34f),
            Outfit("Цилиндр", "Top hat", "out_tophat", "стадия Факел", "Torch stage"),
            Outfit("Наушники", "Headphones", "out_headphones", "стадия Маяк", "Lighthouse stage", 0.3f, 0.44f),
            Outfit("Корона", "Crown", "out_crown", "стадия Звезда", "Star stage"),
            Outfit("Звёздный нимб", "Star halo", "out_halo", "стадия Солнце", "Sun stage", 0.5f, 0.42f),
            Outfit("Новогодний колпак", "Winter hat", "out_santa", "праздник: Новый год", "event: New Year"),
            Outfit("Сердечко", "Heart", "out_heart", "праздник: Тёплые сердца", "event: Warm Hearts"),
            Outfit("Тюбетейка", "Tubeteika", "out_tubeteika", "праздник: Наурыз", "event: Nauryz"),
            Outfit("Венок", "Flower wreath", "out_wreath", "праздник: Весна", "event: Spring"),
            Outfit("Солнечные очки", "Sunglasses", "out_sunglasses", "праздник: Лето", "event: Summer", 0.16f, 0.34f),
            Outfit("Перо феникса", "Phoenix feather", "out_feather", "праздник: День огня", "event: Fire Day"),
            Outfit("Осенний венок", "Leaf crown", "out_leaves", "праздник: Листопад", "event: Leaf Fall"),
            Outfit("Шляпа-тыква", "Pumpkin hat", "out_pumpkin", "праздник: Хэллоуин", "event: Halloween"),
            Outfit("Шляпа волшебника", "Wizard hat", "out_wizard", "секретный коробок", "secret box"),
            Outfit("Вязаная шапочка", "Beanie", "out_beanie", "секретный коробок", "secret box"),
            Outfit("Праздничный колпак", "Party hat", "out_party", "секретный коробок", "secret box"),
            Outfit("Капюшон-капля", "Drop hood", "out_drop", "неделя: Вода", "week: Water"),
            Outfit("Вертушка", "Pinwheel", "out_pinwheel", "неделя: Ветер", "week: Wind"),
            Outfit("Монокль", "Monocle", "out_monocle", "неделя: Золото", "week: Gold", 0.16f, 0.26f),
            Outfit("Молния", "Lightning", "out_bolt", "неделя: Быстрый огонь", "week: Quick fire"),
            Outfit("Цепочка", "Chain", "out_chain", "неделя: Цепь", "week: Chain", 0.02f, 0.4f),
            Outfit("Заколка-звезда", "Star clip", "out_starclip", "неделя: Звёзды", "week: Stars"),
            Outfit("Часики", "Pocket watch", "out_watch", "неделя: Маятник", "week: Pendulum", 0.04f, 0.3f),
            Outfit("Ночной колпак", "Nightcap", "out_nightcap", "неделя: Тишина", "week: Silence"),
        };

        // Места — в долях сцены Очага (1000×1000, x слева, y снизу); ширина — доля сцены
        s.decor = new List<RetDecor>
        {
            Decor("Коврик", "Rug", 8, 0, "dec_rug", 0.5f, 0.055f, 0.36f, 2, 0),
            Decor("Полка", "Shelf", 12, 0, "dec_shelf", 0.15f, 0.88f, 0.22f, 0, 0),
            Decor("Кресло", "Armchair", 16, 0, "dec_chair", 0.87f, 0.2f, 0.24f, 3, 5),
            Decor("Портрет", "Portrait", 20, 0, "dec_portrait", 0.86f, 0.74f, 0.17f, 0, 0),
            Decor("Поленница", "Woodpile", 26, 0, "dec_wood", 0.13f, 0.14f, 0.19f, 3, 0),
            Decor("Растение", "Plant", 0, 300, "dec_plant", 0.06f, 0.3f, 0.12f, 3, 3),
            Decor("Лампа", "Lamp", 0, 450, "dec_lamp", 0.32f, 0.518f, 0.09f, 1, 2),
            Decor("Часы", "Clock", 0, 650, "dec_clock", 0.5f, 0.82f, 0.13f, 0, 1),
            Decor("Шторы", "Curtains", 0, 850, "dec_curtains", 0.17f, 0.66f, 0.27f, 0, 5),
            Decor("Медный чайник", "Copper kettle", 0, 1100, "dec_kettle", 0.68f, 0.518f, 0.09f, 1, 4),
        };

        s.questPool = new List<RetQuest>
        {
            Quest(QuestKind.WinLevels, 2, RetFuel.Cherry, "Пройди {0} уровня", "Win {0} levels"),
            Quest(QuestKind.WinLevels, 4, RetFuel.Cherry, "Пройди {0} уровня", "Win {0} levels"),
            Quest(QuestKind.WinStars3, 1, RetFuel.Oak, "Пройди уровень на 3 звезды", "Get 3 stars on a level"),
            Quest(QuestKind.WinStars3, 3, RetFuel.Oak, "3 звезды на {0} уровнях", "3 stars on {0} levels"),
            Quest(QuestKind.BurnMatches, 30, RetFuel.Cherry, "Сожги {0} спичек", "Burn {0} matches"),
            Quest(QuestKind.BurnMatches, 80, RetFuel.Cherry, "Сожги {0} спичек", "Burn {0} matches"),
            Quest(QuestKind.ChainInLevel, 10, RetFuel.Cherry, "Сожги {0} спичек за один уровень", "Burn {0} matches in one level"),
            Quest(QuestKind.ChainInLevel, 15, RetFuel.Cherry, "Сожги {0} спичек за один уровень", "Burn {0} matches in one level"),
            Quest(QuestKind.BurnGold, 1, RetFuel.Oak, "Сожги золотую спичку", "Burn a golden match"),
            Quest(QuestKind.BurnGold, 3, RetFuel.Oak, "Сожги {0} золотые спички", "Burn {0} golden matches"),
            Quest(QuestKind.WinWater, 1, RetFuel.Pine, "Пройди уровень с краном", "Win a level with a tap"),
            Quest(QuestKind.WinWater, 2, RetFuel.Pine, "Пройди {0} уровня с краном", "Win {0} levels with a tap"),
            Quest(QuestKind.WinWind, 1, RetFuel.Pine, "Пройди уровень с вентилятором", "Win a level with a fan"),
            Quest(QuestKind.WinWind, 2, RetFuel.Pine, "Пройди {0} уровня с вентилятором", "Win {0} levels with a fan"),
            Quest(QuestKind.WinPendulum, 1, RetFuel.Pine, "Пройди уровень с маятником", "Win a level with a pendulum"),
            Quest(QuestKind.WinImmune, 1, RetFuel.Pine, "Пройди уровень с голубыми спичками", "Win a level with blue matches"),
            Quest(QuestKind.WinNoRestart, 2, RetFuel.Cherry, "Пройди {0} уровня без «Заново»", "Win {0} levels without Restart"),
            Quest(QuestKind.WinFast, 12, RetFuel.Cherry, "Пройди уровень быстрее {0} с", "Win a level in under {0} s"),
            Quest(QuestKind.PlayDaily, 1, RetFuel.Cherry, "Пройди уровень дня", "Win the daily level"),
            Quest(QuestKind.Daily3Stars, 1, RetFuel.Oak, "Уровень дня на 3 звезды", "Daily level with 3 stars"),
            Quest(QuestKind.FeedHearth, 50, RetFuel.Oak, "Подкинь в очаг {0} угольков", "Feed {0} embers to the hearth"),
            Quest(QuestKind.EarnStars, 6, RetFuel.Oak, "Собери {0} звёзд", "Earn {0} stars"),
            Quest(QuestKind.WinHard, 1, RetFuel.Oak, "Пройди сложный уровень (3 огонька)", "Win a hard level (3 flames)"),
            Quest(QuestKind.WinDifferent, 3, RetFuel.Cherry, "Пройди {0} разных уровня", "Win {0} different levels"),
        };

        s.weekThemes = new List<RetWeekTheme>
        {
            Theme(QuestKind.WinWater, 5, "Неделя воды", "Water week", "Пройди {0} уровней с краном", "Win {0} levels with a tap", 18, new Color(0.45f, 0.8f, 1f)),
            Theme(QuestKind.WinWind, 5, "Неделя ветра", "Wind week", "Пройди {0} уровней с вентилятором", "Win {0} levels with a fan", 19, new Color(0.7f, 0.95f, 0.9f)),
            Theme(QuestKind.BurnGold, 8, "Золотая неделя", "Golden week", "Сожги {0} золотых спичек", "Burn {0} golden matches", 20, new Color(1f, 0.82f, 0.3f)),
            Theme(QuestKind.WinFast, 15, "Быстрый огонь", "Quick fire", "5 побед быстрее {0} с", "5 wins in under {0} s", 21, new Color(1f, 0.5f, 0.25f)),
            Theme(QuestKind.BurnMatches, 150, "Неделя цепей", "Chain week", "Сожги {0} спичек", "Burn {0} matches", 22, new Color(1f, 0.6f, 0.35f)),
            Theme(QuestKind.EarnStars, 15, "Звёздная неделя", "Star week", "Собери {0} звёзд", "Earn {0} stars", 23, new Color(1f, 0.9f, 0.5f)),
            Theme(QuestKind.WinPendulum, 4, "Неделя маятника", "Pendulum week", "Пройди {0} уровня с маятником", "Win {0} levels with a pendulum", 24, new Color(0.85f, 0.7f, 1f)),
            Theme(QuestKind.WinNoRestart, 6, "Тихая неделя", "Quiet week", "Пройди {0} уровней без «Заново»", "Win {0} levels without Restart", 25, new Color(0.6f, 0.65f, 0.95f)),
        };

        string[] setRU = { "Лес", "Море", "Город", "Кухня", "Цирк", "Космос" }, setEN = { "Forest", "Sea", "City", "Kitchen", "Circus", "Space" };
        string[][] itemRU =
        {
            new[] { "Ёлка", "Гриб", "Сова", "Лис" }, new[] { "Волна", "Ракушка", "Кит", "Маяк" }, new[] { "Трамвай", "Фонарь", "Мост", "Башня" },
            new[] { "Чашка", "Пирог", "Чайник", "Пряник" }, new[] { "Мяч", "Шатёр", "Клоун", "Слон" }, new[] { "Луна", "Комета", "Ракета", "Сатурн" },
        };
        string[][] itemEN =
        {
            new[] { "Fir", "Mushroom", "Owl", "Fox" }, new[] { "Wave", "Shell", "Whale", "Lighthouse" }, new[] { "Tram", "Lantern", "Bridge", "Tower" },
            new[] { "Cup", "Pie", "Teapot", "Gingerbread" }, new[] { "Ball", "Tent", "Clown", "Elephant" }, new[] { "Moon", "Comet", "Rocket", "Saturn" },
        };
        s.boxes = new List<RetBox>();
        for (int set = 0; set < 6; set++)
            for (int i = 0; i < 4; i++)
            {
                BoxRarity r = i < 2 ? BoxRarity.Common : i == 2 ? BoxRarity.Rare : set < 2 ? BoxRarity.Common : set == 2 ? BoxRarity.Rare : BoxRarity.Legendary;
                s.boxes.Add(new RetBox { nameRU = $"{setRU[set]}: {itemRU[set][i]}", nameEN = $"{setEN[set]}: {itemEN[set][i]}", rarity = r, set = set, sprite = $"box_{set * 4 + i:00}" });
            }
        string[] sRU = { "Новогодний", "Сердечный", "Наурыз", "Весенний", "Летний", "Огненный", "Осенний", "Тыквенный" };
        string[] sEN = { "New Year", "Hearts", "Nauryz", "Spring", "Summer", "Fire Day", "Autumn", "Pumpkin" };
        for (int i = 0; i < sRU.Length; i++)
            s.boxes.Add(new RetBox { nameRU = sRU[i] + " коробок", nameEN = sEN[i] + " box", rarity = BoxRarity.Rare, set = -1, seasonal = true, sprite = $"box_{24 + i:00}" });

        s.chapterList = new List<RetChapter>
        {
            Chapter(0, 5, "Первые искры", "First sparks", new Color(1f, 0.62f, 0.25f), 0),
            Chapter(6, 11, "Качели", "Swings", new Color(0.95f, 0.75f, 0.35f), 1),
            Chapter(12, 16, "Ловкость рук", "Sleight of hand", new Color(1f, 0.5f, 0.3f), 2),
            Chapter(17, 21, "Вода и ветер", "Water and wind", new Color(0.45f, 0.78f, 1f), 3),
            Chapter(22, 26, "Мастерская", "Workshop", new Color(0.85f, 0.65f, 1f), 4),
            Chapter(27, 31, "Большой огонь", "Great fire", new Color(1f, 0.4f, 0.3f), 5),
        };
    }

    private static RetStage Stage(string ru, string en, int warmth, int coins, int outfit, float scale, string lineRU, string lineEN) =>
        new RetStage { nameRU = ru, nameEN = en, warmth = warmth, coins = coins, outfit = outfit, scale = scale, lineRU = lineRU, lineEN = lineEN };

    private static RetOutfit Outfit(string ru, string en, string sprite, string srcRU, string srcEN, float y = 0.42f, float size = 0.36f) =>
        new RetOutfit { nameRU = ru, nameEN = en, sprite = sprite, sourceRU = srcRU, sourceEN = srcEN, offset = new Vector2(0f, y), size = size };

    private static RetDecor Decor(string ru, string en, int coins, int embers, string sprite, float x, float y, float w, int layer, int life) =>
        new RetDecor { nameRU = ru, nameEN = en, priceCoins = coins, priceEmbers = embers, sprite = sprite, slot = new Vector2(x, y), width = w, layer = layer, life = life,
                       behind = layer <= 1, underFlame = layer == 2 };

    private static RetQuest Quest(QuestKind k, int target, RetFuel fuel, string ru, string en) =>
        new RetQuest { kind = k, target = target, fuel = fuel, textRU = ru, textEN = en };

    private static RetWeekTheme Theme(QuestKind k, int target, string nru, string nen, string tru, string ten, int outfit, Color c) =>
        new RetWeekTheme { kind = k, target = target, nameRU = nru, nameEN = nen, textRU = tru, textEN = ten, outfit = outfit, color = c };

    private static RetChapter Chapter(int a, int b, string ru, string en, Color c, int motif) =>
        new RetChapter { first = a, last = b, nameRU = ru, nameEN = en, color = c, motif = motif };
}
