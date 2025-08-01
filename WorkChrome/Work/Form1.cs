using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;

using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using SeleniumUndetectedChromeDriver;

using Formatting = Newtonsoft.Json.Formatting;

namespace Work
{
    public partial class Form1 : Form
    {
        private UndetectedChromeDriver driver;
        private readonly string driverPath = $"{Application.StartupPath}/chromedriver.exe";
        private readonly string chromeProfileDir = Path.Combine(Application.StartupPath, "chrome_profile");

        public Form1()
        {
            InitializeComponent();
        }

        private void NavigateFast(string url)
        {
            try { driver.Navigate().GoToUrl(url); }
            catch (WebDriverTimeoutException) { }
        }

        private async void buttonOpen_Click(object sender, EventArgs e)
        {
            buttonOpen.Enabled = false;
            listBoxCookies.Items.Clear();
            listBoxCookies.Items.Add("Ожидание запуска браузера...");

            await Task.Run(() =>
            {
                try
                {
                    RestartDriver();

                    driver = UndetectedChromeDriver.Create(
                        driverExecutablePath: driverPath,
                        options: BuildChromeOptionsWithProxy()
                    );

                    NavigateFast("https://www.etsy.com/");

                    Invoke(new Action(() =>
                    {
                        listBoxCookies.Items.Clear();
                        listBoxCookies.Items.Add("Сайт открыт. Авторизуйтесь/примите куки, затем сохраните куки.");
                        buttonOpen.Enabled = true;
                    }));
                }
                catch (Exception ex)
                {
                    Invoke(new Action(() =>
                    {
                        MessageBox.Show("Ошибка при запуске браузера:\n" + ex.Message);
                        buttonOpen.Enabled = true;
                    }));
                }
            });
        }

        private async void buttonSaveCookies_Click(object sender, EventArgs e)
        {
            if (driver == null)
            {
                MessageBox.Show("Сначала откройте сайт.");
                return;
            }

            string cookieName = Prompt.ShowDialog("Введите имя для куки файла:", "Сохранение куки");
            if (string.IsNullOrWhiteSpace(cookieName))
            {
                MessageBox.Show("Имя не указано.");
                return;
            }

            string folderPath = Path.Combine(Application.StartupPath, "Cookies");
            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);

            string filePath = Path.Combine(folderPath, $"{cookieName}.json");

            var raw = driver.Manage().Cookies.AllCookies;
            var cookies = raw.Select(c => new SerializableCookie
            {
                Name = c.Name,
                Value = c.Value,
                Domain = c.Domain,
                Path = c.Path,
                Expiry = c.Expiry, // может быть null
                HttpOnly = c.IsHttpOnly,
                Secure = c.Secure
            }).ToList();

            File.WriteAllText(filePath, JsonConvert.SerializeObject(cookies, Formatting.Indented));

            listBoxCookies.Items.Clear();

            foreach (var file in Directory.GetFiles(folderPath, "*.json"))
            {
                listBoxCookies.Items.Add(Path.GetFileNameWithoutExtension(file));
            }

            MessageBox.Show($"Куки сохранены в файл: {cookieName}.json");
        }

        private async void buttonSend_Click(object sender, EventArgs e)
        {
            string selectedName = listBoxCookies.SelectedItem?.ToString();

            if (string.IsNullOrWhiteSpace(selectedName))
            {
                MessageBox.Show("Выберите имя куки из списка.");
                return;
            }

            var ofd = new OpenFileDialog
            {
                Title = "Выберите TXT: каждая строка = ссылка ИЛИ 'ссылка:текст'",
                Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
                Multiselect = false
            };
            if (ofd.ShowDialog() != DialogResult.OK) return;

            var parsed = ParseUrlTextFile(ofd.FileName);
            var urls = parsed.Urls;
            var perLinkTexts = parsed.Map;

            if (urls.Count == 0)
            {
                MessageBox.Show("Файл не содержит ссылок.");
                return;
            }

            string message = messageBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(message))
            {
                MessageBox.Show("Введите текст сообщения.");
                return;
            }

            if (message.Contains("***") && perLinkTexts.Count == 0)
            {
                var res = MessageBox.Show(
                    "В шаблоне есть '***', но в выбранном файле не найдено ни одной пары 'ссылка:текст'",
                    "Подстановка текста",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Question);
                if (res == DialogResult.OK) return;
            }

