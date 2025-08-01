using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using OpenQA.Selenium.Interactions;
using Newtonsoft.Json;
// Если у вас другой пакет undetected — поправьте namespace:
using SeleniumUndetectedChromeDriver;

class Program
{
	// ==== настройки ====
	private const string ListingUrl = "https://www.etsy.com/listing/1079488068";
	private const string CookieFile = "cookies.json";
	private const string ChromeDriverPath = @"C:/chromedriver.exe"; // требуемый путь

	static void Main(string[] args)
	{
		Console.WriteLine("Выберите режим:");
		Console.WriteLine("1) Регистрация и сохранение cookies");
		Console.WriteLine("2) Загрузка cookies, открытие товара и автозаполнение вариантов");
		Console.Write("Введите 1 или 2: ");
		var choice = Console.ReadLine();

		UndetectedChromeDriver driver = null;
		try
		{
			var options = BuildChromeOptions();

			driver = UndetectedChromeDriver.Create(
				options: options,
				driverExecutablePath: ChromeDriverPath
			);

			// Явные ожидания, без сильного упора на implicit
			driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(0);

			var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(30));
			wait.IgnoreExceptionTypes(typeof(NoSuchElementException), typeof(StaleElementReferenceException));

			if (choice == "1")
			{
				driver.Navigate().GoToUrl("https://www.etsy.com/");
				WaitForDocumentReady(driver, wait);

				var cookies = JsonConvert.DeserializeObject<List<CookieDto>>(File.ReadAllText(CookieFile)) ?? new List<CookieDto>();
				foreach (var c in cookies)
				{
					try
					{
						var ck = new OpenQA.Selenium.Cookie(c.Name, c.Value, c.Domain, c.Path, c.Expiry);
						driver.Manage().Cookies.AddCookie(ck);
					}
					catch { /* часть кук может не примениться — это нормально */ }
				}

				// ⚠️ Обновим страницу, чтобы сессия реально применилась в UI
				driver.Navigate().Refresh();
				WaitForDocumentReady(driver, wait);
				TryAcceptCookiesEtsy(driver, wait);

				Console.WriteLine("Выполните вход/регистрацию вручную, затем нажмите Enter для сохранения cookies…");
				driver.Navigate().GoToUrl("https://www.etsy.com/join");

				Console.ReadLine();

				var cookiesSave = driver.Manage().Cookies.AllCookies.Select(CookieDto.FromSelenium).ToList();
				File.WriteAllText(CookieFile, JsonConvert.SerializeObject(cookiesSave, Formatting.Indented));
				Console.WriteLine("✅ Cookies сохранены: " + Path.GetFullPath(CookieFile));
			}
			else if (choice == "2")
			{
				// --- режим загрузки кук и автозаполнения ---
				if (File.Exists(CookieFile))
				{
					driver.Navigate().GoToUrl("https://www.etsy.com/");
					WaitForDocumentReady(driver, wait);

					var cookies = JsonConvert.DeserializeObject<List<CookieDto>>(File.ReadAllText(CookieFile)) ?? new List<CookieDto>();
					foreach (var c in cookies)
					{
						try
						{
							var ck = new OpenQA.Selenium.Cookie(c.Name, c.Value, c.Domain, c.Path, c.Expiry);
							driver.Manage().Cookies.AddCookie(ck);
						}
						catch { /* часть кук может не примениться — это нормально */ }
					}

					// ⚠️ Обновим страницу, чтобы сессия реально применилась в UI
					driver.Navigate().Refresh();
					WaitForDocumentReady(driver, wait);
					TryAcceptCookiesEtsy(driver, wait);

					// ✅ Проверка авторизации через отсутствие кнопки "Войти"
					if (!IsLoggedIn(driver))
					{
						Console.WriteLine("❌ Похоже, вы не авторизованы (кнопка «Войти» видна).");
						Console.WriteLine("➡️ Выберите режим '1' для входа/регистрации и сохранения cookies, затем повторите режим '2'.");
						Console.WriteLine("Нажмите Enter для выхода…");
						Console.ReadLine();
						return; // не продолжаем дальше
					}
				}

				// (остальной код режима 2 — как было)
				driver.Navigate().GoToUrl(ListingUrl);
				WaitForDocumentReady(driver, wait);
				TryAcceptCookiesEtsy(driver, wait);

				// 1) Заполнение ВСЕХ variation-селектов до стабилизации, с задержкой 1s между выборами
				// 1) Заполнение селектов (как было у вас)
				FillVariationSelectsUntilStable(driver, maxPasses: 12, perSelectTimeoutSeconds: 10, selectionDelayMs: 1000);

				// 2) Персонализация — быстрая JS-версия
				TryFillPersonalizationFast(driver, "There is no difference");

				// 3) (опционально) кастомные комбобоксы внутри buy-box — теперь не тронут глобальный поиск
				FillCustomComboboxesOnce(driver, new WebDriverWait(driver, TimeSpan.FromSeconds(20)));

				// 4) Нажать «Купить сейчас»
				if (ClickBuyNow(driver, TimeSpan.FromSeconds(5)))
				{
					Console.WriteLine("🛒 Нажал «Купить сейчас». Жду страницу оформления…");
				}
				else
				{
					Console.WriteLine("⚠️ Не удалось нажать «Купить сейчас».");
				}

				// 5) На странице корзины/оформления: «Оплатите сейчас…»
				if (ClickCheckoutNow(driver, TimeSpan.FromSeconds(5)))
				{
					Console.WriteLine("💳 Нажал «Оплатите сейчас…».");
				}
				else
				{
					Console.WriteLine("⚠️ Не удалось нажать «Оплатите сейчас…».");
				}

			}
			else
			{
				Console.WriteLine("Неверный выбор.");
				Console.WriteLine("Нажмите Enter для выхода…");
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine("Ошибка: " + ex);
			Console.WriteLine("Нажмите Enter для выхода…");
		}
	}

