using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using OpenQA.Selenium.Chrome;
using SeleniumUndetectedChromeDriver;

internal class Program
{
    static void Main(string[] args)
    {
        // === НАСТРОЙКА БРАУЗЕРА ===
        var options = new ChromeOptions();
        options.AddArgument("--start-maximized");
        // Часто лучше БЕЗ этого флага (undetected и так маскирует WebDriver).
        // Если у вас без него хуже — раскомментируйте следующую строку.
        // options.AddArgument("--disable-blink-features=AutomationControlled");
        options.AddArgument("--lang=en-GB");
        options.AddUserProfilePreference("intl.accept_languages", "en-GB,en");

        // Персистентный профиль уменьшает шанс блокировок и хранит куки
        var profileDir = Path.Combine(Environment.CurrentDirectory, "chrome_profile");
        Directory.CreateDirectory(profileDir);
        options.AddArgument($"--user-data-dir={profileDir}");

        string driverExecutablePath = @"C:\chromedriver.exe"; // или "" если не нужен
        using var driver = UndetectedChromeDriver.Create(options: options, driverExecutablePath: driverExecutablePath);
        var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(25));

        try
        {
            string url = "https://www.etsy.com/uk/listing/1480176028/dainty-14k-gold-heart-promise-ring-for";
            driver.Navigate().GoToUrl(url);

            wait.Until(d => ((IJavaScriptExecutor)d).ExecuteScript("return document.readyState")?.ToString() == "complete");
            TryAcceptCookies(driver);

            // ВЫБОР ВСЕХ ВАРИАЦИЙ (1..6)
            SelectAllVariations(driver, wait);

            // Скриншот результата (для контроля)
            Console.WriteLine("Готово.");

            Console.WriteLine("Нажмите Enter для выхода...");
            Console.ReadLine();
        }
        finally
        {
            try { driver.Quit(); } catch { }
        }
    }

    // =========================== ОСНОВНАЯ ЛОГИКА ===========================

    static void SelectAllVariations(IWebDriver driver, WebDriverWait wait, int maxRounds = 10)
    {
        for (int round = 0; round < maxRounds; round++)
        {
            bool changedAnything = false;

            // Каждый раунд пере-находим селекты по всей странице (Etsy часто их перерисовывает)
            var selects = driver.FindElements(By.CssSelector(
                    "select[id^='variation-selector-'], " +
                    "select[data-variation-number], " +
                    "select[name*='variation']"))
                .Select(s => (el: s, idx: ParseIndex(s.GetAttribute("id"))))
                .OrderBy(t => t.idx)
                .Select(t => t.el)
                .ToList();

            if (selects.Count == 0)
                break;

            foreach (var s in selects)
            {
                try
                {
                    // Ждём появления реальных опций (value != "")
                    WaitForOptionsToAppear(driver, s, minRealOptions: 1, maxWaitSeconds: 15);

                    var sel = new SelectElement(s);

                    // Уже выбран нормальный вариант?
                    var curTxt = (sel.SelectedOption?.Text ?? "").Trim();
                    var curVal = (sel.SelectedOption?.GetAttribute("value") ?? "").Trim();
                    if (!string.IsNullOrEmpty(curVal) && !IsPlaceholderText(curTxt))
                        continue;

                    // Пытаемся выбрать лучшую доступную опцию
                    if (ChooseFirstAvailableOption(driver, sel, s))
                    {
                        // Ждём применения выбора
                        var shortWait = new WebDriverWait(driver, TimeSpan.FromSeconds(10));
                        shortWait.Until(_ =>
                        {
                            try
                            {
                                var se = new SelectElement(s);
                                var v = (se.SelectedOption?.GetAttribute("value") ?? "").Trim();
                                var t = (se.SelectedOption?.Text ?? "").Trim();
                                return !string.IsNullOrWhiteSpace(v) && !IsPlaceholderText(t);
                            }
                            catch (StaleElementReferenceException) { return true; }
                        });

                        RandomJitter(200, 500);
                        changedAnything = true;
                    }
                    else
                    {
                        DumpSelect("Не удалось выбрать (нет подходящих опций)", s, sel, driver);
                    }
                }
                catch (WebDriverTimeoutException)
                {
                    DumpSelect("Таймаут ожидания появления реальных опций", s, SafeSelect(s), driver);
                }
                catch (StaleElementReferenceException)
                {
                    // Селект перерисовали — повторим в следующем раунде
                    changedAnything = true;
                }
                catch (NoSuchElementException)
                {
                    // Селект исчез — пропускаем
                }
            }

            if (!changedAnything)
                break;
        }

        // Финальная проверка — что-то осталось пустым?
        var remaining = driver.FindElements(By.CssSelector(
                "select[id^='variation-selector-'], select[data-variation-number], select[name*='variation']"))
            .Where(s =>
            {
                try
                {
                    var se = new SelectElement(s);
                    var val = (se.SelectedOption?.GetAttribute("value") ?? "").Trim();
                    var txt = (se.SelectedOption?.Text ?? "").Trim();
                    return string.IsNullOrEmpty(val) || IsPlaceholderText(txt);
                }
                catch { return false; }
            }).ToList();

        if (remaining.Count > 0)
        {
            Console.WriteLine("Внимание: не все вариации выбраны:");
            foreach (var r in remaining)
            {
                var id = r.GetAttribute("id") ?? r.GetAttribute("name") ?? "<select>";
                var se = SafeSelect(r);
                DumpSelect($"Остался незаполненным: {id}", r, se, driver);
            }
        }
        else
        {
            Console.WriteLine("Все вариации выбраны успешно.");
        }
    }
    static SelectElement SafeSelect(IWebElement s)
    {
        try { return new SelectElement(s); }
        catch { return null; } // если селект уже перерисовали/исчез — вернём null
    }

    static bool ChooseFirstAvailableOption(IWebDriver driver, SelectElement sel, IWebElement selectEl)
    {
        // 1) Нормальная валидная опция
        var best = sel.Options.FirstOrDefault(o => IsValidOption(driver, o));
        if (best != null)
            return TrySelectByValue(sel, selectEl, (best.GetAttribute("value") ?? "").Trim(), driver);

        // 2) Фолбэк: первая с непустым value (даже если помечена data-*)
        var firstNonEmpty = sel.Options.FirstOrDefault(o => !string.IsNullOrWhiteSpace((o.GetAttribute("value") ?? "").Trim()));
        if (firstNonEmpty != null)
            return TrySelectByValue(sel, selectEl, (firstNonEmpty.GetAttribute("value") ?? "").Trim(), driver);

        // 3) Жёсткий фолбэк: если есть хотя бы две опции — ставим selectedIndex=1
        try
        {
            var ok = (bool?)((IJavaScriptExecutor)driver).ExecuteScript(@"
                const el = arguments[0];
                if (!el || !el.options || el.options.length < 2) return false;
                el.selectedIndex = 1;
                el.dispatchEvent(new Event('input', { bubbles: true }));
                el.dispatchEvent(new Event('change', { bubbles: true }));
                return true;
            ", selectEl) == true;
            return ok;
        }
        catch { return false; }
    }

    // =========================== ХЕЛПЕРЫ ===========================

    static void TryAcceptCookies(IWebDriver driver)
    {
        try
        {
            // прямой поиск
            IWebElement btn =
                driver.FindElements(By.CssSelector("button[data-gdpr-single-choice-accept], [data-gdpr-accept]")).FirstOrDefault()
                ?? driver.FindElements(By.XPath("//button[contains(., 'Accept') or contains(., 'Agree') or contains(., 'OK')]")).FirstOrDefault();

            if (btn == null)
            {
                // иногда в iframe
                var iframe = driver.FindElements(By.CssSelector("iframe[src*='consent'], iframe[id*='gdpr'], iframe[title*='privacy']")).FirstOrDefault();
                if (iframe != null)
                {
                    driver.SwitchTo().Frame(iframe);
                    btn = driver.FindElements(By.CssSelector("button[data-gdpr-single-choice-accept], [data-gdpr-accept]")).FirstOrDefault()
                       ?? driver.FindElements(By.XPath("//button[contains(., 'Accept') or contains(., 'Agree') or contains(., 'OK')]")).FirstOrDefault();
                }
            }

            if (btn != null)
            {
                ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].click();", btn);
            }
        }
        catch { }
        finally
        {
            try { driver.SwitchTo().DefaultContent(); } catch { }
        }
    }

    static bool TrySelectByValue(SelectElement sel, IWebElement selectEl, string value, IWebDriver driver)
    {
        try
        {
            sel.SelectByValue(value); // обычный путь
            return true;
        }
        catch (InvalidOperationException)
        {
            // JS-фолбэк
            try
            {
                ((IJavaScriptExecutor)driver).ExecuteScript(@"
                    const el = arguments[0], v = arguments[1];
                    el.value = v;
                    el.dispatchEvent(new Event('input', { bubbles: true }));
                    el.dispatchEvent(new Event('change', { bubbles: true }));
                ", selectEl, value);
                return true;
            }
            catch { return false; }
        }
    }

    static void WaitForOptionsToAppear(IWebDriver driver, IWebElement selectEl, int minRealOptions, int maxWaitSeconds)
    {
        var localWait = new WebDriverWait(driver, TimeSpan.FromSeconds(maxWaitSeconds));
        localWait.Until(_ =>
        {
            try
            {
                // ждём, пока появится хотя бы N опций с непустым value
                var count = (long)((IJavaScriptExecutor)driver).ExecuteScript(@"
                    const el = arguments[0];
                    if (!el || !el.options) return 0;
                    let n = 0;
                    for (let i=0;i<el.options.length;i++){
                        const o = el.options[i];
                        if ((o.value||'').trim().length > 0) n++;
                    }
                    return n;
                ", selectEl);
                return count >= minRealOptions;
            }
            catch { return false; }
        });
    }

    static bool IsValidOption(IWebDriver driver, IWebElement opt)
    {
        if (opt == null) return false;

        var val = (opt.GetAttribute("value") ?? "").Trim();
        if (string.IsNullOrEmpty(val)) return false;

        var text = (opt.Text ?? "").Trim();
        if (IsPlaceholderText(text)) return false;
        if (IsSoldOutText(text)) return false;

        // data-* пометки недоступности (часто на Etsy)
        var dataFlags = new[]
        {
            "data-sold-out", "data-unavailable", "data-disabled", "data-is-sold-out",
            "data-out-of-stock", "data-inventory-unavailable"
        };
        foreach (var f in dataFlags)
        {
            var v = opt.GetAttribute(f);
            if (!string.IsNullOrEmpty(v) && v.Equals("true", StringComparison.OrdinalIgnoreCase))
                return false;
        }

        // класс может нести маркеры недоступности
        var cls = (opt.GetAttribute("class") ?? "").ToLowerInvariant();
        if (cls.Contains("sold") || cls.Contains("unavailable") || cls.Contains("disabled"))
            return false;

        // стандартные disabled
        if (!opt.Enabled) return false;
        if (opt.GetAttribute("disabled") != null) return false;
        if (string.Equals(opt.GetAttribute("aria-disabled"), "true", StringComparison.OrdinalIgnoreCase)) return false;
        try
        {
            var domDisabled = (bool?)((IJavaScriptExecutor)driver).ExecuteScript("return arguments[0].disabled===true;", opt) == true;
            if (domDisabled) return false;
        }
        catch { }

        return true;
    }

    static bool IsPlaceholderText(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return true;
        var t = Regex.Replace(text, @"\s+", " ").Trim();

        // Разные локали/варианты плейсхолдеров
        return Regex.IsMatch(t, @"^(Select|Choose|Please select|Choose an option)\b", RegexOptions.IgnoreCase)
            || Regex.IsMatch(t, @"^(Выберите|Пожалуйста, выберите)\b", RegexOptions.IgnoreCase)
            || Regex.IsMatch(t, @"^(Auswählen|Choisir|Seleziona|Selecciona|Selecionar)\b", RegexOptions.IgnoreCase);
    }

    static bool IsSoldOutText(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        var t = text.ToLowerInvariant();
        return t.Contains("sold out") || t.Contains("out of stock")
            || t.Contains("нет в наличии") || t.Contains("распродано")
            || t.Contains("niet op voorraad") || t.Contains("esgotado")
            || t.Contains("agotado") || t.Contains("non disponibile")
            || t.Contains("ausverkauft");
    }

    static int ParseIndex(string id)
    {
        if (string.IsNullOrEmpty(id)) return int.MaxValue;
        var m = Regex.Match(id, @"variation-selector-(\d+)");
        return m.Success ? int.Parse(m.Groups[1].Value) : int.MaxValue;
    }

    static void RandomJitter(int minMs, int maxMs)
    {
        var r = new Random();
        Thread.Sleep(r.Next(minMs, maxMs));
    }

    static void DumpSelect(string title, IWebElement selectEl, SelectElement sel, IWebDriver driver)
    {
        try
        {
            var id = selectEl.GetAttribute("id");
            var name = selectEl.GetAttribute("name");
            Console.WriteLine($"[{title}] select id='{id}' name='{name}'");
            int i = 0;
            foreach (var o in sel?.Options ?? new List<IWebElement>())
            {
                var val = (o.GetAttribute("value") ?? "").Trim();
                var txt = (o.Text ?? "").Trim();
                var disabledAttr = o.GetAttribute("disabled");
                var aria = o.GetAttribute("aria-disabled");
                var ds1 = o.GetAttribute("data-sold-out");
                var ds2 = o.GetAttribute("data-unavailable");
                var ds3 = o.GetAttribute("data-disabled");
                var ds4 = o.GetAttribute("data-is-sold-out");
                bool enabledProp = false, domDisabled = false;
                try { enabledProp = o.Enabled; } catch { }
                try { domDisabled = (bool?)((IJavaScriptExecutor)driver).ExecuteScript("return arguments[0].disabled===true;", o) == true; } catch { }

                var valid = IsValidOption(driver, o);
                Console.WriteLine($"  [{i++}] val='{val}' txt='{txt}' enabled={enabledProp} disabledAttr={(disabledAttr != null)} aria={aria} dataSoldOut={ds1 ?? ds4} dataUnavail={ds2} dataDisabled={ds3} domDisabled={domDisabled} -> valid={valid}");
            }
        }
        catch { /* ignore */ }
    }
}
