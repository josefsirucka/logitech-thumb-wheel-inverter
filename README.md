# MX Thumb Wheel Reader

Konzolová aplikace pro Windows a .NET 10. Používá HID++ feature `0x2150` pro zjištění a nastavení směru horizontálního palcového kolečka myši Logitech MX.

## Spuštění

```powershell
dotnet restore .\MxThumbWheelReader.csproj
dotnet run --project .\MxThumbWheelReader.csproj -- --status
dotnet run --project .\MxThumbWheelReader.csproj -- --normal
dotnet run --project .\MxThumbWheelReader.csproj -- --inverted
```

Staženou aplikaci lze spustit například `.\MxThumbWheelReader.exe --status`. Bez parametru se provede `status`. Příkazy fungují i bez předpony `--`. `normal` a `inverted` mění směr kolečka pouze v případě, že se aktuální hodnota liší. Po zápisu se stav znovu přečte.

Při prvním úspěšném čtení aplikace uloží název počítače, VID/PID, název zařízení, HID cestu, index zařízení a aktuální směr do `appsettings.json` vedle spuštěného programu. Při dalším spuštění zkusí tento záznam přednostně; pokud cesta už neplatí, znovu prohledá Logitech HID zařízení. Nové úspěšné čtení záznam aktualizuje.

Při `dotnet run` je soubor v `bin\Debug\net10.0-windows10.0.19041.0\appsettings.json`. Po publikování je vedle `.exe`. Proces musí mít právo do této složky zapisovat. Zdrojový `appsettings.json` je prázdná šablona, která se kopíruje do výstupní složky sestavení.

Pro sestavení je potřeba přístup k NuGetu kvůli balíčku HidSharp. Myš musí být probuzená a připojená. Program vybere první zařízení, které odpoví s feature `0x2150`.

## Automatická obnova přes Bluetooth bez pravidelných dotazů (od v1.2.0)

Od v1.2.2 má skryté hlídání ikonu **MX Thumb Wheel** v systémové liště vedle hodin (případně pod šipkou skrytých ikon). Pravé tlačítko → **Ukončit** zastaví hlídání a odstraní ikonu. Aktuální směr myši ani uložená preference se tím nemění. Automatické spuštění při příštím přihlášení zůstává zapnuté; pro trvalé vypnutí použijte `Uninstall-Startup.ps1`. Pro opětovné spuštění nyní lze znovu spustit `Install-Startup.ps1`. Ikona používá události Windows, žádný periodický časovač. Release obsahuje i potřebný Windows Desktop runtime.

Od v1.2.3 používá lišta i EXE vlastní ikonu myši. Ikona je vložená přímo do aplikace v několika velikostech pro různé DPI; vedle EXE nejsou potřeba žádné další obrázky.

Rozbalte ZIP do trvalé zapisovatelné složky. Pro obrácený směr spusťte:

```powershell
.\MxThumbWheelReader.exe --inverted
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Install-Startup.ps1
```

První příkaz uloží požadavek i při odpojené myši (v tom případě vrátí nenulový kód kvůli nedostupnému zařízení). Instalační skript spustí hlídání skrytě a zaregistruje úlohu pro přihlášení aktuálního uživatele. Při restartu PC se tedy obnoví po přihlášení. Složku pak nepřesouvejte. Pokud zásady Windows registraci úlohy zakážou, skript vypíše chybu.

Pro ruční hlídání v konzoli spusťte `.\MxThumbWheelReader.exe --watch`; ukončení je Ctrl+C. Pro odstranění automatického spouštění spusťte `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Uninstall-Startup.ps1`.

Od v1.2.1 instalační skript spouští proces bez konzole, takže nemá okno ani položku na hlavním panelu. Volba `--watch --hidden` navíc odpojí konzoli při ručním spuštění; od v1.2.2 ho ukončíte přes ikonu v systémové liště. Pro běžný provoz použijte instalační skript. Hlídání nadále čeká na události Windows bez pravidelných dotazů.