	// ====== Chrome options / undetected ======
	static ChromeOptions BuildChromeOptions()
	{
		var o = new ChromeOptions();
		o.PageLoadStrategy = PageLoadStrategy.Eager;

		o.AddArgument("--start-maximized");
		o.AddArgument("--disable-blink-features=AutomationControlled");
		o.AddArgument("--disable-gpu");
		o.AddArgument("--disable-dev-shm-usage");
		o.AddArgument("--no-sandbox");
		o.AddArgument("--disable-extensions");
		o.AddArgument("--disable-infobars");
		o.AddArgument("--user-agent=Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
		return o;
	}

	// ====== DTO для устойчивых ссылок на variation-select ======
	class VariationRef
	{
		public string SelectorCss { get; set; }   // например: select[data-variation-number='1'] или #variation-selector-0
		public int OrderKey { get; set; }         // сортировка (обычно номер вариации)
		public int Y { get; set; }                // запасная сортировка по вертикали
		public string Label { get; set; }         // для логов
	}

	// ====== Главный алгоритм: только VariationRef, каждый раз ре-фечим IWebElement ======
	static void FillVariationSelectsUntilStable(IWebDriver driver, int maxPasses = 10, int perSelectTimeoutSeconds = 8, int selectionDelayMs = 1000)
	{
		// Дождёмся появления хотя бы одного variation-select
		var bootWait = new WebDriverWait(driver, TimeSpan.FromSeconds(20));
		bootWait.Until(d =>
			d.FindElements(By.CssSelector("select[id^='variation-selector-'], select[data-variation-number]")).Count > 0
		);

		for (int pass = 1; pass <= maxPasses; pass++)
		{
			driver.SwitchTo().DefaultContent(); // на всякий случай, если фокус ушёл во фрейм
			bool anyPickedThisPass = false;

			var refs = FindVariationRefsFresh(driver);
			if (refs.Count == 0)
			{
				System.Threading.Thread.Sleep(150);
				refs = FindVariationRefsFresh(driver);
				if (refs.Count == 0)
				{
					Console.WriteLine("ℹ️ Селекторы вариаций не обнаружены.");
					break;
				}
			}

			foreach (var r in refs)
			{
				try
				{
					// каждый раз берём «свежий» элемент по устойчивому CSS
					IWebElement s;
					try { s = driver.FindElement(By.CssSelector(r.SelectorCss)); }
					catch (NoSuchElementException) { continue; }

					ScrollIntoView(driver, s);

					// если уже выбран НЕ placeholder — пропускаем
					if (!SelectHasPlaceholder(driver, s)) continue;

					// дождёмся пригодной опции
					var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(Math.Max(3, perSelectTimeoutSeconds)));
					wait.IgnoreExceptionTypes(typeof(StaleElementReferenceException), typeof(NoSuchElementException));
					TryWaitUntilSelectPickable(driver, s, wait);

					// выбор первой валидной опции
					if (ForcePickFirstAvailableOptionJS(driver, s))
					{
						// задержка между выборами, чтобы страница успела перерисоваться
						System.Threading.Thread.Sleep(Math.Max(0, selectionDelayMs));

						// логируем из СВЕЖЕГО экземпляра (старый может стать stale)
						string chosen = "";
						try
						{
							var s2 = driver.FindElement(By.CssSelector(r.SelectorCss));
							var sel = new SelectElement(s2);
							chosen = sel.SelectedOption?.Text?.Trim() ?? "";
						}
						catch { /* если не удалось — лог без текста */ }

						Console.WriteLine($"  <{r.Label}> выбран: {chosen}");
						anyPickedThisPass = true;
					}
					else
					{
						Console.WriteLine($"⚠️ Не удалось выбрать опцию для <{r.Label}>");
					}
				}
				catch (StaleElementReferenceException)
				{
					// DOM перерисован — на следующем проходе возьмём новый экземпляр
					anyPickedThisPass = true;
					break;
				}
				catch
				{
					// не фатально; переходим к следующему
				}
			}

			if (!anyPickedThisPass)
			{
				// Проверим, остались ли плейсхолдеры
				var remaining = FindVariationRefsFresh(driver).Count(r =>
				{
					try
					{
						var s = driver.FindElement(By.CssSelector(r.SelectorCss));
						return SelectHasPlaceholder(driver, s);
					}
					catch { return false; }
				});

				if (remaining == 0)
					Console.WriteLine("✅ Все variation-селекты заполнены.");
				else
					Console.WriteLine($"ℹ️ Осталось незаполненных variation-селектов: {remaining}.");

				break;
			}

			System.Threading.Thread.Sleep(120);
		}
	}

