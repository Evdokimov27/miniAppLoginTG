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
		private readonly string driverPath = @"C:\chromedriver.exe";

		public Form1()
		{
			InitializeComponent();

		}

		private void NavigateFast(string url)
		{
			try { driver.Navigate().GoToUrl(url); }
			catch (WebDriverTimeoutException) {  }
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

					NavigateFast("https://www.marktplaats.nl");

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
				Title = "Выберите TXT со списком ссылок (каждая с новой строки)",
				Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
				Multiselect = false
			};
			if (ofd.ShowDialog() != DialogResult.OK) return;

			var urls = File.ReadAllLines(ofd.FileName)
				.Select(l => l.Trim())
				.Where(l => !string.IsNullOrWhiteSpace(l))
				.ToList();

			if (urls.Count == 0)
			{
				MessageBox.Show("Файл не содержит ссылок.");
				return;
			}

			string message = textBox1.Text.Trim();
			if (string.IsNullOrWhiteSpace(message))
			{
				MessageBox.Show("Введите текст сообщения.");
				return;
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
				NavigateFast("https://www.marktplaats.nl/");

				AddCookiesSafe(driver, cookieList);

				await SendMessageToAds(message, urls);

				MessageBox.Show("Готово. Обработка ссылок завершена.");
			}
			catch (Exception ex)
			{ }
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

		public async Task SendMessageToAds(string message, List<string> urls)
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

					await OpenAdAndSendMessage(message, url);
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
			TryAcceptCookies(driver, wait);

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
						// корректный setter для textarea
						try {
							const setter = Object.getOwnPropertyDescriptor(HTMLTextAreaElement.prototype, 'value').set;
							setter.call(el, v);
						} catch(e) {
							el.value = v;
						}
						// react value tracker
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
							// generic fallback
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
		private ChromeOptions BuildChromeOptionsWithProxy()
		{
			var opts = new ChromeOptions();
			opts.AddArguments("--disable-blink-features=AutomationControlled");
			opts.AddArguments("--start-maximized");
			opts.PageLoadStrategy = PageLoadStrategy.Eager;

			var raw = proxyBox?.Text?.Trim();
			if (string.IsNullOrEmpty(raw))
				return opts;

			try
			{
				if (!raw.Contains("://"))
					raw = "http://" + raw;

				if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri))
					throw new FormatException("Неверный формат прокси. Примеры: host:port, http://host:port, socks5://host:port");

				string hostPort = $"{uri.Host}:{uri.Port}";
				string scheme = uri.Scheme.ToLowerInvariant();

				var proxy = new OpenQA.Selenium.Proxy();

				switch (scheme)
				{
					case "http":
					case "https":
						proxy.HttpProxy = hostPort;
						proxy.SslProxy = hostPort;
						proxy.FtpProxy = hostPort;
						opts.AddArgument($"--proxy-server={scheme}://{hostPort}");
						break;

					case "socks":
					case "socks5":
					case "socks5h":
						proxy.SocksProxy = hostPort;
						proxy.SocksVersion = 5;
						// если есть user:pass
						if (!string.IsNullOrEmpty(uri.UserInfo))
						{
							var up = uri.UserInfo.Split(':');
							if (up.Length > 0) proxy.SocksUserName = up[0];
							if (up.Length > 1) proxy.SocksPassword = up[1];
						}
						opts.AddArgument($"--proxy-server=socks5://{hostPort}");
						break;

					case "socks4":
						proxy.SocksProxy = hostPort;
						proxy.SocksVersion = 4;
						opts.AddArgument($"--proxy-server=socks4://{hostPort}");
						break;

					default:
						// по умолчанию http
						proxy.HttpProxy = hostPort;
						proxy.SslProxy = hostPort;
						proxy.FtpProxy = hostPort;
						opts.AddArgument($"--proxy-server=http://{hostPort}");
						break;
				}

				opts.AddArgument("--ignore-certificate-errors");

				opts.Proxy = proxy;
			}
			catch (Exception ex)
			{
				MessageBox.Show("Прокси не будет использован. Ошибка парсинга: " + ex.Message);
			}

			return opts;
		}


		private void RestartDriver()
		{
			try
			{
				driver?.Quit();
			}
			catch { /* игнор */ }
			try
			{
				driver?.Dispose();
			}
			catch { /* игнор */ }
			driver = null;
		}

		private static void ScrollIntoView(IWebDriver driver, IWebElement element)
		{
			((IJavaScriptExecutor)driver)
				.ExecuteScript("arguments[0].scrollIntoView({block:'center', inline:'center'});", element);
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
					return false; // повторит до таймаута
				}
				return false;
			});
		}

		private static void TryAcceptCookies(IWebDriver driver, WebDriverWait wait)
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

			// Попытка в основном документе
			if (ClickIfFound())
				return;

			// Поиск по iframe
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
				catch { /* некоторые фреймы кросс-доменные */ }
				finally
				{
					driver.SwitchTo().DefaultContent();
				}
			}
		}

		private static void AddCookiesSafe(UndetectedChromeDriver driver, List<SerializableCookie> cookieList)
		{
			foreach (var c in cookieList)
			{
				try
				{
					// Нормализуем домен (убираем ведущую точку, задаём путь по умолчанию)
					var domain = (c.Domain ?? "marktplaats.nl").Trim();
					if (domain.StartsWith(".")) domain = domain.Substring(1);
					if (string.IsNullOrWhiteSpace(c.Path)) c.Path = "/";

					Cookie cookie = c.Expiry.HasValue
						? new Cookie(c.Name, c.Value, domain, c.Path, c.Expiry)
						: new Cookie(c.Name, c.Value, domain, c.Path, null);

					driver.Manage().Cookies.AddCookie(cookie);
				}
				catch
				{
					// Некоторые куки (HttpOnly/secure с cross-domain) не добавятся — это нормально.
				}
			}
		}
	}

	// Сериализуемый тип для куки
	public class SerializableCookie
	{
		public string Name { get; set; } = "";
		public string Value { get; set; } = "";
		public string Domain { get; set; } = "";
		public string Path { get; set; } = "/";
		public DateTime? Expiry { get; set; } // может быть null (session-cookie)
		public bool HttpOnly { get; set; }
		public bool Secure { get; set; }
	}

	// ваш Prompt.ShowDialog должен быть в проекте (как и раньше)
}