Aktualizace na v1.2.1: ve staré složce spusťte `Uninstall-Startup.ps1`, ruční hlídání ukončete Ctrl+C, nahraďte soubory novým ZIPem (zachovejte svůj `appsettings.json`) a znovu spusťte `Install-Startup.ps1`. Při instalaci do nové složky do ní přeneste svůj `appsettings.json`.

Požadovaný směr je uložen zvlášť v `DesiredDirectionInverted`. Příkaz `--status` ho nepřepíše. Při aktualizaci ze starší verze zvolte jednou `--inverted` nebo `--normal`; stará naměřená hodnota se automaticky nepovažuje za požadavek.

Hlídání reaguje na oznámení Windows o připojení spárovaného Bluetooth zařízení, objevení Logitech HID rozhraní, probuzení počítače a na ruční příkazy programu. Používá DeviceWatcher se stavem System.Devices.Aep.IsConnected pro spárované Bluetooth/BLE endpointy a oznámení Windows o probuzení. Nespouští vyhledávání nespárovaných zařízení ani periodické HID dotazy. Oznámení o jiném spárovaném Bluetooth zařízení může také vyvolat kontrolu uložené myši.

Při spuštění a po události zkusí ihned obnovit požadovaný směr. Pokud rozhraní ještě není připravené, následují nejvýše tři další pokusy s prodlevami 250 ms, 750 ms a 2 s. Po úspěchu nebo vyčerpání pokusů čeká neomezeně na další událost; není zde pravidelný ping ani kontrolní časovač. Původní WatchIntervalSeconds se ignoruje. Samotná HID komunikace má své timeouty, takže okamžitou reakci před zpřístupněním spojení Windows nelze garantovat. Když firmware resetuje nastavení bez události připojení/probuzení nebo je přepíše jiný program, tato verze změnu průběžně nezjišťuje.

Událostní hlídání je určeno pro přímé Bluetooth na Windows 10 verze 2004 nebo novějších a Windows 11 (x64). Bezdrátové připojení myši za trvale zapojeným USB receiverem vyžaduje jiný typ oznámení; tuto variantu --watch odmítne, pokud je rozpoznaná v konfiguraci. Jednorázové příkazy zůstávají použitelné i přes receiver. Balíček obsahuje vlastní .NET runtime.

Při aktualizaci z v1.1.0 nejdřív spusťte Uninstall-Startup.ps1 ve staré složce, případný ruční --watch ukončete Ctrl+C, rozbalte nový ZIP a zopakujte oba instalační příkazy výše. Tím zabráníte tomu, aby zůstal spuštěný starý proces s periodickým dotazováním.

Známé zařízení se hledá přednostně podle uložené HID cesty, PID a indexu. Při novém párování nebo výměně receiveru obnovte identifikaci příkazem `--status`. Pokud směr spravuje současně jiný program, může jej přepisovat.

## Vytvoření dalšího GitHub Release

Tento adresář nahrajte jako **kořen samostatného GitHub repozitáře**, včetně skryté složky `.github`. První nahrání proveďte například takto (doplňte URL svého repozitáře):

```powershell
git init -b main
git add .
git commit -m "Initial release workflow"
git remote add origin https://github.com/OWNER/REPO.git
git push -u origin main
```

Novou verzi potom vytvoříte příkazem:

```powershell
.\New-Release.ps1 -Version 1.0.0
```

Skript vyžaduje čistý commitnutý pracovní strom, Git remote `origin` směřující na GitHub a dosud nepoužitou verzi. Pushne aktuální commit a tag `v1.0.0`. Workflow `.github/workflows/release.yml` po pushnutí tagu sestaví samostatně spustitelnou Windows x64 aplikaci a zveřejní **jeden ZIP** `MxThumbWheelReader-v1.0.0-win-x64.zip` na stránce Releases. ZIP obsahuje `.exe`, výchozí `appsettings.json` a tento návod. Na cílovém počítači není potřeba instalovat .NET runtime.

Před vydáním další verze nejdřív commitněte změny a spusťte skript s novým číslem. V nastavení repozitáře musí být povolené GitHub Actions; workflow používá `GITHUB_TOKEN` s oprávněním `contents: write`.