	// === Поиск variation-select'ов: создаём только VariationRef (без удержания IWebElement) ===
	static List<VariationRef> FindVariationRefsFresh(IWebDriver driver)
	{
		var refs = new List<VariationRef>();
		var reId = new Regex(@"variation-selector-(\d+)");

		var all = driver.FindElements(By.CssSelector("select[id^='variation-selector-'], select[data-variation-number]"));
		foreach (var s in all)
		{
			try
			{
				// наличие опций — грубая проверка «это настоящий селект»
				if ((s.GetAttribute("outerHTML") ?? "").IndexOf("<option", StringComparison.OrdinalIgnoreCase) < 0)
					continue;

				string selector = null;
				string label = null;
				int order = int.MaxValue;

				var dvn = s.GetAttribute("data-variation-number");
				if (!string.IsNullOrEmpty(dvn) && int.TryParse(dvn, out var n))
				{
					selector = $"select[data-variation-number='{n}']";
					label = $"variation-selector-{n}";
					order = n;
				}
				else
				{
					var id = s.GetAttribute("id") ?? "";
					var m = reId.Match(id);
					if (m.Success && int.TryParse(m.Groups[1].Value, out var k))
					{
						selector = $"#{id}";
						label = id;
						order = k;
					}
				}

				if (string.IsNullOrEmpty(selector)) continue;

				int y = 0;
				try { y = s.Location.Y; } catch { }

				refs.Add(new VariationRef
				{
					SelectorCss = selector,
					OrderKey = order,
					Y = y,
					Label = label
				});
			}
			catch { /* пропускаем сбойный экземпляр */ }
		}

		return refs.OrderBy(r => r.OrderKey).ThenBy(r => r.Y).ToList();
	}