            string folderPath = Path.Combine(Application.StartupPath, "Cookies");
            string filePath = Path.Combine(folderPath, selectedName + ".json");

            if (!File.Exists(filePath))
            {
                MessageBox.Show("Файл куки не найден: " + filePath);
                return;
            }

            try
            {
                var json = File.ReadAllText(filePath);
                var cookieList = JsonConvert.DeserializeObject<List<SerializableCookie>>(json) ?? new List<SerializableCookie>();

                RestartDriver();

                driver = UndetectedChromeDriver.Create(
                    driverExecutablePath: driverPath,
                    options: BuildChromeOptionsWithProxy()
                );

                // Разделяем ссылки по доменам
                var etsyLinks = urls.Where(u => SafeGetHost(u).Contains("etsy.com")).ToList();
                var marktplaatsLinks = urls.Where(u => SafeGetHost(u).Contains("marktplaats.nl")).ToList();

                // --- Etsy: авто-выбор вариаций + скриншоты ---
                if (etsyLinks.Count > 0)
                {
                    NavigateFast("https://www.etsy.com/");
                    // Etsy обычно не требует логина для выбора вариаций, куки не нужны
                    await AutoSelectEtsyVariations(etsyLinks);
                }

                // --- Marktplaats: авторизация по куки + отправка сообщений ---
                if (marktplaatsLinks.Count > 0)
                {
                    NavigateFast("https://www.marktplaats.nl/");
                    AddCookiesSafe(driver, cookieList);
                    await SendMessageToAds(message, marktplaatsLinks, perLinkTexts);
                }

                MessageBox.Show("Готово. Обработка ссылок завершена.");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка: " + ex.Message);
            }
            finally
            {
                RestartDriver();
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            string folderPath = Path.Combine(Application.StartupPath, "Cookies");
            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);

