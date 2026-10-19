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

Při `dotnet run` je soubor v `bin\Debug\net10.0-windows\appsettings.json`. Po publikování je vedle `.exe`. Proces musí mít právo do této složky zapisovat. Zdrojový `appsettings.json` je prázdná šablona, která se kopíruje do výstupní složky sestavení.

Pro sestavení je potřeba přístup k NuGetu kvůli balíčku HidSharp. Myš musí být probuzená a připojená. Program vybere první zařízení, které odpoví s feature `0x2150`.

## Automatická obnova po připojení a restartu (od v1.1.0)

Rozbalte ZIP do trvalé zapisovatelné složky. Pro obrácený směr spusťte:

```powershell
.\MxThumbWheelReader.exe --inverted
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Install-Startup.ps1
```

První příkaz uloží požadavek i při odpojené myši (v tom případě vrátí nenulový kód kvůli nedostupnému zařízení). Instalační skript spustí hlídání skrytě a zaregistruje úlohu pro přihlášení aktuálního uživatele. Při restartu PC se tedy obnoví po přihlášení. Složku pak nepřesouvejte. Pokud zásady Windows registraci úlohy zakážou, skript vypíše chybu.

Pro ruční hlídání v konzoli spusťte `.\MxThumbWheelReader.exe --watch`; ukončení je Ctrl+C. Pro odstranění automatického spouštění spusťte `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Uninstall-Startup.ps1`.

Požadovaný směr je uložen zvlášť v `DesiredDirectionInverted`. Příkaz `--status` ho nepřepíše. Při aktualizaci ze starší verze zvolte jednou `--inverted` nebo `--normal`; stará naměřená hodnota se automaticky nepovažuje za požadavek.

Hlídání kontroluje stav ve výchozím nastavení každých 5 sekund. Mezi kontrolami proces spí; při chybě nebo odpojení interval prodlužuje na 10, 20 a nejvýše 30 sekund. Obnova po návratu myši proto může chvíli trvat. `WatchIntervalSeconds` lze zvýšit (rozsah 5–300 sekund). Delší interval snižuje počet HID dotazů a prodlužuje reakci. Zápis do myši probíhá jen při odchylce, konfigurace se zapisuje jen při změně a konzole hlásí jen změny dostupnosti. Konkrétní spotřeba CPU a vliv na baterii závisí na zařízení; na fyzické myši je nutné je ověřit.

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