	// ====== Комбобоксы (кастомные) — опционально ======
	static void FillCustomComboboxesOnce(IWebDriver driver, WebDriverWait wait)
	{
		var scope = GetBuyBoxScope(driver); // только внутри buy-box!
		var triggers = scope.FindElements(By.CssSelector("button[aria-haspopup='listbox'], .wt-select__trigger, [role='combobox']"))
							.Where(e =>
							{
								try
								{
									// игнорируем любые input (в т.ч. глобальный поиск)
									var tag = (e.TagName ?? "").ToLowerInvariant();
									if (tag == "input") return false;
									// защита от глобального поиска на всякий случай
									var id = (e.GetAttribute("id") ?? "").ToLowerInvariant();
									if (id == "global-enhancements-search-query") return false;
									return e.Displayed && e.Enabled;
								}
								catch { return false; }
							})
							.OrderBy(e => e.Location.Y)
							.ToList();

		foreach (var trigger in triggers)
		{
			string trigText = (trigger.Text ?? "").Trim().ToLowerInvariant();
			if (!string.IsNullOrEmpty(trigText) && !IsPlaceholderText(trigText))
				continue;

			ScrollIntoView(driver, trigger);
			SafeClick(driver, trigger);

			IWebElement list = null;
			try
			{
				list = wait.Until(d =>
					scope.FindElements(By.CssSelector("[role='listbox']:not([aria-hidden='true'])"))
						 .FirstOrDefault(el => el.Displayed));
			}
			catch { }

			if (list == null)
			{
				list = scope.FindElements(By.CssSelector("ul[role='listbox'], ul[role='menu'], .wt-popover__body ul"))
							.FirstOrDefault(el => el.Displayed);
			}
			if (list == null) continue;

			var options = list.FindElements(By.CssSelector("[role='option'], li > button, li"))
							  .Where(o =>
							  {
								  var t = (o.Text ?? "").Trim();
								  if (string.IsNullOrWhiteSpace(t)) return false;
								  var low = t.ToLowerInvariant();
								  if (IsPlaceholderText(low)) return false;

								  var disabled = (o.GetAttribute("aria-disabled") ?? "").Equals("true", StringComparison.OrdinalIgnoreCase)
											  || ((o.GetAttribute("class") ?? "").IndexOf("disabled", StringComparison.OrdinalIgnoreCase) >= 0);
								  var alreadySel = (o.GetAttribute("aria-selected") ?? "").Equals("true", StringComparison.OrdinalIgnoreCase);
								  return !disabled && !alreadySel && o.Displayed;
							  })
							  .ToList();

			var first = options.FirstOrDefault();
			if (first != null)
			{
				ScrollIntoView(driver, first);
				SafeClick(driver, first);
				Console.WriteLine($"  combobox выбран: {first.Text.Trim()}");
			}
		}
	}


	// ====== Радио — опционально ======
	static void FillRadioGroupsOnce(IWebDriver driver, WebDriverWait wait)
	{
		var radios = driver.FindElements(By.CssSelector("input[type='radio']"))
						  .Where(r => r.Displayed && r.Enabled)
						  .OrderBy(r => r.Location.Y)
						  .ToList();

		var byName = radios.GroupBy(r => r.GetAttribute("name") ?? Guid.NewGuid().ToString());
		foreach (var grp in byName)
		{
			if (grp.Any(r => r.Selected || r.GetAttribute("checked") != null)) continue;

			var first = grp.FirstOrDefault(r => r.Enabled && r.Displayed);
			if (first == null) continue;

			ScrollIntoView(driver, first);

			var id = first.GetAttribute("id");
			if (!string.IsNullOrWhiteSpace(id))
			{
				var label = driver.FindElements(By.CssSelector($"label[for='{id}']")).FirstOrDefault();
				if (label != null && label.Displayed && label.Enabled)
					SafeClick(driver, label);
				else
					SafeClick(driver, first);
			}
			else
			{
				SafeClick(driver, first);
			}

			try { wait.Until(_ => first.Selected || (first.GetAttribute("checked") != null)); } catch { }
			Console.WriteLine("  radio выбран.");
		}
	}