            listBoxCookies.Items.Clear();
            foreach (var file in Directory.GetFiles(folderPath, "*.json"))
            {
                listBoxCookies.Items.Add(Path.GetFileNameWithoutExtension(file));
            }
        }

        // ===================== Marktplaats отправка =====================

        public async Task SendMessageToAds(string messageTemplate, List<string> urls, Dictionary<string, string> perLinkTexts = null)
        {
            if (driver == null) throw new InvalidOperationException("WebDriver не инициализирован.");

            void Log(string s)
            {
                try
                {
                    if (IsHandleCreated)
                    {
                        Invoke(new Action(() =>
                        {
                            smsBox.Items.Add(s);
                            smsBox.TopIndex = smsBox.Items.Count - 1;
                        }));
                    }
                }
                catch { }
            }

            foreach (var url in urls)
            {
                try
                {
                    if (!Uri.IsWellFormedUriString(url, UriKind.Absolute))
                    {
                        Log($"[SKIP] Некорректный URL: {url}");
                        continue;
                    }

                    var host = new Uri(url).Host.ToLowerInvariant();
                    if (!host.Contains("marktplaats.nl"))
                    {
                        Log($"[SKIP] Домен не marktplaats.nl: {url}");
                        continue;
                    }

                    var messageForThisUrl = messageTemplate;
                    if (messageForThisUrl.Contains("***"))
                    {
                        if (perLinkTexts != null &&
                            perLinkTexts.TryGetValue(NormalizeUrlForKey(url), out var replacementText))
                        {
                            messageForThisUrl = messageForThisUrl.Replace("***", replacementText);
                        }
                        else
                        {
                            Log($"[SKIP] Нет текста для замены *** для ссылки: {url}");
                            continue;
                        }
                    }

                    await OpenAdAndSendMessage(messageForThisUrl, url);
                    Log($"[OK] Сообщение отправлено: {url}");
                }
                catch (Exception ex)
                {
                    Log($"[ERR] {url} -> {ex.Message}");
                }
            }
        }

        public async Task OpenAdAndSendMessage(string message, string url)
        {
            if (driver == null) throw new InvalidOperationException("WebDriver не инициализирован.");

            var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(25));
            wait.IgnoreExceptionTypes(typeof(NoSuchElementException), typeof(StaleElementReferenceException));

            NavigateFast(url);
            TryAcceptCookies_Marktplaats(driver);

            var berichtBtn = wait.Until(d =>
                d.FindElements(By.XPath("//button[normalize-space()='Bericht' or .//span[normalize-space()='Bericht']]"))
                 .FirstOrDefault(e => e.Displayed && e.Enabled)
            );
            ScrollIntoView(driver, berichtBtn);
            SafeClick(driver, wait, berichtBtn);

            // ===== Локальные хелперы (внутри метода) =====
            string NormalizeNewlines(string s) => (s ?? string.Empty).Replace("\r\n", "\n").Replace("\r", "\n");

            string ReadValue(IWebElement el)
            {
                try
                {
                    var tag = (el.TagName ?? string.Empty).ToLowerInvariant();

                    if (tag == "textarea" || tag == "input")
                    {
                        var v = el.GetAttribute("value");
                        if (v != null) return v;
                    }

                    // contenteditable
                    var ce = el.GetAttribute("contenteditable");
                    if (!string.IsNullOrEmpty(ce) && ce.Equals("true", StringComparison.OrdinalIgnoreCase))
                    {
                        var js = (IJavaScriptExecutor)driver;
                        var txt = (string)js.ExecuteScript("return arguments[0].innerText || arguments[0].textContent || '';", el);
                        return txt ?? string.Empty;
                    }

                    // fallback
                    return el.Text ?? string.Empty;
                }
                catch { return string.Empty; }
            }

            bool ValuesEqual(string a, string b) => string.Equals(NormalizeNewlines(a), NormalizeNewlines(b), StringComparison.Ordinal);

            Func<IWebDriver, IWebElement> findInput = drv =>
            {
                var selectors = new By[]
                {
                    By.CssSelector("textarea#message"),
                    By.CssSelector("textarea[name='message']"),
                    By.CssSelector("textarea.hz-TextField-input.hz-TextField-input--multiline"),
                    By.CssSelector("textarea")
                };
                foreach (var sel in selectors)
                {
                    var el = drv.FindElements(sel).FirstOrDefault(e => e.Displayed && e.Enabled);
                    if (el != null) return el;
                }

                // запасной вариант: contenteditable
                var editable = drv.FindElements(By.CssSelector("[contenteditable='true']")).FirstOrDefault(e => e.Displayed && e.Enabled);
                if (editable != null) return editable;

                return null;
            };

            async Task<bool> SetTextReliableAsync(TimeSpan overallTimeout)
            {
                var js = (IJavaScriptExecutor)driver;
                var start = DateTime.UtcNow;

                IWebElement input = null;

                while (DateTime.UtcNow - start < overallTimeout)
                {
                    input = wait.Until(findInput);
                    ScrollIntoView(driver, input);

                    try
                    {
                        input.Click();
                        input.SendKeys(OpenQA.Selenium.Keys.Control + "a");
                        input.SendKeys(OpenQA.Selenium.Keys.Delete);
                        input.SendKeys(message);
                    }
                    catch (StaleElementReferenceException)
                    {
                        await Task.Delay(120);
                        continue;
                    }
                    catch { }

                    await Task.Delay(120);
                    var val = ReadValue(input);
                    if (ValuesEqual(val, message)) return true;

                    try
                    {
                        var tag = (input.TagName ?? string.Empty).ToLowerInvariant();
                        var isTextarea = tag == "textarea";
                        var isContentEditable = (input.GetAttribute("contenteditable") ?? "")
                            .Equals("true", StringComparison.OrdinalIgnoreCase);

                        if (isTextarea)
                        {
                            js.ExecuteScript(@"
                                const el = arguments[0], v = arguments[1];
                                const last = el.value;
                                try {
                                    const setter = Object.getOwnPropertyDescriptor(HTMLTextAreaElement.prototype, 'value').set;
                                    setter.call(el, v);
                                } catch(e) {
                                    el.value = v;
                                }
                                if (el._valueTracker) {
                                    el._valueTracker.setValue(last);
                                }
                                el.dispatchEvent(new Event('input', { bubbles: true }));
                                el.dispatchEvent(new Event('change', { bubbles: true }));
                            ", input, message);
                        }
                        else if (isContentEditable)
                        {
                            js.ExecuteScript(@"
                                const el = arguments[0], v = arguments[1];
                                el.innerText = v;
                                el.dispatchEvent(new Event('input', { bubbles: true }));
                                el.dispatchEvent(new Event('change', { bubbles: true }));
                            ", input, message);
                        }
                        else
                        {
                            js.ExecuteScript(@"
                                const el = arguments[0], v = arguments[1];
                                try {
                                    if ('value' in el) {
                                        const last = el.value;
                                        el.value = v;
                                        if (el._valueTracker) {
                                            el._valueTracker.setValue(last);
                                        }
                                    } else {
                                        el.innerText = v;
                                    }
                                } catch(e) {}
                                el.dispatchEvent(new Event('input', { bubbles: true }));
                                el.dispatchEvent(new Event('change', { bubbles: true }));
                            ", input, message);
                        }

                        js.ExecuteScript("arguments[0].focus();", input);
                        await Task.Delay(60);
                        js.ExecuteScript("arguments[0].blur();", input);
                    }
                    catch { }

                    await Task.Delay(140);
                    val = ReadValue(input);
                    if (ValuesEqual(val, message)) return true;

                    await Task.Delay(180);
                }

                return false;
            }
            // ===== конец локальных хелперов =====

            var ok = await SetTextReliableAsync(TimeSpan.FromSeconds(8));
            if (!ok)
            {
                ok = await SetTextReliableAsync(TimeSpan.FromSeconds(4));
            }
            if (!ok)
            {
                throw new InvalidOperationException("Не удалось надёжно установить текст сообщения — отправка отменена.");
            }

            var sendBtn = wait.Until(d =>
                d.FindElements(By.XPath("//button[normalize-space()='Stuur bericht' or .//span[normalize-space()='Stuur bericht']]"))
                 .FirstOrDefault(e => e.Displayed && e.Enabled)
            );

            ScrollIntoView(driver, sendBtn);
            SafeClick(driver, wait, sendBtn);

            await Task.Delay(1500);
        }

        // ===================== Etsy: выбор вариаций =====================

        private async Task AutoSelectEtsyVariations(List<string> etsyLinks)
        {
            if (driver == null) throw new InvalidOperationException("WebDriver не инициализирован.");
            var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(25));

            foreach (var url in etsyLinks)
            {
                try
                {
                    NavigateFast(url);
                    wait.Until(d => ((IJavaScriptExecutor)d).ExecuteScript("return document.readyState")?.ToString() == "complete");
                    TryAcceptCookies_Etsy(driver);

                    SelectAllVariations(driver, wait); // ключевая логика

                    // Скриншот результата
                    var shot = ((ITakesScreenshot)driver).GetScreenshot();
                    var file = $"etsy_{SanitizeFileName(url)}.png";
                    var outPath = Path.Combine(Application.StartupPath, file);

                    AppendLogUi($"[OK][Etsy] Вариации выбраны: {url}. Скрин: {file}");
                }
                catch (Exception ex)
                {
                    AppendLogUi($"[ERR][Etsy] {url}: {ex.Message}");
                }
            }
            await Task.CompletedTask;
        }

        private void SelectAllVariations(IWebDriver driver, WebDriverWait wait, int maxRounds = 10)
        {
            for (int round = 0; round < maxRounds; round++)
            {
                bool changedAnything = false;

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
                        WaitForOptionsToAppear(driver, s, minRealOptions: 1, maxWaitSeconds: 15);
                        var sel = new SelectElement(s);

                        var curTxt = (sel.SelectedOption?.Text ?? "").Trim();
                        var curVal = (sel.SelectedOption?.GetAttribute("value") ?? "").Trim();
                        if (!string.IsNullOrEmpty(curVal) && !IsPlaceholderText(curTxt))
                            continue;

                        if (ChooseFirstAvailableOption(driver, sel, s))
                        {
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
                        changedAnything = true;
                    }
                    catch (NoSuchElementException) { }
                }

                if (!changedAnything)
                    break;
            }

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
                AppendLogUi("Внимание: не все вариации выбраны:");
                foreach (var r in remaining)
                {
                    var id = r.GetAttribute("id") ?? r.GetAttribute("name") ?? "<select>";
                    var se = SafeSelect(r);
                    DumpSelect($"Остался незаполненным: {id}", r, se, driver);
                }
            }
            else
            {
                AppendLogUi("Все вариации выбраны успешно.");
            }
        }

        // ===================== Браузер/опции/куки =====================

        private ChromeOptions BuildChromeOptionsWithProxy()
        {
            var options = new ChromeOptions();

            options.AddArgument("--start-maximized");
            // В большинстве случаев — лучше БЕЗ этого флага. Если нужно — раскомментируйте.
            // options.AddArgument("--disable-blink-features=AutomationControlled");

            // Локаль под Etsy /uk/
            options.AddArgument("--lang=en-GB");
            options.AddUserProfilePreference("intl.accept_languages", "en-GB,en");

            // Персистентный профиль — меньше блокировок, хранит куки
            Directory.CreateDirectory(chromeProfileDir);
            options.AddArgument($"--user-data-dir={chromeProfileDir}");

            // Прокси — только если указан
            var proxy = proxyBox.Text?.Trim();
            if (!string.IsNullOrEmpty(proxy))
            {
                options.AddArgument($"--proxy-server={proxy}");
                options.AddArgument("ignore-certificate-errors");
            }

            return options;
        }

        private void RestartDriver()
        {
            try { driver?.Quit(); } catch { }
            try { driver?.Dispose(); } catch { }
            driver = null;
        }

        // ===================== Общие утилиты =====================

        private static void ScrollIntoView(IWebDriver driver, IWebElement element)
        {
            try
            {
                ((IJavaScriptExecutor)driver)
                    .ExecuteScript("arguments[0].scrollIntoView({block:'center', inline:'center'});", element);
            }
            catch { }
        }

        private static void SafeClick(IWebDriver driver, WebDriverWait wait, IWebElement element)
        {
            wait.Until(d =>
            {
                try
                {
                    if (element.Displayed && element.Enabled)
                    {
                        element.Click();
                        return true;
                    }
                }
                catch (ElementClickInterceptedException)
                {
                    ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].click();", element);
                    return true;
                }
                catch (StaleElementReferenceException)
                {
                    return false;
                }
                return false;
            });
        }

        private void TryAcceptCookies_Marktplaats(IWebDriver driver)
        {
            try
            {
                driver.SwitchTo().DefaultContent();

                bool ClickIfFound()
                {
                    var selectors = new[]
                    {
                        "//button[@title='Accepteren' or normalize-space()='Accepteren' or normalize-space()='Akkoord' or @data-testid='accept-all']",
                        "//button[contains(.,'Akkoord')]",
                        "//button[contains(.,'Accepteer')]",
                    };
                    foreach (var xp in selectors)
                    {
                        var btn = driver.FindElements(By.XPath(xp))
                            .FirstOrDefault(e => e.Displayed && e.Enabled);
                        if (btn != null)
                        {
                            try { btn.Click(); }
                            catch { ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].click();", btn); }
                            return true;
                        }
                    }
                    return false;
                }

                if (ClickIfFound()) return;

                var frames = driver.FindElements(By.TagName("iframe"));
                foreach (var frame in frames)
                {
                    try
                    {
                        driver.SwitchTo().Frame(frame);
                        if (ClickIfFound())
                        {
                            driver.SwitchTo().DefaultContent();
                            return;
                        }
                    }
                    catch { }
                    finally
                    {
                        driver.SwitchTo().DefaultContent();
                    }
                }
            }
            catch { }
        }

        private void TryAcceptCookies_Etsy(IWebDriver driver)
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

        private static string NormalizeUrlForKey(string url)
        {
            try
            {
                var uri = new Uri(url);
                var host = uri.Host;
                if (host.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
                    host = host.Substring(4);

                var path = uri.AbsolutePath.TrimEnd('/');
                var query = uri.Query;
                return $"{uri.Scheme}://{host}{path}{query}";
            }
            catch
            {
                return (url ?? string.Empty).Trim();
            }
        }

        private static (List<string> Urls, Dictionary<string, string> Map) ParseUrlTextFile(string filePath)
        {
            var urls = new List<string>();
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var rawLine in File.ReadAllLines(filePath))
            {
                var line = rawLine?.Trim();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;

                string link = null, text = null;

                int tabIdx = line.IndexOf('\t');
                if (tabIdx > 0)
                {
                    link = line.Substring(0, tabIdx).Trim();
                    text = line.Substring(tabIdx + 1).Trim();
                }
                else
                {
                    int pipeIdx = line.IndexOf('|');
                    if (pipeIdx > 0)
                    {
                        link = line.Substring(0, pipeIdx).Trim();
                        text = line.Substring(pipeIdx + 1).Trim();
                    }
                    else
                    {
                        int schemeIdx = line.IndexOf("://", StringComparison.Ordinal);
                        int startIdx = (schemeIdx >= 0) ? schemeIdx + 3 : 0;
                        int colonIdx = line.IndexOf(':', startIdx);
                        if (colonIdx > 0)
                        {
                            link = line.Substring(0, colonIdx).Trim();
                            text = line.Substring(colonIdx + 1).Trim();
                        }
                        else
                        {
                            link = line;
                        }
                    }
                }

                if (string.IsNullOrEmpty(link)) continue;

                var norm = NormalizeUrlForKey(link);

                if (!seen.Contains(norm))
                {
                    urls.Add(link);
                    seen.Add(norm);
                }

                if (text != null)
                    map[norm] = text;
            }

            return (urls, map);
        }

        private static Dictionary<string, string> LoadLinkTextMap(string filePath)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var rawLine in File.ReadAllLines(filePath))
            {
                var line = rawLine?.Trim();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;

                string link = null, text = null;

                int tabIdx = line.IndexOf('\t');
                if (tabIdx > 0)
                {
                    link = line.Substring(0, tabIdx).Trim();
                    text = line.Substring(tabIdx + 1).Trim();
                }
                else
                {
                    int pipeIdx = line.IndexOf('|');
                    if (pipeIdx > 0)
                    {
                        link = line.Substring(0, pipeIdx).Trim();
                        text = line.Substring(pipeIdx + 1).Trim();
                    }
                    else
                    {
                        int schemeIdx = line.IndexOf("://", StringComparison.Ordinal);
                        int startIdx = (schemeIdx >= 0) ? schemeIdx + 3 : 0;
                        int colonIdx = line.IndexOf(':', startIdx);
                        if (colonIdx > 0)
                        {
                            link = line.Substring(0, colonIdx).Trim();
                            text = line.Substring(colonIdx + 1).Trim();
                        }
                    }
                }

                if (!string.IsNullOrEmpty(link))
                {
                    var key = NormalizeUrlForKey(link);
                    map[key] = text ?? string.Empty;
                }
            }
            return map;
        }

        private static void AddCookiesSafe(UndetectedChromeDriver driver, List<SerializableCookie> cookieList)
        {
            foreach (var c in cookieList)
            {
                try
                {
                    var domain = (c.Domain ?? "marktplaats.nl").Trim();
                    if (domain.StartsWith(".")) domain = domain.Substring(1);
                    var path = string.IsNullOrWhiteSpace(c.Path) ? "/" : c.Path;

                    Cookie cookie = c.Expiry.HasValue
                        ? new Cookie(c.Name, c.Value, domain, path, c.Expiry)
                        : new Cookie(c.Name, c.Value, domain, path, null);

                    driver.Manage().Cookies.AddCookie(cookie);
                }
                catch
                { }
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            try { driver?.Quit(); } catch { }
        }

        private void Form1_Load_1(object sender, EventArgs e)
        {
            listBoxCookies.Items.Clear();
            string folderPath = Path.Combine(Application.StartupPath, "Cookies");

            foreach (var file in Directory.GetFiles(folderPath, "*.json"))
            {
                listBoxCookies.Items.Add(Path.GetFileNameWithoutExtension(file));
            }
        }

        private void AppendLogUi(string text)
        {
            try
            {
                if (IsHandleCreated)
                {
                    Invoke(new Action(() =>
                    {
                        smsBox.Items.Add(text);
                        smsBox.TopIndex = smsBox.Items.Count - 1;
                    }));
                }
            }
            catch { }
        }

        private static string SanitizeFileName(string input)
        {
            var safe = Regex.Replace(input ?? "", @"[^\w\-\.]+", "_");
            if (safe.Length > 120) safe = safe.Substring(0, 120);
            return safe;
        }

        private static string SafeGetHost(string url)
        {
            try { return new Uri(url).Host.ToLowerInvariant(); }
            catch { return ""; }
        }

        // ===================== Etsy хелперы =====================

        private static SelectElement SafeSelect(IWebElement s)
        {
            try { return new SelectElement(s); }
            catch { return null; }
        }

        private static void WaitForOptionsToAppear(IWebDriver driver, IWebElement selectEl, int minRealOptions, int maxWaitSeconds)
        {
            var localWait = new WebDriverWait(driver, TimeSpan.FromSeconds(maxWaitSeconds));
            localWait.Until(_ =>
            {
                try
                {
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

        private static bool ChooseFirstAvailableOption(IWebDriver driver, SelectElement sel, IWebElement selectEl)
        {
            var best = sel.Options.FirstOrDefault(o => IsValidOption(driver, o));
            if (best != null)
                return TrySelectByValue(sel, selectEl, (best.GetAttribute("value") ?? "").Trim(), driver);

            var firstNonEmpty = sel.Options.FirstOrDefault(o => !string.IsNullOrWhiteSpace((o.GetAttribute("value") ?? "").Trim()));
            if (firstNonEmpty != null)
                return TrySelectByValue(sel, selectEl, (firstNonEmpty.GetAttribute("value") ?? "").Trim(), driver);

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

        private static bool TrySelectByValue(SelectElement sel, IWebElement selectEl, string value, IWebDriver driver)
        {
            try
            {
                sel.SelectByValue(value);
                return true;
            }
            catch (InvalidOperationException)
            {
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

        private static bool IsValidOption(IWebDriver driver, IWebElement opt)
        {
            if (opt == null) return false;

            var val = (opt.GetAttribute("value") ?? "").Trim();
            if (string.IsNullOrEmpty(val)) return false;

            var text = (opt.Text ?? "").Trim();
            if (IsPlaceholderText(text)) return false;
            if (IsSoldOutText(text)) return false;

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

            var cls = (opt.GetAttribute("class") ?? "").ToLowerInvariant();
            if (cls.Contains("sold") || cls.Contains("unavailable") || cls.Contains("disabled"))
                return false;

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

        private static bool IsPlaceholderText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return true;
            var t = Regex.Replace(text, @"\s+", " ").Trim();

            return Regex.IsMatch(t, @"^(Select|Choose|Please select|Choose an option)\b", RegexOptions.IgnoreCase)
                || Regex.IsMatch(t, @"^(Выберите|Пожалуйста, выберите)\b", RegexOptions.IgnoreCase)
                || Regex.IsMatch(t, @"^(Auswählen|Choisir|Seleziona|Selecciona|Selecionar)\b", RegexOptions.IgnoreCase);
        }

        private static bool IsSoldOutText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            var t = text.ToLowerInvariant();
            return t.Contains("sold out") || t.Contains("out of stock")
                || t.Contains("нет в наличии") || t.Contains("распродано")
                || t.Contains("niet op voorraad") || t.Contains("esgotado")
                || t.Contains("agotado") || t.Contains("non disponibile")
                || t.Contains("ausverkauft");
        }

        private static int ParseIndex(string id)
        {
            if (string.IsNullOrEmpty(id)) return int.MaxValue;
            var m = Regex.Match(id, @"variation-selector-(\d+)");
            return m.Success ? int.Parse(m.Groups[1].Value) : int.MaxValue;
        }

        private static void RandomJitter(int minMs, int maxMs)
        {
            var r = new Random();
            Thread.Sleep(r.Next(minMs, maxMs));
        }

        private static void DumpSelect(string title, IWebElement selectEl, SelectElement sel, IWebDriver driver)
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

    public class SerializableCookie
    {
        public string Name { get; set; } = "";
        public string Value { get; set; } = "";
        public string Domain { get; set; } = "";
        public string Path { get; set; } = "/";
        public DateTime? Expiry { get; set; }  // <-- добавлена пропущенная ; в исходнике
        public bool HttpOnly { get; set; }
        public bool Secure { get; set; }
    }
}