	// ====== Персонализация — опционально ======
	static void TryFillPersonalizationFast(IWebDriver driver, string text)
	{
		try
		{
			var textarea = driver.FindElement(By.CssSelector("textarea#listing-page-personalization-textarea"));
			if (textarea.Displayed && textarea.Enabled)
			{
				ScrollIntoView(driver, textarea);
				// Мгновенно проставляем значение и шлём события
				string js = @"
                const el = arguments[0], val = arguments[1];
                el.value = val;
                el.dispatchEvent(new Event('input', { bubbles: true }));
                el.dispatchEvent(new Event('change', { bubbles: true }));
                el.dispatchEvent(new Event('blur', { bubbles: true }));";
				((IJavaScriptExecutor)driver).ExecuteScript(js, textarea, text);
				Console.WriteLine("Персонализация заполнена (быстрый ввод).");
			}
		}
		catch (NoSuchElementException) { /* поля нет — ок */ }
	}
	static bool ClickBuyNow(IWebDriver driver, TimeSpan timeout)
	{
		string startUrl = driver.Url;
		var wait = new WebDriverWait(driver, timeout);
		wait.IgnoreExceptionTypes(typeof(NoSuchElementException), typeof(StaleElementReferenceException));

		for (int attempt = 1; attempt <= 3; attempt++)
		{
			try
			{
				var scope = GetBuyBoxScope(driver);

				var btns = scope.FindElements(By.CssSelector("button[type='submit'][data-skip-bin-overlay='true']"))
					.Where(b => { try { return b.Displayed && b.Enabled; } catch { return false; } })
					.ToList();

				if (btns.Count == 0)
				{
					btns = scope.FindElements(By.XPath(".//button[normalize-space()='Купить сейчас' or contains(translate(., 'BUY', 'buy'),'buy')]"))
						.Where(b => { try { return b.Displayed && b.Enabled; } catch { return false; } })
						.ToList();
				}

				if (btns.Count == 0)
				{
					System.Threading.Thread.Sleep(200);
					continue;
				}

				var btn = btns.First();
				ScrollIntoView(driver, btn);
				if (!SafeClick(driver, btn))
					continue;

				// ждём реакцию
				try
				{
					wait.Until(d =>
					{
						try
						{
							if (d.Url != startUrl) return true;
							var sp = d.FindElements(By.CssSelector("[data-bin-button-loading-indicator]")).FirstOrDefault();
							if (sp != null && sp.Displayed) return true;
							var again = GetBuyBoxScope(d).FindElements(By.CssSelector("button[type='submit'][data-skip-bin-overlay='true']")).FirstOrDefault();
							if (again == null) return true;
							if (!again.Displayed || !again.Enabled) return true;
							return false;
						}
						catch { return false; }
					});
				}
				catch { }

				return true;
			}
			catch { }
			System.Threading.Thread.Sleep(200);
		}
		return false;
	}
	static bool ClickCheckoutNow(IWebDriver driver, TimeSpan timeout)
	{
		var wait = new WebDriverWait(driver, timeout);
		wait.IgnoreExceptionTypes(typeof(NoSuchElementException), typeof(StaleElementReferenceException));

		// ждём появления кнопки checkout на следующем шаге
		for (int attempt = 1; attempt <= 3; attempt++)
		{
			try
			{
				IWebElement btn = null;

				// приоритет — стабильный data-testid
				btn = wait.Until(d =>
				{
					var b = d.FindElements(By.CssSelector("button[data-testid='default-checkout-button']")).FirstOrDefault();
					if (b != null && b.Displayed && b.Enabled) return b;
					return null;
				});

				// резерв — по тексту RU/EN (название магазина может меняться)
				if (btn == null)
				{
					btn = driver.FindElements(By.XPath("//button[contains(., 'Оплатите сейчас') or contains(translate(., 'PROCEED TO CHECKOUT','proceed to checkout'),'proceed to checkout')]"))
								.FirstOrDefault(b => { try { return b.Displayed && b.Enabled; } catch { return false; } });
				}

				if (btn == null)
				{
					System.Threading.Thread.Sleep(300);
					continue;
				}

				ScrollIntoView(driver, btn);
				if (!SafeClick(driver, btn)) { System.Threading.Thread.Sleep(250); continue; }

				// ждём какую-то реакцию: исчезновение кнопки / переход / лоадер
				try
				{
					wait.Until(d =>
					{
						try
						{
							var again = d.FindElements(By.CssSelector("button[data-testid='default-checkout-button']")).FirstOrDefault();
							if (again == null) return true; // исчезла — ок
							if (!again.Displayed || !again.Enabled) return true;
							// либо URL сменился на checkout
							var u = d.Url.ToLowerInvariant();
							if (u.Contains("/checkout") || u.Contains("/cart")) return true;
							return false;
						}
						catch { return false; }
					});
				}
				catch { }

				return true;
			}
			catch { }
		}
		return false;
	}



	// ====== Хелперы ======
	static void WaitForDocumentReady(IWebDriver driver, WebDriverWait wait)
	{
		try
		{
			wait.Until(d => ((IJavaScriptExecutor)d).ExecuteScript("return document.readyState")?.ToString() == "complete");
		}
		catch { }
	}

	static void TryAcceptCookiesEtsy(IWebDriver driver, WebDriverWait wait)
	{
		try
		{
			var btn = driver.FindElements(By.CssSelector("button[data-gdpr-single-choice-accept], [data-gdpr-accept]")).FirstOrDefault()
				  ?? driver.FindElements(By.XPath("//button[contains(., 'Accept') or contains(., 'Agree') or contains(., 'OK')]")).FirstOrDefault();

			if (btn != null)
			{
				try { btn.Click(); }
				catch { ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].click();", btn); }
				try { wait.Until(_ => !btn.Displayed); } catch { }
			}
		}
		catch { }
	}

	static void ScrollIntoView(IWebDriver driver, IWebElement element)
	{
		try
		{
			((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].scrollIntoView({block:'center', inline:'center'});", element);
		}
		catch { }
	}

	static bool SafeClick(IWebDriver driver, IWebElement el, int maxAttempts = 3)
	{
		for (int attempt = 1; attempt <= maxAttempts; attempt++)
		{
			try
			{
				el.Click();
				return true;
			}
			catch (ElementClickInterceptedException)
			{
				try { ScrollIntoView(driver, el); } catch { }
				try
				{
					new Actions(driver)
						.MoveToElement(el, 1, 1)
						.Pause(TimeSpan.FromMilliseconds(50))
						.Click()
						.Perform();
					return true;
				}
				catch { }
			}
			catch (ElementNotInteractableException)
			{
				try { ScrollIntoView(driver, el); } catch { }
			}
			catch (StaleElementReferenceException)
			{
				return false;
			}
			catch { }

			try
			{
				((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].click();", el);
				return true;
			}
			catch { }

			System.Threading.Thread.Sleep(120);
		}
		return false;
	}

	static bool IsPlaceholderText(string lowerText)
	{
		lowerText = (lowerText ?? "").Trim().ToLowerInvariant();
		return string.IsNullOrEmpty(lowerText)
			|| lowerText.Contains("select")
			|| lowerText.Contains("please")
			|| lowerText.Contains("выберите вариант")
			|| lowerText.Contains("выберите варинат")
			|| lowerText.Contains("выберите опцию")
			|| lowerText == "—";
	}

	// текущий select показывает placeholder?
	static bool SelectHasPlaceholder(IWebDriver driver, IWebElement selectEl)
	{
		try
		{
			string js = @"
                var s=arguments[0];
                if(!s||s.selectedIndex<0||s.selectedIndex>=s.options.length) return true;
                var o=s.options[s.selectedIndex];
                var t=(o.text||'').toLowerCase().replace(/[\s\u00A0]+/g,' ').trim();
                var v=(o.value||'').trim();
                if(!v) return true;
                if(t.includes('select')||t.includes('please')
                   || t.includes('выберите вариант') || t.includes('выберите варинат') || t.includes('выберите опцию')) return true;
                return false;";
			return (bool)((IJavaScriptExecutor)driver).ExecuteScript(js, selectEl);
		}
		catch { return true; }
	}

	// ожидание, что есть пригодная опция
	static bool TryWaitUntilSelectPickable(IWebDriver driver, IWebElement selectEl, WebDriverWait wait)
	{
		string js = @"
            const s = arguments[0];
            if (!s) return false;
            const busy = s.closest('[aria-busy=""true""]'); if (busy) return false;
            if (!s.options || s.options.length===0) return false;
            for (let i=0;i<s.options.length;i++){
                const o = s.options[i];
                const txt=(o.text||'').toLowerCase();
                const val=(o.value||'').trim();
                const placeholder = !val || txt.includes('select') || txt.includes('please')
                    || txt.includes('выберите вариант') || txt.includes('выберите варинат') || txt.includes('выберите опцию');
                const disabled = o.disabled || o.getAttribute('disabled')!==null
                    || o.getAttribute('aria-disabled')==='true'
                    || o.getAttribute('data-unavailable')==='true'
                    || ((o.className||'').toLowerCase().includes('disabled'));
                if (!placeholder && !disabled) return true;
            }
            return false;";
		try { return wait.Until(_ => (bool)((IJavaScriptExecutor)driver).ExecuteScript(js, selectEl)); }
		catch { return false; }
	}

	// форс-выбор первой доступной опции
	static bool ForcePickFirstAvailableOptionJS(IWebDriver driver, IWebElement selectEl)
	{
		try
		{
			string js = @"
                var s=arguments[0];
                if(!s||!s.options||!s.options.length) return false;
                var pick=-1;
                for(var i=0;i<s.options.length;i++){
                    var o=s.options[i];
                    var txt=(o.text||'').toLowerCase();
                    var val=(o.value||'').trim();
                    var placeholder=!val || txt.indexOf('select')>-1 || txt.indexOf('please')>-1
                        || txt.indexOf('выберите вариант')>-1 || txt.indexOf('выберите варинат')>-1 || txt.indexOf('выберите опцию')>-1;
                    var disabled=o.disabled || o.getAttribute('disabled')!==null
                        || o.getAttribute('aria-disabled')==='true'
                        || o.getAttribute('data-unavailable')==='true'
                        || ((o.className||'')+'').toLowerCase().indexOf('disabled')>-1;
                    if(!placeholder && !disabled){ pick=i; break; }
                }
                if(pick===-1) return false;
                if(s.selectedIndex===pick) return true;
                s.selectedIndex=pick;
                s.dispatchEvent(new Event('input',{bubbles:true}));
                s.dispatchEvent(new Event('change',{bubbles:true}));
                s.dispatchEvent(new Event('blur',{bubbles:true}));
                if(document.activeElement && document.activeElement.blur) document.activeElement.blur();
                return true;";
			return (bool)((IJavaScriptExecutor)driver).ExecuteScript(js, selectEl);
		}
		catch { return false; }
	}
	
	static ISearchContext GetBuyBoxScope(IWebDriver driver)
	{
		var js = (IJavaScriptExecutor)driver;

		try
		{
			// 1) Сначала попробуем привязаться к variation-select
			var variation = driver.FindElements(By.CssSelector("select[id^='variation-selector-'], select[data-variation-number]"))
								  .FirstOrDefault();
			if (variation != null)
			{
				var container = js.ExecuteScript(@"
				let el = arguments[0];
				while (el && el !== document.body) {
					if (el.matches &&
						el.matches('#listing-page-cart, [data-buy-box-region], [data-region=""buy-box""], [data-buy-box-container], form[action*=""cart/add""], .listing-page-buy-box'))
					{
						return el;
					}
					el = el.parentElement;
				}
				return null;", variation) as IWebElement;

				if (container != null) return container;
			}

			// 2) Или привяжемся к кнопке «Купить сейчас»
			var buyBtn = driver.FindElements(By.CssSelector("button[type='submit'][data-skip-bin-overlay='true']"))
							   .FirstOrDefault()
						?? driver.FindElements(By.XPath("//button[normalize-space()='Купить сейчас' or contains(translate(.,'BUY','buy'),'buy')]"))
							   .FirstOrDefault();

			if (buyBtn != null)
			{
				var container = js.ExecuteScript(@"
				let el = arguments[0];
				while (el && el !== document.body) {
					if (el.matches &&
						el.matches('#listing-page-cart, [data-buy-box-region], [data-region=""buy-box""], [data-buy-box-container], form[action*=""cart/add""], .listing-page-buy-box'))
					{
						return el;
					}
					el = el.parentElement;
				}
				return null;", buyBtn) as IWebElement;

				if (container != null) return container;
			}

			// 3) Прямой поиск известных контейнеров
			var direct = driver.FindElements(By.CssSelector(
								"#listing-page-cart, [data-buy-box-region], [data-region='buy-box'], [data-buy-box-container], form[action*='cart/add'], .listing-page-buy-box"))
							   .FirstOrDefault();

			return (ISearchContext)(direct ?? (IWebElement)driver.FindElement(By.TagName("body")));
		}
		catch
		{
			// Фоллбек — весь документ
			return driver;
		}
	}
	// ====== Проверка авторизации по отсутствию кнопки "Войти" ======
	static bool IsLoggedIn(IWebDriver driver)
	{
		try
		{
			// По классам из ТЗ (порядок классов в CSS селекторе не важен)
			bool hasSignInButtonByClass = driver
				.FindElements(By.CssSelector(
					"button.wt-btn.wt-btn--small.wt-btn--transparent.wt-mr-xs-1.inline-overlay-trigger.signin-header-action.select-signin.header-button"))
				.Any(b => { try { return b.Displayed; } catch { return false; } });

			if (hasSignInButtonByClass) return false;

			// Резерв: по тексту RU/EN на случай другой верстки/локали
			bool hasSignInButtonByText = driver
				.FindElements(By.XPath("//button[normalize-space()='Войти' or contains(translate(normalize-space(.),'ABCDEFGHIJKLMNOPQRSTUVWXYZ','abcdefghijklmnopqrstuvwxyz'),'sign in')]"))
				.Any(b => { try { return b.Displayed; } catch { return false; } });

			if (hasSignInButtonByText) return false;

			// Кнопок "Войти" не нашли — считаем, что пользователь авторизован
			return true;
		}
		catch
		{
			// При ошибке подстрахуемся и вернём "не авторизован", чтобы не продолжать критические шаги
			return false;
		}
	}

	// ====== DTO для cookies ======
	class CookieDto
	{
		public string Name { get; set; }
		public string Value { get; set; }
		public string Domain { get; set; }
		public string Path { get; set; }
		public DateTime? Expiry { get; set; }
		public bool Secure { get; set; }
		public bool IsHttpOnly { get; set; }

		public static CookieDto FromSelenium(OpenQA.Selenium.Cookie c) => new CookieDto
		{
			Name = c.Name,
			Value = c.Value,
			Domain = c.Domain,
			Path = c.Path,
			Expiry = c.Expiry,
			Secure = c.Secure,
			IsHttpOnly = c.IsHttpOnly
		};
	}
}
